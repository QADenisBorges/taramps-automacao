using Android.Content;
using Android.Media;
using Android.Provider;
using BgsControle.Nucleo.Player;
using BgsControle.Telas;

namespace BgsControle.App.Plataforma;

/// <summary>
/// O seletor de arquivos de áudio do Android (história 11): ACTION_OPEN_DOCUMENT com <c>audio/*</c> e
/// várias músicas de uma vez. Para cada uma, guarda a permissão persistente da URI (o Player toca depois
/// de reiniciar o celular) e lê o nome, a pasta e a duração. O app guarda só a URI; não copia a música.
/// </summary>
public sealed class SeletorMusicas : ISeletorAudio
{
    private const int Codigo = 4208;
    private static TaskCompletionSource<Intent?>? _pedido;

    public async Task<IReadOnlyList<ArquivoAudio>> EscolherAsync()
    {
        var dados = await MainThread.InvokeOnMainThreadAsync(() =>
        {
            var atividade = Platform.CurrentActivity;
            if (atividade is null)
                return Task.FromResult<Intent?>(null);
            _pedido?.TrySetResult(null);
            var pedido = new TaskCompletionSource<Intent?>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pedido = pedido;
            var intent = new Intent(Intent.ActionOpenDocument);
            intent.AddCategory(Intent.CategoryOpenable);
            intent.SetType("audio/*");
            intent.PutExtra(Intent.ExtraAllowMultiple, true);
            intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantPersistableUriPermission);
#pragma warning disable CA1422 // StartActivityForResult segue funcionando em todas as versões
            atividade.StartActivityForResult(intent, Codigo);
#pragma warning restore CA1422
            return pedido.Task;
        });
        var escolhida = dados;
        if (escolhida is null)
            return []; // desistiu

        var uris = new List<Android.Net.Uri>();
        if (escolhida.ClipData is { } clip)
        {
            for (var i = 0; i < clip.ItemCount; i++)
            {
                if (clip.GetItemAt(i)?.Uri is { } u)
                    uris.Add(u);
            }
        }
        else if (escolhida.Data is { } uma)
        {
            uris.Add(uma);
        }
        // Ler a duração abre cada arquivo: fora da thread da tela.
        return await Task.Run(() => uris.Select(Ler).ToList());
    }

    /// <summary>Chamado pela MainActivity.</summary>
    public static void Resultado(int requestCode, Android.App.Result resultCode, Intent? data)
    {
        if (requestCode != Codigo)
            return;
        var pedido = _pedido;
        _pedido = null;
        pedido?.TrySetResult(resultCode == Android.App.Result.Ok ? data : null);
    }

    private static ArquivoAudio Ler(Android.Net.Uri uri)
    {
        var contexto = Android.App.Application.Context;
        var resolver = contexto.ContentResolver;
        try
        {
            // A música continua acessível depois de reiniciar o app e o celular.
            resolver?.TakePersistableUriPermission(uri, ActivityFlags.GrantReadUriPermission);
        }
        catch (Java.Lang.SecurityException)
        {
            // O provedor não dá permissão persistente: toca enquanto o Android deixar.
        }

        var nome = NomeExibido(resolver, uri) ?? uri.LastPathSegment ?? "Música";
        return new ArquivoAudio(uri.ToString()!, Path.GetFileNameWithoutExtension(nome), Pasta(uri), Duracao(contexto, uri));
    }

    private static string? NomeExibido(ContentResolver? resolver, Android.Net.Uri uri)
    {
        try
        {
            using var cursor = resolver?.Query(uri, [IOpenableColumns.DisplayName], null, null, null);
            if (cursor is not null && cursor.MoveToFirst())
                return cursor.GetString(0);
        }
        catch (Exception)
        {
            // Sem o nome: fica o fim da URI.
        }
        return null;
    }

    /// <summary>"Music/Trilhas", pelo id do documento ("primary:Music/Trilhas/abertura.mp3"); "" sem saber.</summary>
    private static string Pasta(Android.Net.Uri uri)
    {
        try
        {
            if (!DocumentsContract.IsDocumentUri(Android.App.Application.Context, uri))
                return "";
            var id = DocumentsContract.GetDocumentId(uri) ?? "";
            var caminho = id.Contains(':') ? id[(id.IndexOf(':') + 1)..] : id;
            var barra = caminho.LastIndexOf('/');
            return barra > 0 ? caminho[..barra] : "";
        }
        catch (Exception)
        {
            return "";
        }
    }

    private static TimeSpan Duracao(Context contexto, Android.Net.Uri uri)
    {
        using var leitor = new MediaMetadataRetriever();
        try
        {
            leitor.SetDataSource(contexto, uri);
            return long.TryParse(leitor.ExtractMetadata(MetadataKey.Duration), out var ms) ? TimeSpan.FromMilliseconds(ms) : TimeSpan.Zero;
        }
        catch (Exception)
        {
            return TimeSpan.Zero;
        }
    }
}
