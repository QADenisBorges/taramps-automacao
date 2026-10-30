namespace BgsControle.Nucleo.Uso;

/// <summary>Em que pé está a IA.</summary>
public enum EstadoIa
{
    /// <summary>Sem tarefa e sem comando da IA desde o último revogar o acesso ou fim de tarefa.</summary>
    Inativa,

    /// <summary>Uma tarefa foi iniciada, ou chegou um comando da IA.</summary>
    Ativa,

    /// <summary>O Denis mexeu (o toque pausa): a IA espera 10 s sem toques.</summary>
    Pausada,
}
