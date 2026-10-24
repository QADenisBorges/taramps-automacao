using BgsControle.Nucleo;
using BgsControle.Nucleo.Memorias;
using BgsControle.Nucleo.Setups;

namespace BgsControle.Telas;

/// <summary>
/// O que o cartão da memória em uso diz (EXPERIENCE, "O que o cartão da memória em uso mostra"). O nome
/// aparece só quando o app sabe o que está tocando.
/// </summary>
/// <param name="Nome">"FILME", "MUSICA ORIG", "Som do aparelho", "Nenhuma memória" ou null (sem conexão, ainda conferindo).</param>
/// <param name="Linha">"Memória 2 do aparelho (app da Taramps)", "O app não sabe qual memória está tocando.", ou null.</param>
/// <param name="NomePreset">O preset lembrado pela memória do celular em uso (o chip), "Nenhum", ou null sem chip.</param>
/// <param name="SomDiferente">"Som diferente da FILME" (com Reaplicar), ou null.</param>
/// <param name="SemMemorias">Setup sem memórias: o cartão oferece "Criar memória do som atual".</param>
public sealed record SomEmUsoTexto(string? Nome, string? Linha, string? NomePreset, string? SomDiferente, bool SemMemorias)
{
    public const string SomDoAparelho = "Som do aparelho";
    public const string NaoSabe = "O app não sabe qual memória está tocando.";
    public const string Nenhuma = "Nenhuma memória";
    public const string SemPreset = "Nenhum";

    /// <summary>"Memória 2 do aparelho (app da Taramps)".</summary>
    public static string LinhaDoAparelho(int indice) => $"Memória {indice + 1} do aparelho (app da Taramps)";

    public static SomEmUsoTexto De(GerenteSetups setups, GerenteMemorias memorias, Sessao sessao)
    {
        if (sessao.Estado != EstadoSessao.Conectado)
            return new(null, null, null, null, false);
        var uso = setups.EmUso;
        switch (uso.Tipo)
        {
            case TipoSomEmUso.MemoriaCelular when setups.Memoria(uso.IdMemoria) is { } m:
                return new(m.Nome, null, setups.PresetDe(m)?.Nome ?? SemPreset, null, false);
            case TipoSomEmUso.MemoriaCelular:
                // A memória em uso não existe mais (apagada): o app não sabe o que toca.
                return new(SomDoAparelho, NaoSabe, null, null, false);
            case TipoSomEmUso.MemoriaAparelho when uso.MemoriaAparelho is { } n:
                return new(memorias.Nome(n) is { Length: > 0 } nome ? nome : $"Memória {n + 1}", LinhaDoAparelho(n), null, null, false);
            case TipoSomEmUso.SomDoAparelho:
            {
                var diferente = setups.Estado == EstadoConferencia.SomDiferente && setups.Ativa(setups.SetupEmUso?.Id) is { } ativa
                    ? GerenteSetups.MsgSomDiferente(ativa.Nome)
                    : null;
                return new(SomDoAparelho, NaoSabe, null, diferente, false);
            }
            default:
                // Conectado, conferido, num Setup sem memórias. Antes da conferência, nada.
                if (setups.Conferido && !setups.EmEscolha && setups.SetupEmUso is not null)
                    return new(Nenhuma, null, null, null, true);
                return new(null, null, null, null, false);
        }
    }
}
