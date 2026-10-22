namespace BgsControle.Nucleo.Uso;

/// <summary>
/// O que o toque do Denis faz enquanto a IA trabalha (decisão do Denis, 21/10). Fica guardado no
/// celular, em <c>uso-ia.json</c>; só a tela muda.
/// </summary>
public enum ModoToque
{
    /// <summary>"Controles travados até PARAR IA" (o padrão).</summary>
    TravarAtePararIa,

    /// <summary>"O toque pausa a IA": a IA retoma depois de 10 s sem toques.</summary>
    PausarIa,
}

/// <summary>Em que pé está a IA.</summary>
public enum EstadoIa
{
    /// <summary>Sem tarefa e sem comando da IA desde o último PARAR IA ou fim de tarefa.</summary>
    Inativa,

    /// <summary>Uma tarefa foi iniciada, ou chegou um comando da IA.</summary>
    Ativa,

    /// <summary>No modo "O toque pausa a IA", o Denis mexeu: a IA espera 10 s sem toques.</summary>
    Pausada,
}
