'use strict';
/* Iron Assault — synthesized sound effects, announcer voice and a small
   chiptune sequencer. Everything is generated with the Web Audio API. */

const AU = {
  ctx: null, master: null, musicBus: null, sfxBus: null, noiseBuf: null,
  pulse: {}, delay: null, delaySend: null, last: Object.create(null),
  musicVol: 0.6, sfxVol: 0.8, voice: true, muted: false, loops: {},
};

function initAudio() {
  if (AU.ctx) {
    if (AU.ctx.state === 'suspended') AU.ctx.resume();
    return;
  }
  const AC = window.AudioContext || window.webkitAudioContext;
  if (!AC) return;
  let c;
  try { c = new AC(); } catch (e) { return; }
  AU.ctx = c;
  const comp = c.createDynamicsCompressor();
  comp.threshold.value = -16; comp.knee.value = 12; comp.ratio.value = 4;
  comp.attack.value = 0.003; comp.release.value = 0.25;
  AU.master = c.createGain();
  AU.musicBus = c.createGain();
  AU.sfxBus = c.createGain();
  AU.musicBus.connect(AU.master);
  AU.sfxBus.connect(AU.master);
  AU.master.connect(comp);
  comp.connect(c.destination);
  applyVolumes();

  const len = c.sampleRate;
  const buf = c.createBuffer(1, len, c.sampleRate);
  const d = buf.getChannelData(0);
  for (let i = 0; i < len; i++) d[i] = Math.random() * 2 - 1;
  AU.noiseBuf = buf;
  for (const duty of [0.125, 0.25, 0.5]) AU.pulse[duty] = makePulse(c, duty);

  // Tempo-synced echo used by music leads.
  AU.delay = c.createDelay(1.5);
  AU.delay.delayTime.value = 0.3;
  const fb = c.createGain(); fb.gain.value = 0.28;
  const wet = c.createGain(); wet.gain.value = 0.32;
  const lp = c.createBiquadFilter(); lp.type = 'lowpass'; lp.frequency.value = 2600;
  AU.delaySend = c.createGain();
  AU.delaySend.connect(AU.delay);
  AU.delay.connect(lp); lp.connect(fb); fb.connect(AU.delay);
  lp.connect(wet); wet.connect(AU.musicBus);

  if (MUSIC.pending) {
    const p = MUSIC.pending;
    MUSIC.pending = null;
    playMusic(p.name);
  }
}

function onUserGesture() {
  initAudio();
}
addEventListener('pointerdown', onUserGesture, { passive: true });
addEventListener('touchstart', onUserGesture, { passive: true });

function applyVolumes() {
  if (!AU.ctx) return;
  const t = AU.ctx.currentTime;
  AU.master.gain.setTargetAtTime(AU.muted ? 0 : 0.9, t, 0.02);
  AU.musicBus.gain.setTargetAtTime(AU.musicVol * 0.55 * (MUSIC.duck ? 0.35 : 1), t, 0.05);
  AU.sfxBus.gain.setTargetAtTime(AU.sfxVol, t, 0.02);
}

function makePulse(c, d) {
  const n = 40;
  const re = new Float32Array(n), im = new Float32Array(n);
  for (let k = 1; k < n; k++) {
    re[k] = Math.sin(TAU * k * d) / (Math.PI * k);
    im[k] = (1 - Math.cos(TAU * k * d)) / (Math.PI * k);
  }
  return c.createPeriodicWave(re, im);
}

function can(name, gap) {
  if (!AU.ctx || AU.muted || AU.sfxVol <= 0) return false;
  const t = performance.now();
  if (AU.last[name] && t - AU.last[name] < gap) return false;
  AU.last[name] = t;
  return true;
}

function out(pan) {
  if (!pan || !AU.ctx.createStereoPanner) return AU.sfxBus;
  const p = AU.ctx.createStereoPanner();
  p.pan.value = clamp(pan, -0.9, 0.9);
  p.connect(AU.sfxBus);
  return p;
}

/** Oscillator voice with an attack / hold / decay envelope. */
function tone(o) {
  const c = AU.ctx;
  const t = o.t !== undefined ? o.t : c.currentTime;
  const osc = c.createOscillator();
  const g = c.createGain();
  if (o.wave) osc.setPeriodicWave(o.wave); else osc.type = o.type || 'square';
  osc.frequency.setValueAtTime(o.f, t);
  if (o.f2 && o.f2 !== o.f) osc.frequency.exponentialRampToValueAtTime(o.f2, t + (o.slide || o.dur));
  const a = o.a !== undefined ? o.a : 0.003;
  const hold = o.hold || 0;
  g.gain.setValueAtTime(0.0001, t);
  g.gain.linearRampToValueAtTime(o.vol, t + a);
  if (hold) g.gain.setValueAtTime(o.vol, t + a + hold);
  g.gain.exponentialRampToValueAtTime(0.0001, t + a + hold + o.dur);
  let node = osc;
  if (o.filter) {
    const f = c.createBiquadFilter();
    f.type = o.filter; f.frequency.value = o.ff; f.Q.value = o.q || 1;
    osc.connect(f); node = f;
  }
  node.connect(g);
  g.connect(o.dest || AU.sfxBus);
  const end = t + a + hold + o.dur + 0.05;
  if (o.vib) {
    const l = c.createOscillator(), lg = c.createGain();
    l.frequency.value = o.vib; lg.gain.value = o.vibAmt || 12;
    l.connect(lg); lg.connect(osc.frequency);
    l.start(t); l.stop(end);
  }
  osc.start(t); osc.stop(end);
  return g;
}

/** Filtered noise burst. */
function noise(o) {
  const c = AU.ctx;
  const t = o.t !== undefined ? o.t : c.currentTime;
  const s = c.createBufferSource();
  s.buffer = AU.noiseBuf; s.loop = true;
  const f = c.createBiquadFilter();
  f.type = o.type || 'lowpass';
  f.frequency.setValueAtTime(o.f, t);
  if (o.f2) f.frequency.exponentialRampToValueAtTime(o.f2, t + o.dur);
  f.Q.value = o.q || 0.8;
  const g = c.createGain();
  const a = o.a !== undefined ? o.a : 0.002;
  g.gain.setValueAtTime(0.0001, t);
  g.gain.linearRampToValueAtTime(o.vol, t + a);
  g.gain.exponentialRampToValueAtTime(0.0001, t + a + o.dur);
  s.connect(f); f.connect(g); g.connect(o.dest || AU.sfxBus);
  s.start(t, Math.random() * 0.7);
  s.stop(t + a + o.dur + 0.05);
}

function arp(freqs, step, o) {
  const t0 = AU.ctx.currentTime;
  freqs.forEach((f, i) => tone(Object.assign({ f, t: t0 + i * step }, o)));
}

const SFX = {
  pistol(p) {
    if (!can('pistol', 35)) return;
    const d = out(p);
    noise({ dur: 0.07, vol: 0.5, type: 'bandpass', f: 2600, f2: 700, q: 1.1, dest: d });
    tone({ type: 'square', f: 540, f2: 120, dur: 0.05, vol: 0.12, dest: d });
  },
  mg(p) {
    if (!can('mg', 45)) return;
    const d = out(p);
    noise({ dur: 0.06, vol: 0.45, type: 'bandpass', f: 2100, f2: 500, q: 0.9, dest: d });
    tone({ type: 'square', f: 280, f2: 70, dur: 0.045, vol: 0.13, dest: d });
    tone({ type: 'sine', f: 95, f2: 40, dur: 0.06, vol: 0.3, dest: d });
  },
  shotgun(p) {
    if (!can('shotgun', 60)) return;
    const d = out(p);
    noise({ dur: 0.32, vol: 0.85, type: 'lowpass', f: 4200, f2: 260, dest: d });
    tone({ type: 'sine', f: 170, f2: 38, dur: 0.2, vol: 0.8, dest: d });
  },
  rocket(p) {
    if (!can('rocket', 60)) return;
    const d = out(p);
    noise({ dur: 0.42, vol: 0.45, type: 'bandpass', f: 500, f2: 2600, q: 1.6, a: 0.02, dest: d });
    tone({ type: 'sawtooth', f: 170, f2: 520, dur: 0.35, vol: 0.06, dest: d });
  },
  flame(p) {
    if (!can('flame', 60)) return;
    const d = out(p);
    noise({ dur: 0.45, vol: 0.55, type: 'lowpass', f: 1700, f2: 240, a: 0.03, dest: d });
    tone({ type: 'sawtooth', f: 84, f2: 52, dur: 0.35, vol: 0.09, dest: d });
  },
  laser(p) {
    if (!can('laser', 70)) return;
    const d = out(p);
    tone({ type: 'square', f: 1650, f2: 1150, dur: 0.06, vol: 0.045, dest: d });
    tone({ type: 'sine', f: 3300, f2: 2500, dur: 0.07, vol: 0.05, dest: d });
  },
  eshot(p) {
    if (!can('eshot', 40)) return;
    const d = out(p);
    noise({ dur: 0.08, vol: 0.26, type: 'bandpass', f: 1500, f2: 480, q: 1, dest: d });
    tone({ type: 'square', f: 420, f2: 150, dur: 0.04, vol: 0.05, dest: d });
  },
  cannon(p) {
    if (!can('cannon', 80)) return;
    const d = out(p);
    noise({ dur: 0.5, vol: 0.8, type: 'lowpass', f: 1300, f2: 80, dest: d });
    tone({ type: 'sine', f: 120, f2: 34, dur: 0.4, vol: 0.8, dest: d });
  },
  explode(size = 1, p) {
    if (!can('explode', 55)) return;
    const d = out(p);
    const s = clamp(size, 0.4, 2.5);
    noise({ dur: 0.45 + s * 0.35, vol: 0.55 + s * 0.2, type: 'lowpass', f: 1900, f2: 50, dest: d });
    tone({ type: 'sine', f: 105, f2: 26, dur: 0.4 + s * 0.3, vol: 0.9, dest: d });
    noise({ dur: 0.14, vol: 0.28, type: 'highpass', f: 2600, dest: d });
    if (s > 1.2) noise({ dur: 0.6, vol: 0.25, type: 'bandpass', f: 700, f2: 200, t: AU.ctx.currentTime + 0.12, dest: d });
  },
  throw() {
    if (!can('throw', 60)) return;
    tone({ type: 'sine', f: 380, f2: 950, dur: 0.12, vol: 0.08 });
    noise({ dur: 0.1, vol: 0.08, type: 'highpass', f: 3000 });
  },
  hit(p) {
    if (!can('hit', 30)) return;
    tone({ type: 'square', f: 1300, f2: 380, dur: 0.035, vol: 0.09, dest: out(p) });
  },
  clink(p) {
    if (!can('clink', 45)) return;
    const d = out(p);
    tone({ type: 'square', f: 2500, f2: 2200, dur: 0.06, vol: 0.09, dest: d });
    tone({ type: 'triangle', f: 3800, dur: 0.14, vol: 0.06, dest: d });
  },
  scream(p) {
    if (!can('scream', 140)) return;
    const d = out(p);
    const f = rand(520, 820);
    tone({ type: 'sawtooth', f, f2: f * 0.34, dur: 0.5, vol: 0.14, filter: 'bandpass', ff: 1050, q: 3, vib: 17, vibAmt: 35, a: 0.02, dest: d });
    tone({ type: 'sawtooth', f, f2: f * 0.34, dur: 0.45, vol: 0.05, filter: 'bandpass', ff: 2500, q: 5, a: 0.02, dest: d });
  },
  hurt() {
    if (!can('hurt', 100)) return;
    tone({ type: 'sawtooth', f: 520, f2: 110, dur: 0.25, vol: 0.2, filter: 'lowpass', ff: 2200 });
    noise({ dur: 0.12, vol: 0.2, type: 'bandpass', f: 900 });
  },
  pdie() {
    if (!can('pdie', 400)) return;
    tone({ type: 'sawtooth', f: 720, f2: 90, dur: 0.85, vol: 0.2, filter: 'bandpass', ff: 950, q: 2.5, vib: 9, vibAmt: 55, a: 0.02 });
  },
  pickup() {
    if (!can('pickup', 80)) return;
    arp([988, 1319, 1568, 1976], 0.055, { type: 'triangle', dur: 0.1, vol: 0.2 });
  },
  weapon(name) {
    if (can('weapon', 150)) arp([523, 659, 784, 1047, 1319], 0.06, { wave: AU.pulse[0.25], dur: 0.12, vol: 0.1 });
    if (name) say(name);
  },
  rescue() {
    if (can('rescue', 200)) arp([784, 988, 1175, 1568], 0.07, { type: 'triangle', dur: 0.12, vol: 0.18 });
    say('Thank you!', { pitch: 1.15, rate: 1.05 });
  },
  jump() {
    if (!can('jump', 80)) return;
    tone({ type: 'square', f: 190, f2: 380, dur: 0.07, vol: 0.035 });
  },
  land() {
    if (!can('land', 90)) return;
    noise({ dur: 0.07, vol: 0.14, type: 'lowpass', f: 500, f2: 120 });
  },
  knife() {
    if (!can('knife', 60)) return;
    noise({ dur: 0.1, vol: 0.28, type: 'bandpass', f: 2600, f2: 7000, q: 0.8 });
  },
  ui() {
    if (!can('ui', 30)) return;
    tone({ type: 'square', f: 880, dur: 0.035, vol: 0.05 });
  },
  select() {
    if (!can('select', 40)) return;
    tone({ type: 'square', f: 660, f2: 1320, dur: 0.1, vol: 0.07, slide: 0.06 });
  },
  alarm() {
    if (!AU.ctx || AU.muted) return;
    const t = AU.ctx.currentTime;
    for (let i = 0; i < 4; i++) {
      tone({ wave: AU.pulse[0.5], f: 620, dur: 0.05, hold: 0.2, vol: 0.09, t: t + i * 0.5 });
      tone({ wave: AU.pulse[0.5], f: 880, dur: 0.05, hold: 0.2, vol: 0.09, t: t + i * 0.5 + 0.25 });
    }
  },
  stomp(p) {
    if (!can('stomp', 120)) return;
    const d = out(p);
    tone({ type: 'sine', f: 72, f2: 26, dur: 0.5, vol: 1, dest: d });
    noise({ dur: 0.45, vol: 0.55, type: 'lowpass', f: 420, f2: 60, dest: d });
  },
  step(p) {
    if (!can('step', 120)) return;
    tone({ type: 'sine', f: 90, f2: 40, dur: 0.18, vol: 0.45, dest: out(p) });
    noise({ dur: 0.1, vol: 0.12, type: 'lowpass', f: 700, dest: out(p) });
  },
  charge(p) {
    if (!can('charge', 300)) return;
    tone({ type: 'sawtooth', f: 110, f2: 950, dur: 0.15, hold: 0.8, slide: 0.9, vol: 0.07, filter: 'lowpass', ff: 2400, dest: out(p) });
  },
  beam(p) {
    if (!can('beam', 110)) return;
    const d = out(p);
    noise({ dur: 0.18, vol: 0.28, type: 'bandpass', f: 900, q: 3, dest: d });
    tone({ type: 'sawtooth', f: 92, dur: 0.18, vol: 0.12, dest: d });
  },
  missile(p) {
    if (!can('missile', 70)) return;
    noise({ dur: 0.28, vol: 0.22, type: 'bandpass', f: 1100, f2: 3200, q: 1.4, dest: out(p) });
  },
  thunder() {
    if (!AU.ctx || AU.muted) return;
    const t = AU.ctx.currentTime + 0.35;
    noise({ dur: 1.8, vol: 0.55, type: 'lowpass', f: 950, f2: 60, a: 0.04, t });
    noise({ dur: 1.4, vol: 0.3, type: 'lowpass', f: 400, f2: 50, a: 0.2, t: t + 0.4 });
  },
  crate(p) {
    if (!can('crate', 60)) return;
    const d = out(p);
    noise({ dur: 0.16, vol: 0.4, type: 'bandpass', f: 950, f2: 300, dest: d });
    tone({ type: 'square', f: 210, f2: 90, dur: 0.08, vol: 0.08, dest: d });
  },
  heal() {
    if (can('heal', 100)) arp([660, 880, 1320], 0.07, { type: 'sine', dur: 0.14, vol: 0.2 });
  },
  gem() {
    if (!can('gem', 60)) return;
    tone({ type: 'square', f: 1319, dur: 0.05, vol: 0.07 });
    tone({ type: 'square', f: 1760, dur: 0.16, vol: 0.07, t: AU.ctx.currentTime + 0.06 });
  },
  go() {
    if (can('go', 200)) arp([880, 1175], 0.09, { wave: AU.pulse[0.25], dur: 0.12, vol: 0.09 });
  },
  loopStart(name) {
    if (!AU.ctx || AU.loops[name]) return;
    const c = AU.ctx;
    const src = c.createBufferSource(); src.buffer = AU.noiseBuf; src.loop = true;
    const f = c.createBiquadFilter(); f.type = 'lowpass'; f.frequency.value = 380;
    const g = c.createGain(); g.gain.value = 0;
    const lfo = c.createOscillator(); lfo.frequency.value = 13;
    const lg = c.createGain(); lg.gain.value = 0.18;
    lfo.connect(lg); lg.connect(g.gain);
    src.connect(f); f.connect(g); g.connect(AU.sfxBus);
    src.start(); lfo.start();
    AU.loops[name] = { src, lfo, g };
  },
  loopStop(name) {
    const l = AU.loops[name];
    if (!l) return;
    delete AU.loops[name];
    try { l.src.stop(); l.lfo.stop(); } catch (e) { /* already stopped */ }
  },
  loopStopAll() { for (const k of Object.keys(AU.loops)) SFX.loopStop(k); },
};

/* ------------------------------------------------------------ announcer */

function say(text, opts = {}) {
  if (!AU.voice || AU.muted) return;
  try {
    const s = window.speechSynthesis;
    if (!s || typeof SpeechSynthesisUtterance === 'undefined') return;
    s.cancel();
    const u = new SpeechSynthesisUtterance(text);
    u.lang = 'en-US';
    u.rate = opts.rate || 0.92;
    u.pitch = opts.pitch !== undefined ? opts.pitch : 0.35;
    u.volume = Math.min(1, AU.sfxVol + 0.2);
    const voices = s.getVoices ? s.getVoices() : [];
    const v = voices.find(v => /en[-_]US/i.test(v.lang) && /male|david|alex|daniel|fred/i.test(v.name)) ||
      voices.find(v => /^en/i.test(v.lang));
    if (v) u.voice = v;
    s.speak(u);
  } catch (e) { /* speech is optional */ }
}

/* ---------------------------------------------------------------- music */

const NOTE = m => 440 * Math.pow(2, (m - 69) / 12);
const SCALES = {
  minor: [0, 2, 3, 5, 7, 8, 10],
  dorian: [0, 2, 3, 5, 7, 9, 10],
  major: [0, 2, 4, 5, 7, 9, 11],
  harm: [0, 2, 3, 5, 7, 8, 11],
  phryd: [0, 1, 4, 5, 7, 8, 10],
  mixo: [0, 2, 4, 5, 7, 9, 10],
};
const BASS_PATS = {
  drive: [[0, 'r', 2], [2, 'r', 2], [4, 'o', 2], [6, 'r', 2], [8, 'r', 2], [10, 'f', 2], [12, 'o', 2], [14, 'r', 2]],
  gallop: [[0, 'r', 1], [1, 'r', 1], [2, 'r', 2], [4, 'o', 1], [5, 'r', 1], [6, 'r', 2], [8, 'r', 1], [9, 'r', 1], [10, 'f', 2], [12, 'o', 1], [13, 'r', 1], [14, 'f', 2]],
  half: [[0, 'r', 6], [6, 'r', 2], [8, 'f', 6], [14, 'o', 2]],
  synth: [[0, 'r', 1], [2, 'o', 1], [4, 'r', 1], [6, 'o', 1], [8, 'r', 1], [10, 'o', 1], [12, 'f', 1], [14, 'o', 1]],
  march: [[0, 'r', 3], [4, 'f', 3], [8, 'r', 3], [12, 'o', 2], [14, 'f', 2]],
};
const DRUMS = {
  rock: { k: 'x.......x.x.....', s: '....x.......x...', h: 'x.x.x.x.x.x.x.x.' },
  march: { k: 'x...x...x...x...', s: '....x..x....x.xx', h: '..x...x...x...x.' },
  tribal: { k: 'x..x..x...x..x..', s: '....x..t....x.tt', h: 'x.xxx.xxx.xxx.xx' },
  half: { k: 'x.........x.....', s: '........x.......', h: 'x...x...x...x...' },
  synth: { k: 'x...x...x...x...', s: '....x.......x...', h: '..o...o...o...o.' },
  metal: { k: 'x.x.x.x.x.xxx.x.', s: '....x.......x...', h: 'x.x.x.x.x.x.x.x.' },
  double: { k: 'xxxxxxxxxxxxxxxx', s: '....x.......x...', h: 'x.x.x.x.x.x.x.x.' },
};
const FILL = '....x...x.xxxxxx';

const TRACKS = {
  title: { bpm: 116, root: 45, scale: 'minor', prog: [0, 5, 2, 6], drums: 'march', bass: 'march', seed: 11, lead: 0.25, arp: true },
  jungle: { bpm: 138, root: 43, scale: 'dorian', prog: [0, 6, 5, 6], drums: 'rock', bass: 'drive', seed: 23, lead: 0.25, arp: false },
  desert: { bpm: 124, root: 40, scale: 'phryd', prog: [0, 1, 0, 6], drums: 'tribal', bass: 'gallop', seed: 37, lead: 0.125, arp: false },
  snow: { bpm: 106, root: 41, scale: 'minor', prog: [0, 3, 5, 4], drums: 'half', bass: 'half', seed: 45, lead: 0, arp: true },
  city: { bpm: 128, root: 38, scale: 'minor', prog: [0, 5, 6, 4], drums: 'synth', bass: 'synth', seed: 58, lead: 0.5, arp: true },
  base: { bpm: 146, root: 40, scale: 'harm', prog: [0, 0, 5, 4], drums: 'metal', bass: 'gallop', seed: 64, lead: 0.25, arp: false },
  boss: { bpm: 162, root: 42, scale: 'harm', prog: [0, 1, 5, 4], drums: 'double', bass: 'gallop', seed: 79, lead: 0.125, arp: true },
};

const JINGLES = {
  start: { bpm: 150, lead: [[67, 2], [0, 1], [67, 1], [72, 3], [0, 1], [74, 2], [76, 6]], bass: [[43, 4], [48, 4], [48, 8]], drums: 'march' },
  clear: { bpm: 140, lead: [[72, 2], [74, 2], [76, 2], [79, 4], [76, 2], [79, 2], [84, 10]], bass: [[48, 4], [43, 4], [45, 4], [48, 12]], drums: 'march' },
  over: { bpm: 84, lead: [[69, 4], [67, 4], [64, 4], [62, 4], [60, 12]], bass: [[45, 8], [41, 8], [45, 12]], drums: null },
  victory: { bpm: 132, lead: [[72, 2], [72, 1], [72, 1], [72, 4], [68, 4], [70, 4], [72, 2], [0, 2], [70, 2], [72, 12]], bass: [[48, 8], [44, 4], [46, 4], [48, 16]], drums: 'march' },
};

const MUSIC = { cur: null, name: null, timer: 0, step: 0, next: 0, pending: null, duck: false };

function composeTrack(name) {
  const d = TRACKS[name];
  const rng = mulberry32(d.seed);
  const sc = SCALES[d.scale];
  const n = d.prog.length, bars = n * 2, steps = bars * 16;
  const T = { d, spb: 60 / d.bpm / 4, steps, bass: [], lead: [], harm: [], arp: [] };
  const deg = i => sc[((i % 7) + 7) % 7] + 12 * Math.floor(i / 7);
  const chordOf = bar => d.prog[bar % n];

  for (let b = 0; b < bars; b++) {
    const r = chordOf(b);
    for (const [s, kind, len] of BASS_PATS[d.bass]) {
      const off = kind === 'o' ? 12 : kind === 'f' ? deg(r + 4) - deg(r) : 0;
      T.bass[b * 16 + s] = { m: d.root + deg(r) + off, len };
    }
    if (d.arp) {
      const tones = [r, r + 2, r + 4, r + 7];
      for (let s = 0; s < 16; s += 2) T.arp[b * 16 + s] = { m: d.root + 12 + deg(tones[(s / 2) % 4]) };
    }
  }

  const snap = (dg, r) => {
    for (let o = 0; o < 4; o++) {
      for (const cand of [dg - o, dg + o]) {
        const k = (((cand - r) % 7) + 7) % 7;
        if (k === 0 || k === 2 || k === 4) return cand;
      }
    }
    return dg;
  };
  const phrase = startBar => {
    const notes = [];
    let s = 0, cur = 7 + chordOf(startBar);
    while (s < 32) {
      const opts = s % 8 === 0 ? [4, 2, 6, 3, 2, 4] : [2, 2, 1, 4, 2];
      let len = opts[Math.floor(rng() * opts.length)];
      if (s + len > 32) len = 32 - s;
      if (s > 0 && rng() < 0.12) { s += len; continue; }
      cur += [-2, -1, -1, 1, 1, 2, 0, 3, -3][Math.floor(rng() * 9)];
      if (s % 4 === 0) cur = snap(cur, chordOf(startBar + (s >> 4)));
      cur = clamp(cur, 4, 15);
      notes.push({ s, dg: cur, len });
      s += len;
    }
    return notes;
  };
  const place = (notes, bar0, shift, finalRoot, withHarm) => {
    notes.forEach((nt, i) => {
      let dg = nt.dg + shift;
      const bar = bar0 + (nt.s >> 4);
      if (nt.s % 4 === 0) dg = snap(dg, chordOf(bar));
      let len = nt.len;
      if (finalRoot && i === notes.length - 1) { dg = 7 + chordOf(bar); len = 32 - nt.s; }
      const m = d.root + 24 + deg(dg);
      T.lead[bar0 * 16 + nt.s] = { m, len };
      if (withHarm) T.harm[bar0 * 16 + nt.s] = { m: d.root + 24 + deg(dg - 2), len };
    });
  };
  const A = phrase(0), B = phrase(4);
  place(A, 0, 0, false, false);
  place(A, 2, chordOf(2) - chordOf(0), true, false);
  place(B, 4, 0, false, true);
  place(A, 6, chordOf(6) - chordOf(0), true, true);
  return T;
}

function composeJingle(name) {
  const j = JINGLES[name];
  const T = { d: { drums: j.drums, lead: 0.25 }, spb: 60 / j.bpm / 4, bass: [], lead: [], harm: [], arp: [], once: true };
  let s = 0;
  for (const [m, len] of j.lead) {
    if (m) { T.lead[s] = { m, len }; T.harm[s] = { m: m - 12, len }; }
    s += len;
  }
  T.steps = s + 4;
  s = 0;
  for (const [m, len] of j.bass) { T.bass[s] = { m, len }; s += len; }
  return T;
}

function kick(t, bus) { tone({ type: 'sine', f: 150, f2: 42, slide: 0.1, dur: 0.16, vol: 0.85, t, dest: bus }); }
function snare(t, bus) {
  noise({ dur: 0.13, vol: 0.38, type: 'bandpass', f: 1900, q: 0.7, t, dest: bus });
  tone({ type: 'triangle', f: 210, f2: 130, dur: 0.08, vol: 0.18, t, dest: bus });
}
function tom(t, bus) { tone({ type: 'sine', f: 190, f2: 85, dur: 0.16, vol: 0.5, t, dest: bus }); }
function hat(t, bus, open) { noise({ dur: open ? 0.12 : 0.025, vol: open ? 0.08 : 0.1, type: 'highpass', f: 7500, t, dest: bus }); }

function playStep(T, step, t) {
  const i = step % T.steps, spb = T.spb, bus = AU.musicBus, d = T.d;
  const b = T.bass[i];
  if (b) {
    tone({ type: 'triangle', f: NOTE(b.m), dur: 0.06, hold: b.len * spb * 0.7, vol: 0.42, t, dest: bus });
    tone({ wave: AU.pulse[0.5], f: NOTE(b.m), dur: 0.05, hold: b.len * spb * 0.5, vol: 0.05, t, dest: bus, filter: 'lowpass', ff: 800 });
  }
  const l = T.lead[i];
  if (l) {
    const f = NOTE(l.m);
    const g = tone({
      wave: d.lead ? AU.pulse[d.lead] : null, type: 'triangle', f, dur: 0.12, hold: l.len * spb * 0.75,
      vol: d.lead ? 0.1 : 0.2, t, dest: bus, vib: l.len >= 4 ? 5.5 : 0, vibAmt: f * 0.012, a: 0.01,
    });
    if (AU.delaySend) g.connect(AU.delaySend);
  }
  const h = T.harm[i];
  if (h) tone({ wave: AU.pulse[0.125], f: NOTE(h.m), dur: 0.1, hold: h.len * spb * 0.6, vol: 0.045, t, dest: bus });
  const a = T.arp[i];
  if (a) tone({ wave: AU.pulse[0.125], f: NOTE(a.m), dur: spb * 1.4, vol: 0.035, t, dest: bus });

  const kit = d.drums && DRUMS[d.drums];
  if (kit) {
    const di = i % 16, lastBar = !T.once && (i >> 4) === T.steps / 16 - 1;
    if (kit.k[di] === 'x') kick(t, bus);
    const sn = (lastBar ? FILL : kit.s)[di];
    if (sn === 'x') snare(t, bus); else if (sn === 't') tom(t, bus);
    const hh = kit.h[di];
    if (hh === 'x') hat(t, bus, false); else if (hh === 'o') hat(t, bus, true);
  }
}

function musicTick() {
  const T = MUSIC.cur;
  if (!T || !AU.ctx) return;
  const c = AU.ctx;
  if (MUSIC.next < c.currentTime - 0.25) MUSIC.next = c.currentTime + 0.05;
  while (MUSIC.next < c.currentTime + 0.14) {
    playStep(T, MUSIC.step, MUSIC.next);
    MUSIC.next += T.spb;
    MUSIC.step++;
    if (T.once && MUSIC.step >= T.steps) {
      const cb = T.then;
      stopMusic();
      if (cb) cb();
      return;
    }
  }
}

function startSeq(T, name) {
  stopMusic();
  MUSIC.cur = T; MUSIC.name = name; MUSIC.step = 0;
  MUSIC.next = AU.ctx.currentTime + 0.06;
  if (AU.delay) AU.delay.delayTime.setValueAtTime(Math.min(1.4, T.spb * 3), AU.ctx.currentTime);
  MUSIC.timer = setInterval(musicTick, 25);
  musicTick();
}

function playMusic(name) {
  if (MUSIC.name === name && MUSIC.cur) return;
  if (!AU.ctx) { stopMusic(); MUSIC.pending = { name }; return; }
  startSeq(composeTrack(name), name);
}

function playJingle(name, then) {
  if (!AU.ctx) {
    stopMusic();
    if (then) then();
    return;
  }
  const T = composeJingle(name);
  T.then = then;
  startSeq(T, 'jingle:' + name);
}

function stopMusic() {
  clearInterval(MUSIC.timer);
  MUSIC.timer = 0; MUSIC.cur = null; MUSIC.name = null; MUSIC.pending = null;
}

function duckMusic(on) {
  MUSIC.duck = on;
  applyVolumes();
}
