'use strict';
/* Iron Assault — shared constants, math helpers and input handling. */

const W = 480, H = 270, TAU = Math.PI * 2, GRAV = 0.32, STEP = 1000 / 60;

const clamp = (v, a, b) => (v < a ? a : v > b ? b : v);
const lerp = (a, b, t) => a + (b - a) * t;
const rand = (a, b) => (b === undefined ? Math.random() * a : a + Math.random() * (b - a));
const randi = (a, b) => Math.floor(a + Math.random() * (b - a + 1));
const chance = p => Math.random() < p;
const pick = arr => arr[(Math.random() * arr.length) | 0];
const sign = v => (v < 0 ? -1 : 1);
const approach = (v, t, d) => (v < t ? Math.min(v + d, t) : Math.max(v - d, t));
const pad = (n, len) => String(Math.max(0, Math.floor(n))).padStart(len, '0');

function mulberry32(seed) {
  let a = seed >>> 0;
  return function () {
    a = (a + 0x6d2b79f5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

function overlap(a, b) {
  return a.x < b.x + b.w && a.x + a.w > b.x && a.y < b.y + b.h && a.y + a.h > b.y;
}

function circleRect(cx, cy, r, b) {
  const nx = clamp(cx, b.x, b.x + b.w), ny = clamp(cy, b.y, b.y + b.h);
  return (cx - nx) * (cx - nx) + (cy - ny) * (cy - ny) <= r * r;
}

function weighted(map, rng = Math.random) {
  let tot = 0;
  for (const k in map) tot += map[k];
  let r = rng() * tot;
  for (const k in map) {
    r -= map[k];
    if (r <= 0) return k;
  }
  return Object.keys(map)[0];
}

/** Remove entries flagged `dead` in place. */
function sweep(arr) {
  let j = 0;
  for (let i = 0; i < arr.length; i++) if (!arr[i].dead) arr[j++] = arr[i];
  arr.length = j;
}

function rotVec(d, a) {
  const c = Math.cos(a), s = Math.sin(a);
  return { x: d.x * c - d.y * s, y: d.x * s + d.y * c };
}

/* ---------------------------------------------------------------- input */

const ACTIONS = ['left', 'right', 'up', 'down', 'fire', 'jump', 'bomb', 'pause'];

const Input = {
  down: Object.create(null),
  pad: Object.create(null),
  pressed: Object.create(null),
  active: false,
  map: {
    ArrowLeft: 'left', KeyA: 'left',
    ArrowRight: 'right', KeyD: 'right',
    ArrowUp: 'up', KeyW: 'up',
    ArrowDown: 'down', KeyS: 'down',
    KeyJ: 'fire', KeyZ: 'fire',
    KeyK: 'jump', KeyX: 'jump', Space: 'jump',
    KeyL: 'bomb', KeyC: 'bomb',
    KeyP: 'pause', Escape: 'pause',
  },
  press(a) {
    if (!this.down[a]) this.pressed[a] = true;
    this.down[a] = true;
  },
  release(a) { this.down[a] = false; },
  is(a) { return !!(this.down[a] || this.pad[a]); },
  hit(a) { return !!this.pressed[a]; },
  consume() { for (const k in this.pressed) delete this.pressed[k]; },
  clear() {
    for (const k in this.down) this.down[k] = false;
    this.consume();
  },
};

addEventListener('keydown', e => {
  if (typeof onUserGesture === 'function') onUserGesture();
  const a = Input.map[e.code];
  if (!a) return;
  if (Input.active) {
    e.preventDefault();
    if (!e.repeat) Input.press(a);
  } else if (a === 'pause' && !e.repeat && typeof onMenuBack === 'function') {
    e.preventDefault();
    onMenuBack();
  }
});
addEventListener('keyup', e => {
  const a = Input.map[e.code];
  if (a) Input.release(a);
});
addEventListener('blur', () => Input.clear());

const PAD_BUTTONS = [['jump', 0], ['bomb', 1], ['fire', 2], ['fire', 3], ['fire', 7], ['pause', 9]];

function pollPad() {
  let p = null;
  try {
    const pads = navigator.getGamepads ? navigator.getGamepads() : null;
    p = pads && (pads[0] || pads[1]);
  } catch (e) { p = null; }
  const now = {};
  if (p) {
    const b = i => p.buttons[i] && p.buttons[i].pressed;
    for (const [a, i] of PAD_BUTTONS) if (b(i)) now[a] = true;
    const ax = p.axes[0] || 0, ay = p.axes[1] || 0;
    if (ax < -0.4 || b(14)) now.left = true;
    if (ax > 0.4 || b(15)) now.right = true;
    if (ay < -0.55 || b(12)) now.up = true;
    if (ay > 0.55 || b(13)) now.down = true;
  }
  for (const a of ACTIONS) {
    if (now[a] && !Input.pad[a]) Input.pressed[a] = true;
    Input.pad[a] = !!now[a];
  }
}
