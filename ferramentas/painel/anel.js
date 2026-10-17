// anel.js · o anel de luz da Vitrine (DESIGN.md · Anel de luz; EXPERIENCE.md · Reação ao som).
//
// Desenha num quadro lógico de 1920 × 1080 (centro 960 × 402, raio base 232). Ele só LÊ som:
// na abertura, o AnalyserNode do próprio som sintetizado; na Vitrine, o microfone. O microfone
// abre num AudioContext só de entrada, e a fonte vai apenas para um AnalyserNode: nada daqui é
// ligado a uma saída de áudio. Sem microfone (ou com a sala em silêncio) o anel só respira,
// devagar e claramente calmo. Nunca simula batida.

const W = 1920, H = 1080, CX = 960, CY = 402, R0 = 232;
const NB = 64; // faixas para a deformação do contorno
const LUZ_FRIA = '#8FB4FF', LUZ_LILAS = '#C59BFF', LUZ_QUENTE = '#FFD6A8';
const OCIOSO_PERIODO_S = 6; // motion.anel-ocioso-periodo
const DPR_MAX = 1.5, LARGURA_MAX = 2560; // backing store do canvas: numa TV 4K não passa disso
const QUADRO_LENTO_MS = 24; // média acima disso: os halos param de usar blur
const ATAQUE = 0.55, SOLTURA = 0.10; // por quadro a 60 fps (motion.anel-ataque / anel-soltura)

const lim = (x, a = 0, b = 1) => Math.max(a, Math.min(b, x));
const suave = x => { x = lim(x); return x * x * (3 - 2 * x); };
const saidaExpo = x => (x >= 1 ? 1 : 1 - Math.pow(2, -10 * lim(x)));

// ---------------------------------------------------------------- leitura de um AnalyserNode

/** Resume o espectro em grave (20–150 Hz), nível geral e 64 faixas de 0 a 1, com pisos e
 * picos que se adaptam à sala (o ganho do microfone muda de notebook para notebook). */
export class Analise {
  constructor(analisador) {
    this.a = analisador;
    this.db = new Float32Array(analisador.frequencyBinCount);
    const hzPorBin = analisador.context.sampleRate / analisador.fftSize;
    const bin = hz => lim(Math.round(hz / hzPorBin), 1, this.db.length - 1);
    this.faixaGrave = [bin(20), Math.max(bin(150), bin(20) + 1)];
    this.faixaGeral = [bin(20), bin(8000)];
    this.faixas = [];
    for (let i = 0; i < NB; i++) {
      const f1 = 20 * Math.pow(16000 / 20, i / NB), f2 = 20 * Math.pow(16000 / 20, (i + 1) / NB);
      const b1 = bin(f1);
      this.faixas.push([b1, Math.max(b1 + 1, bin(f2))]);
    }
    this.bandas = new Float32Array(NB);
    this.pico = -50; // pico recente do grave (dB), desce 3 dB/s
    this.piso = -80; // piso de ruído (dB): sobe devagar, desce rápido
    this.pisoGeral = -80;
    this.grave = 0; // 0..1 com ataque/soltura
    this.media = 0; // média lenta do grave, para achar a batida
  }

  _db(a, b) {
    let s = 0;
    for (let i = a; i < b; i++) s += Math.pow(10, this.db[i] / 10);
    const m = s / Math.max(1, b - a);
    return m > 0 ? 10 * Math.log10(m) : -140;
  }

  ler(dt) {
    this.a.getFloatFrequencyData(this.db);
    const graveDb = this._db(...this.faixaGrave);
    const geralDb = this._db(...this.faixaGeral);
    this.pico = Math.max(this.pico - 3 * dt, graveDb, -50);
    this.piso = graveDb < this.piso ? graveDb : Math.min(this.piso + 0.5 * dt, this.pico - 12);
    this.pisoGeral = geralDb < this.pisoGeral ? geralDb : this.pisoGeral + 0.5 * dt;
    const baixo = Math.max(this.piso + 6, this.pico - 30);
    const alvo = this.pico > baixo ? lim((graveDb - baixo) / (this.pico - baixo)) : 0;
    const k = 1 - Math.pow(1 - (alvo > this.grave ? ATAQUE : SOLTURA), dt * 60);
    this.grave += (alvo - this.grave) * k;
    this.media += (this.grave - this.media) * (1 - Math.exp(-dt * 2));
    for (let i = 0; i < NB; i++) {
      const v = lim((this._db(...this.faixas[i]) - (this.pisoGeral + 10)) / 35);
      const kb = 1 - Math.pow(1 - (v > this.bandas[i] ? ATAQUE : SOLTURA), dt * 60);
      this.bandas[i] += (v - this.bandas[i]) * kb;
    }
    let medio = 0, agudo = 0;
    for (let i = 8; i < 32; i++) medio += this.bandas[i];
    for (let i = 32; i < NB; i++) agudo += this.bandas[i];
    return { grave: this.grave, medio: medio / 24, agudo: agudo / 32, golpe: Math.max(0, this.grave - this.media) };
  }
}

// ---------------------------------------------------------------- microfone (só escuta)

export class Microfone {
  constructor() {
    this.estado = 'desligado'; // desligado | ouvindo | negado | indisponivel
    this.analisador = null;
    this.aoMudar = () => {};
    this._abrindo = null;
    this.ctx = null;
    this.fluxo = null;
    this.analise = null;
  }

  get ouvindo() { return this.estado === 'ouvindo'; }

  /** Solta o microfone e fecha o AudioContext de entrada. */
  _fechar() {
    if (this.fluxo) { for (const t of this.fluxo.getTracks()) { try { t.stop(); } catch (e) { /* nada */ } } }
    if (this.ctx && this.ctx.state !== 'closed') { try { this.ctx.close().catch(() => {}); } catch (e) { /* nada */ } }
    this.ctx = null;
    this.fluxo = null;
    this.analisador = null;
    this.analise = null;
  }

  _mudar(estado) {
    if (this.estado === estado) return;
    this.estado = estado;
    try { this.aoMudar(estado); } catch (e) { /* nada */ }
  }

  async permissao() {
    try {
      const p = await navigator.permissions.query({ name: 'microphone' });
      return p.state; // granted | prompt | denied
    } catch (e) {
      return 'desconhecida';
    }
  }

  /** Na Vitrine: abre só se o navegador já tiver autorizado. Nunca mostra pedido aos convidados. */
  async abrirSeAutorizado() {
    if (this.ouvindo) return true;
    const p = await this.permissao();
    if (p !== 'granted') {
      if (p === 'denied') this._mudar('negado');
      return false;
    }
    return this.abrir();
  }

  /** Pede o microfone (o navegador pode perguntar). Só os Bastidores chamam direto. */
  abrir() {
    if (this.ouvindo) return Promise.resolve(true);
    if (this._abrindo) return this._abrindo;
    this._abrindo = this._abrir().finally(() => { this._abrindo = null; });
    return this._abrindo;
  }

  async _abrir() {
    const md = navigator.mediaDevices;
    const AC = window.AudioContext || window.webkitAudioContext;
    if (!md || !md.getUserMedia || !AC) { this._mudar('indisponivel'); return false; }
    let fluxo;
    try {
      fluxo = await md.getUserMedia({
        audio: { echoCancellation: false, noiseSuppression: false, autoGainControl: false },
        video: false,
      });
    } catch (e) {
      this._mudar(e && e.name === 'NotAllowedError' ? 'negado' : 'indisponivel');
      return false;
    }
    let ctx = null;
    try {
      ctx = new AC();
      const fonte = ctx.createMediaStreamSource(fluxo);
      const analisador = ctx.createAnalyser();
      analisador.fftSize = 2048;
      analisador.smoothingTimeConstant = 0.55;
      fonte.connect(analisador); // e só: o microfone nunca vai para uma saída
      try { await ctx.resume(); } catch (e) { /* segue */ }
      this._fechar(); // reabrir não acumula contextos: o anterior (se sobrou) fecha antes
      this.ctx = ctx;
      this.fluxo = fluxo;
      this.analisador = analisador;
      this.analise = new Analise(analisador);
      for (const t of fluxo.getAudioTracks()) {
        t.addEventListener('ended', () => { if (this.fluxo === fluxo) { this._fechar(); this._mudar('desligado'); } });
      }
      this._mudar('ouvindo');
      return true;
    } catch (e) {
      for (const t of fluxo.getTracks()) t.stop();
      if (ctx) { try { ctx.close().catch(() => {}); } catch (e2) { /* nada */ } }
      this._mudar('indisponivel');
      return false;
    }
  }
}

// ---------------------------------------------------------------- o anel

export class Anel {
  constructor(canvas, quadro, { reduzido = false } = {}) {
    this.cv = canvas;
    this.ctx = canvas.getContext('2d');
    this.quadro = quadro;
    this.reduzido = reduzido;
    this.modo = 'parado'; // toque | abertura | vitrine | parado
    this.raf = 0;
    this.ultimo = 0;
    this.T = 0; // rotação do gradiente
    this.ondas = [];
    this.bandas = new Float32Array(NB);
    this.p = { R: R0, brilho: 0.12, def: 0, alfa: 1, ponto: 0 };
    this.vivo = 0; // 0 = respiro ocioso, 1 = segue a sala
    this.escala = 1;
    this.analiseAbertura = null;
    this.mic = null;
    this.comBlur = typeof this.ctx.filter === 'string'; // sem suporte: halos sem blur
    this.mediaQuadroMs = 16.7;
    new ResizeObserver(() => this._ajustar()).observe(quadro);
    this._ajustar();
  }

  _ajustar() {
    const r = this.quadro.getBoundingClientRect();
    let d = Math.min(window.devicePixelRatio || 1, DPR_MAX);
    if (r.width * d > LARGURA_MAX) d = LARGURA_MAX / Math.max(1, r.width);
    const w = Math.max(1, Math.round(r.width * d)), h = Math.max(1, Math.round(r.height * d));
    if (this.cv.width !== w || this.cv.height !== h) { this.cv.width = w; this.cv.height = h; }
    this.escala = w / W;
    this.ctx.setTransform(w / W, 0, 0, h / H, 0, 0);
    if (!this.raf) this._desenharParado();
  }

  _desenharParado() {
    if (this.modo === 'toque') this._desenhar({ R: 6, brilho: 0, def: 0, alfa: 0, ponto: 1 });
    else if (this.modo === 'vitrine' || (this.modo === 'abertura' && this.reduzido)) this._desenhar({ R: R0, brilho: 0.12, def: 0, alfa: 1, ponto: 0 });
    else this.ctx.clearRect(0, 0, W, H);
  }

  // ---- modos
  toque() { this._parar(); this.modo = 'toque'; this._desenharParado(); }

  abertura(inicio, analisador) {
    this._parar();
    this.modo = 'abertura';
    this.inicio = inicio;
    this.analiseAbertura = analisador ? new Analise(analisador) : null;
    this.ondasFeitas = 0;
    if (this.reduzido) { this._desenharParado(); return; }
    this._rodar();
  }

  /** Fim da abertura: a luz passa ao microfone (se estiver ouvindo) ou ao respiro ocioso. */
  vitrine(mic, { direto = false } = {}) {
    this.mic = mic;
    this.analiseAbertura = null;
    const continua = this.modo === 'abertura' && !direto; // fim natural: assenta sem salto
    this.modo = 'vitrine';
    if (!continua) { this.p = { R: R0, brilho: 0.12, def: 0, alfa: 1, ponto: 0 }; this.ondas = []; }
    if (this.reduzido) { this._parar(); this._desenharParado(); return; }
    this._rodar();
  }

  parar() { this._parar(); this.modo = 'parado'; }

  _parar() { if (this.raf) cancelAnimationFrame(this.raf); this.raf = 0; }

  _rodar() {
    if (this.raf) return;
    this.ultimo = performance.now();
    const passo = agora => {
      this.raf = requestAnimationFrame(passo); // o próximo primeiro: um erro aqui não para o laço
      const real = agora - this.ultimo;
      const dt = lim(real / 1000, 0, 0.05);
      this.ultimo = agora;
      if (real > 0 && real < 250) { // aba escondida não conta
        this.mediaQuadroMs += (real - this.mediaQuadroMs) * 0.05;
        if (this.comBlur && this.mediaQuadroMs > QUADRO_LENTO_MS) this.comBlur = false; // máquina lenta: sem blur
      }
      try { this._quadro(agora, dt); } catch (e) { /* um quadro perdido; o próximo tenta de novo */ }
    };
    this.raf = requestAnimationFrame(passo);
  }

  // ---- um quadro
  _quadro(agora, dt) {
    if (this.modo === 'abertura') return this._quadroAbertura((agora - this.inicio) / 1000, dt);
    if (this.modo === 'vitrine') return this._quadroVitrine(agora / 1000, dt);
  }

  _quadroAbertura(t, dt) {
    let nivel = 0, def = 0;
    if (this.analiseAbertura) {
      try {
        const s = this.analiseAbertura.ler(dt);
        nivel = s.grave;
        this.bandas = this.analiseAbertura.bandas;
        def = 0.5;
      } catch (e) { this.analiseAbertura = null; }
    }
    // Fase 1: um ponto de luz acende devagar. Fase 2: abre num anel que cresce e ganha cor.
    const ponto = t < 2 ? suave((t - 0.2) / 1.6) : 1 - suave((t - 2) / 0.9);
    const k = t < 2 ? 0 : saidaExpo((t - 2) / 3.5);
    let R = 6 + (R0 * 0.96 - 6) * k;
    let brilho = 0.12 + 0.3 * k + 0.5 * nivel;
    let alfa = k;
    if (t >= 5.5 && t < 5.8) R = R0 * (0.96 - 0.04 * suave((t - 5.5) / 0.3)); // respira antes
    if (t >= 5.8) { // Fase 3: o pulso de brilho e a onda, junto com o nome
      const u = t - 5.8;
      const pulso = 1 + 0.09 * Math.exp(-u * 2.6); // de 1,09 assenta em 1,0
      R = R0 * (0.92 + (pulso - 0.92) * lim(u / 0.06)); // 60 ms de 0,92 até o pulso
      brilho = 0.12 + 0.25 * nivel + 0.95 * Math.exp(-u * 2.2) + 0.2 * Math.exp(-u * 0.6);
      alfa = 1;
      if (this.ondasFeitas === 0) { this.ondas.push({ r: R, a: 0.42 }); this.ondasFeitas = 1; }
      if (this.ondasFeitas === 1 && u > 0.22) { this.ondas.push({ r: R, a: 0.24 }); this.ondasFeitas = 2; }
    }
    R *= 1 + 0.03 * nivel;
    this.T += dt * 0.18 * (0.3 + k);
    this.p = { R, brilho, def: def * k, alfa, ponto };
    this._desenhar(this.p, dt);
  }

  _quadroVitrine(t, dt) {
    let alvoVivo = 0, s = null;
    const mic = this.mic;
    if (mic && mic.ouvindo && mic.analise) {
      try { s = mic.analise.ler(dt); } catch (e) { s = null; }
      if (s) {
        this.bandas = mic.analise.bandas;
        // Sala em silêncio (grave quase nulo por ~2 s): volta ao respiro ocioso.
        this.silencio = s.grave < 0.03 ? (this.silencio || 0) + dt : 0;
        alvoVivo = this.silencio > 2 ? 0 : 1;
      }
    }
    this.vivo += (alvoVivo - this.vivo) * (1 - Math.exp(-dt * 2.5));
    const respiro = 1 + 0.015 * Math.sin((2 * Math.PI * t) / OCIOSO_PERIODO_S);
    const ocioso = { R: R0 * respiro, brilho: 0.12, def: 0 };
    let vivo = ocioso;
    if (s) {
      vivo = { R: R0 * (1 + 0.075 * s.grave + 0.02 * lim(s.golpe * 3)), brilho: s.grave, def: 1 };
      // Um pico de grave acima da média recente solta uma onda fina.
      if (s.golpe > 0.18 && s.grave > 0.4 && t - (this.ultimaOnda || 0) > 0.28 && this.vivo > 0.5) {
        this.ondas.push({ r: vivo.R, a: 0.32 });
        this.ultimaOnda = t;
      }
    }
    const v = this.vivo;
    const alvo = {
      R: ocioso.R + (vivo.R - ocioso.R) * v,
      brilho: ocioso.brilho + (vivo.brilho - ocioso.brilho) * v,
      def: vivo.def * v,
      alfa: 1,
      ponto: 0,
    };
    // Saída suave da abertura para o estado da Vitrine.
    const k = 1 - Math.exp(-dt * 3);
    for (const c of ['R', 'brilho', 'def', 'alfa', 'ponto']) this.p[c] += (alvo[c] - this.p[c]) * k;
    this.T += dt * 0.18;
    this._desenhar(this.p, dt);
  }

  // ---- desenho (a lógica do mock Keynote, com o sinal de verdade)
  _contorno(R, t, amp) {
    const c = this.ctx, n = 260, b = this.bandas;
    c.beginPath();
    for (let i = 0; i <= n; i++) {
      const a = (i / n) * Math.PI * 2;
      let d = 0;
      if (amp > 0.001) {
        for (let h = 2; h <= 6; h++) d += b[h * 5] * Math.sin(h * a + t * (0.35 + h * 0.11) + h * 1.7);
        let agudo = 0;
        for (let j = 40; j < 60; j++) agudo += b[j];
        d = d * 0.013 * R * amp + 0.004 * R * (agudo / 20) * amp * Math.sin(18 * a + t * 4);
      }
      const r = R + d, x = CX + Math.cos(a) * r, y = CY + Math.sin(a) * r;
      if (i) c.lineTo(x, y); else c.moveTo(x, y);
    }
    c.closePath();
  }

  _desenhar(p, dt = 0) {
    const c = this.ctx, s = this.escala;
    c.clearRect(0, 0, W, H);
    const b = lim(p.brilho, 0, 1.4);
    if (p.alfa > 0.002) {
      const R = p.R;
      const g = c.createRadialGradient(CX, CY, 0, CX, CY, R * 1.6);
      g.addColorStop(0, `rgba(150,170,255,${(0.015 + 0.05 * b) * p.alfa})`);
      g.addColorStop(0.6, `rgba(197,155,255,${(0.03 + 0.07 * b) * p.alfa})`);
      g.addColorStop(1, 'rgba(0,0,0,0)');
      c.fillStyle = g;
      c.fillRect(0, 0, W, H);

      c.globalCompositeOperation = 'lighter';
      for (const o of this.ondas) {
        o.r += dt * 95;
        o.a *= Math.pow(0.955, dt * 60);
        c.strokeStyle = `rgba(255,255,255,${o.a * p.alfa})`;
        c.lineWidth = 1.4;
        c.beginPath();
        c.arc(CX, CY, o.r, 0, Math.PI * 2);
        c.stroke();
      }
      while (this.ondas.length && this.ondas[0].a < 0.01) this.ondas.shift();

      const cg = c.createConicGradient(this.T, CX, CY);
      cg.addColorStop(0, LUZ_FRIA);
      cg.addColorStop(0.33, LUZ_LILAS);
      cg.addColorStop(0.66, LUZ_QUENTE);
      cg.addColorStop(1, LUZ_FRIA);
      c.strokeStyle = cg;
      if (this.comBlur) {
        c.filter = `blur(${22 * s}px)`;
        c.globalAlpha = lim((0.22 + 0.35 * b) * p.alfa);
        c.lineWidth = 70;
        this._contorno(R, this.T, p.def);
        c.stroke();
        c.filter = `blur(${7 * s}px)`;
        c.globalAlpha = lim((0.55 + 0.3 * b) * p.alfa);
        c.lineWidth = 16;
        this._contorno(R, this.T, p.def);
        c.stroke();
        c.filter = 'none';
      } else { // sem blur: só um halo fino e mais fraco, que não pesa
        c.globalAlpha = lim((0.3 + 0.25 * b) * p.alfa);
        c.lineWidth = 9;
        this._contorno(R, this.T, p.def);
        c.stroke();
      }
      c.globalAlpha = lim((0.1 + 0.08 * b) * p.alfa);
      c.lineWidth = 1;
      c.strokeStyle = '#fff';
      this._contorno(R * 0.88, this.T + 2, p.def * 0.6);
      c.stroke();
      c.globalAlpha = lim(0.96 * p.alfa);
      c.lineWidth = 2.6 + 1.6 * lim(b);
      c.shadowColor = '#fff';
      c.shadowBlur = this.comBlur ? 22 * s : 0;
      this._contorno(R, this.T, p.def);
      c.stroke();
      c.shadowBlur = 0;
      c.globalAlpha = 1;
      c.globalCompositeOperation = 'source-over';
    }
    if (p.ponto > 0.002) { // o ponto de luz do "Toque para começar" e da fase 1
      const a = lim(p.ponto);
      const g = c.createRadialGradient(CX, CY, 0, CX, CY, 70);
      g.addColorStop(0, `rgba(255,255,255,${a})`);
      g.addColorStop(0.08, `rgba(255,255,255,${0.9 * a})`);
      g.addColorStop(0.22, `rgba(143,180,255,${0.35 * a})`);
      g.addColorStop(1, 'rgba(143,180,255,0)');
      c.fillStyle = g;
      c.beginPath();
      c.arc(CX, CY, 70, 0, Math.PI * 2);
      c.fill();
    }
  }
}
