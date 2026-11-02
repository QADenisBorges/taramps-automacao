using System.Net;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Porta;
using BgsControle.Nucleo.Seguranca;

namespace Agente.Avaliacao;

/// <summary>Um texto guardado em memória (os arquivos privados do celular).</summary>
public sealed class TextoMemoria(string? inicial = null) : IRepositorioTexto
{
    private string? _texto = inicial;

    public string? Texto => Volatile.Read(ref _texto);

    public Task<string?> LerAsync() => Task.FromResult(Texto);

    public Task GravarAsync(string conteudo)
    {
        Volatile.Write(ref _texto, conteudo);
        return Task.CompletedTask;
    }
}

/// <summary>As chaves de acesso guardadas em memória.</summary>
public sealed class ChavesMemoria : IRepositorioChaves
{
    private string? _json;

    public Task<string?> LerAsync() => Task.FromResult(Volatile.Read(ref _json));

    public Task GravarAsync(string json)
    {
        Volatile.Write(ref _json, json);
        return Task.CompletedTask;
    }
}

/// <summary>O cofre da senha do processador, já com a senha do simulador.</summary>
public sealed class CofreMemoria(string senha) : ICofreSenha
{
    private string? _senha = senha;

    public Task<string?> LerAsync() => Task.FromResult(_senha);

    public Task GuardarAsync(string senha)
    {
        _senha = senha;
        return Task.CompletedTask;
    }

    public Task ApagarAsync()
    {
        _senha = null;
        return Task.CompletedTask;
    }
}

/// <summary>
/// A rede local da bancada: só o loopback (o notebook e o "celular" são a mesma máquina). A <see cref="RedeLocal"/> do app
/// recusa o loopback; a porta aceita outra <see cref="IRedeLocal"/>, então a regra do app não muda.
/// </summary>
public sealed class RedeLocalhost : IRedeLocal
{
    public bool Aceita(IPAddress remoto) => IPAddress.IsLoopback(RedeLocal.Normalizar(remoto));

    public string? Endereco => "127.0.0.1";
}
