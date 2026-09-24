'use strict';
/* Iron Assault — particles, physics, the player, weapons, regular enemies,
   projectiles, prisoners, items and supply crates. */

/* ================================================================ misc */
function pan(x) { return S ? clamp((x - S.camX - W / 2) / (W / 2), -1, 1) * 0.7 : 0; }
function shake(a) { if (Save.shake && S) S.shake = Math.max(S.shake, a); }
function light(x, y, r, kind = 'fire', a = 1) { S.lights.push({ x, y, r, kind, a }); }
function addScore(n, x, y, col) {
  const v = Math.max(10, Math.round((n * S.diff.scoreMul) / 10) * 10);
  Game.score += v;
  if (x !== undefined) popText(x, y, String(v), col);
}

/* =========================================================== particles */
function addPart(o) {
  o.max = o.life;
  S.parts.push(o);
  return o;
}
function spark(x, y, n = 4, col = '#ffd86a', spd = 2.4) {
  for (let i = 0; i < n; i++) addPart({ type: 'spark', x, y, vx: rand(-spd, spd), vy: rand(-spd, spd * 0.4), life: randi(7, 14), col, grav: 0.12 });
}
function smokeAt(x, y, n = 1, r = 4, col, wait = 0) {
  for (let i = 0; i < n; i++) addPart({ type: 'smoke', x: x + rand(-3, 3), y: y + rand(-3, 3), vx: rand(-0.3, 0.3), vy: rand(-0.7, -0.25), r: r * rand(0.7, 1.2), grow: 0.1, life: randi(30, 55), col, wait });
}
function dust(x, y, n = 3) {
  for (let i = 0; i < n; i++) addPart({ type: 'dust', x: x + rand(-4, 4), y: y - 1, vx: rand(-0.8, 0.8), vy: rand(-0.5, -0.1), r: rand(1.5, 3), life: randi(14, 24) });
}
function debris(x, y, n, cols = ['#3a3530', '#5a524a', '#8a7a66']) {
  for (let i = 0; i < n; i++) addPart({ type: 'debris', x, y, vx: rand(-2.6, 2.6), vy: rand(-4.5, -1), w: randi(2, 4), h: randi(1, 3), rot: rand(TAU), vr: rand(-0.4, 0.4), grav: 0.22, life: randi(40, 70), col: pick(cols) });
}
function popText(x, y, s, col = '#ffffff') {
  addPart({ type: 'text', x, y, vx: 0, vy: -0.45, s, col, life: 50 });
}
function flashAt(m, d, s = 1) {
  addPart({ type: 'mflash', x: m.x, y: m.y, dx: d.x, dy: d.y, s, life: 3 });
}
function casing(p) {
  addPart({ type: 'shell', x: p.x, y: p.y - 15, vx: -p.f * rand(0.6, 1.4), vy: rand(-2.6, -1.6), grav: 0.2, rot: 0, vr: 0.3, life: 40 });
}
function explosion(x, y, size = 1) {
  const n = Math.round(5 + size * 8);
  for (let i = 0; i < n; i++) {
    const a = rand(TAU), sp = rand(0.2, 1.4) * size, d = rand(0, 7 * size);
    addPart({ type: 'fire', x: x + Math.cos(a) * d, y: y + Math.sin(a) * d * 0.7 - 3 * size, vx: Math.cos(a) * sp, vy: Math.sin(a) * sp - 0.5 * size, r: rand(4, 9) * size, life: randi(18, 30), drag: 0.9, rise: 0.03, wait: i > n * 0.6 ? randi(1, 6) : 0 });
  }
  smokeAt(x, y - 6 * size, Math.round(3 + size * 3), 6 * size, '#3e3632', 10);
  debris(x, y, Math.round(2 + size * 4));
  spark(x, y, Math.round(4 + size * 4), '#ffe08a', 3 * size);
  addPart({ type: 'ring', x, y, r: 3, vr: 2.6 * size, life: 12 });
  addPart({ type: 'flash', x, y, r: 46 * size, life: 7 });
  shake(2 + size * 3.5);
  SFX.explode(size, pan(x));
  if (size >= 1.6) S.whiteFlash = Math.max(S.whiteFlash, 0.25);
}

function updateParts() {
  for (const p of S.parts) {
    if (p.wait > 0) { p.wait--; continue; }
    if (--p.life <= 0) { p.dead = true; continue; }
    p.x += p.vx || 0;
    p.y += p.vy || 0;
    if (p.grav) p.vy += p.grav;
    if (p.drag) { p.vx *= p.drag; p.vy *= p.drag; }
    if (p.rise) p.vy -= p.rise;
    if (p.type === 'smoke') p.r += p.grow;
    else if (p.type === 'ring') p.r += p.vr;
    else if (p.type === 'debris' || p.type === 'shell') {
      const gy = groundY(p.x);
      if (p.y > gy) {
        p.y = gy; p.vy *= -0.35; p.vx *= 0.6;
        if (Math.abs(p.vy) < 0.6) { p.vy = 0; p.grav = 0; }
      }
      p.rot += p.vr * (p.grav ? 1 : 0);
    }
  }
  sweep(S.parts);
  if (S.parts.length > 900) S.parts.splice(0, S.parts.length - 900);
}

function drawParts() {
  for (const p of S.parts) {
    if (p.wait > 0) continue;
    const k = 1 - p.life / p.max;
    switch (p.type) {
      case 'fire': {
        const r = p.r * (k < 0.2 ? 0.5 + k * 2.5 : 1 - (k - 0.2) * 0.55);
        circ(p.x, p.y, r, fireColor(k));
        if (k < 0.4 && (p.life & 1)) light(p.x, p.y, r * 3.2, 'fire', 0.5);
        break;
      }
      case 'smoke':
        X.globalAlpha = (1 - k) * (p.a || 0.55);
        circ(p.x, p.y, p.r, p.col || '#4a4541');
        X.globalAlpha = 1;
        break;
      case 'spark':
        line(p.x, p.y, p.x - p.vx * 1.6, p.y - p.vy * 1.6, p.col, 1);
        break;
      case 'debris':
        X.save(); X.translate(p.x, p.y); X.rotate(p.rot);
        R(-p.w / 2, -p.h / 2, p.w, p.h, p.col);
        X.restore();
        break;
      case 'shell':
        R(Math.round(p.x), Math.round(p.y) - 1, 2, 1, '#e8bc50');
        break;
      case 'dust':
        X.globalAlpha = (1 - k) * 0.55;
        circ(p.x, p.y, p.r * (0.6 + k), p.col || '#cbbb9c');
        X.globalAlpha = 1;
        break;
      case 'text':
        g.globalAlpha = k > 0.6 ? (1 - k) / 0.4 : 1;
        pxText(p.s, Math.round(p.x), Math.round(p.y), p.col, 'center');
        g.globalAlpha = 1;
        break;
      case 'ring':
        X.globalAlpha = 1 - k;
        X.strokeStyle = '#ffe9c0'; X.lineWidth = 2;
        X.beginPath(); X.arc(p.x, p.y, p.r, 0, TAU); X.stroke();
        X.globalAlpha = 1;
        break;
      case 'flash':
        light(p.x, p.y, p.r * (1 - k * 0.5), 'white', 1 - k);
        X.globalAlpha = (1 - k) * 0.9;
        circ(p.x, p.y, p.r * 0.35 * (1 - k), '#fffbe8');
        X.globalAlpha = 1;
        break;
      case 'mflash': {
        const s = p.s * (p.life === 3 ? 1 : 0.7);
        X.save(); X.translate(p.x, p.y); X.rotate(Math.atan2(p.dy, p.dx));
        poly([0, -3 * s, 9 * s, 0, 0, 3 * s, 3 * s, 0], '#ffd75a');
        poly([0, -1.5 * s, 5 * s, 0, 0, 1.5 * s], '#fffbe0');
        X.restore();
        light(p.x, p.y, 34 * s, 'yellow', 0.9);
        break;
      }
      case 'slash':
        X.save(); X.globalAlpha = 1 - k;
        X.strokeStyle = '#ffffff'; X.lineWidth = 2;
        X.beginPath();
        if (p.f > 0) X.arc(p.x - 6, p.y, 11, -1.2, 1.0); else X.arc(p.x + 6, p.y, 11, Math.PI - 1.0, Math.PI + 1.2);
        X.stroke(); X.restore();
        break;
      case 'bang': {
        const y = p.y - Math.min(4, (p.max - p.life) * 0.6);
        R(p.x - 4, y - 10, 9, 12, '#fff'); R(p.x - 3, y - 11, 7, 14, '#fff');
        R(p.x - 1, y - 8, 3, 6, '#d8342a'); R(p.x - 1, y - 1, 3, 2, '#d8342a');
        break;
      }
      case 'splash':
        R(Math.round(p.x), Math.round(p.y), 1, 1, 'rgba(180,200,255,0.7)');
        break;
    }
  }
}

/* ============================================================= physics */
function physics(e) {
  e.vy = Math.min(e.vy + GRAV, 7.5);
  e.hitWall = false;
  if (e.vx) {
    const nx = e.x + e.vx;
    const gy = groundY(nx + sign(e.vx) * (e.hw || 4));
    if (gy < e.y - 6) e.hitWall = true; else e.x = nx;
  }
  const py = e.y;
  e.y += e.vy;
  e.onGround = false;
  if (e.vy >= 0) {
    let land = groundY(e.x), plat = null;
    if (!(e.drop > 0)) {
      for (const pl of S.L.plats) {
        if (e.x >= pl.x - 3 && e.x <= pl.x + pl.w + 3 && py <= pl.y + 0.5 && e.y >= pl.y && pl.y < land) { land = pl.y; plat = pl; }
      }
    }
    if (e.y >= land) { e.y = land; e.vy = 0; e.onGround = true; e.plat = plat; } else e.plat = null;
  }
}

/* ============================================================== player */
const WEAPONS = {
  P: { gun: 'pistol', label: 'PISTOL', ammo: Infinity },
  H: { gun: 'hmg', label: 'H.M.G', ammo: 200, voice: 'Heavy machine gun!' },
  R: { gun: 'rocket', label: 'ROCKET', ammo: 30, voice: 'Rocket launcher!' },
  F: { gun: 'flame', label: 'FLAME', ammo: 30, voice: 'Flame shot!' },
  S: { gun: 'shotgun', label: 'SHOTGUN', ammo: 20, voice: 'Shotgun!' },
  L: { gun: 'laser', label: 'LASER', ammo: 360, voice: 'Laser gun!' },
};

function makePlayer(x) {
  const d = S.diff;
  return {
    x, y: groundY(x), vx: 0, vy: 0, f: 1, hw: 5, onGround: true, crouch: false, aim: 'fwd',
    weapon: 'P', ammo: Infinity, bombs: 10, cd: 0, bombCd: 0, hp: d.hp, maxHp: d.hp, inv: 120,
    dead: 0, phase: 0, t: 0, recoil: 0, knife: 0, drop: 0, run: false, laser: null, plat: null,
  };
}
function pbox(p) {
  const h = p.crouch ? 15 : 24;
  return { x: p.x - 5, y: p.y - h, w: 10, h };
}
function aimDir(p) {
  return p.aim === 'up' ? { x: 0, y: -1 } : p.aim === 'down' ? { x: 0, y: 1 } : { x: p.f, y: 0 };
}
function muzzle(p) {
  const len = MUZ[WEAPONS[p.weapon].gun];
  const dy = p.crouch ? 7 : 0;
  if (p.aim === 'up') return { x: p.x + 2 * p.f, y: p.y - 18 - len + dy };
  if (p.aim === 'down') return { x: p.x + 2 * p.f, y: p.y - 14 + len + dy };
  return { x: p.x + (3 + len) * p.f, y: p.y - 16 + dy };
}

function updatePlayer() {
  const p = S.player;
  if (!p) return;
  p.t++;
  if (p.inv > 0) p.inv--;
  if (p.dead) { updatePlayerDeath(p); return; }
  p.laser = null;
  if (S.clearT) {
    p.vx = 0; p.crouch = false; p.aim = 'fwd';
    physics(p);
    return;
  }
  const Lf = Input.is('left'), Rt = Input.is('right'), U = Input.is('up'), D = Input.is('down');
  const mv = (Rt ? 1 : 0) - (Lf ? 1 : 0);
  p.crouch = D && p.onGround;
  if (mv) p.f = mv;
  p.vx = mv * (p.crouch ? 0.7 : 1.75);
  if (Input.hit('jump') && p.onGround) {
    if (D && p.plat) { p.drop = 14; p.y += 1; p.onGround = false; }
    else { p.vy = -6.3; p.onGround = false; p.crouch = false; SFX.jump(); dust(p.x, p.y, 3); }
  }
  if (!Input.is('jump') && p.vy < -2.5) p.vy += 0.35;
  if (p.drop > 0) p.drop--;
  p.aim = U ? 'up' : !p.onGround && D ? 'down' : 'fwd';
  const air = !p.onGround, vy0 = p.vy;
  physics(p);
  if (air && p.onGround && vy0 > 3) { SFX.land(); dust(p.x, p.y, 4); }
  p.x = clamp(p.x, S.camX + 6, S.camX + W - 6);
  p.run = p.onGround && mv !== 0 && !p.crouch;
  if (p.run) p.phase += 0.075; else if (p.crouch && mv) p.phase += 0.04;

  if (p.cd > 0) p.cd--;
  if (p.bombCd > 0) p.bombCd--;
  if (p.recoil > 0) p.recoil--;
  if (p.knife > 0) p.knife--;
  if (Input.is('fire')) {
    if (p.weapon === 'L') playerLaser(p);
    else if (p.cd <= 0 || (p.weapon === 'P' && Input.hit('fire') && p.cd <= 4)) playerFire(p);
  }
  if (Input.hit('bomb') && p.bombs > 0 && p.bombCd <= 0) throwGrenade(p);
  light(p.x, p.y - 14, 60, 'white', 0.55);
}

function updatePlayerDeath(p) {
  p.dead++;
  p.vy += GRAV; p.x += p.vx; p.y += p.vy;
  const gy = groundY(p.x);
  if (p.y >= gy) { p.y = gy; p.vy = 0; p.vx *= 0.8; }
  if (p.dead === 110) {
    if (Game.lives > 0) respawn(p);
    else gameOver();
  }
}

function respawn(p) {
  Object.assign(p, {
    dead: 0, hp: p.maxHp, inv: 160, weapon: 'P', ammo: Infinity, bombs: Math.max(p.bombs, 10),
    x: S.camX + 80, y: -24, vx: 0, vy: 0, crouch: false, aim: 'fwd', laser: null,
  });
  for (const b of S.eb) if (b.x > S.camX - 20 && b.x < S.camX + W + 20) b.dead = true;
  sweep(S.eb);
}

function hurtPlayer() {
  const p = S.player;
  if (!p || p.dead || p.inv > 0 || S.clearT) return;
  p.hp--;
  S.noHit = false;
  p.inv = 80;
  shake(4);
  S.redFlash = 0.4;
  if (p.hp <= 0) {
    p.dead = 1; p.vy = -4; p.vx = -p.f * 1.2; p.crouch = false;
    Game.lives--; S.deaths++;
    SFX.pdie();
  } else SFX.hurt();
}

function pShot(m, d, sp, dmg, big) {
  S.pb.push({ x: m.x, y: m.y, vx: d.x * sp, vy: d.y * sp, dmg, big, life: 90 });
}

function playerFire(p) {
  const d = aimDir(p);
  if (p.aim !== 'down') {
    const kb = { x: p.f > 0 ? p.x : p.x - 20, y: p.y - 26, w: 20, h: 26 };
    for (const e of S.enemies) {
      if (!e.dead && e.melee && e.state !== 'chute' && overlap(kb, ebox(e))) {
        p.knife = 12; p.cd = 14;
        SFX.knife();
        addPart({ type: 'slash', x: p.x + p.f * 11, y: p.y - (p.crouch ? 10 : 17), f: p.f, life: 9 });
        damageEnemy(e, 5, { kind: 'knife', vx: p.f, x: e.x, y: e.y - 14 });
        return;
      }
    }
  }
  const m = muzzle(p);
  p.recoil = 2;
  const w = p.weapon;
  if (w === 'P') {
    pShot(m, d, 7, 1); p.cd = 9;
    SFX.pistol(pan(m.x)); flashAt(m, d, 0.8); casing(p);
  } else if (w === 'H') {
    pShot(m, rotVec(d, rand(-0.07, 0.07)), 8, 1, true); p.cd = 4;
    SFX.mg(pan(m.x)); flashAt(m, d, 1.1); casing(p);
  } else if (w === 'R') {
    S.rockets.push({ x: m.x, y: m.y, a: Math.atan2(d.y, d.x), sp: 1.2, t: 0 });
    p.cd = 22;
    SFX.rocket(pan(m.x));
    smokeAt(m.x - d.x * 16, m.y - d.y * 16, 3, 4, '#8a8580');
  } else if (w === 'F') {
    S.flames.push({ x: m.x, y: m.y, vx: d.x * 4.2, vy: d.y * 4.2, t: 0, seen: new Set() });
    p.cd = 16;
    SFX.flame(pan(m.x));
  } else if (w === 'S') {
    shotgunBlast(p, m, d); p.cd = 26;
    SFX.shotgun(pan(m.x)); shake(3);
  }
  if (w !== 'P' && --p.ammo <= 0) { p.weapon = 'P'; p.ammo = Infinity; }
}

function shotgunBlast(p, m, d) {
  let rect;
  if (d.x) rect = { x: d.x > 0 ? m.x : m.x - 84, y: m.y - 24, w: 84, h: 48 };
  else if (d.y < 0) rect = { x: m.x - 24, y: m.y - 84, w: 48, h: 84 };
  else rect = { x: m.x - 24, y: m.y, w: 48, h: 84 };
  hitArea(b => overlap(rect, b), 7, { kind: 'blast', x: m.x, y: m.y, vx: d.x });
  for (let i = 0; i < 18; i++) {
    const v = rotVec(d, rand(-0.4, 0.4)), sp = rand(3, 7);
    addPart({ type: i < 8 ? 'fire' : 'spark', x: m.x, y: m.y, vx: v.x * sp, vy: v.y * sp, r: rand(2, 4), life: randi(6, 12), drag: 0.86, col: '#ffe08a' });
  }
  flashAt(m, d, 2);
}

function playerLaser(p) {
  const m = muzzle(p), d = aimDir(p);
  let len;
  if (d.x) len = d.x > 0 ? S.camX + W - m.x : m.x - S.camX;
  else if (d.y < 0) len = m.y + 10;
  else len = Math.max(0, groundY(m.x) - m.y);
  const x2 = m.x + d.x * len, y2 = m.y + d.y * len;
  p.laser = { x1: m.x, y1: m.y, x2, y2 };
  p.recoil = 1;
  const rect = d.x
    ? { x: Math.min(m.x, x2), y: m.y - 2, w: Math.abs(x2 - m.x), h: 4 }
    : { x: m.x - 2, y: Math.min(m.y, y2), w: 4, h: Math.abs(y2 - m.y) };
  if (S.t % 4 === 0) hitArea(b => overlap(rect, b), 1, { kind: 'laser', x: m.x, y: m.y, vx: d.x });
  if (S.t % 3 === 0) spark(x2, y2, 1, '#9ff4ff', 1.5);
  SFX.laser(pan(m.x));
  if (--p.ammo <= 0) { p.weapon = 'P'; p.ammo = Infinity; p.laser = null; }
}

function throwGrenade(p) {
  p.bombs--;
  p.bombCd = 18;
  S.grenades.push({ x: p.x + p.f * 4, y: p.y - (p.crouch ? 12 : 20), vx: p.f * 2.6 + p.vx * 0.4, vy: -3.6, t: 0, bounces: 0, rot: 0 });
  SFX.throw();
}

/* ============================================================ damage */
function hitArea(test, dmg, src, seen) {
  for (const e of S.enemies) {
    if (e.dead || (seen && seen.has(e))) continue;
    if (test(ebox(e))) { if (seen) seen.add(e); damageEnemy(e, dmg, src); }
  }
  const boxes = bossBoxes();
  if (boxes.length && !(seen && seen.has(S.boss))) {
    let best = null;
    for (const bx of boxes) if (test(bx) && (!best || bx.mul > best.mul)) best = bx;
    if (best) { if (seen) seen.add(S.boss); bossDamage(dmg, best, src); }
  }
  for (const c of S.crates) if (!c.dead && test(cbox(c))) crateDamage(c, dmg);
  for (const w of S.pows) if (w.state === 'tied' && test(wbox(w))) freePow(w);
}

function hitPoint(x, y, r, dmg, src) {
  for (const e of S.enemies) {
    if (!e.dead && circleRect(x, y, r, ebox(e))) return damageEnemy(e, dmg, src) || true;
  }
  for (const bx of bossBoxes()) if (circleRect(x, y, r, bx)) return bossDamage(dmg, bx, src);
  for (const c of S.crates) if (!c.dead && circleRect(x, y, r, cbox(c))) { crateDamage(c, dmg); return true; }
  for (const w of S.pows) if (w.state === 'tied' && circleRect(x, y, r, wbox(w))) { freePow(w); return true; }
  for (const b of S.eb) {
    if (b.hp > 0 && !b.dead && Math.hypot(b.x - x, b.y - y) < (b.r || 3) + r + 3) {
      b.hp -= dmg;
      if (b.hp <= 0) { b.dead = true; explosion(b.x, b.y, 0.5); addScore(50, b.x, b.y - 8); }
      else spark(x, y, 2);
      return true;
    }
  }
  return false;
}

function blast(x, y, r, dmg, owner, size) {
  explosion(x, y, size !== undefined ? size : r / 26);
  if (owner === 'p') hitArea(b => circleRect(x, y, r, b), dmg, { kind: 'blast', x, y, vx: 0 });
  else {
    const p = S.player;
    if (p && !p.dead && circleRect(x, y, r * 0.8, pbox(p))) hurtPlayer();
  }
}

/* ============================================================ enemies */
const EDEF = {
  rifle: { hp: 2, hw: 5, h: 26, score: 100, melee: true },
  grenadier: { hp: 2, hw: 5, h: 26, score: 150, melee: true },
  bazooka: { hp: 3, hw: 5, h: 26, score: 200, melee: true },
  shield: { hp: 5, hw: 7, h: 26, score: 300, melee: true },
  para: { hp: 2, hw: 5, h: 26, score: 150, melee: true },
  turret: { hp: 14, hw: 13, h: 20, score: 500 },
  tank: { hp: 28, hw: 22, h: 26, score: 1200 },
  drone: { hp: 6, hw: 12, h: 12, score: 400 },
};

function spawnEnemy(type, x, y, o = {}) {
  const d = EDEF[type];
  const veh = type === 'tank' || type === 'turret' || type === 'drone';
  const hp = Math.max(1, Math.round(d.hp * S.diff.hpMul));
  const e = Object.assign({
    type, x, y, vx: 0, vy: 0, f: -1, hw: d.hw, h: d.h, hp, maxHp: hp, score: d.score, melee: !!d.melee,
    t: 0, flash: 0, cd: randi(40, 100), state: 'active', phase: rand(1), onGround: false,
    pal: veh ? S.theme.veh : S.theme.pal, pref: rand(70, 170), shootT: 0, throwT: 0, crouch: false,
  }, o);
  if (type === 'shield') { e.shieldUp = true; e.pref = rand(45, 70); }
  if (type === 'bazooka') e.pref = rand(150, 210);
  if (type === 'grenadier') e.pref = rand(100, 160);
  if (type === 'turret') { e.aim = Math.PI; e.burst = 0; }
  if (type === 'tank') { e.contact = true; e.tread = 0; e.aim = -0.2; }
  if (type === 'drone') { e.hy = rand(66, 104); }
  S.enemies.push(e);
  return e;
}

function ebox(e) {
  if (e.type === 'tank') return { x: e.x - 22, y: e.y - 26, w: 44, h: 26 };
  if (e.type === 'turret') return { x: e.x - 13, y: e.y - 20, w: 26, h: 20 };
  if (e.type === 'drone') return { x: e.x - 12, y: e.y - 13, w: 24, h: 13 };
  const h = e.crouch ? 19 : e.state === 'idle' && e.idlePose === 'sit' ? 21 : 26;
  return { x: e.x - e.hw, y: e.y - h, w: e.hw * 2, h };
}
const onScreen = (e, m = 0) => e.x > S.camX - m && e.x < S.camX + W + m;

function aimClamp(f, sx, sy, p, lim = 0.45, spread = 0.06) {
  const base = f > 0 ? 0 : Math.PI;
  let a = Math.atan2(p.y - 13 - sy, p.x - sx) - base;
  while (a > Math.PI) a -= TAU;
  while (a < -Math.PI) a += TAU;
  return base + clamp(a, -lim, lim) + rand(-spread, spread);
}
function eShot(x, y, a, sp, kind = 'b', extra) {
  const b = Object.assign({ kind, x, y, vx: Math.cos(a) * sp, vy: Math.sin(a) * sp, r: 2.5, t: 0 }, extra);
  S.eb.push(b);
  return b;
}
function lob(sx, sy, tx, ty, T, grav) {
  return { vx: clamp((tx - sx) / T, -3.4, 3.4), vy: (ty - sy - 0.5 * grav * T * T) / T };
}

function soldierIdle(e, p) {
  if (e.state === 'idle') {
    e.vx = 0;
    physics(e);
    if (p && Math.abs(p.x - e.x) < 200 && onScreen(e, -12)) {
      e.state = 'alert'; e.st = 0; e.vy = -2; e.onGround = false;
      addPart({ type: 'bang', x: e.x, y: e.y - 34, life: 34 });
    }
    return true;
  }
  if (e.state === 'alert') {
    e.vx = 0;
    if (p) e.f = sign(p.x - e.x);
    physics(e);
    if (++e.st > 20) e.state = 'active';
    return true;
  }
  return false;
}

function soldierMove(e, p, speed) {
  const dx = p.x - e.x, adx = Math.abs(dx);
  e.f = sign(dx);
  let mv = 0;
  if (e.shootT <= 0 && e.throwT <= 0) {
    if (adx > e.pref + 20) mv = sign(dx);
    else if (adx < e.pref - 40) mv = -sign(dx);
  }
  e.vx = mv * speed * S.diff.spd;
  if (e.hitWall && e.onGround) { e.vy = -5; e.onGround = false; }
  physics(e);
  if (mv) e.phase += 0.065;
  return mv;
}

const AI = {
  rifle(e, p) {
    if (soldierIdle(e, p)) return;
    if (!p) { e.vx = 0; physics(e); return; }
    soldierMove(e, p, 0.8);
    if (e.shootT > 0) {
      e.shootT--;
      if (e.shootT === 8) {
        const mx = e.x + e.f * 15, my = e.y - (e.crouch ? 9 : 16);
        eShot(mx, my, aimClamp(e.f, mx, my, p), 2.3 * S.diff.bspd);
        flashAt({ x: mx, y: my }, { x: e.f, y: 0 }, 0.8);
        SFX.eshot(pan(mx));
      }
      if (e.shootT === 0) e.crouch = false;
    } else if (onScreen(e, -8) && --e.cd <= 0) {
      e.shootT = 24;
      e.crouch = chance(0.35);
      e.cd = Math.round(rand(90, 170) / S.diff.fire);
    }
  },
  grenadier(e, p) {
    if (soldierIdle(e, p)) return;
    if (!p) { e.vx = 0; physics(e); return; }
    soldierMove(e, p, 0.7);
    if (e.throwT > 0) {
      if (--e.throwT === 10) {
        const sx = e.x - e.f * 2, sy = e.y - 28;
        const v = lob(sx, sy, p.x + rand(-18, 18), p.y - 4, rand(44, 56), 0.2);
        S.eb.push({ kind: 'gren', x: sx, y: sy, vx: v.vx, vy: v.vy, grav: 0.2, fuse: 95, r: 3, t: 0, rot: 0, bounces: 0 });
        SFX.throw();
      }
    } else if (onScreen(e, -8) && --e.cd <= 0) {
      e.throwT = 26;
      e.cd = Math.round(rand(120, 190) / S.diff.fire);
    }
  },
  bazooka(e, p) {
    if (soldierIdle(e, p)) return;
    if (!p) { e.vx = 0; physics(e); return; }
    soldierMove(e, p, 0.6);
    if (e.shootT > 0) {
      e.crouch = true;
      if (--e.shootT === 14) {
        const sx = e.x + e.f * 16, sy = e.y - 12;
        S.eb.push({ kind: 'erock', x: sx, y: sy, vx: e.f * 0.8, vy: 0, ax: e.f * 0.07, max: 3.3 * S.diff.bspd, hp: 1, r: 3, t: 0, blastR: 22 });
        SFX.rocket(pan(sx));
        smokeAt(e.x - e.f * 12, sy, 3, 4, '#8a8580');
      }
      if (e.shootT === 0) e.crouch = false;
    } else if (onScreen(e, -8) && --e.cd <= 0) {
      e.shootT = 36;
      e.cd = Math.round(rand(150, 220) / S.diff.fire);
    }
  },
  shield(e, p) {
    if (soldierIdle(e, p)) return;
    if (!p) { e.vx = 0; physics(e); return; }
    soldierMove(e, p, 0.5);
    if (e.shootT > 0) {
      e.shieldUp = false;
      if (--e.shootT === 18) {
        const mx = e.x + e.f * 10, my = e.y - 16;
        eShot(mx, my, aimClamp(e.f, mx, my, p), 2.4 * S.diff.bspd);
        flashAt({ x: mx, y: my }, { x: e.f, y: 0 }, 0.7);
        SFX.eshot(pan(mx));
      }
      if (e.shootT === 0) e.shieldUp = true;
    } else if (onScreen(e, -8) && --e.cd <= 0) {
      e.shootT = 40;
      e.cd = Math.round(rand(110, 170) / S.diff.fire);
    }
  },
  para(e, p) {
    if (e.state === 'chute') {
      e.vx = Math.sin(e.t * 0.05) * 0.35;
      e.x += e.vx;
      e.y += 0.7;
      if (p) e.f = sign(p.x - e.x);
      if (e.y >= surfaceY(e.x, e.y - 2)) {
        e.y = surfaceY(e.x, e.y - 2);
        e.state = 'active'; e.onGround = true;
      }
      return;
    }
    AI.rifle(e, p);
  },
  turret(e, p) {
    e.y = groundY(e.x);
    if (!p) return;
    let ta = Math.atan2(p.y - 12 - (e.y - 15), p.x - e.x);
    if (ta > 0.15 && ta < Math.PI - 0.15) ta = ta < Math.PI / 2 ? 0.15 : Math.PI - 0.15;
    let da = ta - e.aim;
    while (da > Math.PI) da -= TAU;
    while (da < -Math.PI) da += TAU;
    e.aim += clamp(da, -0.03, 0.03);
    if (!onScreen(e, -6)) return;
    if (e.burst > 0) {
      if (e.t % 8 === 0) {
        e.burst--;
        const mx = e.x + Math.cos(e.aim) * 17, my = e.y - 15 + Math.sin(e.aim) * 17;
        eShot(mx, my, e.aim + rand(-0.05, 0.05), 2.5 * S.diff.bspd);
        flashAt({ x: mx, y: my }, { x: Math.cos(e.aim), y: Math.sin(e.aim) }, 0.9);
        SFX.eshot(pan(mx));
      }
    } else if (--e.cd <= 0) {
      e.burst = 3;
      e.cd = Math.round(rand(100, 150) / S.diff.fire);
    }
  },
  tank(e, p) {
    if (!p) { e.vx = 0; physics(e); return; }
    const dx = p.x - e.x;
    e.f = sign(dx);
    let mv = 0;
    if (Math.abs(dx) > 160) mv = sign(dx); else if (Math.abs(dx) < 100) mv = -sign(dx);
    e.vx = mv * 0.55 * S.diff.spd;
    physics(e);
    e.tread += Math.abs(e.x - (e.px || e.x)) * 2;
    e.px = e.x;
    e.aim = approach(e.aim, -0.2 - (e.recoil || 0) * 0.02, 0.02);
    if (e.recoil > 0) e.recoil--;
    if (e.t % 10 === 0 && e.hp < e.maxHp * 0.5) smokeAt(e.x, e.y - 20, 1, 3, '#2a2624');
    if (onScreen(e, -10) && --e.cd <= 0) {
      const sx = e.x + e.f * 24, sy = e.y - 24;
      const v = lob(sx, sy, p.x + rand(-10, 10), p.y, 56, 0.2);
      S.eb.push({ kind: 'shell', x: sx, y: sy, vx: v.vx, vy: v.vy, grav: 0.2, r: 3, t: 0, blastR: 28 });
      SFX.cannon(pan(sx));
      smokeAt(sx, sy, 3, 4, '#6a6560');
      flashAt({ x: sx, y: sy }, { x: e.f, y: -0.3 }, 1.4);
      e.recoil = 8;
      e.cd = Math.round(rand(120, 180) / S.diff.fire);
    }
  },
  drone(e, p) {
    const tx = p ? p.x + Math.sin(e.t * 0.02 + e.phase * 6) * 60 : e.x - 1;
    e.vx = approach(e.vx, clamp((tx - e.x) * 0.02, -1.6, 1.6), 0.05);
    e.x += e.vx;
    e.y += (e.hy + Math.sin(e.t * 0.05) * 6 - e.y) * 0.04;
    e.f = p ? sign(p.x - e.x) : -1;
    if (e.cd > 0) e.cd--;
    if (p && Math.abs(p.x - e.x) < 26 && e.cd <= 0 && onScreen(e, -6)) {
      S.eb.push({ kind: 'bomb', x: e.x, y: e.y + 1, vx: e.vx * 0.5, vy: 0.4, grav: 0.16, r: 3, t: 0, blastR: 22 });
      e.cd = Math.round(rand(60, 100) / S.diff.fire);
    }
  },
};

function updateEnemies() {
  const p = S.player && !S.player.dead ? S.player : null;
  for (const e of S.enemies) {
    if (e.dead) continue;
    e.t++;
    if (e.flash > 0) e.flash--;
    AI[e.type](e, p);
    if (!e.wave && e.x < S.camX - 90) e.dead = true;
    if (e.wave) e.x = Math.max(e.x, S.camX - 40);
    if (e.y > H + 60) e.dead = true;
    if (p && e.contact && overlap(ebox(e), pbox(p))) hurtPlayer();
  }
  sweep(S.enemies);
}

function damageEnemy(e, dmg, src) {
  if (e.dead) return false;
  if (e.type === 'shield' && e.shieldUp && src.kind === 'bullet' && sign(src.vx) === -e.f && Math.abs(src.vx) > Math.abs(src.vy || 0)) {
    SFX.clink(pan(e.x));
    spark(e.x + e.f * 8, src.y !== undefined ? src.y : e.y - 14, 4, '#e8eef4');
    return 'block';
  }
  e.hp -= dmg;
  e.flash = 4;
  if (e.state === 'idle' || e.state === 'alert') e.state = 'active';
  if (e.hp <= 0) killEnemy(e, src); else SFX.hit(pan(e.x));
  return true;
}

function killEnemy(e, src) {
  e.dead = true;
  S.kills++;
  addScore(e.score, e.x, e.y - (e.type === 'drone' ? 20 : 32));
  if (e.type === 'tank') {
    explosion(e.x, e.y - 12, 1.7);
    debris(e.x, e.y - 14, 10, ['#3a3d33', '#5a5e50', e.pal.hull]);
    dropItem(pick(['H', 'R', 'F', 'S', 'L', 'B']), e.x, e.y - 20);
    return;
  }
  if (e.type === 'turret') {
    explosion(e.x, e.y - 12, 1.2);
    debris(e.x, e.y - 12, 8, ['#50565e', e.pal.bunker, e.pal.bunkerD]);
  } else if (e.type === 'drone') {
    explosion(e.x, e.y - 6, 1);
    debris(e.x, e.y - 6, 6, [e.pal.hull, e.pal.hullD, '#2b4a63']);
  } else {
    const mode = src.kind === 'blast' ? 'fly' : src.kind === 'flame' || src.kind === 'laser' ? 'burn' : 'fall';
    const dir = src.vx ? sign(src.vx) : sign(e.x - (S.player ? S.player.x : e.x - 1));
    S.corpses.push({
      x: e.x, y: e.y, y0: e.y, f: -dir, pal: mode === 'burn' ? PAL.burnt : e.pal, mode, t: 0,
      vx: dir * (mode === 'fly' ? 2 : 0.8), vy: mode === 'fly' ? -4.5 : mode === 'fall' ? -1.4 : 0, rot: 0,
      shield: e.type === 'shield',
    });
    if (chance(0.75)) SFX.scream(pan(e.x));
  }
  if (chance(S.diff.drop)) dropItem(pick(['B', 'food', 'gem', 'H', 'R', 'F', 'S', 'L']), e.x, e.y - 14, true);
}

function updateCorpses() {
  for (const c of S.corpses) {
    c.t++;
    if (c.mode === 'burn' && c.t < 26) {
      if (c.t % 3 === 0) smokeAt(c.x, c.y - 16, 1, 3, '#2a2522');
      if (c.t % 4 === 0) addPart({ type: 'fire', x: c.x + rand(-4, 4), y: c.y - rand(4, 22), vx: 0, vy: -0.6, r: rand(2, 4), life: 14 });
      continue;
    }
    c.vy += GRAV; c.x += c.vx; c.y += c.vy;
    const floor = surfaceY(c.x, c.y0 - 2);
    if (c.y >= floor) { c.y = floor; c.vy = 0; c.vx *= 0.7; c.rot = approach(c.rot, -Math.PI / 2, 0.2); }
    else c.rot -= c.mode === 'fly' ? 0.22 : 0.1;
    if (c.rot < -Math.PI / 2 && c.vy === 0) c.rot = -Math.PI / 2;
    if (c.t > 90) c.dead = true;
  }
  sweep(S.corpses);
}

function drawCorpse(c) {
  if (c.t > 62 && (c.t >> 2) & 1) return;
  const burning = c.mode === 'burn' && c.t < 26;
  const lying = Math.min(1, Math.abs(c.rot) / (Math.PI / 2));
  drawSoldier({
    x: c.x, y: c.y - 4 * lying, f: c.f, pal: c.pal, pose: burning ? 'stand' : 'air', rot: c.rot,
    gear: 'helmet', weapon: null, arms: burning ? 'flail' : 'idle', t: c.t, shadow: false,
  });
}

function drawEnemy(e) {
  const fn = ENEMY_DRAW[e.type];
  if (e.flash > 0) drawFlashed(() => fn(e), e.x - 40, e.y - 64, 80, 70);
  else fn(e);
}

function soldierPose(e) {
  if (e.state === 'idle' && e.idlePose === 'sit') return 'sit';
  if (e.crouch) return 'kneel';
  if (!e.onGround && e.state !== 'chute') return 'air';
  return Math.abs(e.vx) > 0.05 ? 'run' : 'stand';
}

const ENEMY_DRAW = {
  rifle(e) {
    drawSoldier({
      x: e.x, y: e.y, f: e.f, pal: e.pal, pose: soldierPose(e), phase: e.phase, aim: 'fwd', weapon: 'rifle',
      gear: 'helmet', t: e.t, arms: e.state === 'idle' || e.state === 'chute' ? 'idle' : null, chute: e.state === 'chute',
      recoil: e.shootT > 4 && e.shootT < 8 ? 1 : 0,
    });
  },
  para(e) { ENEMY_DRAW.rifle(e); },
  grenadier(e) {
    drawSoldier({
      x: e.x, y: e.y, f: e.f, pal: e.pal, pose: soldierPose(e), phase: e.phase, gear: 'helmet', t: e.t, weapon: null,
      arms: e.throwT > 10 ? 'throw' : e.state === 'idle' ? 'idle' : 'grenade',
    });
  },
  bazooka(e) {
    drawSoldier({
      x: e.x, y: e.y, f: e.f, pal: e.pal, pose: soldierPose(e), phase: e.phase, gear: 'helmet', t: e.t,
      weapon: 'bazooka', arms: e.state === 'idle' ? 'idle' : null,
    });
  },
  shield(e) {
    drawSoldier({
      x: e.x, y: e.y, f: e.f, pal: e.pal, pose: soldierPose(e), phase: e.phase, aim: 'fwd', gear: 'helmet', t: e.t,
      weapon: e.shieldUp ? null : 'pistol', arms: e.shieldUp ? 'idle' : null, shield: e.shieldUp,
    });
    if (!e.shieldUp) {
      X.save(); X.translate(Math.round(e.x), Math.round(e.y)); if (e.f < 0) X.scale(-1, 1);
      R(-12, -26, 5, 24, '#79838e'); R(-12, -26, 1, 24, '#b3bcc6');
      X.restore();
    }
  },
  turret(e) { drawTurret(e); },
  tank(e) { drawMiniTank(e); },
  drone(e) { drawDrone(e); },
};

/* ======================================================== projectiles */
function updatePlayerShots() {
  for (const b of S.pb) {
    b.x += b.vx; b.y += b.vy;
    if (--b.life <= 0 || b.x < S.camX - 12 || b.x > S.camX + W + 12 || b.y < -12 || b.y > H + 12) { b.dead = true; continue; }
    if (b.y >= groundY(b.x)) { b.dead = true; spark(b.x, b.y, 2); dust(b.x, b.y, 1); continue; }
    const r = hitPoint(b.x, b.y, 2, b.dmg, { kind: 'bullet', vx: b.vx, vy: b.vy, x: b.x, y: b.y });
    if (r) { b.dead = true; if (r !== 'block') spark(b.x, b.y, 2, '#fff2b0', 1.6); }
  }
  sweep(S.pb);

  for (const r of S.rockets) {
    r.t++;
    r.sp = Math.min(6.5, r.sp + 0.22);
    if (r.t > 5 && r.t % 2 === 0) {
      let best = null, bd = 230;
      const cands = S.enemies.filter(e => !e.dead).map(e => { const b = ebox(e); return { x: b.x + b.w / 2, y: b.y + b.h / 2 }; });
      for (const bx of bossBoxes()) if (bx.mul > 0) cands.push({ x: bx.x + bx.w / 2, y: bx.y + bx.h / 2 });
      for (const c of cands) {
        const d = Math.hypot(c.x - r.x, c.y - r.y);
        const ang = Math.atan2(c.y - r.y, c.x - r.x);
        let da = ang - r.a;
        while (da > Math.PI) da -= TAU;
        while (da < -Math.PI) da += TAU;
        if (d < bd && Math.abs(da) < 1.2) { bd = d; best = da; }
      }
      if (best !== null) r.a += clamp(best, -0.09, 0.09);
    }
    r.x += Math.cos(r.a) * r.sp;
    r.y += Math.sin(r.a) * r.sp;
    if (r.t % 2 === 0) smokeAt(r.x - Math.cos(r.a) * 6, r.y - Math.sin(r.a) * 6, 1, 2.5, '#9a948e');
    const hit = hitPoint(r.x, r.y, 4, 5, { kind: 'blast', vx: Math.cos(r.a), x: r.x, y: r.y });
    if (hit || r.y >= groundY(r.x) || r.x < S.camX - 20 || r.x > S.camX + W + 20 || r.y < -30) {
      r.dead = true;
      if (r.x > S.camX - 10 && r.x < S.camX + W + 10) blast(r.x, r.y, 26, 4, 'p', 0.9);
    }
  }
  sweep(S.rockets);

  for (const f of S.flames) {
    f.t++;
    f.x += f.vx; f.y += f.vy;
    const gy = groundY(f.x);
    if (f.y > gy - 6) { f.y = gy - 6; f.vy = 0; if (!f.vx) f.vx = rand(-1, 1); }
    const r = 7 + Math.min(6, f.t * 0.3);
    if (f.t % 2 === 0) addPart({ type: 'fire', x: f.x + rand(-r, r) * 0.5, y: f.y + rand(-r, r) * 0.5, vx: f.vx * 0.2, vy: -0.4, r: r * 0.6, life: 14 });
    light(f.x, f.y, r * 5, 'fire', 0.8);
    hitArea(b => circleRect(f.x, f.y, r, b), 4, { kind: 'flame', vx: f.vx, x: f.x, y: f.y }, f.seen);
    if (f.t > 38 || f.x < S.camX - 20 || f.x > S.camX + W + 20 || f.y < -20) f.dead = true;
  }
  sweep(S.flames);

  for (const gr of S.grenades) {
    gr.t++;
    gr.vy += 0.22; gr.x += gr.vx; gr.y += gr.vy;
    gr.rot += gr.vx * 0.3;
    const gy = groundY(gr.x);
    let boom = gr.t > 120;
    if (gr.y >= gy) {
      gr.y = gy;
      if (gr.bounces++ >= 1) boom = true;
      else { gr.vy = -gr.vy * 0.45; gr.vx *= 0.6; SFX.land(); }
    }
    if (!boom) {
      for (const e of S.enemies) if (!e.dead && circleRect(gr.x, gr.y, 3, ebox(e))) { boom = true; break; }
      if (!boom) for (const bx of bossBoxes()) if (circleRect(gr.x, gr.y, 3, bx)) { boom = true; break; }
    }
    if (boom) { gr.dead = true; blast(gr.x, gr.y - 4, 32, 8, 'p', 1.1); }
  }
  sweep(S.grenades);
}

function updateEnemyShots() {
  const p = S.player && !S.player.dead ? S.player : null;
  for (const b of S.eb) {
    if (b.dead) continue;
    b.t++;
    switch (b.kind) {
      case 'b': case 'orb':
        b.x += b.vx; b.y += b.vy;
        if (b.y >= groundY(b.x)) { b.dead = true; dust(b.x, b.y, 1); }
        break;
      case 'gren':
        b.vy += b.grav; b.x += b.vx; b.y += b.vy; b.rot += b.vx * 0.3;
        if (b.y >= groundY(b.x)) {
          b.y = groundY(b.x);
          if (b.bounces++ < 1) { b.vy = -b.vy * 0.4; b.vx *= 0.5; } else { b.vx = 0; b.vy = 0; }
        }
        if (--b.fuse <= 0) { b.dead = true; blast(b.x, b.y - 3, 26, 0, 'e', 0.9); }
        break;
      case 'erock':
        b.vx = clamp(b.vx + b.ax, -b.max, b.max);
        b.x += b.vx;
        if (b.t % 3 === 0) smokeAt(b.x - sign(b.vx) * 5, b.y, 1, 2, '#8a8580');
        break;
      case 'shell': case 'bomb':
        b.vy += b.grav; b.x += b.vx; b.y += b.vy;
        if (b.y >= groundY(b.x)) { b.dead = true; blast(b.x, groundY(b.x) - 3, b.blastR, 0, 'e', b.kind === 'shell' ? 1.1 : 0.9); }
        break;
      case 'missile': {
        if (p && b.t > (b.hold || 0)) {
          const ta = Math.atan2(p.y - 12 - b.y, p.x - b.x);
          let da = ta - b.a;
          while (da > Math.PI) da -= TAU;
          while (da < -Math.PI) da += TAU;
          b.a += clamp(da, -b.turn, b.turn);
        }
        b.x += Math.cos(b.a) * b.sp; b.y += Math.sin(b.a) * b.sp;
        if (b.t % 2 === 0) smokeAt(b.x - Math.cos(b.a) * 5, b.y - Math.sin(b.a) * 5, 1, 2.5, '#8a8580');
        if (b.t > b.life || b.y >= groundY(b.x)) { b.dead = true; blast(b.x, b.y, 22, 0, 'e', 0.8); }
        break;
      }
      case 'mdrop':
        if (b.phase === 0) {
          b.y -= 5;
          if (b.t % 2 === 0) smokeAt(b.x, b.y + 6, 1, 2.5, '#8a8580');
          if (b.y < -30) { b.phase = 1; b.t = 0; b.x = b.tx; }
        } else if (b.phase === 1) {
          if (b.t > b.delay) { b.phase = 2; b.y = -20; }
        } else {
          b.y += 5.5;
          if (b.y >= groundY(b.x)) { b.dead = true; blast(b.x, groundY(b.x) - 3, 26, 0, 'e', 1); }
        }
        break;
      case 'wave':
        b.x += b.vx;
        b.y = groundY(b.x);
        if (b.t % 3 === 0) dust(b.x, b.y, 2);
        break;
    }
    if (b.dead) continue;
    if (p && b.kind !== 'mdrop' || (p && b.kind === 'mdrop' && b.phase === 2)) {
      const hit = b.kind === 'wave'
        ? overlap({ x: b.x - 6, y: b.y - 14, w: 12, h: 14 }, pbox(p))
        : circleRect(b.x, b.y, b.r || 2.5, pbox(p));
      if (hit) {
        if (b.kind === 'wave') hurtPlayer();
        else {
          b.dead = true;
          if (b.blastR) blast(b.x, b.y, b.blastR, 0, 'e', 0.8); else { hurtPlayer(); spark(b.x, b.y, 3, '#ff8a5a'); }
        }
      }
    }
    if (b.kind !== 'mdrop' && (b.x < S.camX - 60 || b.x > S.camX + W + 60 || b.y > H + 20 || b.y < -220)) b.dead = true;
  }
  sweep(S.eb);
}

function drawShots() {
  for (const b of S.pb) {
    if (b.big) {
      const a = Math.atan2(b.vy, b.vx);
      X.save(); X.translate(b.x, b.y); X.rotate(a);
      R(-7, -1, 8, 2, '#ff9a2e'); R(-2, -1, 4, 2, '#fff2b0');
      X.restore();
      light(b.x, b.y, 14, 'fire', 0.7);
    } else {
      const a = Math.atan2(b.vy, b.vx);
      X.save(); X.translate(b.x, b.y); X.rotate(a);
      R(-4, -1, 5, 2, '#ffd35a'); R(-1, -1, 2, 2, '#ffffff');
      X.restore();
      light(b.x, b.y, 10, 'yellow', 0.6);
    }
  }
  for (const r of S.rockets) {
    X.save(); X.translate(r.x, r.y); X.rotate(r.a);
    R(-6, -2, 9, 4, '#5a6a3a'); R(-6, -2, 9, 1, '#8a9a5a'); R(3, -1, 3, 2, '#c0392b'); R(-8, -3, 2, 6, '#3a4424');
    poly([-8, -2, -14 - rand(3), 0, -8, 2], '#ffb040');
    X.restore();
    light(r.x, r.y, 26, 'fire', 0.8);
  }
  for (const gr of S.grenades) {
    X.save(); X.translate(gr.x, gr.y - 2); X.rotate(gr.rot);
    oval(0, 0, 3, 2.3, '#3d4a2a'); R(-1, -3, 2, 1, '#999');
    X.restore();
  }
  for (const f of S.flames) {
    const r = 7 + Math.min(6, f.t * 0.3);
    circ(f.x, f.y, r, '#e2531f'); circ(f.x, f.y, r * 0.7, '#ff9a2e'); circ(f.x + sign(f.vx || 1) * 2, f.y, r * 0.4, '#fff1b0');
  }
  const p = S.player;
  if (p && p.laser) {
    const l = p.laser, w = 3 + Math.sin(S.t * 0.8);
    drawBeam(l.x1, l.y1, l.x2, l.y2, w, '#ffffff', '#35c8ff');
    light(l.x1, l.y1, 30, 'cyan', 1);
    light((l.x1 + l.x2) / 2, (l.y1 + l.y2) / 2, 60, 'cyan', 0.5);
  }
  for (const b of S.eb) {
    switch (b.kind) {
      case 'b':
        circ(b.x, b.y, 2.6, '#ff6a3a'); circ(b.x, b.y, 1.3, '#fff0c0');
        light(b.x, b.y, 14, 'red', 0.8);
        break;
      case 'orb':
        circ(b.x, b.y, b.r + 1, b.col || '#ff3a6a'); circ(b.x, b.y, b.r * 0.5, '#fff');
        light(b.x, b.y, 22, b.glow || 'red', 0.9);
        break;
      case 'gren':
        X.save(); X.translate(b.x, b.y - 2); X.rotate(b.rot);
        oval(0, 0, 3, 2.3, '#44532c'); R(-1, -3, 2, 1, '#999');
        X.restore();
        if (b.fuse < 30 && (b.fuse >> 2) & 1) circ(b.x, b.y - 2, 1.2, '#ff4a3a');
        break;
      case 'erock': {
        X.save(); X.translate(b.x, b.y); if (b.vx < 0) X.scale(-1, 1);
        R(-6, -2, 10, 4, '#6b6f5a'); R(4, -1, 3, 2, '#c0392b'); poly([-6, -2, -11 - rand(3), 0, -6, 2], '#ffb040');
        X.restore();
        light(b.x, b.y, 20, 'fire', 0.7);
        break;
      }
      case 'shell': case 'bomb':
        oval(b.x, b.y, 3.5, 2.5, '#2e3128', Math.atan2(b.vy, b.vx)); circ(b.x - 1, b.y - 1, 1, '#6a6e60');
        break;
      case 'missile': {
        X.save(); X.translate(b.x, b.y); X.rotate(b.a);
        R(-6, -2, 11, 4, '#d8dde2'); R(-6, -2, 11, 1, '#ffffff'); R(5, -1, 2, 2, '#c0392b'); R(-7, -3, 2, 6, '#5a5f66');
        poly([-7, -2, -12 - rand(3), 0, -7, 2], '#ffb040');
        X.restore();
        light(b.x, b.y, 22, 'fire', 0.7);
        break;
      }
      case 'mdrop':
        if (b.phase === 1 || b.phase === 2) {
          const gy = groundY(b.tx);
          const blink = (S.t >> 2) & 1;
          X.globalAlpha = 0.5 + blink * 0.4;
          oval(b.tx, gy - 1, 12, 3, '#ff3a2a');
          X.globalAlpha = 1;
          R(b.tx - 1, gy - 12, 2, 6, '#ff3a2a'); R(b.tx - 1, gy - 5, 2, 2, '#ff3a2a');
        }
        if (b.phase !== 1) {
          X.save(); X.translate(b.x, b.y); X.rotate(b.phase === 0 ? -Math.PI / 2 : Math.PI / 2);
          R(-6, -2, 12, 4, '#b8975c'); R(6, -1, 2, 2, '#c0392b'); poly([-6, -2, -11 - rand(3), 0, -6, 2], '#ffb040');
          X.restore();
        }
        break;
      case 'wave': {
        X.globalAlpha = 0.85;
        poly([b.x - 7, b.y, b.x - 2, b.y - 14, b.x + 3, b.y - 6, b.x + 7, b.y], '#ffd35a');
        poly([b.x - 4, b.y, b.x - 1, b.y - 9, b.x + 3, b.y], '#fff6d0');
        X.globalAlpha = 1;
        light(b.x, b.y - 6, 26, 'fire', 0.8);
        break;
      }
    }
  }
}

/* ===================================================== prisoners & loot */
function wbox(w) { return { x: w.x - 7, y: w.y - 22, w: 14, h: 22 }; }
function cbox(c) { return { x: c.x - 9, y: c.y - 16, w: 18, h: 16 }; }

function freePow(w) {
  if (w.state !== 'tied') return;
  w.state = 'free'; w.t = 0;
  S.rescued++;
  addScore(500, w.x, w.y - 30, '#9ff4a0');
}

function updatePows() {
  const p = S.player && !S.player.dead ? S.player : null;
  for (const w of S.pows) {
    w.t++;
    if (w.state === 'tied') {
      if (p && Math.abs(p.x - w.x) < 12 && Math.abs(p.y - w.y) < 20) freePow(w);
    } else if (w.state === 'free') {
      if (w.t > 20) { w.state = 'salute'; w.t = 0; SFX.rescue(); }
    } else if (w.state === 'salute') {
      if (w.t === 24) dropItem(w.item, w.x + 10, w.y - 18);
      if (w.t > 52) { w.state = 'run'; w.t = 0; }
    } else {
      w.x -= 2.6; w.phase += 0.12;
      w.y = groundY(w.x);
      if (w.x < S.camX - 30) w.dead = true;
    }
  }
  sweep(S.pows);
}

function drawPow(w) {
  const tied = w.state === 'tied';
  drawSoldier({
    x: w.x + (tied && (w.t % 40) < 6 ? (w.t & 2 ? 1 : -1) : 0), y: w.y, f: w.state === 'run' ? -1 : 1, pal: PAL.pow,
    pose: tied ? 'sit' : w.state === 'run' ? 'run' : 'stand', phase: w.phase || 0, gear: 'pow', weapon: null, t: w.t,
    arms: tied ? 'tied' : w.state === 'salute' ? 'salute' : w.state === 'run' ? 'flail' : 'idle',
  });
  if (tied) {
    R(Math.round(w.x) - 12, Math.round(w.y) - 22, 2, 22, '#6b4a26');
    if ((S.t >> 5) % 2 === 0) pxText('HELP!', Math.round(w.x), Math.round(w.y) - 40, '#fff6c8', 'center');
  }
}

function dropItem(kind, x, y, timed) {
  S.items.push({ kind, x, y, vy: -3, vx: 0, t: 0, timed: !!timed });
}

function applyItem(k) {
  const p = S.player;
  if (WEAPONS[k] && k !== 'P') {
    if (p.weapon === k) p.ammo += WEAPONS[k].ammo;
    else { p.weapon = k; p.ammo = WEAPONS[k].ammo; }
    SFX.weapon(WEAPONS[k].voice);
    popText(p.x, p.y - 40, WEAPONS[k].label, '#ffd35a');
  } else if (k === 'B') {
    p.bombs += 10;
    SFX.pickup();
    popText(p.x, p.y - 40, 'BOMB+10', '#ffd35a');
  } else if (k === 'food') {
    p.hp = Math.min(p.maxHp, p.hp + 1);
    SFX.heal();
    addScore(300, p.x, p.y - 40, '#9ff4a0');
  } else if (k === 'gem') {
    SFX.gem();
    addScore(2000, p.x, p.y - 40, '#9fe8ff');
  } else if (k === 'medal') {
    SFX.gem();
    addScore(5000, p.x, p.y - 40, '#ffd35a');
  }
}

function updateItems() {
  const p = S.player && !S.player.dead ? S.player : null;
  for (const it of S.items) {
    it.t++;
    if (!it.rest) {
      it.vy += 0.2;
      const py = it.y;
      it.y += it.vy;
      const gy = surfaceY(it.x, py - 1);
      if (it.vy > 0 && it.y >= gy) { it.y = gy; it.rest = true; }
    }
    if (p && overlap({ x: it.x - 7, y: it.y - 14, w: 14, h: 14 }, pbox(p))) { applyItem(it.kind); it.dead = true; }
    if ((it.timed && it.t > 900) || it.x < S.camX - 40) it.dead = true;
  }
  sweep(S.items);
}

function crateDamage(c, dmg) {
  c.hp -= dmg;
  spark(c.x, c.y - 8, 2, '#e8c080');
  if (c.hp <= 0 && !c.dead) {
    c.dead = true;
    SFX.crate(pan(c.x));
    debris(c.x, c.y - 8, 8, ['#9c6b36', '#c38b4c', '#5e3d1c', '#8a8f96']);
    dropItem(c.item, c.x, c.y - 8);
    addScore(100, c.x, c.y - 24);
  }
}
