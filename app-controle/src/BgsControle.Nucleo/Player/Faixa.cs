namespace BgsControle.Nucleo.Player;

/// <summary>
/// Um arquivo de áudio do celular, como o seletor do Android devolve: a URI (o app guarda só o
/// endereço, não copia a música), o nome, a pasta e a duração.
/// </summary>
public sealed record ArquivoAudio(string Uri, string Nome, string Pasta, TimeSpan Duracao);

/// <summary>
/// Uma faixa de uma playlist (CAP-15): um id único e estável, a URI do arquivo, o nome, a pasta e a
/// duração. Tudo vem do seletor na hora de adicionar.
/// </summary>
public sealed record Faixa(string Id, string Uri, string Nome, string Pasta, TimeSpan Duracao);
