// abertura.js · o som da abertura da Vitrine BGS Sound Lab (EXPERIENCE.md · Abertura audiovisual).
//
// O único som de todo o painel: cerca de 10 s de efeitos sintetizados com Web Audio, em 4 fases
// (início, construção, clímax, finalização). Sem música, sem arquivo de áudio, sem rede. Ao fim,
// ou ao pular, o AudioContext de saída é fechado e nada mais toca nessa carga da página.
//
// Cadeia final: fontes → mestre (GainNode) → limitador (DynamicsCompressor, −12 dB, 20:1, 3 ms)
// → compensação do ganho automático do compressor → destination. O pico fica perto de −12 dBFS;
// o limiter do processador é a segunda camada. Um AnalyserNode lê o barramento para o anel de
// luz seguir o envelope do próprio som (ele não vai para a saída).

const DURACAO_S = 10.0;
const DURACAO_REDUZIDA_S = 2.5;
const SAIDA_SUAVE_S = 0.15; // pular: o mestre desce a zero nisso, sem estalo
export const ATRASO_S = 0.06; // o som começa isto depois de iniciar(): folga para o agendamento
const TETO_DB = -12;
const RAZAO = 20;
const MARGEM_DB = 2; // o ataque de 3 ms deixa passar ~2 dB nos transientes (medido offline)

// O DynamicsCompressorNode aplica sozinho um ganho de compensação de (1 / ganho em 0 dBFS)^0,6
// (especificação Web Audio). Para o teto valer de verdade, um GainNode depois dele desfaz isso,
// com uma margem para o que o ataque deixa passar.
function compensacaoDoLimitador() {
  const saidaEm0dB = TETO_DB + (0 - TETO_DB) / RAZAO; // joelho 0
  const ganho = Math.pow(10, saidaEm0dB / 20);
  return Math.pow(10, -MARGEM_DB / 20) / Math.pow(1 / ganho, 0.6);
}

function bufferDeRuido(ctx, segundos) {
  const n = Math.floor(ctx.sampleRate * segundos);
  const buf = ctx.createBuffer(1, n, ctx.sampleRate);
  const d = buf.getChannelData(0);
  for (let i = 0; i < n; i++) d[i] = Math.random() * 2 - 1;
  return buf;
}

// Resposta ao impulso gerada no código: ruído estéreo com decaimento (sala larga).
function respostaAoImpulso(ctx, segundos, curva) {
  const n = Math.floor(ctx.sampleRate * segundos);
  const atraso = Math.floor(ctx.sampleRate * 0.018);
  const buf = ctx.createBuffer(2, n, ctx.sampleRate);
  for (let c = 0; c < 2; c++) {
    const d = buf.getChannelData(c);
    for (let i = atraso; i < n; i++) d[i] = (Math.random() * 2 - 1) * Math.pow(1 - i / n, curva);
  }
  return buf;
}

/**
 * Cria o som da abertura. Precisa ser chamado dentro do gesto (clique ou tecla) do
 * "Toque para começar". Devolve null se o áudio do navegador não abrir: a abertura roda sem som.
 */
export function criarAbertura({ reduzido = false } = {}) {
  const AC = window.AudioContext || window.webkitAudioContext;
  if (!AC) return null;
  let ctx;
  try {
    ctx = new AC({ latencyHint: 'interactive' });
  } catch (e) {
    return null;
  }
  let retomada = Promise.resolve();
  try {
    const r = ctx.resume();
    if (r && r.then) retomada = r.catch(() => {}); // segue: o estado real diz se tocou
  } catch (e) { /* idem */ }

  const mestre = ctx.createGain();
  mestre.gain.value = 0.9;
  const limitador = ctx.createDynamicsCompressor();
  limitador.threshold.value = TETO_DB;
  limitador.knee.value = 0;
  limitador.ratio.value = RAZAO;
  limitador.attack.value = 0.003;
  limitador.release.value = 0.25;
  const compensa = ctx.createGain();
  compensa.gain.value = compensacaoDoLimitador();
  mestre.connect(limitador);
  limitador.connect(compensa);
  compensa.connect(ctx.destination);

  const analisador = ctx.createAnalyser(); // só lê: não vai para destination
  analisador.fftSize = 2048;
  analisador.smoothingTimeConstant = 0.6;
  compensa.connect(analisador);

  const reverb = ctx.createConvolver();
  reverb.buffer = respostaAoImpulso(ctx, 3.0, 3.2);
  const molhado = ctx.createGain();
  molhado.gain.value = 0.32;
  reverb.connect(molhado);
  molhado.connect(mestre);

  const ruido = bufferDeRuido(ctx, 2.0);
  const duracao = reduzido ? DURACAO_REDUZIDA_S : DURACAO_S;
  let estado = 'pronto';
  let fechando = null;

  // ---------------------------------------------------------------- peças
  function saida(nivelSeco, envio) {
    const g = ctx.createGain();
    g.gain.value = 0;
    const seco = ctx.createGain();
    seco.gain.value = nivelSeco;
    g.connect(seco);
    seco.connect(mestre);
    if (envio) {
      const e = ctx.createGain();
      e.gain.value = envio;
      g.connect(e);
      e.connect(reverb);
    }
    return g;
  }

  function osc(tipo, freq, destino, inicio, fim) {
    const o = ctx.createOscillator();
    o.type = tipo;
    o.frequency.value = freq;
    o.connect(destino);
    o.start(inicio);
    o.stop(fim);
    return o;
  }

  function fonteDeRuido(destino, inicio, fim) {
    const s = ctx.createBufferSource();
    s.buffer = ruido;
    s.loop = true;
    s.connect(destino);
    s.start(inicio, Math.random() * 1.5);
    s.stop(fim);
    return s;
  }

  // Fase 1 (0–2 s) e fundo da 2: drone grave misterioso que cresce do silêncio.
  function drone(t0) {
    const g = saida(1, 0.15);
    const lp = ctx.createBiquadFilter();
    lp.type = 'lowpass';
    lp.Q.value = 0.7;
    lp.frequency.setValueAtTime(180, t0);
    lp.frequency.exponentialRampToValueAtTime(650, t0 + 5.5);
    lp.connect(g);
    const g2 = ctx.createGain();
    g2.gain.value = 0.5;
    g2.connect(lp);
    const g3 = ctx.createGain();
    g3.gain.value = 0.3;
    g3.connect(lp);
    osc('sine', 45, lp, t0, t0 + 6.6);
    osc('sine', 90, g2, t0, t0 + 6.6);
    osc('triangle', 45.4, g3, t0, t0 + 6.6);
    g.gain.setValueAtTime(0.0001, t0);
    g.gain.exponentialRampToValueAtTime(0.11, t0 + 2.0);
    g.gain.linearRampToValueAtTime(0.19, t0 + 5.5);
    g.gain.setValueAtTime(0.19, t0 + 5.8);
    g.gain.exponentialRampToValueAtTime(0.0001, t0 + 6.4);
  }

  // Fase 1: brilho distante (senos agudos fracos, levemente desafinados).
  function brilhoDistante(t0) {
    const g = saida(0.35, 0.9);
    const trem = ctx.createGain();
    trem.gain.value = 0.6;
    trem.connect(g);
    const lfo = ctx.createOscillator();
    lfo.frequency.value = 0.45;
    const prof = ctx.createGain();
    prof.gain.value = 0.35;
    lfo.connect(prof);
    prof.connect(trem.gain);
    lfo.start(t0);
    lfo.stop(t0 + 6.2);
    for (const f of [1760, 1763.4, 2637, 2641.5]) osc('sine', f, trem, t0, t0 + 6.2);
    g.gain.setValueAtTime(0.0001, t0 + 0.4);
    g.gain.exponentialRampToValueAtTime(0.012, t0 + 2.0);
    g.gain.linearRampToValueAtTime(0.024, t0 + 5.5);
    g.gain.exponentialRampToValueAtTime(0.0001, t0 + 5.95);
  }

  // Fase 2 (2–5,5 s): riser de ruído filtrado com o passa-banda subindo.
  function riser(t0) {
    const g = saida(1, 0.5);
    const bp = ctx.createBiquadFilter();
    bp.type = 'bandpass';
    bp.Q.value = 7;
    bp.frequency.setValueAtTime(260, t0 + 2.0);
    bp.frequency.exponentialRampToValueAtTime(7200, t0 + 5.7);
    bp.connect(g);
    fonteDeRuido(bp, t0 + 2.0, t0 + 5.9);
    g.gain.setValueAtTime(0.0001, t0 + 2.0);
    g.gain.exponentialRampToValueAtTime(0.13, t0 + 5.7);
    g.gain.linearRampToValueAtTime(0, t0 + 5.82);
  }

  // Fase 2: pulsos graves que aceleram.
  function pulsos(t0) {
    const g = saida(1, 0.2);
    g.gain.value = 1;
    const a = 2.4, b = 5.7;
    let t = a;
    while (t < b) {
      const k = (t - a) / (b - a);
      const o = ctx.createOscillator();
      const e = ctx.createGain();
      o.type = 'sine';
      o.frequency.setValueAtTime(118, t0 + t);
      o.frequency.exponentialRampToValueAtTime(52, t0 + t + 0.09);
      e.gain.setValueAtTime(0.0001, t0 + t);
      e.gain.exponentialRampToValueAtTime(0.05 + 0.11 * k, t0 + t + 0.004);
      e.gain.exponentialRampToValueAtTime(0.0001, t0 + t + 0.13);
      o.connect(e);
      e.connect(g);
      o.start(t0 + t);
      o.stop(t0 + t + 0.15);
      t += 0.55 * Math.pow(0.07 / 0.55, k);
    }
  }

  // Fase 2: shimmer em camadas (um acorde aberto que sobe em intensidade).
  function shimmer(t0) {
    const g = saida(0.5, 0.8);
    for (const f of [587.33, 588.7, 880, 881.6, 1174.66, 1176.9]) osc('sine', f, g, t0 + 3.4, t0 + 5.95);
    g.gain.setValueAtTime(0.0001, t0 + 3.4);
    g.gain.exponentialRampToValueAtTime(0.018, t0 + 5.7);
    g.gain.exponentialRampToValueAtTime(0.0001, t0 + 5.86);
  }

  // Fase 3 (~5,8 s): impacto sub, explosão de brilho e reverberação larga, junto com o nome.
  function impacto(t) {
    const sub = saida(1, 0.25);
    const o = ctx.createOscillator();
    o.type = 'sine';
    o.frequency.setValueAtTime(62, t);
    o.frequency.exponentialRampToValueAtTime(30, t + 1.2);
    o.connect(sub);
    o.start(t);
    o.stop(t + 2.0);
    sub.gain.setValueAtTime(0.0001, t);
    sub.gain.linearRampToValueAtTime(0.5, t + 0.008);
    sub.gain.exponentialRampToValueAtTime(0.001, t + 1.8);

    const corpo = saida(1, 0.3);
    const o2 = ctx.createOscillator();
    o2.type = 'triangle';
    o2.frequency.setValueAtTime(124, t);
    o2.frequency.exponentialRampToValueAtTime(58, t + 0.5);
    o2.connect(corpo);
    o2.start(t);
    o2.stop(t + 0.7);
    corpo.gain.setValueAtTime(0.0001, t);
    corpo.gain.linearRampToValueAtTime(0.13, t + 0.006);
    corpo.gain.exponentialRampToValueAtTime(0.0001, t + 0.6);

    const brilho = saida(0.7, 1.2);
    const hp = ctx.createBiquadFilter();
    hp.type = 'highpass';
    hp.frequency.value = 1800;
    hp.connect(brilho);
    fonteDeRuido(hp, t, t + 0.6);
    brilho.gain.setValueAtTime(0.0001, t);
    brilho.gain.linearRampToValueAtTime(0.2, t + 0.005);
    brilho.gain.exponentialRampToValueAtTime(0.0005, t + 0.5);
  }

  // Fase 4 (~7,6 s): assinatura sonora, três notas de sino em FM, decaindo até o silêncio.
  function sino(t, f, nivel, decaimento) {
    const g = saida(1, 0.55);
    const portadora = ctx.createOscillator();
    portadora.type = 'sine';
    portadora.frequency.value = f;
    const modulador = ctx.createOscillator();
    modulador.type = 'sine';
    modulador.frequency.value = f * 1.4;
    const indice = ctx.createGain();
    indice.gain.setValueAtTime(f * 2.0, t);
    indice.gain.exponentialRampToValueAtTime(f * 0.08, t + decaimento * 0.6);
    modulador.connect(indice);
    indice.connect(portadora.frequency);
    portadora.connect(g);
    const parcial = ctx.createOscillator();
    parcial.type = 'sine';
    parcial.frequency.value = f * 2.0;
    const gp = ctx.createGain();
    gp.gain.value = 0.18;
    parcial.connect(gp);
    gp.connect(g);
    for (const o of [portadora, modulador, parcial]) { o.start(t); o.stop(t + decaimento + 0.1); }
    g.gain.setValueAtTime(0.0001, t);
    g.gain.linearRampToValueAtTime(nivel, t + 0.004);
    g.gain.exponentialRampToValueAtTime(0.0001, t + decaimento);
  }

  function assinatura(t, fim) {
    sino(t, 659.25, 0.12, 1.9);
    sino(t + 0.24, 987.77, 0.1, 1.9);
    sino(t + 0.5, 1318.51, 0.085, Math.min(2.2, fim - (t + 0.5)));
  }

  // ---------------------------------------------------------------- controle
  function iniciar() {
    if (estado !== 'pronto') return;
    estado = 'tocando';
    const t0 = ctx.currentTime + ATRASO_S;
    if (reduzido) {
      assinatura(t0 + 0.2, t0 + duracao - 0.2);
      mestre.gain.setValueAtTime(0.9, t0 + duracao - 0.7);
      mestre.gain.linearRampToValueAtTime(0, t0 + duracao - 0.15);
      return;
    }
    drone(t0);
    brilhoDistante(t0);
    riser(t0);
    pulsos(t0);
    shimmer(t0);
    impacto(t0 + 5.8);
    assinatura(t0 + 7.6, t0 + duracao - 0.2);
    // O fim desce suave até o silêncio (a cauda da reverberação também), antes de fechar.
    mestre.gain.setValueAtTime(0.9, t0 + 8.9);
    mestre.gain.linearRampToValueAtTime(0, t0 + duracao - 0.15);
  }

  function fechar() {
    if (fechando) return fechando;
    estado = 'fechando';
    fechando = ctx.close()
      .catch(() => {})
      .then(() => { estado = 'fechado'; return ctx.state; });
    return fechando;
  }

  function pular() {
    if (fechando) return fechando;
    const agora = ctx.currentTime;
    const g = mestre.gain;
    try {
      if (typeof g.cancelAndHoldAtTime === 'function') g.cancelAndHoldAtTime(agora);
      else { g.cancelScheduledValues(agora); g.setValueAtTime(g.value, agora); }
      g.linearRampToValueAtTime(0, agora + SAIDA_SUAVE_S);
    } catch (e) { /* fecha mesmo assim */ }
    return new Promise(ok => setTimeout(ok, SAIDA_SUAVE_S * 1000 + 40)).then(fechar);
  }

  /** Espera o contexto rodar (o resume() do gesto), no máximo `ms`. Devolve o estado real. */
  function pronto(ms = 400) {
    if (ctx.state === 'running') return Promise.resolve(ctx.state);
    return Promise.race([retomada, new Promise(ok => setTimeout(ok, ms))]).then(() => ctx.state);
  }

  const api = {
    duracao,
    analisador,
    iniciar,
    pular,
    fechar,
    pronto,
    aoMudar: () => {},
    get estado() { return ctx.state; },
    get tocando() { return estado === 'tocando' && ctx.state === 'running'; },
    // O que separa o agendamento do som do que sai na caixa (Bluetooth inclusive), em segundos.
    get latencia() { return ctx.outputLatency || ctx.baseLatency || 0; },
  };
  ctx.onstatechange = () => { try { api.aoMudar(ctx.state); } catch (e) { /* nada */ } };
  return api;
}
