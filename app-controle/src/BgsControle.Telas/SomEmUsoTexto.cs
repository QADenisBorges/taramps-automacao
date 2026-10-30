using BgsControle.Nucleo;
using BgsControle.Nucleo.Setups;

namespace BgsControle.Telas;

/// <summary>
/// O que o cartão da memória em uso diz (EXPERIENCE, "O que o cartão da memória em uso mostra"). O nome
/// aparece só quando o app sabe o que está tocando.
/// </summary>
/// <param name="Nome">"FILME", "MUSICA ORIG", "Som do aparelho", "Nenhuma memória" ou null (sem conexão, ainda conferindo).</param>
/// <param name="Linha">"O app não sabe qual memória está tocando.", ou null.</param>
/// <param name="NomePreset">O preset lembrado pela memória do celular em uso (o chip), "Nenhum", ou null sem chip.</param>
/// <param name="SomDiferente">"Som diferente da FILME" (com Reaplicar), ou null.</param>
/// <param name="SemMemorias">Setup sem memórias: o cartão oferece "Criar memória do som atual".</param>
public sealed record SomEmUsoTexto(string? Nome, string? Linha, string? NomePreset, string? SomDiferente, bool SemMemorias)
{
    public const string SomDoAparelho = "Som do aparelho";
    public const string NaoSabe = "O app não sabe qual memória está tocando.";
    public const string Nenhuma = "Nenhuma memória";
    public const string SemPreset = "Nenhum";

    /// <summary>
    /// O texto do som em uso. As memórias do processador não aparecem (decisão do Denis, 29/10/2026):
    /// o que não é uma memória do celular é o "Som do aparelho".
    /// </summary>
    public static SomEmUsoTexto De(GerenteSetups setups, Sessao sessao)
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
