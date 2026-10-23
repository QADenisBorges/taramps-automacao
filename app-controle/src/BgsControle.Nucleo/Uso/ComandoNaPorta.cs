using System.Diagnostics;
using System.Text.RegularExpressions;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Protocolo;

namespace BgsControle.Nucleo.Uso;

/// <summary>O que um comando faz, para o histórico e para o uso compartilhado.</summary>
public enum TipoComando
{
    /// <summary>Um ajuste de áudio do Marco 1 (MAPA §4).</summary>
    AjusteAudio,

    /// <summary>Não sai mais do app (história 8c); fica para ler o <c>historico.json</c> antigo.</summary>
    SalvarMemoria,
    CarregarMemoria,

    /// <summary>Não sai mais do app (história 8c); fica para ler o <c>historico.json</c> antigo.</summary>
    NomearMemoria,

    /// <summary>Um parâmetro do gerador ou o liga/desliga (200–202, 210–213).</summary>
    Gerador,
    RemoteOut,
    BluetoothAudio,
    NomeProduto,
    ResetFabrica,
    ResetAudio,

    /// <summary>Entra sem as senhas.</summary>
    TrocaSenha,

    /// <summary>Um texto fora do catálogo (a porta recusa); só o código, se houver.</summary>
    Desconhecido,
}

/// <summary>
/// Um comando de escrita que chegou à porta, já sem nada secreto: o tipo, o endereço (o código e,
/// quando há, o canal ou a banda) e o valor como vai no fio. A troca de senha não leva as senhas;
/// um texto desconhecido leva só o código.
/// </summary>
public sealed record ComandoNaPorta(TipoComando Tipo, Endereco? Endereco, string? Valor)
{
    private static readonly Regex QuadroSenha = new(@"^\*000\*", RegexOptions.CultureInvariant);

    // A forma de leitura (#CCC#…, ou CCC#b da banda): mesmo fora do catálogo, nunca entra.
    private static readonly Regex FormaDeLeitura = new(@"^(#|\d{3}#)", RegexOptions.CultureInvariant);

    /// <summary><c>*202*0*</c>: desligar o gerador nunca fica travado.</summary>
    public bool DesligaGerador =>
        Tipo == TipoComando.Gerador && Endereco is { Codigo: Catalogo.CodigoGeradorLigado } && Valor == "0";

    /// <summary>
    /// O comando de um quadro, ou null para o que nunca entra no histórico: as leituras do catálogo
    /// (e qualquer quadro na forma de leitura, como <c>#004#</c>) e o quadro da senha (qualquer
    /// texto do código 000).
    /// </summary>
    public static ComandoNaPorta? Ler(string quadro)
    {
        if (Catalogo.EhLeitura(quadro) || FormaDeLeitura.IsMatch(quadro) || QuadroSenha.IsMatch(quadro))
            return null;

        if (Catalogo.EscritaDeAudio(quadro) is { } audio)
            return new(TipoComando.AjusteAudio, new Endereco(audio.Parametro.Codigo, audio.Indice), audio.NoFio);

        if (Catalogo.ComandoDeMemoria(quadro) is { } memoria)
            return new(TipoComando.CarregarMemoria, new Endereco(Catalogo.CodigoCarregarMemoria, null), Texto(memoria.Memoria));

        if (Catalogo.ComandoDeGerador(quadro) is { } gerador)
        {
            var q = Quadros.Analisar(quadro)!;
            return new(TipoComando.Gerador, new Endereco(gerador.Parametro.Codigo, null), q.Segmentos[0]);
        }

        if (Catalogo.ComandoDeAparelho(quadro) is { } aparelho)
        {
            return aparelho.Tipo switch
            {
                TipoComandoAparelho.RemoteOut => new(TipoComando.RemoteOut,
                    new Endereco(Catalogo.CodigoRemoteOut, null), aparelho.Ligar == true ? "1" : "0"),
                TipoComandoAparelho.BluetoothAudio => new(TipoComando.BluetoothAudio,
                    new Endereco(Catalogo.CodigoBluetoothAudio, null), aparelho.Ligar == true ? "1" : "0"),
                TipoComandoAparelho.NomeProduto => new(TipoComando.NomeProduto,
                    new Endereco(Catalogo.CodigoNomeProduto, null), aparelho.Nome),
                TipoComandoAparelho.ResetFabrica => new(TipoComando.ResetFabrica,
                    new Endereco(Catalogo.CodigoResetFabrica, null), null),
                TipoComandoAparelho.ResetAudio => new(TipoComando.ResetAudio,
                    new Endereco(Catalogo.CodigoResetAudio, null), null),
                TipoComandoAparelho.TrocaSenha => new(TipoComando.TrocaSenha,
                    new Endereco(Catalogo.CodigoTrocaSenha, null), null),
                _ => throw new UnreachableException($"Comando de aparelho sem tipo no histórico: {aparelho.Tipo}"),
            };
        }

        // Fora do catálogo: nunca o texto (pode trazer uma senha), só o código.
        var codigo = Quadros.Analisar(quadro)?.Codigo;
        if (codigo == Quadros.CodigoSenha)
            return null; // a senha fora do formato também nunca entra
        if (codigo == Catalogo.CodigoTrocaSenha)
            return new(TipoComando.TrocaSenha, new Endereco(codigo, null), null);
        return new(TipoComando.Desconhecido, codigo is null ? null : new Endereco(codigo, null), null);
    }

    private static string Texto(int n) => n.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
