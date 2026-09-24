'use strict';
/* Iron Assault — canvas setup, drawing primitives and character sprites.
   Sprites are painted procedurally at native 480x270 resolution so they
   stay crisp when the canvas is scaled up with pixelated rendering. */

const cv = document.getElementById('game');
const g = cv.getContext('2d');
g.imageSmoothingEnabled = false;
let X = g; // current sprite target

function mkCanvas(w, h) {
  const c = document.createElement('canvas');
  c.width = w; c.height = h;
  const x = c.getContext('2d');
  x.imageSmoothingEnabled = false;
  return [c, x];
}
function withCtx(c, fn) {
  const prev = X;
  X = c;
  try { fn(); } finally { X = prev; }
}
function R(x, y, w, h, c) { X.fillStyle = c; X.fillRect(x, y, w, h); }
function circ(x, y, r, c) { X.fillStyle = c; X.beginPath(); X.arc(x, y, Math.max(0, r), 0, TAU); X.fill(); }
function oval(x, y, rx, ry, c, rot = 0) { X.fillStyle = c; X.beginPath(); X.ellipse(x, y, Math.max(0, rx), Math.max(0, ry), rot, 0, TAU); X.fill(); }
function poly(pts, c) {
  X.fillStyle = c;
  X.beginPath();
  X.moveTo(pts[0], pts[1]);
  for (let i = 2; i < pts.length; i += 2) X.lineTo(pts[i], pts[i + 1]);
  X.closePath();
  X.fill();
}
function line(x1, y1, x2, y2, c, w = 1) {
  X.strokeStyle = c; X.lineWidth = w;
  X.beginPath(); X.moveTo(x1, y1); X.lineTo(x2, y2); X.stroke();
}
function vgrad(y0, y1, stops) {
  const gr = X.createLinearGradient(0, y0, 0, y1);
  for (const [o, col] of stops) gr.addColorStop(o, col);
  return gr;
}
function rrect(x, y, w, h, r, c) {
  X.fillStyle = c;
  X.beginPath();
  X.moveTo(x + r, y);
  X.lineTo(x + w - r, y); X.quadraticCurveTo(x + w, y, x + w, y + r);
  X.lineTo(x + w, y + h - r); X.quadraticCurveTo(x + w, y + h, x + w - r, y + h);
  X.lineTo(x + r, y + h); X.quadraticCurveTo(x, y + h, x, y + h - r);
  X.lineTo(x, y + r); X.quadraticCurveTo(x, y, x + r, y);
  X.fill();
}

/* ---- glow sprites (cheap bloom) ---- */
function makeGlow(r, gg, b) {
  const [c, x] = mkCanvas(64, 64);
  const gr = x.createRadialGradient(32, 32, 0, 32, 32, 32);
  gr.addColorStop(0, `rgba(${r},${gg},${b},1)`);
  gr.addColorStop(0.25, `rgba(${r},${gg},${b},0.5)`);
  gr.addColorStop(0.6, `rgba(${r},${gg},${b},0.12)`);
  gr.addColorStop(1, `rgba(${r},${gg},${b},0)`);
  x.fillStyle = gr;
  x.fillRect(0, 0, 64, 64);
  return c;
}
const GLOW = {
  fire: makeGlow(255, 160, 60),
  white: makeGlow(255, 246, 225),
  cyan: makeGlow(80, 210, 255),
  red: makeGlow(255, 60, 40),
  green: makeGlow(120, 255, 140),
  yellow: makeGlow(255, 220, 110),
};

/* ---- white hit-flash via an offscreen buffer ---- */
const [flashC, flashX] = mkCanvas(360, 240);
function drawFlashed(fn, rx, ry, w, h, alpha = 0.82) {
  rx = Math.floor(rx); ry = Math.floor(ry);
  w = Math.min(360, Math.ceil(w)); h = Math.min(240, Math.ceil(h));
  flashX.setTransform(1, 0, 0, 1, 0, 0);
  flashX.clearRect(0, 0, w, h);
  flashX.setTransform(1, 0, 0, 1, -rx, -ry);
  withCtx(flashX, fn);
  flashX.setTransform(1, 0, 0, 1, 0, 0);
  flashX.globalCompositeOperation = 'source-atop';
  flashX.fillStyle = `rgba(255,255,255,${alpha})`;
  flashX.fillRect(0, 0, w, h);
  flashX.globalCompositeOperation = 'source-over';
  g.drawImage(flashC, 0, 0, w, h, rx, ry, w, h);
}

/* ---- palettes ---- */
const PAL = {
  player: {
    skin: '#f3c89c', skinD: '#c98e62', hair: '#e8b848', band: '#d8342a',
    shirt: '#3d6fbd', shirtD: '#274b88', vest: '#7c6a3c', vestD: '#57492a',
    pants: '#8a7a4e', pantsD: '#62573a', boot: '#3a2716', belt: '#2a1e12', pack: '#5d5a34',
  },
  jungle: {
    skin: '#eab88a', skinD: '#b98659', shirt: '#86964a', shirtD: '#5d6a31', pants: '#7a8a42', pantsD: '#56622d',
    helmet: '#6b7a35', helmetD: '#465022', helmetL: '#8c9c4c', boot: '#3b2b1c', belt: '#4a3a22',
  },
  desert: {
    skin: '#e3ad7c', skinD: '#b27c50', shirt: '#cfae72', shirtD: '#9d7f4a', pants: '#bf9c62', pantsD: '#8e7243',
    helmet: '#b59360', helmetD: '#80663e', helmetL: '#d6b67e', boot: '#4a3522', belt: '#6a4e2c',
  },
  snow: {
    skin: '#efc39a', skinD: '#c08e66', shirt: '#e2e8ef', shirtD: '#a8b5c4', pants: '#c9d3de', pantsD: '#94a2b3',
    helmet: '#eef2f6', helmetD: '#98a7b8', helmetL: '#ffffff', boot: '#39414c', belt: '#5a6270',
  },
  city: {
    skin: '#e8b48a', skinD: '#b8845c', shirt: '#56607a', shirtD: '#3a4156', pants: '#474f66', pantsD: '#32384a',
    helmet: '#3f4658', helmetD: '#2a2f3c', helmetL: '#5d667c', boot: '#1f2028', belt: '#2a2c34',
  },
  base: {
    skin: '#e8b48a', skinD: '#b8845c', shirt: '#34303a', shirtD: '#221f27', pants: '#302c35', pantsD: '#211e25',
    helmet: '#962d24', helmetD: '#5e1a15', helmetL: '#c4473a', boot: '#17151a', belt: '#4a1a16',
  },
  pow: {
    skin: '#e7b58a', skinD: '#b98659', shirt: '#dcd4c0', shirtD: '#aaa28e', pants: '#8a8577', pantsD: '#66625a',
    beard: '#6a4a2a', boot: '#4a3a2a', belt: '#5a4a3a',
  },
  burnt: {
    skin: '#3a2e28', skinD: '#2a211d', shirt: '#2a2522', shirtD: '#1c1917', pants: '#262220', pantsD: '#1a1715',
    helmet: '#2a2522', helmetD: '#1a1715', helmetL: '#3a3330', boot: '#141210', belt: '#1c1917', hair: '#1c1917', band: '#2a2522',
  },
};

/* ---- guns ---- */
const MUZ = { pistol: 6, hmg: 13, rocket: 12, flame: 11, shotgun: 13, laser: 11, rifle: 12, bazooka: 14 };

function drawGun(k) {
  switch (k) {
    case 'pistol':
      R(0, -1, 6, 2, '#3b3f45'); R(0, -1, 6, 1, '#737a83'); R(0, 1, 2, 2, '#26282c'); break;
    case 'hmg':
      R(-3, -2, 13, 3, '#474c53'); R(-3, -2, 13, 1, '#858c95'); R(10, -1, 3, 1, '#2a2d31');
      R(1, 1, 4, 4, '#3a3220'); R(1, 1, 4, 1, '#7a6838'); R(-3, 1, 2, 2, '#2a2d31'); break;
    case 'rocket':
      R(-8, -4, 18, 4, '#56702e'); R(-8, -4, 18, 1, '#8aa64c'); R(10, -5, 2, 6, '#2d3a1a');
      R(-10, -5, 2, 6, '#2d3a1a'); R(0, 0, 2, 3, '#2a2a2a'); R(4, -5, 3, 1, '#c9a54a'); break;
    case 'flame':
      R(-3, -2, 10, 3, '#7a2a22'); R(-3, -2, 10, 1, '#c0503f'); R(7, -1, 4, 1, '#8f969d');
      R(-2, 1, 5, 4, '#c9862f'); R(-2, 1, 5, 1, '#f3b957'); break;
    case 'shotgun':
      R(-5, -1, 16, 2, '#6a4322'); R(-5, -1, 16, 1, '#8e5e34'); R(2, -2, 11, 1, '#3a3d42'); R(-5, 0, 3, 2, '#4a2e16'); R(4, 1, 4, 1, '#3a2412'); break;
    case 'laser':
      R(-2, -2, 11, 3, '#d6dde6'); R(-2, 0, 11, 1, '#8e98a6'); R(1, -1, 7, 1, '#2fb6ff'); R(9, -1, 2, 1, '#c9f6ff'); break;
    case 'rifle':
      R(-5, -1, 15, 2, '#5a3b1e'); R(-5, -1, 15, 1, '#7a5530'); R(1, -2, 10, 1, '#3b3f45'); R(3, 1, 2, 2, '#222'); break;
    case 'bazooka':
      R(-10, -3, 22, 5, '#4d6a2a'); R(-10, -3, 22, 1, '#7c9a46'); R(12, -4, 3, 7, '#2d3a1a');
      R(-12, -4, 2, 7, '#2d3a1a'); R(0, 2, 2, 3, '#2a2a2a'); break;
  }
}

/**
 * Draw a soldier. o: {x,y,f,pal,pose,phase,aim,weapon,gear,t,arms,recoil,rot,shield,chute}
 * Local coords: feet at y=0, facing +x.
 */
function drawSoldier(o) {
  const P = o.pal;
  X.save();
  X.translate(Math.round(o.x), Math.round(o.y));
  if (o.f < 0) X.scale(-1, 1);
  if (o.rot) X.rotate(o.rot);
  const pose = o.pose;
  if (o.shadow !== false && !o.rot) oval(0, 0, 7, 1.5, 'rgba(0,0,0,0.28)');

  if (o.chute) drawChute(o.t);

  // legs
  if (pose === 'crouch' || pose === 'kneel') {
    R(-6, -8, 7, 4, P.pantsD); R(-7, -4, 4, 4, P.pantsD); R(-9, -2, 5, 2, P.boot);
    R(-1, -8, 7, 4, P.pants); R(3, -5, 3, 4, P.pants); R(3, -2, 5, 2, P.boot);
  } else if (pose === 'sit') {
    R(-4, -4, 10, 3, P.pants); R(-4, -2, 10, 2, P.pantsD); R(5, -4, 3, 4, P.boot);
  } else if (pose === 'air') {
    R(-4, -9, 3, 5, P.pantsD); R(-7, -5, 4, 2, P.pantsD); R(-9, -6, 3, 3, P.boot);
    R(0, -9, 3, 4, P.pants); R(1, -6, 4, 3, P.pants); R(3, -4, 4, 2, P.boot);
  } else {
    const run = pose === 'run';
    const s = Math.sin(o.phase * TAU), c = Math.cos(o.phase * TAU);
    const a = run ? Math.round(s * 4) : 0;
    const l1 = run ? Math.round(Math.max(0, c) * 3) : 0;
    const l2 = run ? Math.round(Math.max(0, -c) * 3) : 0;
    R(-3 - a, -9, 3, 8 - l2, P.pantsD); R(-4 - a, -2 - l2, 5, 2, P.boot);
    R(a, -9, 3, 8 - l1, P.pants); R(a, -2 - l1, 5, 2, P.boot);
  }

  const dy = pose === 'crouch' || pose === 'kneel' ? 7 : pose === 'sit' ? 6 : 0;
  const bob = pose === 'run' ? Math.round(Math.abs(Math.sin(o.phase * TAU)) * -1) : 0;
  X.translate(0, dy + bob);

  if (P.pack) { R(-7, -18, 3, 8, P.pack); R(-7, -18, 1, 8, '#3e3c22'); }
  // torso
  R(-4, -19, 8, 10, P.shirt); R(-4, -19, 2, 10, P.shirtD);
  if (P.vest) { R(-3, -18, 6, 7, P.vest); R(-3, -18, 1, 7, P.vestD); R(1, -16, 2, 2, P.vestD); }
  R(-4, -10, 8, 2, P.belt); R(1, -10, 2, 2, '#c9a54a');

  // head
  R(-3, -26, 7, 7, P.skin); R(-3, -20, 7, 1, P.skinD); R(4, -23, 1, 2, P.skin);
  R(2, -23, 1, 1, '#1a1410');
  if (o.gear === 'band') {
    R(-3, -27, 7, 3, P.hair); R(-4, -26, 2, 5, P.hair); R(-2, -28, 5, 1, P.hair);
    R(-3, -25, 7, 1, P.band);
    const w = Math.round(Math.sin((o.t || 0) * 0.3));
    R(-6, -25 + w, 3, 1, P.band); R(-8, -24 + w, 2, 1, P.band);
  } else if (o.gear === 'helmet') {
    R(-4, -28, 9, 4, P.helmet); R(-3, -29, 7, 1, P.helmet); R(-5, -25, 11, 1, P.helmetD);
    R(-3, -28, 3, 1, P.helmetL); R(-4, -24, 1, 3, P.helmetD);
  } else if (o.gear === 'pow') {
    R(-3, -27, 7, 2, P.skinD); R(-2, -21, 6, 3, P.beard); R(-3, -24, 1, 3, P.beard); R(2, -22, 2, 1, '#8a3a2a');
  }

  // arms and weapon
  const arms = o.arms;
  if (arms === 'tied') {
    R(-6, -17, 3, 6, P.shirtD); R(-7, -13, 5, 1, '#c9a86a'); R(-4, -15, 8, 1, '#c9a86a');
  } else if (arms === 'salute') {
    R(-2, -18, 3, 8, P.shirtD); R(1, -21, 3, 4, P.shirt); R(3, -25, 3, 3, P.shirt); R(4, -26, 2, 2, P.skin);
  } else if (arms === 'flail') {
    const w = Math.sin((o.t || 0) * 0.6) * 3;
    R(-5, -24 + w, 3, 7, P.shirtD); R(2, -24 - w, 3, 7, P.shirt); R(2, -25 - w, 3, 2, P.skin);
  } else if (arms === 'throw') {
    R(-5, -26, 3, 9, P.shirt); circ(-3.5, -28, 2.5, '#44532c'); R(-4, -30, 1, 2, '#999');
    R(1, -17, 5, 2, P.shirtD);
  } else if (arms === 'grenade') {
    R(0, -18, 3, 7, P.shirt); circ(1.5, -9.5, 2.3, '#44532c'); R(-3, -18, 2, 6, P.shirtD);
  } else if (arms === 'knife') {
    R(-1, -17, 4, 2, P.shirtD); R(0, -17, 10, 2, P.shirt); R(9, -17, 2, 2, P.skin); R(11, -18, 6, 1, '#e8eef4'); R(11, -17, 5, 1, '#9aa4ae');
  } else if (arms === 'idle' || !o.weapon) {
    R(-2, -18, 3, 8, P.shirtD); R(1, -18, 3, 8, P.shirt); R(1, -11, 3, 2, P.skin);
    if (o.weapon) { X.save(); X.translate(0, -12); X.rotate(-0.6); drawGun(o.weapon); X.restore(); }
  } else if (o.weapon === 'bazooka') {
    X.save(); X.translate(0, -20); drawGun('bazooka'); X.restore();
    R(-1, -18, 4, 4, P.shirtD); R(2, -17, 4, 2, P.shirt); R(5, -17, 2, 2, P.skin);
  } else if (o.aim === 'up') {
    X.save(); X.translate(2, -18); X.rotate(-Math.PI / 2); drawGun(o.weapon); X.restore();
    R(0, -24, 3, 7, P.shirt); R(1, -25, 3, 2, P.skin);
  } else if (o.aim === 'down') {
    X.save(); X.translate(2, -14); X.rotate(Math.PI / 2); drawGun(o.weapon); X.restore();
    R(0, -17, 3, 5, P.shirt); R(1, -12, 3, 2, P.skin);
  } else {
    R(-1, -17, 5, 3, P.shirtD);
    X.save(); X.translate(3 - (o.recoil || 0), -16); drawGun(o.weapon); X.restore();
    R(0, -16, 6, 2, P.shirt); R(5 - (o.recoil || 0), -16, 2, 2, P.skin);
  }

  if (o.shield) {
    R(5, -27, 5, 27 - dy, '#79838e'); R(5, -27, 1, 27 - dy, '#b3bcc6'); R(9, -27, 1, 27 - dy, '#4a525b');
    R(6, -22, 3, 2, '#1e2328'); R(6, -12, 3, 1, '#5d666f'); R(6, -6, 3, 1, '#5d666f');
  }
  X.restore();
}

function drawChute(t) {
  const sway = Math.sin(t * 0.05) * 0.12;
  X.save();
  X.rotate(sway);
  X.strokeStyle = 'rgba(40,40,40,0.8)'; X.lineWidth = 1;
  X.beginPath();
  X.moveTo(-3, -18); X.lineTo(-15, -44);
  X.moveTo(3, -18); X.lineTo(15, -44);
  X.moveTo(0, -19); X.lineTo(0, -46);
  X.stroke();
  X.fillStyle = '#e9e3cf';
  X.beginPath(); X.ellipse(0, -46, 20, 12, 0, Math.PI, TAU); X.fill();
  X.fillStyle = '#c2413a';
  X.beginPath(); X.ellipse(0, -46, 7, 12, 0, Math.PI, TAU); X.fill();
  R(-20, -46, 40, 2, '#8e8a7a');
  X.restore();
}

/* ---- vehicles and emplacements ---- */
function drawMiniTank(e) {
  X.save();
  X.translate(Math.round(e.x), Math.round(e.y));
  if (e.f < 0) X.scale(-1, 1);
  oval(0, 0, 24, 2, 'rgba(0,0,0,0.35)');
  rrect(-22, -10, 44, 10, 4, '#2a2a28');
  const off = (e.tread || 0) % 4;
  for (let k = 0; k < 11; k++) { R(-21 + ((k * 4 + off + 44) % 42), -10, 2, 1, '#4c4c46'); R(-21 + ((k * 4 - off + 44) % 42), -1, 2, 1, '#191917'); }
  for (let i = 0; i < 5; i++) { circ(-16 + i * 8, -5, 3.2, '#55574e'); circ(-16 + i * 8, -5, 1.2, '#8b8d80'); }
  const pal = e.pal;
  poly([-21, -10, 21, -10, 17, -18, -19, -18], pal.hull);
  R(-19, -18, 36, 1, pal.hullL); R(-21, -11, 42, 1, pal.hullD);
  for (let i = -16; i < 16; i += 6) R(i, -15, 1, 1, pal.hullD);
  rrect(-9, -25, 18, 8, 3, pal.hull); R(-7, -25, 12, 1, pal.hullL); R(-4, -27, 6, 2, pal.hullD);
  X.save();
  X.translate(6, -21);
  X.rotate(e.aim || 0);
  R(0, -1, 16, 3, '#3a3d33'); R(0, -1, 16, 1, '#5a5e50'); R(14, -2, 4, 5, '#2a2c25');
  X.restore();
  circ(-2, -14, 2.5, '#b8322a'); R(-3, -15, 2, 1, '#f0e0c0');
  X.restore();
}

function drawDrone(e) {
  X.save();
  X.translate(Math.round(e.x), Math.round(e.y));
  if (e.f < 0) X.scale(-1, 1);
  const body = e.pal.hull;
  R(-20, -6, 14, 3, body); R(-24, -10, 4, 7, e.pal.hullD);
  const tr = (e.t * 0.9) % TAU;
  oval(-22, -7, 1.5, 5 * Math.abs(Math.cos(tr)), 'rgba(30,30,30,0.6)');
  oval(0, -5, 11, 6, body);
  oval(0, -7, 10, 3, e.pal.hullL);
  oval(5, -5, 5, 4, '#2b4a63'); oval(6, -6, 2.5, 1.5, '#9fd6ff');
  R(-6, 0, 14, 1, '#2a2a2a'); R(-4, -1, 1, 2, '#2a2a2a'); R(5, -1, 1, 2, '#2a2a2a');
  R(-1, -13, 2, 3, '#2a2a2a');
  const L = 18 * Math.cos(e.t * 0.8);
  R(-Math.abs(L), -13, Math.abs(L) * 2, 1, '#1d1d1d');
  oval(0, -13, 18, 1.4, 'rgba(40,40,40,0.25)');
  if ((e.t >> 3) & 1) R(-9, -3, 1, 1, '#ff4a3a');
  X.restore();
}

function drawTurret(e) {
  X.save();
  X.translate(Math.round(e.x), Math.round(e.y));
  const pal = e.pal;
  poly([-14, 0, 14, 0, 10, -14, -10, -14], pal.bunker);
  R(-10, -14, 20, 2, pal.bunkerL);
  R(-14, -2, 28, 2, pal.bunkerD);
  for (let i = -12; i < 12; i += 5) R(i, -9, 3, 1, pal.bunkerD);
  X.save();
  X.translate(0, -15);
  X.rotate(e.aim || Math.PI);
  R(0, -2, 15, 4, '#3a3d42'); R(0, -2, 15, 1, '#6d737b'); R(13, -3, 3, 6, '#26282c');
  X.restore();
  circ(0, -15, 5, '#50565e'); circ(-1, -16, 2, '#8d949c');
  if (e.cd < 20 && (e.t >> 2) & 1) circ(0, -15, 1.5, '#ff5a3a');
  X.restore();
}

function drawCrate(c) {
  X.save();
  X.translate(Math.round(c.x), Math.round(c.y));
  R(-9, -16, 18, 16, '#9c6b36'); R(-9, -16, 18, 2, '#c38b4c'); R(-9, -2, 18, 2, '#6b4520');
  R(-9, -16, 2, 16, '#5e3d1c'); R(7, -16, 2, 16, '#5e3d1c');
  line(-7, -14, 7, -2, '#6b4520', 2);
  R(-9, -16, 3, 3, '#8a8f96'); R(6, -16, 3, 3, '#8a8f96'); R(-9, -3, 3, 3, '#8a8f96'); R(6, -3, 3, 3, '#8a8f96');
  R(-3, -11, 6, 4, 'rgba(20,20,20,0.35)');
  X.restore();
}

const ITEM_COL = { H: '#e39b2d', R: '#4aa55a', F: '#d9442f', S: '#a86a2e', L: '#2fb6ff', B: '#6b7a3a' };
function drawItem(it, t) {
  const bob = Math.round(Math.sin(t * 0.12) * 1.5);
  X.save();
  X.translate(Math.round(it.x), Math.round(it.y) + bob - 1);
  if (ITEM_COL[it.kind]) {
    R(-7, -14, 14, 13, '#1b1b1b');
    R(-6, -13, 12, 11, ITEM_COL[it.kind]);
    R(-6, -13, 12, 2, 'rgba(255,255,255,0.35)');
    R(-6, -4, 12, 2, 'rgba(0,0,0,0.25)');
    X.fillStyle = '#fff';
    X.font = '8px "Press Start 2P", monospace';
    X.textAlign = 'center'; X.textBaseline = 'alphabetic';
    X.fillText(it.kind, 0.5, -3.5);
  } else if (it.kind === 'food') {
    oval(0, -5, 7, 4, '#9b4a26'); oval(-1, -6, 5, 2.5, '#c96a3a'); R(5, -6, 5, 2, '#f1e7d0'); circ(10, -7, 1.5, '#f1e7d0'); circ(10, -4.5, 1.5, '#f1e7d0');
  } else if (it.kind === 'gem') {
    poly([0, -13, 6, -7, 0, -1, -6, -7], '#39c6ff'); poly([0, -13, 6, -7, 0, -7], '#b4ecff'); poly([0, -7, 0, -1, -6, -7], '#1b86c4');
  } else if (it.kind === 'medal') {
    R(-2, -14, 4, 5, '#d8342a'); circ(0, -6, 4.5, '#e9b44c'); circ(0, -6, 2.5, '#f7d77e');
  }
  X.restore();
}

/* ---- effects ---- */
function fireColor(k) {
  return k < 0.12 ? '#fff6d8' : k < 0.3 ? '#ffd35a' : k < 0.5 ? '#ff9a2e' : k < 0.7 ? '#e2531f' : k < 0.85 ? '#7a3322' : '#4a3a34';
}

function drawBeam(x1, y1, x2, y2, w, core, glowCol) {
  X.save();
  X.globalCompositeOperation = 'lighter';
  X.lineCap = 'round';
  X.strokeStyle = glowCol; X.globalAlpha = 0.35; X.lineWidth = w * 2.4;
  X.beginPath(); X.moveTo(x1, y1); X.lineTo(x2, y2); X.stroke();
  X.globalAlpha = 0.8; X.lineWidth = w;
  X.beginPath(); X.moveTo(x1, y1); X.lineTo(x2, y2); X.stroke();
  X.globalAlpha = 1; X.strokeStyle = core; X.lineWidth = Math.max(1, w * 0.4);
  X.beginPath(); X.moveTo(x1, y1); X.lineTo(x2, y2); X.stroke();
  X.restore();
}

function pxText(s, x, y, col, align = 'left', size = 8) {
  g.font = `${size}px "Press Start 2P", monospace`;
  g.textAlign = align;
  g.textBaseline = 'top';
  g.fillStyle = 'rgba(0,0,0,0.75)';
  g.fillText(s, x + 1, y + 1);
  g.fillStyle = col;
  g.fillText(s, x, y);
}
