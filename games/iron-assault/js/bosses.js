'use strict';
/* Iron Assault — the five mission bosses. Each boss has init / update /
   draw / boxes / bounds, plus optional hurt and dieUpdate hooks. */

const A = () => S.L.arenaX;
const bspd = () => S.diff.bspd;
const bfire = () => S.diff.fire;

function makeBoss(kind) {
  const def = BOSS[kind];
  const mul = S.diff.bossMul * (S.rush ? 0.8 : 1);
  const b = { kind, t: 0, hp: Math.round(def.hp * mul), flash: 0, active: false, dying: 0, st: 0, state: 'enter', cd: 60, f: -1, mul, recoil: 0 };
  b.maxHp = b.hp;
  def.init(b);
  return b;
}

function bossBoxes() {
  const b = S.boss;
  if (!b || b.dying || !b.active) return [];
  return BOSS[b.kind].boxes(b);
}

function bossDamage(dmg, box, src) {
  const b = S.boss;
  if (!b || b.dying) return false;
  if (!box.mul) {
    SFX.clink(pan(src.x || b.x));
    spark(src.x !== undefined ? src.x : b.x, src.y !== undefined ? src.y : b.y - 30, 3, '#e8eef4');
    return 'block';
  }
  const def = BOSS[b.kind];
  if (def.hurt) def.hurt(b, dmg * box.mul, box); else b.hp -= dmg * box.mul;
  b.flash = 3;
  SFX.hit(pan(b.x));
  if (b.hp <= 0) bossDie(b);
  return true;
}

function bossDie(b) {
  if (b.dying) return;
  b.dying = 1;
  b.hp = 0;
  SFX.loopStopAll();
  stopMusic();
  addScore(BOSS[b.kind].score, S.camX + W / 2, 70, '#ffd35a');
  for (const x of S.eb) x.dead = true;
  sweep(S.eb);
  for (const e of S.enemies) if (!e.dead) killEnemy(e, { kind: 'blast', vx: -1 });
  S.slow = 50;
}

function updateBoss() {
  const b = S.boss;
  if (!b) return;
  b.t++;
  if (b.flash > 0) b.flash--;
  const def = BOSS[b.kind];
  if (b.dying) {
    b.dying++;
    if (def.dieUpdate) def.dieUpdate(b);
    const bb = def.bounds(b);
    if (b.dying % 6 === 0) explosion(bb.x + rand(bb.w), bb.y + rand(bb.h), rand(0.7, 1.4));
    if (b.dying >= 140) {
      explosion(bb.x + bb.w / 2, bb.y + bb.h / 2, 2.6);
      debris(bb.x + bb.w / 2, bb.y + bb.h / 2, 30);
      S.whiteFlash = 0.9;
      shake(12);
      S.boss = null;
      onBossDefeated();
    }
    return;
  }
  def.update(b, S.player && !S.player.dead ? S.player : null);
}

function drawBoss() {
  const b = S.boss;
  if (!b) return;
  const def = BOSS[b.kind];
  const hit = b.flash === 3, blink = b.dying && (b.dying >> 2) & 1;
  if (hit || blink) {
    const bb = def.bounds(b);
    drawFlashed(() => def.draw(b), bb.x - 40, bb.y - 40, bb.w + 80, bb.h + 60, blink ? 0.7 : 0.38);
  } else def.draw(b);
}

function mbox(b, lx0, lx1, y0, y1, extra) {
  const a = b.x + lx0 * b.f, c = b.x + lx1 * b.f;
  return Object.assign({ x: Math.min(a, c), y: b.y + y0, w: Math.abs(c - a), h: y1 - y0 }, extra);
}
function segDist(px, py, x1, y1, x2, y2) {
  const dx = x2 - x1, dy = y2 - y1;
  const t = clamp(((px - x1) * dx + (py - y1) * dy) / (dx * dx + dy * dy || 1), 0, 1);
  return Math.hypot(px - (x1 + dx * t), py - (y1 + dy * t));
}

const BOSS = {
  /* ------------------------------------------------ 1. heavy tank */
  tank: {
    hp: 180, score: 20000,
    init(b) {
      b.x = A() + W + 90; b.y = GROUND; b.home = A() + W - 95;
      b.tread = 0; b.aim = Math.PI + 0.25; b.hatch = 0; b.f = -1;
    },
    bounds(b) { return { x: b.x - 60, y: b.y - 62, w: 120, h: 62 }; },
    boxes(b) {
      return [
        { x: b.x - 26, y: b.y - 60, w: 56, h: 18, mul: 1.25 },
        { x: b.x - 58, y: b.y - 42, w: 116, h: 42, mul: 1 },
      ];
    },
    tip(b) { return { x: b.x - 22 + Math.cos(b.aim) * 48, y: b.y - 49 + Math.sin(b.aim) * 48 }; },
    update(b, p) {
      const ph2 = b.hp < b.maxHp * 0.5;
      const px = b.x;
      if (b.state === 'enter') {
        b.x -= 1.1;
        if (b.t % 14 === 0) smokeAt(b.x + 56, b.y - 46, 1, 4, '#3a3634');
        if (b.x <= b.home) { b.state = 'think'; b.cd = 40; b.active = true; }
      } else if (b.state === 'think') {
        if (b.tx === undefined || Math.abs(b.x - b.tx) < 2) b.tx = A() + W * 0.58 + rand(0, W * 0.26);
        b.x += sign(b.tx - b.x) * (ph2 ? 0.9 : 0.6);
        b.aim = approach(b.aim, Math.PI + 0.25, 0.01);
        if (--b.cd <= 0) {
          const r = Math.random();
          b.state = r < 0.42 ? 'cannon' : r < 0.8 ? 'mg' : 'deploy';
          b.st = 0;
        }
      } else if (b.state === 'cannon') {
        b.st++;
        const shots = ph2 ? [34, 52, 70] : [34];
        if (p) b.aim = approach(b.aim, Math.PI + clamp(0.15 + (b.x - p.x) / 900, 0.12, 0.5), 0.02);
        if (shots.includes(b.st) && p) {
          const t = this.tip(b);
          const off = ph2 ? (shots.indexOf(b.st) - 1) * 42 : 0;
          const v = lob(t.x, t.y, p.x + off, p.y, 60, 0.2);
          S.eb.push({ kind: 'shell', x: t.x, y: t.y, vx: v.vx, vy: v.vy, grav: 0.2, r: 4, t: 0, blastR: 32 });
          SFX.cannon(pan(t.x));
          flashAt(t, { x: Math.cos(b.aim), y: Math.sin(b.aim) }, 2);
          smokeAt(t.x, t.y, 4, 5, '#6a6560');
          b.recoil = 8;
          shake(3);
        }
        if (b.st > shots[shots.length - 1] + 36) { b.state = 'think'; b.cd = Math.round((ph2 ? 50 : 80) / bfire()); }
      } else if (b.state === 'mg') {
        b.st++;
        if (p && b.st % 5 === 0 && b.st <= 60) {
          const mx = b.x - 62, my = b.y - 34;
          eShot(mx, my, aimClamp(-1, mx, my, p, 0.7, 0.1), 2.6 * bspd());
          flashAt({ x: mx, y: my }, { x: -1, y: 0 }, 1);
          SFX.eshot(pan(mx));
        }
        if (b.st > 80) { b.state = 'think'; b.cd = Math.round(70 / bfire()); }
      } else if (b.state === 'deploy') {
        b.st++;
        b.hatch = Math.min(1, b.st / 15);
        if ((b.st === 22 || b.st === 44) && S.enemies.length < 8) {
          const e = spawnEnemy('rifle', b.x + 4, b.y - 60);
          e.vy = -4.5; e.onGround = false; e.wave = true;
        }
        if (b.st > 70) { b.hatch = 0; b.state = 'think'; b.cd = Math.round(60 / bfire()); }
      }
      b.x = clamp(b.x, A() + W * 0.42, A() + W + 100);
      b.tread += b.x - px;
      if (b.recoil > 0) b.recoil--;
      if (p && b.active && overlap(this.boxes(b)[1], pbox(p))) hurtPlayer();
      if (ph2 && b.t % 6 === 0) smokeAt(b.x + rand(-30, 30), b.y - 44, 1, 4, '#2a2624');
      if (b.hp < b.maxHp * 0.25 && b.t % 5 === 0) addPart({ type: 'fire', x: b.x + rand(-30, 30), y: b.y - 42, vx: 0, vy: -0.6, r: rand(2, 4), life: 16 });
      if (b.t % 12 === 0) smokeAt(b.x + 50, b.y - 52, 1, 3, '#4a4541');
    },
    draw(b) {
      X.save();
      X.translate(Math.round(b.x), Math.round(b.y));
      X.scale(-1, 1);
      oval(0, 0, 62, 3, 'rgba(0,0,0,0.35)');
      rrect(-58, -20, 116, 20, 9, '#262723');
      const off = ((b.tread % 6) + 6) % 6;
      for (let k = 0; k < 18; k++) {
        R(-54 + ((k * 6 + off) % 108), -20, 3, 2, '#4a4b44');
        R(-54 + ((k * 6 - off + 108) % 108), -2, 3, 2, '#141512');
      }
      for (let i = 0; i < 7; i++) {
        const wx = -45 + i * 15;
        circ(wx, -10, 7, '#3b3d35'); circ(wx, -10, 4.5, '#6a6d60');
        X.save(); X.translate(wx, -10); X.rotate(b.tread * 0.14);
        R(-4, -0.5, 8, 1, '#2a2b26'); R(-0.5, -4, 1, 8, '#2a2b26');
        X.restore();
        circ(wx, -10, 1.5, '#8a8d80');
      }
      R(-60, -27, 120, 9, '#5d6b33'); R(-60, -27, 120, 2, '#8a9a4c'); R(-60, -19, 120, 1, '#3d4722');
      for (let i = -56; i < 60; i += 12) R(i, -24, 2, 2, '#3d4722');
      X.fillStyle = vgrad(-44, -27, [[0, '#8b9a4a'], [1, '#5a6630']]);
      X.beginPath(); X.moveTo(-54, -27); X.lineTo(-46, -42); X.lineTo(36, -42); X.lineTo(58, -30); X.lineTo(58, -27); X.closePath(); X.fill();
      R(-46, -42, 82, 2, '#a4b35e');
      line(-10, -41, -10, -28, '#4a5528'); line(20, -41, 20, -28, '#4a5528');
      R(50, -37, 12, 4, '#2e3326'); R(60, -36, 5, 2, '#1a1d15');
      circ(4, -34, 5, '#b8322a'); R(1, -37, 6, 3, '#f1e4c8'); R(2, -34, 1, 1, '#b8322a'); R(5, -34, 1, 1, '#b8322a');
      R(-52, -52, 5, 11, '#3a3d33'); R(-44, -50, 5, 9, '#3a3d33'); R(-52, -52, 5, 1, '#5a5e50');
      X.fillStyle = vgrad(-58, -42, [[0, '#9aab58'], [1, '#606e33']]);
      X.beginPath(); X.moveTo(-28, -42); X.lineTo(-24, -56); X.lineTo(16, -58); X.lineTo(30, -50); X.lineTo(30, -42); X.closePath(); X.fill();
      R(-22, -57, 36, 2, '#b4c46a');
      for (let i = -20; i < 28; i += 8) R(i, -48, 1, 1, '#4a5528');
      X.save(); X.translate(-6, -58); X.rotate(-b.hatch * 1.4); R(-8, -3, 12, 3, '#4f5b2a'); X.restore();
      line(-20, -58, -24 + Math.sin(b.t * 0.1) * 2, -88, '#2a2a2a');
      circ(-24 + Math.sin(b.t * 0.1) * 2, -88, 1, '#d8342a');
      X.save(); X.translate(22, -49); X.rotate(Math.PI - b.aim);
      const rc = b.recoil * 0.6;
      R(-2 - rc, -3, 44, 6, '#48532a'); R(-2 - rc, -3, 44, 1, '#76864a');
      R(40 - rc, -4, 9, 8, '#2f3519'); R(26 - rc, -4, 3, 8, '#3a4422');
      X.restore();
      X.restore();
    },
  },

  /* ------------------------------------------------ 2. sand scorpion */
  scorpion: {
    hp: 240, score: 25000,
    init(b) {
      b.x = A() + W + 110; b.y = GROUND; b.home = A() + W - 120;
      b.walk = 0; b.ext = 0; b.orb = 0; b.f = -1;
    },
    bounds(b) { return { x: b.x - 90, y: b.y - 130, w: 150, h: 130 }; },
    orbPos(b) { return { x: b.x - 6, y: b.y - 118 }; },
    boxes(b) {
      const o = this.orbPos(b);
      return [
        { x: o.x - 8, y: o.y - 8, w: 16, h: 16, mul: 1.3 },
        { x: b.x - 56, y: b.y - 72, w: 30, h: 26, mul: 1.5 },
        { x: b.x - 24, y: b.y - 76, w: 72, h: 28, mul: 1 },
        { x: b.x - 86 - b.ext, y: b.y - 36, w: 28 + b.ext, h: 20, mul: 0.5, claw: true },
      ];
    },
    update(b, p) {
      const ph2 = b.hp < b.maxHp * 0.5;
      const px = b.x;
      b.orb = approach(b.orb, 0, 0.02);
      if (b.state === 'enter') {
        b.x -= 0.8;
        if (b.x <= b.home) { b.state = 'think'; b.cd = 40; b.active = true; }
      } else if (b.state === 'think') {
        if (b.tx === undefined || Math.abs(b.x - b.tx) < 2) b.tx = A() + W * 0.5 + rand(0, W * 0.32);
        b.x += sign(b.tx - b.x) * 0.7;
        if (--b.cd <= 0) {
          const near = p && Math.abs(p.x - (b.x - 70)) < 60;
          const r = Math.random();
          b.state = near && r < 0.5 ? 'claw' : r < 0.38 ? 'laser' : r < 0.7 ? 'missiles' : 'charge';
          b.st = 0;
        }
      } else if (b.state === 'laser') {
        b.st++;
        const o = this.orbPos(b);
        b.orb = Math.min(1, b.st / 50);
        if (b.st === 1) SFX.charge(pan(o.x));
        if (b.st === 30 && p) { b.lx = p.x; b.ly = p.y - 8; }
        if (b.st > 50 && b.st <= 90) {
          b.lx += ph2 ? 1.4 : 0.9;
          SFX.beam(pan(o.x));
          const dx = b.lx - o.x, dy = b.ly - o.y, k = (GROUND - o.y) / dy;
          b.beamEnd = { x: o.x + dx * k, y: GROUND };
          if (p && segDist(p.x, p.y - (p.crouch ? 8 : 12), o.x, o.y, b.beamEnd.x, b.beamEnd.y) < 8) hurtPlayer();
          if (b.st % 2 === 0) { spark(b.beamEnd.x, b.beamEnd.y, 2, '#aef6ff', 2); dust(b.beamEnd.x, b.beamEnd.y, 1); }
        }
        if (b.st > 104) { b.state = 'think'; b.cd = Math.round(60 / bfire()); b.beamEnd = null; }
      } else if (b.state === 'missiles') {
        b.st++;
        const n = ph2 ? 6 : 4;
        if (p && b.st % 10 === 0 && b.st / 10 <= n) {
          const i = b.st / 10 - 1;
          S.eb.push({ kind: 'mdrop', x: b.x + 30, y: b.y - 84, phase: 0, t: 0, tx: i === 0 ? p.x : p.x + rand(-80, 80), delay: 26 + i * 6, r: 4 });
          SFX.missile(pan(b.x));
        }
        if (b.st > 100) { b.state = 'think'; b.cd = Math.round(70 / bfire()); }
      } else if (b.state === 'charge') {
        b.st++;
        if (b.st < 30) { b.x += Math.sin(b.st) * 1; }
        else if (!b.back) {
          b.x -= ph2 ? 3 : 2.4;
          b.ext = 14 + Math.sin(b.st * 0.6) * 12;
          if (b.x <= A() + 150) { b.back = true; }
        } else {
          b.x += 1.4;
          b.ext = approach(b.ext, 0, 1);
          if (b.x >= b.home - 20) { b.back = false; b.state = 'think'; b.cd = Math.round(50 / bfire()); }
        }
      } else if (b.state === 'claw') {
        b.st++;
        b.ext = b.st < 10 ? b.st * 3 : b.st < 22 ? 30 : Math.max(0, 30 - (b.st - 22) * 2);
        if (b.st === 8) SFX.knife();
        if (b.st > 40) { b.ext = 0; b.state = 'think'; b.cd = Math.round(40 / bfire()); }
      }
      b.x = clamp(b.x, A() + 140, A() + W + 120);
      const moved = Math.abs(b.x - px);
      b.walk += moved * 0.09;
      if (moved > 0.5 && b.t % 18 === 0) { SFX.step(pan(b.x)); dust(b.x - 30, b.y, 2); dust(b.x + 20, b.y, 2); }
      if (p) for (const bx of this.boxes(b)) if ((bx.claw || bx.mul === 1.5) && overlap(bx, pbox(p))) hurtPlayer();
      if (ph2 && b.t % 7 === 0) smokeAt(b.x + rand(-20, 30), b.y - 70, 1, 4, '#3a3230');
      light(b.x - 48, b.y - 62, 28, 'red', 0.8);
      light(this.orbPos(b).x, this.orbPos(b).y, 30 + b.orb * 60, 'cyan', 0.5 + b.orb * 0.5);
    },
    draw(b) {
      const o = this.orbPos(b);
      if (b.state === 'laser' && b.st > 30 && b.st <= 50 && (b.st >> 1) & 1) {
        X.globalAlpha = 0.6; line(o.x, o.y, b.lx, b.ly, '#ff4a4a', 1); X.globalAlpha = 1;
      }
      X.save();
      X.translate(Math.round(b.x), Math.round(b.y));
      X.scale(-1, 1);
      oval(0, 0, 70, 3, 'rgba(0,0,0,0.3)');
      const leg = (hx, ph, col, colL) => {
        const fx = hx + Math.sin(ph) * 10 + (hx > 0 ? 12 : -8);
        const lift = Math.max(0, Math.cos(ph)) * 6;
        const kx = (hx + fx) / 2 + (hx > 0 ? 10 : -10), ky = -84 + lift * 0.4;
        X.lineCap = 'round';
        X.strokeStyle = col; X.lineWidth = 6;
        X.beginPath(); X.moveTo(hx, -56); X.lineTo(kx, ky); X.lineTo(fx, -lift); X.stroke();
        X.strokeStyle = colL; X.lineWidth = 2;
        X.beginPath(); X.moveTo(hx, -57); X.lineTo(kx, ky - 1); X.lineTo(fx, -lift - 1); X.stroke();
        circ(kx, ky, 3.5, '#4a3a22');
        X.lineCap = 'butt';
      };
      [-30, -8, 14].forEach((hx, i) => leg(hx, b.walk + i * 2.1 + Math.PI, '#5a4526', '#7a6034'));
      // tail
      const segs = 7;
      for (let i = 0; i <= segs; i++) {
        const t = i / segs;
        const tx = (1 - t) * (1 - t) * -44 + 2 * (1 - t) * t * -84 + t * t * 6;
        const ty = (1 - t) * (1 - t) * -66 + 2 * (1 - t) * t * -150 + t * t * -112;
        circ(tx, ty, 9 - t * 4, '#8a6a3a'); circ(tx - 1, ty - 2, 6 - t * 3, '#c9a466'); R(tx - 1, ty - 1, 2, 2, '#5a4526');
      }
      poly([2, -110, 14, -120, 10, -104], '#5a4526');
      circ(6, -118, 6 + b.orb * 3, '#1f6a7a');
      circ(6, -118, 4 + b.orb * 3, b.orb > 0.2 ? '#bff8ff' : '#48c8e0');
      // body segments
      for (let i = 0; i < 5; i++) {
        const sx = -40 + i * 14;
        X.fillStyle = vgrad(-78, -46, [[0, '#e6c78a'], [0.5, '#b8945a'], [1, '#6d5530']]);
        X.beginPath(); X.ellipse(sx, -62, 14, 14, 0, 0, TAU); X.fill();
        R(sx - 1, -76, 2, 28, '#7a5c30');
        R(sx - 8, -72, 2, 1, '#f2dca4');
      }
      // head
      X.fillStyle = vgrad(-74, -44, [[0, '#e0bf80'], [1, '#6d5530']]);
      X.beginPath(); X.moveTo(24, -70); X.lineTo(50, -66); X.lineTo(58, -56); X.lineTo(48, -46); X.lineTo(24, -48); X.closePath(); X.fill();
      R(26, -69, 22, 2, '#f2dca4');
      circ(46, -60, 3, '#ff3a2a'); circ(38, -61, 2.5, '#ff3a2a'); circ(46, -61, 1, '#ffd0c0');
      R(52, -50, 6, 2, '#4a3a22'); R(52, -47, 6, 2, '#4a3a22');
      [-20, 2, 24].forEach((hx, i) => leg(hx, b.walk + i * 2.1, '#6d5530', '#a88650'));
      // claws
      const ext = b.ext;
      X.strokeStyle = '#6d5530'; X.lineWidth = 7; X.lineCap = 'round';
      X.beginPath(); X.moveTo(40, -50); X.lineTo(56 + ext * 0.4, -40); X.lineTo(66 + ext, -28); X.stroke();
      X.strokeStyle = '#b8945a'; X.lineWidth = 3;
      X.beginPath(); X.moveTo(40, -51); X.lineTo(56 + ext * 0.4, -41); X.lineTo(66 + ext, -29); X.stroke();
      X.lineCap = 'butt';
      const open = b.state === 'claw' || b.state === 'charge' ? Math.abs(Math.sin(b.t * 0.4)) * 6 : 2;
      poly([62 + ext, -34, 86 + ext, -38 - open, 80 + ext, -30], '#8a6a3a');
      poly([62 + ext, -24, 86 + ext, -18 + open, 80 + ext, -28], '#6d5530');
      circ(64 + ext, -29, 6, '#a88650');
      X.restore();
      if (b.beamEnd) {
        const w = 5 + Math.sin(b.t * 0.9) * 1.5;
        drawBeam(o.x, o.y, b.beamEnd.x, b.beamEnd.y, w, '#ffffff', '#3ad8ff');
        light(b.beamEnd.x, b.beamEnd.y, 40, 'cyan', 1);
      }
    },
  },

  /* ------------------------------------------------ 3. gunship */
  heli: {
    hp: 220, score: 25000,
    init(b) {
      b.x = A() + W + 80; b.y = 60; b.tx = A() + W * 0.68; b.ty = 84; b.vx = 0; b.vy = 0;
      b.tilt = 0; b.rot = 0; b.gun = Math.PI * 0.75;
      SFX.loopStart('rotor');
    },
    bounds(b) { return { x: b.x - 40, y: b.y - 30, w: 80, h: 50 }; },
    boxes(b) {
      return [
        { x: b.x - 32, y: b.y - 13, w: 64, h: 26, mul: 1 },
        { x: b.f < 0 ? b.x + 30 : b.x - 70, y: b.y - 9, w: 40, h: 12, mul: 0.6 },
      ];
    },
    update(b, p) {
      const ph2 = b.hp < b.maxHp * 0.5;
      if (p && b.state !== 'bombrun') b.f = sign(p.x - b.x);
      const seek = (k, max) => {
        b.vx = clamp((b.tx - b.x) * k, -max, max);
        b.vy = clamp((b.ty - b.y) * k, -max, max);
      };
      if (b.state === 'enter') {
        seek(0.03, 2.2);
        if (Math.abs(b.x - b.tx) < 6) { b.state = 'think'; b.cd = 40; b.active = true; }
      } else if (b.state === 'think') {
        seek(0.025, 1.6);
        if (Math.abs(b.x - b.tx) < 4 && chance(0.02)) { b.tx = A() + W * rand(0.35, 0.82); b.ty = rand(62, 100); }
        if (--b.cd <= 0) {
          const r = Math.random();
          b.state = r < 0.4 ? 'gun' : r < 0.62 ? 'bombrun' : r < 0.86 || !ph2 ? 'missiles' : 'paras';
          b.st = 0; b.leg = 0;
        }
      } else if (b.state === 'gun') {
        seek(0.02, 1);
        b.st++;
        const k = b.st % 40;
        if (p && k < 22 && k % 5 === 0 && b.st < 120) {
          const mx = b.x + b.f * 24, my = b.y + 13;
          const a = Math.atan2(p.y - 12 - my, p.x - mx) + rand(-0.06, 0.06);
          b.gun = a;
          eShot(mx, my, a, 2.8 * bspd());
          flashAt({ x: mx, y: my }, { x: Math.cos(a), y: Math.sin(a) }, 0.9);
          SFX.eshot(pan(mx));
        }
        if (b.st > 130) { b.state = 'think'; b.cd = Math.round(60 / bfire()); }
      } else if (b.state === 'bombrun') {
        b.st++;
        if (b.leg === 0) { b.tx = A() + W + 70; b.ty = 54; seek(0.05, 3); b.f = 1; if (b.x > A() + W + 55) { b.leg = 1; } }
        else if (b.leg === 1) {
          b.vx = -2.8; b.vy = (54 - b.y) * 0.05; b.f = -1;
          if (b.st % (ph2 ? 10 : 14) === 0 && b.x > A() + 10 && b.x < A() + W - 10) {
            S.eb.push({ kind: 'bomb', x: b.x, y: b.y + 12, vx: -1, vy: 0.5, grav: 0.16, r: 3, t: 0, blastR: 24 });
          }
          if (b.x < A() - 70) { b.leg = 2; b.tx = A() + W * 0.6; b.ty = 84; }
        } else {
          seek(0.03, 2.4); b.f = 1;
          if (Math.abs(b.x - b.tx) < 8) { b.state = 'think'; b.cd = Math.round(50 / bfire()); }
        }
      } else if (b.state === 'missiles') {
        seek(0.02, 1);
        b.st++;
        const n = ph2 ? 3 : 2;
        if (b.st % 20 === 0 && b.st / 20 <= n) {
          const a = b.f < 0 ? Math.PI * 0.75 : Math.PI * 0.25;
          S.eb.push({ kind: 'missile', x: b.x + b.f * 6, y: b.y + 10, a, sp: 1.9 * bspd(), turn: 0.032, life: 260, hp: 2, r: 3, t: 0, blastR: 22, hold: 12 });
          SFX.missile(pan(b.x));
        }
        if (b.st > 90) { b.state = 'think'; b.cd = Math.round(60 / bfire()); }
      } else if (b.state === 'paras') {
        seek(0.02, 1);
        b.st++;
        if ((b.st === 20 || b.st === 40) && S.enemies.length < 8) {
          const e = spawnEnemy('para', b.x - b.f * 10, b.y + 12, { state: 'chute', wave: true });
          e.f = -1;
        }
        if (b.st > 60) { b.state = 'think'; b.cd = Math.round(60 / bfire()); }
      }
      b.x += b.vx;
      b.y += b.vy + Math.sin(b.t * 0.05) * 0.25;
      b.tilt = approach(b.tilt, clamp(b.vx * b.f * 0.08, -0.25, 0.25), 0.02);
      if (p && b.active && overlap(this.boxes(b)[0], pbox(p))) hurtPlayer();
      if (ph2 && b.t % 6 === 0) smokeAt(b.x - b.f * 10, b.y - 14, 1, 4, '#2a2624');
      if (b.t % 3 === 0) {
        for (let i = 0; i < 2; i++) addPart({ type: 'dust', x: b.x + rand(-40, 40), y: GROUND - 2, vx: rand(-1.5, 1.5), vy: -0.3, r: rand(1.5, 3), life: 16, col: '#e8f0f8' });
      }
    },
    dieUpdate(b) {
      b.vy = (b.vy || 0) + 0.06;
      b.y += b.vy;
      b.x += b.vx * 0.98;
      b.rot += 0.09;
      if (b.dying % 3 === 0) smokeAt(b.x, b.y - 10, 1, 5, '#2a2624');
      if (b.y > GROUND - 12 && b.dying < 139) { b.y = GROUND - 12; b.vy = 0; b.dying = 139; SFX.loopStop('rotor'); }
    },
    draw(b) {
      X.save();
      X.translate(Math.round(b.x), Math.round(b.y));
      X.scale(b.f, 1);
      X.rotate(b.tilt + (b.rot || 0));
      poly([-28, -6, -72, -3, -72, 1, -28, 6], '#8c9aa8'); R(-72, -3, 44, 1, '#c8d2dc');
      poly([-66, -2, -76, -20, -69, -20, -60, -2], '#6f7d8c');
      oval(-70, -9, 2, 9 * Math.abs(Math.cos(b.t * 1.3)) + 1, 'rgba(40,40,40,0.55)');
      X.fillStyle = vgrad(-14, 14, [[0, '#f2f6fa'], [0.5, '#b9c5d1'], [1, '#6d7b89']]);
      X.beginPath(); X.ellipse(0, 0, 32, 13, 0, 0, TAU); X.fill();
      poly([-24, -6, -10, -10, -4, -2, -18, 2], 'rgba(90,104,120,0.55)');
      poly([2, 4, 14, 2, 18, 8, 6, 10], 'rgba(90,104,120,0.55)');
      X.fillStyle = vgrad(-9, 5, [[0, '#b8e8ff'], [1, '#24506e']]);
      X.beginPath(); X.ellipse(19, -2, 11, 7, 0, 0, TAU); X.fill();
      line(14, -6, 22, -6, 'rgba(255,255,255,0.8)');
      rrect(-14, -21, 30, 9, 3, '#8a97a5'); R(-12, -21, 26, 2, '#d6dee6'); R(10, -19, 4, 4, '#333');
      R(-1, -27, 3, 6, '#444');
      R(-10, 4, 22, 3, '#6f7d8c'); rrect(-8, 7, 18, 5, 2, '#5a6470'); circ(10, 9.5, 2, '#222');
      circ(-6, -1, 3.5, '#b8322a'); R(-7, -2, 2, 1, '#f1e4c8');
      const ga = b.f > 0 ? b.gun : Math.PI - b.gun;
      X.save(); X.translate(22, 11); X.rotate(clamp(ga, -0.2, 2.4)); R(0, -1, 11, 2, '#2a2a2a'); X.restore();
      circ(22, 11, 3, '#4a4f55');
      R(-20, 16, 42, 2, '#3a3f45'); R(-14, 11, 2, 5, '#3a3f45'); R(10, 11, 2, 5, '#3a3f45');
      oval(0, -28, 76, 3, 'rgba(30,30,30,0.16)');
      const L = Math.abs(76 * Math.cos(b.t * 0.9));
      R(-L, -29, L * 2, 2, 'rgba(20,20,20,0.85)');
      circ(0, -28, 2.5, '#555');
      X.restore();
    },
  },

  /* ------------------------------------------------ 4. titan mech */
  mech: {
    hp: 300, score: 30000,
    init(b) {
      b.x = A() + W + 60; b.y = GROUND; b.home = A() + W - 90;
      b.walk = 0; b.squat = 0; b.core = 0; b.vy = 0; b.vx = 0; b.air = false; b.spin = 0;
    },
    bounds(b) { return { x: b.x - 44, y: b.y - 110, w: 88, h: 110 }; },
    boxes(b) {
      const sq = b.squat * 10;
      return [
        mbox(b, 4, 26, -104 + sq, -88 + sq, { mul: 1.6 }),
        mbox(b, -26, 26, -90 + sq, -50 + sq, { mul: 1 }),
        mbox(b, -16, 16, -50 + sq, 0, { mul: 0.5, legs: true }),
      ];
    },
    update(b, p) {
      const ph2 = b.hp < b.maxHp * 0.5;
      const px = b.x;
      b.core = approach(b.core, 0, 0.02);
      if (b.state !== 'stomp' && p) b.f = sign(p.x - b.x);
      if (b.state === 'enter') {
        b.f = -1;
        b.x -= 0.8;
        if (b.x <= b.home) { b.state = 'think'; b.cd = 40; b.active = true; }
      } else if (b.state === 'think') {
        if (b.tx === undefined || Math.abs(b.x - b.tx) < 2) b.tx = A() + W * 0.45 + rand(0, W * 0.4);
        b.x += sign(b.tx - b.x) * 0.7;
        if (--b.cd <= 0) {
          const r = Math.random();
          b.state = ph2 && r < 0.25 ? 'beam' : r < 0.5 ? 'stomp' : r < 0.78 ? 'gatling' : 'missiles';
          b.st = 0;
        }
      } else if (b.state === 'stomp') {
        b.st++;
        if (!b.air) {
          if (b.st < 20) b.squat = b.st / 20;
          else if (b.st === 20) {
            b.squat = 0; b.air = true; b.vy = -7.2;
            b.vx = p ? clamp((p.x - b.x) / 55, -2.8, 2.8) : 0;
          } else if (b.st > 20) {
            b.squat = Math.max(0, 1 - (b.st - b.land) / 16);
            if (b.st - b.land > 26) { b.state = 'think'; b.cd = Math.round(50 / bfire()); }
          }
        } else {
          b.vy += 0.26; b.x += b.vx; b.y += b.vy;
          if (b.y >= GROUND) {
            b.y = GROUND; b.air = false; b.land = b.st; b.squat = 1;
            shake(9);
            SFX.stomp(pan(b.x));
            dust(b.x, b.y, 12);
            const sp = 3.1 * bspd();
            S.eb.push({ kind: 'wave', x: b.x - 18, y: GROUND, vx: -sp, vy: 0, t: 0, r: 6 });
            S.eb.push({ kind: 'wave', x: b.x + 18, y: GROUND, vx: sp, vy: 0, t: 0, r: 6 });
            if (ph2) {
              S.eb.push({ kind: 'wave', x: b.x - 18, y: GROUND, vx: -sp * 0.6, vy: 0, t: 0, r: 6 });
              S.eb.push({ kind: 'wave', x: b.x + 18, y: GROUND, vx: sp * 0.6, vy: 0, t: 0, r: 6 });
            }
          }
        }
      } else if (b.state === 'gatling') {
        b.st++;
        b.spin += b.st < 24 ? b.st * 0.02 : 0.5;
        if (b.st === 2) SFX.charge(pan(b.x));
        if (p && b.st >= 24 && b.st < 110 && (b.st - 24) % 16 < 8 && b.st % 2 === 0) {
          const mx = b.x + b.f * 46, my = b.y - 72 + b.squat * 10;
          const base = Math.atan2(p.y - 14 - my, p.x - mx);
          const sweep = -0.38 + ((b.st - 24) / 86) * 0.6;
          const a = base + sweep * (b.f > 0 ? -1 : 1);
          eShot(mx, my, a, 3 * bspd());
          flashAt({ x: mx, y: my }, { x: Math.cos(a), y: Math.sin(a) }, 1);
          SFX.mg(pan(mx));
        }
        if (b.st > 124) { b.state = 'think'; b.cd = Math.round(60 / bfire()); }
      } else if (b.state === 'missiles') {
        b.st++;
        if (b.st % 8 === 0 && b.st / 8 <= 6) {
          const a = -Math.PI / 2 + rand(-0.4, 0.4) - b.f * 0.3;
          S.eb.push({ kind: 'missile', x: b.x - b.f * 14, y: b.y - 104, a, sp: 1.8 * bspd(), turn: 0.034, life: 240, hp: 2, r: 3, t: 0, blastR: 22, hold: 20 });
          SFX.missile(pan(b.x));
          smokeAt(b.x - b.f * 14, b.y - 104, 2, 3, '#8a8580');
        }
        if (b.st > 70) { b.state = 'think'; b.cd = Math.round(70 / bfire()); }
      } else if (b.state === 'beam') {
        b.st++;
        b.core = Math.min(1, b.st / 60);
        if (b.st === 1) SFX.charge(pan(b.x));
        if (b.st > 60 && b.st <= 106) {
          SFX.beam(pan(b.x));
          const x0 = b.x + b.f * 20, x1 = b.f > 0 ? A() + W + 20 : A() - 20;
          b.beam = { x0, x1 };
          if (p && overlap({ x: Math.min(x0, x1), y: b.y - 31, w: Math.abs(x1 - x0), h: 12 }, pbox(p))) hurtPlayer();
        } else b.beam = null;
        if (b.st > 120) { b.state = 'think'; b.cd = Math.round(60 / bfire()); b.beam = null; }
      }
      b.x = clamp(b.x, A() + 40, A() + W + 80);
      const moved = Math.abs(b.x - px);
      if (!b.air && moved > 0.1) {
        b.walk += moved * 0.07;
        if (Math.floor(b.walk / Math.PI) !== Math.floor((b.walk - moved * 0.07) / Math.PI)) { SFX.step(pan(b.x)); shake(1.5); dust(b.x, b.y, 3); }
      }
      if (p && b.active) {
        const bx = this.boxes(b);
        if (overlap(bx[1], pbox(p)) || overlap(bx[2], pbox(p))) hurtPlayer();
      }
      if (ph2 && b.t % 7 === 0) smokeAt(b.x + rand(-20, 20), b.y - 90, 1, 4, '#2a2624');
      light(b.x + b.f * 8, b.y - 68, 20 + b.core * 50, ph2 ? 'red' : 'cyan', 0.7 + b.core * 0.3);
    },
    draw(b) {
      const sq = Math.round(b.squat * 10);
      if (b.state === 'beam' && b.st > 20 && b.st <= 60 && (b.st >> 1) & 1) {
        const x1 = b.f > 0 ? A() + W : A();
        X.globalAlpha = 0.55; R(Math.min(b.x, x1), b.y - 25, Math.abs(x1 - b.x), 1, '#ff4a4a'); X.globalAlpha = 1;
      }
      X.save();
      X.translate(Math.round(b.x), Math.round(b.y));
      X.scale(b.f, 1);
      oval(0, 0, 34, 3, 'rgba(0,0,0,0.35)');
      const legDraw = (off, ph, dark) => {
        const s = b.air ? 0 : Math.sin(ph) * 7;
        const lift = b.air ? 6 : Math.max(0, Math.cos(ph)) * 5;
        const hx = off, hy = -50 + sq, kx = off + 8 + s * 0.5, ky = -28 + sq * 0.5 - lift, ax = off + s, ay = -8 - lift;
        const c1 = dark ? '#3a414d' : '#5d6675', c2 = dark ? '#4d5563' : '#8f9bab';
        X.lineCap = 'round';
        X.strokeStyle = c1; X.lineWidth = 11;
        X.beginPath(); X.moveTo(hx, hy); X.lineTo(kx, ky); X.lineTo(ax, ay); X.stroke();
        X.strokeStyle = c2; X.lineWidth = 3;
        X.beginPath(); X.moveTo(hx - 2, hy); X.lineTo(kx - 2, ky); X.lineTo(ax - 2, ay); X.stroke();
        X.lineCap = 'butt';
        circ(kx, ky, 5, dark ? '#2a2f38' : '#4a525f'); circ(kx, ky, 2, '#e07b2a');
        rrect(ax - 14, ay, 28, 8, 2, c1); R(ax - 14, ay, 28, 2, c2); R(ax + 8, ay + 2, 6, 6, '#e07b2a');
      };
      legDraw(-6, b.walk + Math.PI, true);
      X.save(); X.translate(0, sq);
      rrect(-24, -88, 18, 20, 3, '#4a525f');
      for (let i = 0; i < 3; i++) circ(-19 + i * 5, -84, 2, b.state === 'missiles' ? '#ff6a3a' : '#1c2028');
      X.fillStyle = vgrad(-90, -50, [[0, '#a0acbc'], [0.4, '#6c7686'], [1, '#3e4552']]);
      X.beginPath(); X.moveTo(-26, -88); X.lineTo(18, -90); X.lineTo(28, -76); X.lineTo(24, -52); X.lineTo(-22, -50); X.closePath(); X.fill();
      R(-24, -88, 40, 2, '#c4cedb');
      for (let i = 0; i < 4; i++) poly([-20 + i * 10, -54, -14 + i * 10, -54, -18 + i * 10, -60, -24 + i * 10, -60], '#e07b2a');
      line(-4, -86, -4, -56, '#3e4552');
      circ(8, -68, 6, '#20262e');
      circ(8, -68, 4 + b.core * 2, b.hp < b.maxHp * 0.5 ? '#ff5a3a' : '#5ae8ff');
      circ(8, -68, 1.5 + b.core * 2, '#ffffff');
      X.fillStyle = vgrad(-104, -88, [[0, '#c8f0ff'], [1, '#2a5a7a']]);
      X.beginPath(); X.ellipse(15, -95, 11, 8, 0, Math.PI, TAU); X.fill();
      R(4, -95, 22, 6, '#4a525f'); R(8, -100, 6, 1, '#ffffff');
      X.save(); X.translate(16, -76);
      R(-4, -4, 10, 9, '#4a525f');
      R(4, -5, 26, 10, '#5d6675'); R(4, -5, 26, 2, '#a0acbc');
      for (let i = 0; i < 3; i++) R(30, -4 + i * 3 + (Math.floor(b.spin * 3 + i) % 3 === 0 ? 1 : 0), 8, 2, '#2a2f38');
      X.restore();
      X.restore();
      legDraw(6, b.walk, false);
      X.restore();
      if (b.beam) {
        const w = 10 + Math.sin(b.t * 0.8) * 2;
        drawBeam(b.beam.x0, b.y - 25, b.beam.x1, b.y - 25, w, '#ffffff', b.hp < b.maxHp * 0.5 ? '#ff5a3a' : '#3ad8ff');
        light(b.beam.x0, b.y - 25, 60, 'white', 1);
        light((b.beam.x0 + b.beam.x1) / 2, b.y - 25, 120, 'cyan', 0.5);
      }
    },
  },

  /* ------------------------------------------------ 5. doom fortress */
  fortress: {
    hp: 460, score: 50000,
    init(b) {
      b.x = A() + W - 150; b.y = GROUND; b.rise = 1; b.open = 0; b.phase = 1;
      b.parts = {
        t1: { hp: Math.round(90 * b.mul), x: b.x - 2, y: 92 },
        t2: { hp: Math.round(90 * b.mul), x: b.x - 2, y: 188 },
        core: { hp: Math.round(280 * b.mul) },
      };
      b.maxHp = b.hp = b.parts.t1.hp + b.parts.t2.hp + b.parts.core.hp;
      b.a1 = Math.PI; b.a2 = Math.PI; b.cyc = 0; b.vent = null; b.ventCd = 200; b.pattern = 0; b.spiral = 0;
      b.hangar = 300;
    },
    bounds(b) { return { x: b.x - 10, y: 30 + b.rise * 220, w: 190, h: 184 }; },
    boxes(b) {
      const out = [];
      if (b.parts.t1.hp > 0) out.push({ x: b.x - 16, y: 80, w: 28, h: 24, mul: 1, part: 't1' });
      if (b.parts.t2.hp > 0) out.push({ x: b.x - 16, y: 176, w: 28, h: 24, mul: 1, part: 't2' });
      out.push({ x: b.x + 18, y: 114, w: 40, h: 40, mul: b.phase === 2 && b.open > 0.8 ? 1 : 0, part: 'core' });
      out.push({ x: b.x, y: 24, w: 200, h: GROUND - 24, mul: 0 });
      return out;
    },
    hurt(b, dmg, box) {
      const part = b.parts[box.part];
      if (!part || part.hp <= 0) return;
      part.hp -= dmg;
      if (part.hp <= 0 && box.part !== 'core') {
        part.hp = 0;
        explosion(part.x, part.y, 1.6);
        debris(part.x, part.y, 12, ['#3d424c', '#5a606c', '#2a2e35']);
        addScore(5000, part.x, part.y - 20, '#ffd35a');
        if (b.parts.t1.hp <= 0 && b.parts.t2.hp <= 0 && b.phase === 1) {
          b.phase = 2; b.cyc = 0;
          showBanner('<span class="px">WARNING</span><strong>核心暴露</strong><span class="px sm">CORE EXPOSED</span>', 'b-warn b-small', 1800);
          SFX.alarm();
        }
      }
      b.hp = Math.max(0, b.parts.t1.hp) + Math.max(0, b.parts.t2.hp) + Math.max(0, b.parts.core.hp);
      if (b.parts.core.hp <= 0) b.hp = 0;
    },
    update(b, p) {
      const ph = b.phase;
      if (b.state === 'enter') {
        b.rise = Math.max(0, b.rise - 0.009);
        if (b.t % 4 === 0) { shake(3); dust(b.x + rand(0, 180), GROUND, 3); }
        if (b.rise <= 0) { b.state = 'fight'; b.active = true; }
        return;
      }
      if (p && p.x > b.x - 8) p.x = b.x - 8;
      const fire = bfire();
      if (p) {
        for (const k of ['t1', 't2']) {
          const part = b.parts[k];
          if (part.hp <= 0) continue;
          const ak = k === 't1' ? 'a1' : 'a2';
          const ta = Math.atan2(p.y - 12 - part.y, p.x - part.x);
          b[ak] = approach(b[ak], clamp(ta < 0 ? ta + TAU : ta, Math.PI * 0.55, Math.PI * 1.4), 0.03);
        }
        const t1 = b.parts.t1, t2 = b.parts.t2;
        if (t1.hp > 0 && b.t % Math.round(90 / fire) === 0) {
          for (const d of [-0.18, 0, 0.18]) eShot(t1.x + Math.cos(b.a1) * 18, t1.y + Math.sin(b.a1) * 18, b.a1 + d, 2.4 * bspd());
          SFX.eshot(pan(t1.x));
          flashAt({ x: t1.x + Math.cos(b.a1) * 18, y: t1.y + Math.sin(b.a1) * 18 }, { x: Math.cos(b.a1), y: Math.sin(b.a1) }, 1.2);
        }
        if (t2.hp > 0 && b.t % Math.round(70 / fire) === 35) {
          eShot(t2.x + Math.cos(b.a2) * 18, t2.y + Math.sin(b.a2) * 18, b.a2, 2.7 * bspd());
          SFX.eshot(pan(t2.x));
        }
        if (t2.hp > 0 && b.t % Math.round(170 / fire) === 80) {
          const v = lob(t2.x, t2.y - 10, p.x, p.y, 50, 0.2);
          S.eb.push({ kind: 'gren', x: t2.x, y: t2.y - 10, vx: v.vx, vy: v.vy, grav: 0.2, fuse: 90, r: 3, t: 0, rot: 0, bounces: 0 });
        }
      }
      if (--b.hangar <= 0) {
        b.hangar = Math.round(420 / fire);
        if (S.enemies.filter(e => e.type === 'drone').length < 2) {
          spawnEnemy('drone', b.x + 60, 40, { wave: true });
          SFX.missile(pan(b.x));
        }
      }
      if (b.vent) {
        b.vent.t++;
        if (b.vent.t > 50 && b.vent.t <= 100) {
          for (const vx of b.vent.xs) {
            if (b.vent.t % 2 === 0) addPart({ type: 'fire', x: vx + rand(-6, 6), y: GROUND - rand(0, 60), vx: 0, vy: -1.2, r: rand(4, 7), life: 16 });
            light(vx, GROUND - 30, 60, 'fire', 1);
            if (p && overlap({ x: vx - 9, y: GROUND - 70, w: 18, h: 70 }, pbox(p))) hurtPlayer();
          }
          if (b.vent.t === 51) SFX.flame(0);
        }
        if (b.vent.t > 100) { b.vent = null; b.ventCd = Math.round(220 / fire); }
      } else if (--b.ventCd <= 0) {
        const xs = [A() + 70, A() + 170, A() + 270];
        const n = ph === 2 ? 2 : 1;
        b.vent = { t: 0, xs: xs.sort(() => Math.random() - 0.5).slice(0, n) };
      }
      if (ph === 2) {
        b.cyc++;
        const openT = 330, closeT = 120;
        const c = b.cyc % (openT + closeT);
        b.open = approach(b.open, c < openT ? 1 : 0, 0.04);
        if (c === 0) b.pattern = (b.pattern + 1) % 2;
        if (c < openT && b.open > 0.9) {
          const cx = b.x + 38, cy = 134;
          if (b.pattern === 0) {
            if (b.cyc % Math.max(3, Math.round(6 / fire)) === 0) {
              b.spiral += 0.37;
              const a = Math.PI + Math.sin(b.spiral) * 1.2;
              eShot(cx, cy, a, 1.8 * bspd(), 'orb', { r: 3.5, col: '#ff3a8a', glow: 'red' });
              if (fire > 1.3) eShot(cx, cy, Math.PI + Math.sin(b.spiral + 2) * 1.2, 1.8 * bspd(), 'orb', { r: 3.5, col: '#ff3a8a', glow: 'red' });
            }
          } else {
            const lc = c % 110;
            if (lc === 1) { b.lhigh = chance(0.5); SFX.charge(pan(cx)); }
            if (lc > 50 && lc <= (b.lhigh ? 92 : 78)) {
              b.laser = b.lhigh ? { y0: GROUND - 31, y1: GROUND - 18 } : { y0: GROUND - 10, y1: GROUND };
              SFX.beam(pan(cx));
              if (p && overlap({ x: A() - 10, y: b.laser.y0, w: cx - A() + 10, h: b.laser.y1 - b.laser.y0 }, pbox(p))) hurtPlayer();
            } else b.laser = null;
            b.lwarn = lc > 0 && lc <= 50;
          }
        } else { b.laser = null; b.lwarn = false; }
        light(b.x + 38, 134, 30 + b.open * 60, 'red', 0.6 + b.open * 0.4);
      }
      if (b.t % 40 === 0) light(b.x + 100, 40, 40, 'red', 1);
    },
    draw(b) {
      const oy = Math.round(b.rise * 220);
      const x = Math.round(b.x);
      if (b.vent) {
        for (const vx of b.vent.xs) {
          if (b.vent.t <= 50 && (b.vent.t >> 2) & 1) { oval(vx, GROUND - 1, 12, 3, '#ff5a2a'); light(vx, GROUND - 4, 30, 'red', 0.8); }
          if (b.vent.t > 50) {
            const h = 70 * Math.min(1, (b.vent.t - 50) / 8);
            X.fillStyle = vgrad(GROUND - h, GROUND, [[0, 'rgba(255,200,80,0)'], [0.3, 'rgba(255,150,40,0.8)'], [1, 'rgba(255,240,180,1)']]);
            X.fillRect(vx - 8, GROUND - h, 16, h);
          }
        }
      }
      for (const vx of [A() + 70, A() + 170, A() + 270]) { R(vx - 10, GROUND - 2, 20, 3, '#2a2d33'); for (let i = -8; i < 10; i += 4) R(vx + i, GROUND - 1, 2, 1, '#111'); }
      if (b.lwarn && (b.t >> 1) & 1) {
        const y = b.lhigh ? GROUND - 25 : GROUND - 5;
        X.globalAlpha = 0.6; R(A(), y, b.x - A(), 1, '#ff4a4a'); X.globalAlpha = 1;
      }
      X.save();
      X.translate(0, oy);
      X.fillStyle = vgrad(24, GROUND, [[0, '#5a606c'], [0.5, '#3d424c'], [1, '#2a2e35']]);
      X.beginPath(); X.moveTo(x + 20, 24); X.lineTo(x + 200, 24); X.lineTo(x + 200, GROUND); X.lineTo(x, GROUND); X.lineTo(x, 60); X.closePath(); X.fill();
      R(x + 20, 24, 180, 3, '#8a909c');
      for (let yy = 50; yy < GROUND; yy += 34) { R(x + 4, yy, 196, 2, '#2a2e35'); for (let xx = x + 10; xx < x + 200; xx += 18) R(xx, yy + 4, 2, 2, '#6d7380'); }
      for (let xx = x + 60; xx < x + 200; xx += 46) R(xx, 28, 2, GROUND - 28, '#30343c');
      for (let i = 0; i < 12; i++) poly([x + 4 + i * 16, GROUND - 14, x + 12 + i * 16, GROUND - 14, x + 4 + i * 16, GROUND - 6, x - 4 + i * 16, GROUND - 6], '#e0b22a');
      R(x, GROUND - 14, 200, 1, '#1a1a1a'); R(x, GROUND - 6, 200, 1, '#1a1a1a');
      rrect(x + 50, 30, 60, 20, 3, '#23262c'); R(x + 54, 34, 52, 12, '#101216');
      for (let i = 0; i < 4; i++) R(x + 56 + i * 13, 36, 9, 8, (b.hangar < 40 && (b.t >> 2) & 1) ? '#ff5a2a' : '#2a2e35');
      const blink = (b.t >> 4) & 1;
      circ(x + 100, 40, 3, blink ? '#ff3a2a' : '#5a1a14'); circ(x + 160, 70, 3, blink ? '#5a1a14' : '#ff3a2a');
      R(x + 120, 60, 60, 4, '#5a3a30'); R(x + 120, 100, 60, 4, '#5a3a30');
      const cx = x + 38, cy = 134;
      rrect(cx - 26, cy - 26, 52, 52, 6, '#23262c');
      circ(cx, cy, 20, '#12060a');
      const glowR = 16;
      X.fillStyle = (() => { const gr = X.createRadialGradient(cx - 4, cy - 4, 2, cx, cy, glowR); gr.addColorStop(0, '#fff0e0'); gr.addColorStop(0.35, '#ff5a3a'); gr.addColorStop(1, '#5a0a0a'); return gr; })();
      X.beginPath(); X.arc(cx, cy, glowR, 0, TAU); X.fill();
      const p = S.player;
      if (p) { const a = Math.atan2(p.y - 14 - cy, p.x - cx); circ(cx + Math.cos(a) * 7, cy + Math.sin(a) * 7, 5, '#1a0404'); }
      const sh = Math.round((1 - b.open) * 22);
      R(cx - 24, cy - 24, 48, sh, '#6d7380'); R(cx - 24, cy - 24 + sh - 2, 48, 2, '#9aa1ad');
      R(cx - 24, cy + 24 - sh, 48, sh, '#565c67'); R(cx - 24, cy + 24 - sh, 48, 2, '#9aa1ad');
      for (const [k, ak] of [['t1', 'a1'], ['t2', 'a2']]) {
        const part = b.parts[k];
        if (part.hp > 0) {
          circ(part.x, part.y, 12, '#50565e'); circ(part.x - 3, part.y - 3, 5, '#8d949c');
          X.save(); X.translate(part.x, part.y); X.rotate(b[ak]);
          R(4, -3, 18, 6, '#3a3d42'); R(4, -3, 18, 1, '#6d737b'); R(20, -4, 4, 8, '#26282c');
          X.restore();
          circ(part.x, part.y, 3, (b.t >> 3) & 1 ? '#ff5a3a' : '#a02a1a');
        } else {
          circ(part.x, part.y, 11, '#16181c'); circ(part.x, part.y, 7, '#2a1a14');
          if (b.t % 8 === 0) smokeAt(part.x - 4, part.y, 1, 3, '#2a2624');
          if (b.t % 11 === 0) spark(part.x, part.y, 2);
        }
      }
      X.restore();
      if (b.laser) {
        const my = (b.laser.y0 + b.laser.y1) / 2, w = b.laser.y1 - b.laser.y0;
        drawBeam(cx, my, A() - 10, my, w, '#ffffff', '#ff4a6a');
        light(cx, my, 60, 'red', 1);
      }
    },
  },
};
