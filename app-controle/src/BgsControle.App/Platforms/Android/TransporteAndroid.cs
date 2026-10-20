using System.Threading.Channels;
using Android.Bluetooth;
using Android.Bluetooth.LE;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using BgsControle.Nucleo.Enlace;
using BgsControle.Nucleo.Protocolo;
using Java.Util;

namespace BgsControle.App.Plataforma;

/// <summary>
/// O enlace BLE pela API <c>Android.Bluetooth</c> direta, sem biblioteca de terceiros.
/// Nada do que é escrito vai ao logcat: este arquivo não registra nada.
/// Os callbacks do GATT chegam em threads do Binder: lá só se completa uma tarefa ou se põe
/// o evento numa fila, que uma tarefa à parte entrega à sessão, na ordem.
/// </summary>
public sealed class TransporteAndroid : ITransporte
{
    private static readonly UUID UuidServico = UUID.FromString(Gatt.UuidServico)!;
    private static readonly UUID UuidEscrita = UUID.FromString(Gatt.UuidEscrita)!;
    private static readonly UUID UuidResposta = UUID.FromString(Gatt.UuidResposta)!;
    private static readonly UUID UuidCccd = UUID.FromString(Gatt.UuidCccd)!;

    // O Android bloqueia o app que começa mais de 5 scans em 30 s.
    private const int ScansPorJanela = 5;
    private static readonly TimeSpan JanelaDeScans = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PrazoDeOperacao = TimeSpan.FromSeconds(10);

    private readonly Context _contexto;
    private readonly object _trava = new();
    private readonly Queue<DateTime> _inicioDosScans = new();
    private readonly SemaphoreSlim _operacaoGatt = new(1, 1);
    private readonly Channel<Action> _eventos = Channel.CreateUnbounded<Action>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    private BluetoothGatt? _gatt;
    private RetornoGatt? _retorno;
    private BluetoothGattCharacteristic? _escrita;
    private bool _conectado;

    public TransporteAndroid(Context contexto)
    {
        _contexto = contexto;
        _ = Task.Run(EntregarEventosAsync);
    }

    public event Action<byte[]>? Notificacao;
    public event Action? Caiu;

    // ------------------------------------------------------------------ scan

    public async Task<string?> ProcurarAsync(TimeSpan duracao, CancellationToken ct)
    {
        var adaptador = Adaptador();
        await EsperarVagaDeScanAsync(ct);
        var scanner = adaptador.BluetoothLeScanner
                      ?? throw new BluetoothIndisponivelException(MotivoBluetooth.Desligado);

        var achou = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var retorno = new RetornoScan(achou);
        var filtros = new List<ScanFilter>
        {
            new ScanFilter.Builder().SetServiceUuid(ParcelUuid.FromString(Gatt.UuidAnuncio))!.Build()!,
            new ScanFilter.Builder().SetServiceUuid(ParcelUuid.FromString(Gatt.UuidServico))!.Build()!,
        };
        var ajustes = new ScanSettings.Builder().SetScanMode(Android.Bluetooth.LE.ScanMode.LowLatency)!.Build();

        scanner.StartScan(filtros, ajustes, retorno);
        try
        {
            using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var prazo = Task.Delay(duracao, limite.Token);
            var primeira = await Task.WhenAny(achou.Task, prazo);
            limite.Cancel();
            ct.ThrowIfCancellationRequested();
            return primeira == achou.Task ? await achou.Task : null;
        }
        finally
        {
            try
            {
                scanner.StopScan(retorno);
            }
            catch (Exception)
            {
                // O Bluetooth pode ter sido desligado no meio do scan.
            }
        }
    }

    private async Task EsperarVagaDeScanAsync(CancellationToken ct)
    {
        TimeSpan espera;
        lock (_trava)
        {
            var agora = DateTime.UtcNow;
            while (_inicioDosScans.Count > 0 && agora - _inicioDosScans.Peek() >= JanelaDeScans)
                _inicioDosScans.Dequeue();
            espera = _inicioDosScans.Count >= ScansPorJanela
                ? _inicioDosScans.Peek() + JanelaDeScans - agora + TimeSpan.FromMilliseconds(200)
                : TimeSpan.Zero;
        }
        if (espera > TimeSpan.Zero)
            await Task.Delay(espera, ct);
        lock (_trava)
            _inicioDosScans.Enqueue(DateTime.UtcNow);
    }

    // ------------------------------------------------------------------ conexão

    public async Task ConectarAsync(string dispositivo, CancellationToken ct)
    {
        var adaptador = Adaptador();
        await LiberarAsync();

        var aparelho = adaptador.GetRemoteDevice(dispositivo)
                       ?? throw new FalhaConexaoException("endereço inválido");
        var retorno = new RetornoGatt(this);
        var gatt = aparelho.ConnectGatt(_contexto, false, retorno, BluetoothTransports.Le)
                   ?? throw new FalhaConexaoException("ConnectGatt devolveu null");
        lock (_trava)
        {
            _gatt = gatt;
            _retorno = retorno;
        }

        try
        {
            using var cancelar = ct.Register(retorno.Cancelar);

            await retorno.Conectou.Task;

            var descobrir = retorno.NovaOperacao();
            if (!gatt.DiscoverServices())
                throw new FalhaConexaoException("a descoberta de serviços foi recusada");
            await descobrir;

            // Serviço ausente costuma ser cache do GATT: conta como tentativa.
            var servico = gatt.GetService(UuidServico)
                          ?? throw new FalhaConexaoException("serviço do processador ausente");
            var escrita = servico.GetCharacteristic(UuidEscrita)
                          ?? throw new FalhaConexaoException("característica 1122 ausente");
            var resposta = servico.GetCharacteristic(UuidResposta)
                           ?? throw new FalhaConexaoException("característica 1123 ausente");

            if (!gatt.SetCharacteristicNotification(resposta, true))
                throw new FalhaConexaoException("a notificação foi recusada");
            var cccd = resposta.GetDescriptor(UuidCccd)
                       ?? throw new FalhaConexaoException("descritor 0x2902 ausente");
            // Notificação quando há; senão, indicação (como o app da Taramps).
            var valor = resposta.Properties.HasFlag(GattProperty.Notify)
                ? BluetoothGattDescriptor.EnableNotificationValue!.ToArray()
                : BluetoothGattDescriptor.EnableIndicationValue!.ToArray();

            var ligar = retorno.NovaOperacao();
            if (!EscreverDescritor(gatt, cccd, valor))
                throw new FalhaConexaoException("a escrita do descritor foi recusada");
            await ligar;

            lock (_trava)
            {
                if (!ReferenceEquals(_retorno, retorno))
                    throw new FalhaConexaoException("a conexão foi substituída");
                _escrita = escrita;
                _conectado = true;
            }
        }
        catch (Exception e)
        {
            await LiberarAsync();
            if (ct.IsCancellationRequested)
                throw new System.OperationCanceledException(ct);
            if (e is FalhaConexaoException or BluetoothIndisponivelException)
                throw;
            throw new FalhaConexaoException(e.GetType().Name);
        }
    }

    private static bool EscreverDescritor(BluetoothGatt gatt, BluetoothGattDescriptor cccd, byte[] valor)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
            return gatt.WriteDescriptor(cccd, valor) == (int)CurrentBluetoothStatusCodes.Success;
#pragma warning disable CA1422 // API antiga, para Android 8 a 12
        cccd.SetValue(valor);
        return gatt.WriteDescriptor(cccd);
#pragma warning restore CA1422
    }

    // ------------------------------------------------------------------ escrita

    public async Task EscreverAsync(byte[] dados, CancellationToken ct)
    {
        await _operacaoGatt.WaitAsync(ct);
        try
        {
            BluetoothGatt gatt;
            BluetoothGattCharacteristic escrita;
            RetornoGatt retorno;
            lock (_trava)
            {
                if (!_conectado || _gatt is null || _escrita is null || _retorno is null)
                    throw new InvalidOperationException("sem conexão");
                gatt = _gatt;
                escrita = _escrita;
                retorno = _retorno;
            }

            var feita = retorno.NovaOperacao();
            bool aceitou;
            if (OperatingSystem.IsAndroidVersionAtLeast(33))
            {
                aceitou = gatt.WriteCharacteristic(escrita, dados, (int)GattWriteType.Default)
                          == (int)CurrentBluetoothStatusCodes.Success;
            }
            else
            {
#pragma warning disable CA1422 // API antiga, para Android 8 a 12
                escrita.WriteType = GattWriteType.Default;
                escrita.SetValue(dados);
                aceitou = gatt.WriteCharacteristic(escrita);
#pragma warning restore CA1422
            }
            if (!aceitou)
                throw new IOException("o Android recusou a escrita");

            using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
            limite.CancelAfter(PrazoDeOperacao);
            using var cancelar = limite.Token.Register(retorno.Cancelar);
            await feita;
        }
        finally
        {
            _operacaoGatt.Release();
        }
    }

    // ------------------------------------------------------------------ desconexão

    public Task DesconectarAsync() => LiberarAsync();

    private Task LiberarAsync()
    {
        BluetoothGatt? gatt;
        RetornoGatt? retorno;
        lock (_trava)
        {
            gatt = _gatt;
            retorno = _retorno;
            _gatt = null;
            _retorno = null;
            _escrita = null;
            _conectado = false;
        }
        retorno?.Cancelar();
        if (gatt is not null)
        {
            try
            {
                gatt.Disconnect();
            }
            catch (Exception)
            {
                // já desconectado
            }
            gatt.Close();
        }
        return Task.CompletedTask;
    }

    /// <summary>Chamado pelo callback (thread do Binder) quando o link cai.</summary>
    private void AoDesconectar(RetornoGatt retorno)
    {
        bool estavaConectado;
        BluetoothGatt? gatt;
        lock (_trava)
        {
            if (!ReferenceEquals(_retorno, retorno))
                return;
            estavaConectado = _conectado;
            gatt = _gatt;
            _gatt = null;
            _retorno = null;
            _escrita = null;
            _conectado = false;
        }
        retorno.Cancelar();
        gatt?.Close();
        if (estavaConectado)
            _eventos.Writer.TryWrite(() => Caiu?.Invoke());
    }

    private void AoNotificar(RetornoGatt retorno, byte[] dados)
    {
        lock (_trava)
        {
            if (!ReferenceEquals(_retorno, retorno))
                return;
        }
        _eventos.Writer.TryWrite(() => Notificacao?.Invoke(dados));
    }

    private async Task EntregarEventosAsync()
    {
        await foreach (var evento in _eventos.Reader.ReadAllAsync())
        {
            try
            {
                evento();
            }
            catch (Exception)
            {
                // Um erro na sessão não pode parar a fila.
            }
        }
    }

    // ------------------------------------------------------------------ apoio

    private BluetoothAdapter Adaptador()
    {
        if (_contexto.PackageManager?.HasSystemFeature(PackageManager.FeatureBluetoothLe) != true)
            throw new BluetoothIndisponivelException(MotivoBluetooth.SemSuporte);
        if (!PermissaoBluetooth.Concedida(_contexto))
            throw new BluetoothIndisponivelException(MotivoBluetooth.SemPermissao);
        var gerente = (BluetoothManager?)_contexto.GetSystemService(Context.BluetoothService);
        var adaptador = gerente?.Adapter
                        ?? throw new BluetoothIndisponivelException(MotivoBluetooth.SemSuporte);
        if (!adaptador.IsEnabled)
            throw new BluetoothIndisponivelException(MotivoBluetooth.Desligado);
        return adaptador;
    }

    private sealed class RetornoScan(TaskCompletionSource<string?> achou) : ScanCallback
    {
        public override void OnScanResult(ScanCallbackType callbackType, ScanResult? result)
        {
            var endereco = result?.Device?.Address;
            if (endereco is not null)
                achou.TrySetResult(endereco);
        }

        public override void OnScanFailed(ScanFailure errorCode) =>
            achou.TrySetException(new FalhaConexaoException($"o scan falhou ({(int)errorCode})"));
    }

    /// <summary>Callbacks de uma conexão. Uma operação GATT pendente por vez.</summary>
    private sealed class RetornoGatt(TransporteAndroid dono) : BluetoothGattCallback
    {
        private readonly object _trava = new();
        private TaskCompletionSource? _operacao;

        public TaskCompletionSource Conectou { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task NovaOperacao()
        {
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_trava)
                _operacao = tcs;
            return tcs.Task;
        }

        public void Cancelar()
        {
            TaskCompletionSource? operacao;
            lock (_trava)
            {
                operacao = _operacao;
                _operacao = null;
            }
            Conectou.TrySetCanceled();
            operacao?.TrySetCanceled();
        }

        private void Terminar(GattStatus status, string oQue)
        {
            TaskCompletionSource? operacao;
            lock (_trava)
            {
                operacao = _operacao;
                _operacao = null;
            }
            if (status == GattStatus.Success)
                operacao?.TrySetResult();
            else
                operacao?.TrySetException(new FalhaConexaoException($"{oQue}: status {(int)status}"));
        }

        public override void OnConnectionStateChange(BluetoothGatt? gatt, GattStatus status, ProfileState newState)
        {
            if (newState == ProfileState.Connected && status == GattStatus.Success)
            {
                Conectou.TrySetResult();
                return;
            }
            if (newState == ProfileState.Disconnected || status != GattStatus.Success)
            {
                // Antes de conectar: a tentativa falhou (ex.: status 133). Depois: queda.
                Conectou.TrySetException(new FalhaConexaoException($"conexão: status {(int)status}"));
                lock (_trava)
                    _operacao?.TrySetException(new FalhaConexaoException($"desconectou: status {(int)status}"));
                dono.AoDesconectar(this);
            }
        }

        public override void OnServicesDiscovered(BluetoothGatt? gatt, GattStatus status) =>
            Terminar(status, "descoberta");

        public override void OnDescriptorWrite(BluetoothGatt? gatt, BluetoothGattDescriptor? descriptor, GattStatus status) =>
            Terminar(status, "descritor");

        public override void OnCharacteristicWrite(BluetoothGatt? gatt, BluetoothGattCharacteristic? characteristic, GattStatus status) =>
            Terminar(status, "escrita");

        // Android 13+: o valor vem no parâmetro. Não chama a base, para não entregar duas vezes.
        public override void OnCharacteristicChanged(BluetoothGatt gatt, BluetoothGattCharacteristic characteristic, byte[] value) =>
            dono.AoNotificar(this, value.ToArray());

        // Android 8 a 12.
#pragma warning disable CA1422, CS0672
        public override void OnCharacteristicChanged(BluetoothGatt? gatt, BluetoothGattCharacteristic? characteristic)
        {
            var valor = characteristic?.GetValue();
            if (valor is not null)
                dono.AoNotificar(this, valor.ToArray());
        }
#pragma warning restore CA1422, CS0672
    }
}
