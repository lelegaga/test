'use strict';
/* Iron Assault — session state, main loop, camera and scripting, weather,
   rendering, HUD, menus, touch controls and saved settings. */

/* ============================================================== save */
const SAVE_KEY = 'iron-assault-v1';
const Save = { unlocked: 1, best: 0, diff: 'normal', music: 60, sfx: 80, voice: true, shake: true, crt: true };
function loadSave() {
  try {
    const s = JSON.parse(localStorage.getItem(SAVE_KEY) || 'null');
    if (s && typeof s === 'object') Object.assign(Save, s);
  } catch (e) { /* storage unavailable */ }
  if (!DIFFS[Save.diff]) Save.diff = 'normal';
  if (matchMedia('(prefers-reduced-motion: reduce)').matches && !('shakeSet' in Save)) Save.shake = false;
}
function writeSave() {
  try { localStorage.setItem(SAVE_KEY, JSON.stringify(Save)); } catch (e) { /* ignore */ }
}

/* ============================================================ state */
const Game = {
  state: 'boot', diffKey: 'normal', score: 0, lives: 3, mission: 0, continues: 0, touch: false,
  get diff() { return DIFFS[this.diffKey]; },
};
let S = null;
let demoIdx = 0;
const [LC, LX] = mkCanvas(W, H);
const [VIG] = (() => {
  const [c, x] = mkCanvas(W, H);
  const gr = x.createRadialGradient(W / 2, H / 2, H * 0.35, W / 2, H / 2, H * 0.95);
  gr.addColorStop(0, 'rgba(0,0,0,0)');
  gr.addColorStop(1, 'rgba(0,0,0,0.5)');
  x.fillStyle = gr;
  x.fillRect(0, 0, W, H);
  return [c];
})();

function newSession(def, demo) {
  S = {
    def, diff: Game.diff, demo, t: 0, camX: 0, lockX: null, evi: 0, player: null,
    enemies: [], pb: [], eb: [], rockets: [], flames: [], grenades: [], items: [], pows: [], crates: [],
    corpses: [], parts: [], wx: [], lights: [],
    boss: null, bossStarted: false, bossDelay: 0, bossKind: null, wave: null, goT: 0, waveMsg: 0,
    shake: 0, shakeX: 0, shakeY: 0, whiteFlash: 0, redFlash: 0, lightning: 0, fade: 0,
    kills: 0, rescued: 0, deaths: 0, noHit: true, clearT: 0, clearDelay: 0, slow: 0,
    rush: !!def.rush, rushQueue: def.rush ? RUSH_ORDER.slice() : null, rushNext: 0, lastCam: 0,
  };
  S.L = genLevel(def, S.diff);
  S.theme = getTheme(def.theme);
  return S;
}

/* ======================================================== mission flow */
function missionLabel(idx) { return idx === 'rush' ? 'EX' : String(idx + 1); }

function startMission(idx) {
  const def = idx === 'rush' ? RUSH_DEF : LEVELS[idx];
  Game.mission = idx;
  newSession(def, false);
  S.player = makePlayer(def.rush ? 90 : 60);
  show(null);
  Game.state = 'playing';
  Input.active = true;
  Input.clear();
  duckMusic(false);
  const n = missionLabel(idx);
  showBanner(`<span class="px">MISSION ${n}</span><strong>${def.name}</strong><em class="px">START!</em>`, 'b-mission', 2600);
  playJingle('start', () => {
    if ((Game.state === 'playing' || Game.state === 'paused') && S.def === def && !S.bossStarted) playMusic(def.music);
  });
  say(idx === 'rush' ? 'Boss rush. Start!' : `Mission ${n}. Start!`);
  updateTouch();
}

function newGame(idx) {
  Game.score = 0;
  Game.lives = Game.diff.lives;
  Game.continues = 0;
  startMission(idx);
}

function update() {
  if (S.slow > 0) {
    S.slow--;
    if (S.slow % 2) return;
  }
  S.t++;
  if (Input.hit('pause')) { pauseGame(); return; }
  updatePlayer();
  updateCamera();
  if (S.t > 50) processEvents();
  updateWave();
  updateBossFlow();
  updateEnemies();
  updateBoss();
  updatePlayerShots();
  updateEnemyShots();
  updatePows();
  updateItems();
  updateCorpses();
  sweep(S.crates);
  updateDecoFx();
  updateParts();
  updateWeather();
  if (S.goT > 0) S.goT--;
  if (S.waveMsg > 0) S.waveMsg--;
  if (S.whiteFlash > 0) S.whiteFlash = Math.max(0, S.whiteFlash - 0.04);
  if (S.redFlash > 0) S.redFlash = Math.max(0, S.redFlash - 0.03);
  if (S.clearDelay > 0 && --S.clearDelay === 0) missionClear();
  if (S.clearT && ++S.clearT === 200) showResults();
}

function updateCamera() {
  const p = S.player;
  let maxX = S.L.len - W;
  if (S.lockX !== null) maxX = Math.min(maxX, S.lockX);
  if (p && !p.dead) {
    const target = p.x - W * 0.4;
    S.camX = Math.max(S.camX, Math.min(target, S.camX + 3, maxX));
  }
  if (S.shake > 0.3) {
    S.shakeX = rand(-1, 1) * S.shake;
    S.shakeY = rand(-1, 1) * S.shake * 0.7;
    S.shake *= 0.86;
  } else { S.shake = 0; S.shakeX = 0; S.shakeY = 0; }
}

function processEvents() {
  const L = S.L;
  while (S.evi < L.events.length) {
    const ev = L.events[S.evi];
    const trigger = ev.type === 'wave' ? ev.x - W : ev.x - W - 30;
    if (S.camX < trigger) break;
    if (ev.type === 'wave' && S.wave) break;
    S.evi++;
    runEvent(ev);
  }
  if (!S.bossStarted && !S.wave && S.camX >= L.arenaX - 0.5) startBoss();
}

function runEvent(ev) {
  const x = ev.x;
  switch (ev.type) {
    case 'wave':
      S.lockX = S.camX;
      S.wave = { queue: buildWave(ev.n), t: 0 };
      S.waveMsg = 100;
      SFX.alarm();
      break;
    case 'pow':
      S.pows.push({ x, y: groundY(x), state: 'tied', item: ev.item, t: randi(0, 40), phase: 0 });
      break;
    case 'crate':
      S.crates.push({ x, y: groundY(x), hp: 3, item: ev.item });
      break;
    case 'para':
      for (let i = 0; i < ev.n; i++) spawnEnemy('para', S.camX + rand(150, W - 30), -30 - i * 36, { state: 'chute' });
      break;
    case 'drone':
      for (let i = 0; i < ev.n; i++) spawnEnemy('drone', S.camX + W + 30 + i * 40, 50 + rand(0, 20));
      break;
    case 'tank':
      spawnEnemy('tank', S.camX + W + 40, groundY(S.camX + W + 40));
      break;
    case 'turret':
      spawnEnemy('turret', x, groundY(x));
      break;
    default:
      for (let i = 0; i < ev.n; i++) {
        const ex = x + i * 22;
        const plat = S.L.plats.find(p => ex >= p.x + 4 && ex <= p.x + p.w - 4);
        const onPlat = plat && chance(0.6);
        const e = spawnEnemy(ev.type, ex, onPlat ? plat.y : groundY(ex));
        e.onGround = true;
        if (ev.idle && ev.type !== 'shield') {
          e.state = 'idle';
          e.idlePose = chance(0.55) ? 'sit' : 'stand';
          e.f = chance(0.7) ? -1 : 1;
        }
      }
  }
}

function buildWave(n) {
  const mix = Object.assign({}, S.def.mix);
  delete mix.turret; delete mix.tank;
  const q = [];
  for (let i = 0; i < n; i++) q.push({ at: 30 + i * 42 + (i >= n / 2 ? 90 : 0), type: weighted(mix), side: i % 2 === 0 ? 1 : -1 });
  if (S.def.id >= 3) q.push({ at: 60 + n * 42, type: 'tank', side: 1 });
  return q;
}

function updateWave() {
  const w = S.wave;
  if (!w) return;
  w.t++;
  for (const s of w.queue) {
    if (s.done || w.t < s.at) continue;
    s.done = true;
    let e;
    if (s.type === 'para') e = spawnEnemy('para', S.camX + rand(60, W - 60), -30, { state: 'chute' });
    else if (s.type === 'drone') e = spawnEnemy('drone', S.camX + W + 20, 50);
    else {
      const x = s.side > 0 ? S.camX + W + 18 : S.camX - 18;
      e = spawnEnemy(s.type, x, groundY(x));
      e.onGround = true;
    }
    e.wave = true;
  }
  if (w.queue.every(s => s.done) && !S.enemies.some(e => e.wave && !e.dead)) {
    S.wave = null;
    S.lockX = null;
    S.goT = 180;
    SFX.go();
  }
}

function startBoss() {
  S.bossStarted = true;
  S.lockX = S.L.arenaX;
  const kind = S.rush ? S.rushQueue.shift() : S.def.boss;
  S.bossKind = kind;
  S.bossDelay = 150;
  stopMusic();
  SFX.alarm();
  const info = BOSS_INFO[kind];
  showBanner(`<i class="haz"></i><span class="px">WARNING</span><strong>${info.cn}</strong><span class="px sm">${info.en} APPROACHING</span><i class="haz"></i>`, 'b-warn', 2600);
}

function updateBossFlow() {
  if (S.bossDelay > 0 && --S.bossDelay === 0) {
    S.boss = makeBoss(S.bossKind);
    playMusic('boss');
  }
  if (S.rushNext > 0 && --S.rushNext === 0) startBoss();
}

function onBossDefeated() {
  if (S.rush && S.rushQueue.length) {
    S.rushNext = 220;
    const p = S.player;
    const x = clamp(p ? p.x + 40 : S.camX + 200, S.camX + 40, S.camX + W - 180);
    dropItem(pick(['H', 'R', 'F', 'S', 'L']), x, 40);
    dropItem('B', x + 24, 30);
    dropItem('food', x + 48, 20);
    popText(S.camX + W / 2, 100, 'SUPPLY DROP', '#9ff4a0');
  } else {
    S.clearDelay = 80;
  }
}

function missionClear() {
  S.clearT = 1;
  stopMusic();
  SFX.loopStopAll();
  playJingle('clear');
  say('Mission complete!');
  showBanner(`<span class="px">MISSION ${missionLabel(Game.mission)}</span><strong>任务完成</strong><em class="px">COMPLETE!</em>`, 'b-clear', 3200);
}

function showResults() {
  Game.state = 'results';
  Input.active = false;
  const d = S.diff;
  const secs = Math.round(S.t / 60);
  const rows = [
    ['击毁敌军', `${S.kills}`, 0],
    ['营救俘虏', `${S.rescued} × 1000`, S.rescued * 1000],
    [S.noHit ? '无伤通关' : S.deaths === 0 ? '未曾阵亡' : '阵亡次数', S.noHit ? 'PERFECT' : `${S.deaths}`, S.noHit ? 8000 : S.deaths === 0 ? 3000 : 0],
    ['作战用时', `${Math.floor(secs / 60)}:${pad(secs % 60, 2)}`, Math.max(0, 300 - secs) * 20],
  ];
  let bonus = 0;
  $('#res-tally').innerHTML = rows.map(([k, v, b], i) => {
    const pts = Math.round((b * d.scoreMul) / 10) * 10;
    bonus += pts;
    return `<div style="--i:${i}"><dt>${k}</dt><dd><span>${v}</span><b class="px">${pts ? '+' + pts : '—'}</b></dd></div>`;
  }).join('');
  Game.score += bonus;
  Save.best = Math.max(Save.best, Game.score);
  const last = Game.mission === LEVELS.length - 1;
  if (typeof Game.mission === 'number') Save.unlocked = Math.max(Save.unlocked, Math.min(LEVELS.length, Game.mission + 2));
  writeSave();
  $('#res-kicker').textContent = `MISSION ${missionLabel(Game.mission)} · ${d.en}`;
  $('#res-title').textContent = Game.mission === 'rush' ? '首领连战 通关' : `${S.def.name} · 任务完成`;
  $('#res-total').textContent = pad(Game.score, 8);
  const next = $('#res-next');
  next.hidden = Game.mission === 'rush';
  next.textContent = last ? '查看结局' : '下一关';
  show('results');
  refreshTitle();
}

function nextMission() {
  if (Game.mission === LEVELS.length - 1) { showEnding(); return; }
  startMission(Game.mission + 1);
}

function showEnding() {
  Game.state = 'ending';
  $('#end-score').textContent = pad(Game.score, 8);
  $('#end-diff').textContent = `${Game.diff.cn} · ${Game.diff.en}`;
  show('end');
  playJingle('victory', () => { if (Game.state === 'ending') playMusic('title'); });
  say('Congratulations! Mission accomplished.');
}

let overTimer = 0;
function gameOver() {
  Game.state = 'gameover';
  Input.active = false;
  stopMusic();
  SFX.loopStopAll();
  playJingle('over');
  say('Game over');
  Save.best = Math.max(Save.best, Game.score);
  writeSave();
  let n = 9;
  $('#over-count').textContent = n;
  show('over');
  clearInterval(overTimer);
  overTimer = setInterval(() => {
    n--;
    $('#over-count').textContent = Math.max(0, n);
    if (n <= 0) { clearInterval(overTimer); goTitle(); }
  }, 1000);
  updateTouch();
}

function continueGame() {
  clearInterval(overTimer);
  Game.lives = Game.diff.lives;
  Game.continues++;
  respawn(S.player);
  show(null);
  Game.state = 'playing';
  Input.active = true;
  Input.clear();
  playMusic(S.boss || S.bossDelay ? 'boss' : S.def.music);
  if (S.boss && S.boss.kind === 'heli' && !S.boss.dying) SFX.loopStart('rotor');
  updateTouch();
}

function pauseGame() {
  if (Game.state !== 'playing') return;
  Game.state = 'paused';
  Input.active = false;
  Input.clear();
  duckMusic(true);
  SFX.loopStopAll();
  if (window.speechSynthesis) try { speechSynthesis.cancel(); } catch (e) { /* ignore */ }
  show('pause');
  updateTouch();
}

function resumeGame() {
  if (Game.state !== 'paused') return;
  show(null);
  Game.state = 'playing';
  Input.active = true;
  Input.clear();
  duckMusic(false);
  if (S.boss && S.boss.kind === 'heli' && !S.boss.dying) SFX.loopStart('rotor');
  updateTouch();
}

function goTitle() {
  clearInterval(overTimer);
  SFX.loopStopAll();
  duckMusic(false);
  Game.state = 'title';
  Input.active = false;
  Input.clear();
  newSession(LEVELS[demoIdx], true);
  S.camX = 240;
  show('title');
  playMusic('title');
  refreshTitle();
  updateTouch();
}

/* ============================================================ demo */
function updateDemo() {
  S.t++;
  S.camX += 0.55;
  if (S.camX > S.L.arenaX - 40) S.camX = 0;
  updateDecoFx();
  updateParts();
  updateWeather();
  if (S.lightning > 0) S.lightning--;
  if (S.t > 760) {
    demoIdx = (demoIdx + 1) % LEVELS.length;
    newSession(LEVELS[demoIdx], true);
    S.camX = 240;
  }
  S.fade = S.t < 20 ? 1 - S.t / 20 : S.t > 740 ? (S.t - 740) / 20 : 0;
}

/* ========================================================= weather */
function updateWeather() {
  const th = S.theme;
  const dx = S.camX - S.lastCam;
  S.lastCam = S.camX;
  if (S.lightning > 0 && !S.demo) S.lightning--;
  switch (th.weather) {
    case 'leaves':
      if (chance(0.05)) S.wx.push({ k: 'leaf', x: rand(-20, W + 40), y: -6, vx: rand(-0.5, 0.1), vy: rand(0.35, 0.7), rot: rand(TAU), vr: rand(-0.08, 0.08), col: pick(['#4f9a3a', '#7ab84a', '#c9b04a']) });
      if (S.wx.length < 14 && chance(0.05)) S.wx.push({ k: 'fly', x: rand(W), y: rand(120, 200), vx: 0, vy: 0, ph: rand(TAU), life: randi(200, 400) });
      break;
    case 'sand':
      if (chance(0.55)) S.wx.push({ k: 'sand', x: W + 10, y: rand(H), vx: rand(-7, -4), vy: rand(-0.3, 0.3), len: rand(4, 12), a: rand(0.15, 0.4) });
      break;
    case 'snow':
      while (S.wx.length < 150) {
        const r = rand(0.6, 1.9);
        S.wx.push({ k: 'snow', x: rand(-20, W + 40), y: S.t < 2 ? rand(-10, H) : rand(-20, -4), vx: rand(-0.7, -0.2), vy: rand(0.4, 1.2) * r, r, ph: rand(TAU) });
      }
      break;
    case 'rain':
      while (S.wx.length < 140) S.wx.push({ k: 'rain', x: rand(-20, W + 60), y: rand(-H, 0), vx: -1.6, vy: rand(7, 9.5), len: rand(6, 11) });
      if (chance(1 / 520)) { S.lightning = 12; SFX.thunder(); }
      break;
    case 'embers':
      if (chance(0.35)) S.wx.push({ k: 'ember', x: rand(W), y: H + 4, vx: rand(-0.4, 0.4), vy: rand(-1.4, -0.4), life: randi(120, 260) });
      break;
  }
  for (const w of S.wx) {
    w.x += w.vx - dx * (w.k === 'snow' ? w.r / 1.9 : 1);
    w.y += w.vy;
    switch (w.k) {
      case 'leaf': w.x += Math.sin(S.t * 0.05 + w.rot) * 0.3; w.rot += w.vr; if (w.y > groundY(S.camX + w.x)) w.dead = true; break;
      case 'fly': w.ph += 0.03; w.x += Math.cos(w.ph) * 0.3; w.y += Math.sin(w.ph * 1.3) * 0.2; if (--w.life <= 0) w.dead = true; break;
      case 'snow': w.x += Math.sin(S.t * 0.03 + w.ph) * 0.25; if (w.y > groundY(S.camX + w.x) + 2) w.dead = true; break;
      case 'rain':
        if (w.y > groundY(S.camX + w.x)) {
          if (chance(0.5)) addPart({ type: 'splash', x: S.camX + w.x, y: groundY(S.camX + w.x) - 1, vx: rand(-0.6, 0.6), vy: rand(-1.2, -0.4), grav: 0.15, life: 8 });
          w.dead = true;
        }
        break;
      case 'ember': w.x += Math.sin(S.t * 0.04 + w.life) * 0.3; if (--w.life <= 0) w.dead = true; break;
    }
    if (w.x < -60 || w.x > W + 80 || w.y > H + 10) w.dead = true;
  }
  sweep(S.wx);
}

function drawWeather() {
  for (const w of S.wx) {
    switch (w.k) {
      case 'leaf':
        g.save(); g.translate(w.x, w.y); g.rotate(w.rot);
        g.fillStyle = w.col; g.fillRect(-2, -1, 4, 2);
        g.restore();
        break;
      case 'fly': {
        const a = 0.4 + Math.sin(w.ph * 3) * 0.4;
        if (a > 0.1) { g.globalAlpha = a; g.fillStyle = '#eaff9a'; g.fillRect(w.x, w.y, 1, 1); g.globalCompositeOperation = 'lighter'; g.drawImage(GLOW.yellow, w.x - 5, w.y - 5, 10, 10); g.globalCompositeOperation = 'source-over'; g.globalAlpha = 1; }
        break;
      }
      case 'sand':
        g.globalAlpha = w.a; g.fillStyle = '#ffe2b0'; g.fillRect(w.x, w.y, w.len, 1); g.globalAlpha = 1;
        break;
      case 'snow':
        g.fillStyle = 'rgba(255,255,255,0.9)';
        g.fillRect(w.x, w.y, w.r > 1.3 ? 2 : 1, w.r > 1.3 ? 2 : 1);
        break;
      case 'rain':
        g.strokeStyle = 'rgba(170,190,240,0.4)'; g.lineWidth = 1;
        g.beginPath(); g.moveTo(w.x, w.y); g.lineTo(w.x + w.vx * 1.2, w.y - w.len); g.stroke();
        break;
      case 'ember':
        g.fillStyle = w.life & 8 ? '#ffb040' : '#ff6a2a';
        g.fillRect(w.x, w.y, 1, 1);
        g.globalCompositeOperation = 'lighter'; g.globalAlpha = 0.5;
        g.drawImage(GLOW.fire, w.x - 4, w.y - 4, 8, 8);
        g.globalAlpha = 1; g.globalCompositeOperation = 'source-over';
        break;
    }
  }
}

function updateDecoFx() {
  for (const d of S.L.deco) {
    if (d.x < S.camX - 40 || d.x > S.camX + W + 40) continue;
    const gy = groundY(d.x);
    if (d.kind === 'firebarrel' && S.t % 3 === 0) addPart({ type: 'fire', x: d.x + rand(-4, 4), y: gy - 15, vx: rand(-0.2, 0.2), vy: -0.9, r: rand(2, 3.5), life: 18 });
    if (d.kind === 'firebarrel' && S.t % 16 === 0) smokeAt(d.x, gy - 26, 1, 3, '#3a3634');
    if (d.kind === 'wreck' && S.t % 14 === 0) smokeAt(d.x + 4, gy - 22, 1, 4, '#3e3834');
  }
}

/* ========================================================== render */
function drawGround() {
  const th = S.theme, cam = S.camX;
  const segs = S.L.segs;
  for (const [x0, x1, y] of segs) {
    if (x1 < cam - 10 || x0 > cam + W + 10) continue;
    const a = Math.max(x0, cam - 10), b = Math.min(x1, cam + W + 10);
    g.fillStyle = th.groundPat;
    g.fillRect(a, y, b - a, H - y + 30);
    g.fillStyle = th.shade;
    g.fillRect(a, Math.max(y, GROUND), b - a, H);
    g.save();
    g.translate(0, y - 4);
    g.fillStyle = th.topPat;
    g.fillRect(a, 0, b - a, 16);
    g.restore();
  }
  for (let i = 1; i < segs.length; i++) {
    const x = segs[i][0];
    if (x < cam - 10 || x > cam + W + 10) continue;
    const y0 = segs[i - 1][2], y1 = segs[i][2];
    if (y0 === y1) continue;
    const top = Math.min(y0, y1), bot = Math.max(y0, y1);
    g.fillStyle = y0 > y1 ? th.wall[0] : th.wall[1];
    g.fillRect(y0 > y1 ? x : x - 3, top + 2, 3, bot - top);
    g.fillStyle = 'rgba(0,0,0,0.25)';
    g.fillRect(y0 > y1 ? x + 3 : x - 5, top + 4, 2, bot - top);
  }
}

function drawPlatforms() {
  const th = S.theme;
  for (const p of S.L.plats) {
    if (p.x + p.w < S.camX - 10 || p.x > S.camX + W + 10) continue;
    th.plat(p.x, p.y, p.w, groundY(p.x + p.w / 2));
  }
}

function drawDecos() {
  for (const d of S.L.deco) {
    if (d.x < S.camX - 70 || d.x > S.camX + W + 70) continue;
    const gy = groundY(d.x);
    DECO[d.kind](d.x, gy, d.v);
    if (d.kind === 'lamp') {
      light(d.x + 12, gy - 50, 80, 'mask', 1);
      light(d.x + 12, gy - 46, 40, 'yellow', 0.8);
      g.save();
      g.globalCompositeOperation = 'lighter';
      g.fillStyle = 'rgba(255,230,160,0.07)';
      g.beginPath(); g.moveTo(d.x + 9, gy - 50); g.lineTo(d.x + 15, gy - 50); g.lineTo(d.x + 40, gy); g.lineTo(d.x - 16, gy); g.fill();
      g.restore();
    }
    if (d.kind === 'firebarrel') light(d.x, gy - 20, 70 + Math.sin(S.t * 0.4) * 6, 'fire', 0.9);
    if (d.kind === 'console' && (S.t >> 4) & 1) light(d.x, gy - 14, 22, 'green', 0.6);
  }
}

function drawPlayer() {
  const p = S.player;
  if (!p) return;
  if (p.inv > 0 && !p.dead && (p.inv >> 2) & 1) return;
  const pose = p.dead ? 'air' : p.crouch ? 'crouch' : !p.onGround ? 'air' : p.run ? 'run' : 'stand';
  drawSoldier({
    x: p.x, y: p.y, f: p.f, pal: PAL.player, pose, phase: p.phase, aim: p.aim, gear: 'band', t: p.t,
    weapon: p.dead ? null : WEAPONS[p.weapon].gun, recoil: p.recoil,
    arms: p.dead ? 'flail' : p.knife ? 'knife' : S.clearT && S.clearT > 30 ? 'salute' : null,
    rot: p.dead ? -Math.min(Math.PI / 2, p.dead * 0.09) : 0,
  });
}

function drawLights(cam, sx) {
  const th = S.theme;
  if (th.dark > 0) {
    LX.globalCompositeOperation = 'source-over';
    LX.clearRect(0, 0, W, H);
    LX.fillStyle = th.darkCol + th.dark + ')';
    LX.fillRect(0, 0, W, H);
    LX.globalCompositeOperation = 'destination-out';
    for (const l of S.lights) {
      LX.globalAlpha = clamp(l.a, 0, 1);
      LX.drawImage(GLOW.white, l.x - cam + sx - l.r, l.y - l.r, l.r * 2, l.r * 2);
    }
    LX.globalAlpha = 1;
    LX.globalCompositeOperation = 'source-over';
    g.drawImage(LC, 0, 0);
  }
  g.globalCompositeOperation = 'lighter';
  const k = th.dark > 0 ? 0.45 : 0.3;
  for (const l of S.lights) {
    if (l.kind === 'mask') continue;
    g.globalAlpha = clamp(l.a * k, 0, 1);
    const r = l.r * 0.75;
    g.drawImage(GLOW[l.kind] || GLOW.fire, l.x - cam + sx - r, l.y - r, r * 2, r * 2);
  }
  g.globalAlpha = 1;
  g.globalCompositeOperation = 'source-over';
}

function render() {
  g.setTransform(1, 0, 0, 1, 0, 0);
  if (!S) { g.fillStyle = '#000'; g.fillRect(0, 0, W, H); return; }
  X = g;
  S.lights.length = 0;
  const th = S.theme, cam = Math.round(S.camX), sx = Math.round(S.shakeX), sy = Math.round(S.shakeY);
  g.drawImage(th.sky, 0, 0);
  if (S.lightning > 0) { g.fillStyle = `rgba(210,220,255,${(S.lightning / 16).toFixed(3)})`; g.fillRect(0, 0, W, H); }
  for (const L of th.layers) {
    const off = (((cam * L.p) % TW) + TW) % TW;
    const ox = -Math.round(off) + Math.round(sx * L.p);
    g.drawImage(L.c, ox, sy >> 1);
    g.drawImage(L.c, ox + TW, sy >> 1);
  }
  g.setTransform(1, 0, 0, 1, -cam + sx, sy);
  drawDecos();
  drawGround();
  drawPlatforms();
  for (const c of S.crates) drawCrate(c);
  for (const it of S.items) if (!(it.timed && it.t > 720 && (it.t >> 2) & 1)) drawItem(it, S.t);
  for (const w of S.pows) drawPow(w);
  for (const c of S.corpses) drawCorpse(c);
  drawBoss();
  for (const e of S.enemies) drawEnemy(e);
  drawPlayer();
  drawShots();
  drawParts();
  g.setTransform(1, 0, 0, 1, 0, 0);
  drawLights(cam, sx);
  drawWeather();
  if (S.lightning > 6) { g.fillStyle = 'rgba(230,235,255,0.25)'; g.fillRect(0, 0, W, H); }
  g.drawImage(VIG, 0, 0);
  if (S.redFlash > 0) { g.fillStyle = `rgba(220,30,20,${(S.redFlash * 0.5).toFixed(3)})`; g.fillRect(0, 0, W, H); }
  if (S.whiteFlash > 0) { g.fillStyle = `rgba(255,250,235,${S.whiteFlash.toFixed(3)})`; g.fillRect(0, 0, W, H); }
  if (!S.demo) drawHUD();
  if (S.fade > 0) { g.fillStyle = `rgba(0,0,0,${clamp(S.fade, 0, 1).toFixed(3)})`; g.fillRect(0, 0, W, H); }
}

/* ============================================================= HUD */
function hudPanel(x, y, w, h) {
  g.fillStyle = 'rgba(12,14,10,0.62)';
  g.fillRect(x, y, w, h);
  g.fillStyle = 'rgba(233,180,76,0.85)';
  g.fillRect(x, y, 3, 1); g.fillRect(x, y, 1, 3);
  g.fillRect(x + w - 3, y + h - 1, 3, 1); g.fillRect(x + w - 1, y + h - 3, 1, 3);
}
function heart(x, y, full) {
  const c = full ? '#e8433a' : 'rgba(255,255,255,0.18)';
  g.fillStyle = c;
  g.fillRect(x + 1, y, 2, 1); g.fillRect(x + 4, y, 2, 1);
  g.fillRect(x, y + 1, 7, 2); g.fillRect(x + 1, y + 3, 5, 1); g.fillRect(x + 2, y + 4, 3, 1); g.fillRect(x + 3, y + 5, 1, 1);
  if (full) { g.fillStyle = '#ffb0a8'; g.fillRect(x + 1, y + 1, 1, 1); }
}

function drawHUD() {
  const p = S.player;
  if (!p) return;
  hudPanel(4, 4, 132, 30);
  pxText('1UP', 10, 9, '#e9b44c');
  pxText(pad(Game.score, 8), 42, 9, '#ffffff');
  for (let i = 0; i < p.maxHp; i++) heart(10 + i * 10, 22, i < p.hp);
  pxText('×' + Math.max(0, Game.lives), 132, 21, '#ffffff', 'right');
  g.fillStyle = PAL.player.band; g.fillRect(100, 23, 7, 2);
  g.fillStyle = PAL.player.skin; g.fillRect(100, 25, 7, 4);
  g.fillStyle = PAL.player.hair; g.fillRect(100, 21, 7, 2);

  hudPanel(176, 4, 128, 30);
  pxText('ARMS', 182, 9, '#e9b44c');
  pxText('BOMB', 254, 9, '#e9b44c');
  const wcol = ITEM_COL[p.weapon] || '#ffffff';
  if (p.weapon === 'P') pxText('∞', 182, 21, '#ffffff');
  else {
    g.fillStyle = wcol; g.fillRect(182, 20, 9, 10);
    pxText(p.weapon, 183, 21, '#ffffff');
    pxText(String(p.ammo), 196, 21, p.ammo < 10 ? '#ff8a6a' : '#ffffff');
  }
  pxText(String(p.bombs), 254, 21, p.bombs ? '#ffffff' : '#ff8a6a');

  hudPanel(W - 112, 4, 108, 30);
  pxText('M' + missionLabel(Game.mission), W - 106, 9, '#e9b44c');
  pxText(S.diff.en, W - 10, 9, '#c9cdb8', 'right');
  pxText('POW', W - 106, 21, '#9ff4a0');
  pxText(String(S.rescued), W - 10, 21, '#ffffff', 'right');

  const b = S.boss;
  if (b && b.active) {
    const bw = 260, bx = (W - bw) / 2, by = H - 16;
    hudPanel(bx - 6, by - 14, bw + 12, 26);
    pxText(BOSS_INFO[b.kind].en, bx, by - 10, '#ff8a6a');
    g.fillStyle = '#2a0e0c'; g.fillRect(bx, by, bw, 5);
    const k = clamp(b.hp / b.maxHp, 0, 1);
    g.fillStyle = k > 0.5 ? '#e9b44c' : '#e8433a';
    g.fillRect(bx, by, Math.round(bw * k), 5);
    g.fillStyle = 'rgba(255,255,255,0.35)'; g.fillRect(bx, by, Math.round(bw * k), 1);
  }
  if (S.goT > 0 && (S.goT >> 4) & 1) {
    pxText('GO!', W - 58, 112, '#ffd35a', 'left', 16);
    g.fillStyle = '#ffd35a';
    for (let i = 0; i < 6; i++) g.fillRect(W - 14 + i - 6, 114 + i, 2, 16 - i * 2);
  }
  if (S.waveMsg > 0 && (S.waveMsg >> 3) & 1) pxText('ENEMY WAVE!', W / 2, 60, '#ff6a4a', 'center', 8);
  if (S.rushNext > 0) pxText('NEXT TARGET INBOUND', W / 2, 60, '#ffd35a', 'center');
}

/* ============================================================ loop */
let lastT = performance.now(), acc = 0;
function frame(now) {
  requestAnimationFrame(frame);
  const dt = Math.min(100, now - lastT);
  lastT = now;
  pollPad();
  if (Game.state === 'playing') {
    acc += dt;
    let n = 0;
    while (acc >= STEP && n < 5) {
      update();
      Input.consume();
      acc -= STEP;
      n++;
      if (Game.state !== 'playing') break;
    }
    if (n >= 5) acc = 0;
  } else {
    if (Game.state === 'title') {
      acc += dt;
      while (acc >= STEP) { updateDemo(); acc -= STEP; }
    } else acc = 0;
    menuPad();
  }
  render();
}

/* ============================================================ menus */
const $ = s => document.querySelector(s);
const stage = $('#stage');
const screens = {};
document.querySelectorAll('[data-screen]').forEach(el => { screens[el.id.replace('scr-', '')] = el; });
let curScreen = null;

function show(name) {
  for (const k in screens) screens[k].hidden = k !== name;
  curScreen = name;
  if (name) {
    const el = screens[name];
    const f = el.querySelector('.is-current') || el.querySelector('.menu button, .lcard:not([disabled]), .row-btns button:not([hidden])') || el.querySelector('button:not([disabled]):not([hidden])');
    if (f) f.focus({ preventScroll: true });
  }
}
function focusables() {
  if (!curScreen) return [];
  return [...screens[curScreen].querySelectorAll('button:not([disabled]), input')].filter(el => el.offsetParent !== null && !el.hidden);
}
function moveFocus(d) {
  const list = focusables();
  if (!list.length) return;
  const i = list.indexOf(document.activeElement);
  const n = list[(i + d + list.length) % list.length];
  n.focus({ preventScroll: false });
  SFX.ui();
}
function menuPad() {
  if (curScreen) {
    if (Input.hit('up') || Input.hit('left')) moveFocus(-1);
    if (Input.hit('down') || Input.hit('right')) moveFocus(1);
    if (Input.hit('jump') || Input.hit('fire')) { const a = document.activeElement; if (a && a.click) a.click(); }
    if (Input.hit('pause')) onMenuBack();
  }
  Input.consume();
}
addEventListener('keydown', e => {
  if (Input.active || !curScreen) return;
  const a = document.activeElement;
  if (a && a.type === 'range' && (e.code === 'ArrowLeft' || e.code === 'ArrowRight')) return;
  if (['ArrowUp', 'ArrowLeft', 'KeyW', 'KeyA'].includes(e.code)) { e.preventDefault(); moveFocus(-1); }
  else if (['ArrowDown', 'ArrowRight', 'KeyS', 'KeyD'].includes(e.code)) { e.preventDefault(); moveFocus(1); }
  else if (e.code === 'KeyJ' || e.code === 'KeyZ') { e.preventDefault(); if (a && a.click) a.click(); }
});

function backTarget() { return Game.state === 'paused' ? 'pause' : 'title'; }
function onMenuBack() {
  if (Game.state === 'paused') {
    if (curScreen === 'pause') resumeGame(); else { show('pause'); SFX.ui(); }
  } else if (Game.state === 'title' && curScreen && curScreen !== 'title') {
    show('title');
    SFX.ui();
  }
}

const UI = {
  start() { newGame(0); },
  levels() { buildLevelCards(); show('levels'); },
  diff() { buildDiffCards(); show('diff'); },
  rush() { newGame('rush'); },
  settings() { syncSettings(); show('settings'); },
  help() { show('help'); },
  back() { show(backTarget()); },
  setdiff(btn) {
    Game.diffKey = Save.diff = btn.dataset.diff;
    writeSave();
    buildDiffCards();
    refreshTitle();
    show('title');
  },
  play(btn) { newGame(Number(btn.dataset.m)); },
  resume() { resumeGame(); },
  restart() { const m = Game.mission; Game.lives = Game.diff.lives; SFX.loopStopAll(); startMission(m); },
  quit() { goTitle(); },
  next() { nextMission(); },
  continue() { continueGame(); },
};
document.addEventListener('click', e => {
  const b = e.target.closest('[data-act]');
  if (!b) return;
  onUserGesture();
  SFX.select();
  const fn = UI[b.dataset.act];
  if (fn) fn(b);
});

function refreshTitle() {
  const d = Game.diff;
  $('#diff-label').textContent = d.cn;
  $('#best-score').textContent = pad(Save.best, 8);
}

function buildDiffCards() {
  const wrap = $('#diff-cards');
  const bars = (v, max) => {
    const n = Math.round(clamp(v / max, 0, 1) * 5);
    return '<i class="bars">' + Array.from({ length: 5 }, (_, i) => `<b class="${i < n ? 'on' : ''}"></b>`).join('') + '</i>';
  };
  wrap.innerHTML = Object.values(DIFFS).map(d => `
    <button type="button" class="dcard${d.key === Game.diffKey ? ' is-current' : ''}" data-act="setdiff" data-diff="${d.key}" id="diff-${d.key}">
      <span class="px dcard-en">${d.en}</span>
      <strong>${d.cn}</strong>
      <span class="dcard-desc">${d.desc}</span>
      <span class="dstat"><em>生命</em><b class="px">${d.lives}</b></span>
      <span class="dstat"><em>血量</em><b class="hearts">${'♥'.repeat(d.hp)}</b></span>
      <span class="dstat"><em>敌方火力</em>${bars(d.fire, 1.9)}</span>
      <span class="dstat"><em>敌军密度</em>${bars(d.dens, 1.5)}</span>
      <span class="dstat"><em>得分倍率</em><b class="px">×${d.scoreMul}</b></span>
    </button>`).join('');
}

function buildLevelCards() {
  const wrap = $('#level-cards');
  wrap.innerHTML = '';
  LEVELS.forEach((L, i) => {
    const locked = i + 1 > Save.unlocked;
    const btn = document.createElement('button');
    btn.type = 'button';
    btn.className = 'lcard' + (locked ? ' is-locked' : '');
    btn.id = 'mission-' + L.id;
    if (locked) btn.disabled = true;
    else { btn.dataset.act = 'play'; btn.dataset.m = String(i); }
    const cvs = document.createElement('canvas');
    cvs.width = 192; cvs.height = 108;
    themeThumb(L.theme, cvs);
    btn.appendChild(cvs);
    const info = document.createElement('span');
    info.className = 'lcard-info';
    info.innerHTML = `<span class="px">MISSION ${L.id}</span><strong>${L.name}</strong><em>${locked ? '通关上一关后解锁' : L.brief}</em><small>首领 · ${BOSS_INFO[L.boss].cn}</small>`;
    btn.appendChild(info);
    wrap.appendChild(btn);
  });
  const rush = document.createElement('button');
  rush.type = 'button';
  rush.className = 'lcard lcard-rush';
  rush.id = 'mission-rush';
  rush.dataset.act = 'rush';
  rush.innerHTML = `<span class="rush-art">首领连战</span><span class="lcard-info"><span class="px">EXTRA</span><strong>${RUSH_DEF.name}</strong><em>${RUSH_DEF.brief}</em><small>首领 · 全部五名</small></span>`;
  wrap.appendChild(rush);
}

function syncSettings() {
  $('#set-music').value = Save.music;
  $('#set-sfx').value = Save.sfx;
  $('#set-voice').checked = Save.voice;
  $('#set-shake').checked = Save.shake;
  $('#set-crt').checked = Save.crt;
  $('#set-music-v').textContent = Save.music;
  $('#set-sfx-v').textContent = Save.sfx;
}
function applySettings() {
  AU.musicVol = Save.music / 100;
  AU.sfxVol = Save.sfx / 100;
  AU.voice = Save.voice;
  applyVolumes();
  $('#crt').hidden = !Save.crt;
}
function bindSettings() {
  for (const id of ['music', 'sfx']) {
    $('#set-' + id).addEventListener('input', e => {
      Save[id] = Number(e.target.value);
      $('#set-' + id + '-v').textContent = Save[id];
      applySettings();
      writeSave();
      if (id === 'sfx') SFX.pistol(0);
    });
  }
  for (const id of ['voice', 'shake', 'crt']) {
    $('#set-' + id).addEventListener('change', e => {
      Save[id] = e.target.checked;
      if (id === 'shake') Save.shakeSet = true;
      applySettings();
      writeSave();
      if (id === 'voice' && Save.voice) say('Roger!');
    });
  }
}

let bannerTimer = 0;
function showBanner(html, cls, ms) {
  const b = $('#banner');
  b.className = 'banner ' + cls;
  b.innerHTML = html;
  b.hidden = false;
  b.style.animation = 'none';
  void b.offsetHeight;
  b.style.animation = '';
  clearTimeout(bannerTimer);
  bannerTimer = setTimeout(() => { b.hidden = true; }, ms);
}

/* ====================================================== touch & layout */
function updateTouch() {
  const t = $('#touch');
  const on = Game.touch && (Game.state === 'playing');
  if (t.hidden === on) { t.hidden = !on; fit(); }
}

function setupTouch() {
  const coarse = matchMedia('(pointer: coarse)').matches;
  Game.touch = coarse;
  addEventListener('touchstart', () => { if (!Game.touch) { Game.touch = true; updateTouch(); } }, { passive: true });
  document.querySelectorAll('#touch [data-t]').forEach(btn => {
    const a = btn.dataset.t;
    const on = e => {
      e.preventDefault();
      onUserGesture();
      if (a === 'pause') { pauseGame(); return; }
      Input.press(a);
      btn.classList.add('on');
      try { btn.setPointerCapture(e.pointerId); } catch (err) { /* ignore */ }
    };
    const off = e => {
      e.preventDefault();
      if (a !== 'pause') Input.release(a);
      btn.classList.remove('on');
    };
    btn.addEventListener('pointerdown', on);
    btn.addEventListener('pointerup', off);
    btn.addEventListener('pointercancel', off);
  });
  const pad = $('#dpad');
  const dirs = ['left', 'right', 'up', 'down'];
  const setDirs = set => {
    for (const d of dirs) {
      if (set[d] && !Input.down[d]) Input.press(d);
      if (!set[d] && Input.down[d]) Input.release(d);
    }
    pad.dataset.dir = dirs.filter(d => set[d]).join(' ');
  };
  const move = e => {
    const r = pad.getBoundingClientRect();
    const dx = (e.clientX - (r.left + r.width / 2)) / (r.width / 2);
    const dy = (e.clientY - (r.top + r.height / 2)) / (r.height / 2);
    setDirs({ left: dx < -0.3, right: dx > 0.3, up: dy < -0.45, down: dy > 0.45 });
    const k = pad.querySelector('.dp-knob');
    k.style.transform = `translate(${clamp(dx, -1, 1) * 30}%, ${clamp(dy, -1, 1) * 30}%)`;
  };
  pad.addEventListener('pointerdown', e => { e.preventDefault(); onUserGesture(); try { pad.setPointerCapture(e.pointerId); } catch (err) { /* ignore */ } move(e); });
  pad.addEventListener('pointermove', e => { if (e.buttons || e.pointerType === 'touch') move(e); });
  const end = () => { setDirs({}); pad.querySelector('.dp-knob').style.transform = ''; };
  pad.addEventListener('pointerup', end);
  pad.addEventListener('pointercancel', end);
}

function fit() {
  const app = $('#app');
  const touch = $('#touch');
  const portrait = innerHeight > innerWidth * 1.05;
  document.body.classList.toggle('portrait', portrait);
  const aw = app.clientWidth - 32;
  let ah = app.clientHeight - 16;
  if (!touch.hidden && portrait) ah -= Math.min(240, ah * 0.42);
  const s = Math.max(0.4, Math.min(aw / W, ah / H));
  stage.style.width = Math.floor(W * s) + 'px';
  stage.style.height = Math.floor(H * s) + 'px';
  stage.style.fontSize = clamp(s * 7, 9.5, 19).toFixed(2) + 'px';
}

/* ============================================================ boot */
function boot() {
  loadSave();
  Game.diffKey = Save.diff;
  bindSettings();
  applySettings();
  setupTouch();
  addEventListener('resize', fit);
  addEventListener('orientationchange', () => setTimeout(fit, 120));
  document.addEventListener('visibilitychange', () => { if (document.hidden) pauseGame(); });
  fit();
  goTitle();
  if (document.fonts && document.fonts.load) {
    document.fonts.load('8px "Press Start 2P"').catch(() => {});
  }
  requestAnimationFrame(t => { lastT = t; frame(t); });
}
boot();
