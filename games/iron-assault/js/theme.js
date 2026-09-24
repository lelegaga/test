'use strict';
/* Iron Assault — per-mission scenery: sky, parallax layers, ground
   textures, platforms and set dressing. Layers are painted once into
   seamless 960px tiles (all ridge functions use integer frequencies). */

const TW = 960;

function ridgeFn(rng, terms) {
  const t = terms.map(([k, a]) => [k, a, rng() * TAU]);
  return x => {
    let s = 0;
    for (const [k, a, p] of t) s += a * Math.sin((TAU * k * x) / TW + p);
    return s;
  };
}
function fillRidge(f, base, fill) {
  X.fillStyle = fill;
  X.beginPath();
  X.moveTo(0, H);
  for (let x = 0; x <= TW; x += 3) X.lineTo(x, base + f(x));
  X.lineTo(TW, H);
  X.closePath();
  X.fill();
}
function wrap(x, w, fn) {
  fn(x);
  if (x - w < 0) fn(x + TW);
  if (x + w > TW) fn(x - TW);
}
function cloud(x, y, s, col) {
  X.fillStyle = col;
  for (const [dx, dy, r] of [[0, 0, 14], [14, -7, 12], [28, -1, 13], [-13, 3, 9], [41, 4, 8], [20, 5, 11]]) {
    X.beginPath(); X.arc(x + dx * s, y + dy * s, r * s, 0, TAU); X.fill();
  }
}
function sunGlow(x, y, r, col, a = 1) {
  const gr = X.createRadialGradient(x, y, 0, x, y, r);
  gr.addColorStop(0, `rgba(${col},${a})`);
  gr.addColorStop(0.12, `rgba(${col},${0.7 * a})`);
  gr.addColorStop(0.4, `rgba(${col},${0.18 * a})`);
  gr.addColorStop(1, `rgba(${col},0)`);
  X.fillStyle = gr;
  X.fillRect(x - r, y - r, r * 2, r * 2);
}
function hazeBand(y0, y1, col) {
  X.fillStyle = vgrad(y0, y1, [[0, `rgba(${col},0)`], [1, `rgba(${col},0.75)`]]);
  X.fillRect(0, y0, TW, y1 - y0);
  X.fillStyle = `rgba(${col},0.75)`;
  X.fillRect(0, y1, TW, H - y1);
}
function speckle(rng, w, h, cols, n, size = 1) {
  for (let i = 0; i < n; i++) R((rng() * w) | 0, (rng() * h) | 0, size, size, cols[(rng() * cols.length) | 0]);
}
function pine(x, y, h, body, snow) {
  R(x - 1, y - 6, 3, 6, '#3a2a20');
  const tiers = Math.max(3, Math.round(h / 16));
  for (let i = 0; i < tiers; i++) {
    const ty = y - 4 - i * (h / tiers) * 0.8;
    const tw = (h * 0.34) * (1 - i / (tiers + 1));
    poly([x - tw, ty, x + tw, ty, x, ty - h / tiers * 1.4], body);
    if (snow) poly([x - tw * 0.55, ty - h / tiers * 0.6, x + tw * 0.25, ty - h / tiers * 0.7, x, ty - h / tiers * 1.4], snow);
  }
}
function building(x, y, w, h, body, winCol, rng, winChance, broken) {
  R(x, y - h, w, h, body);
  if (broken) {
    X.fillStyle = 'rgba(0,0,0,0)';
    const k = Math.max(3, (w / 6) | 0);
    for (let i = 0; i < k; i++) {
      const bx = x + (i / k) * w;
      poly([bx, y - h, bx + w / k, y - h, bx + w / k / 2, y - h + rng() * 14 + 2], body);
    }
    X.save(); X.globalCompositeOperation = 'destination-out';
    for (let i = 0; i < k; i++) {
      const bx = x + (i / k) * w;
      poly([bx, y - h - 1, bx + w / k, y - h - 1, bx + (w / k) * rng(), y - h + rng() * 12], '#000');
    }
    X.restore();
  }
  for (let wy = y - h + 6; wy < y - 6; wy += 6) {
    for (let wx = x + 3; wx < x + w - 3; wx += 5) {
      if (rng() < winChance) R(wx, wy, 2, 3, winCol);
    }
  }
}

/* ============================================================ THEMES */
const THEMES = {
  jungle: {
    pal: PAL.jungle,
    veh: { hull: '#6f7d3a', hullL: '#98a856', hullD: '#48521f', bunker: '#8c8a7a', bunkerL: '#b3b09c', bunkerD: '#5f5d52' },
    dark: 0, weather: 'leaves', wall: ['#8a6644', '#3e2a18'],
    deco: { bush: 4, palm: 2, hut: 0.6, sandbags: 1, rock: 1.2, sign: 0.6, wreck: 0.5, barrel: 0.8 },
    sky() {
      X.fillStyle = vgrad(0, H, [[0, '#3f8fc0'], [0.45, '#8fcbd0'], [0.72, '#d6ecc8'], [1, '#eaf0c6']]);
      X.fillRect(0, 0, W, H);
      sunGlow(392, 44, 150, '255,248,214');
      circ(392, 44, 11, '#fffbe6');
      cloud(40, 54, 1.1, 'rgba(255,255,255,0.72)');
      cloud(210, 30, 0.8, 'rgba(255,255,255,0.6)');
      cloud(300, 92, 0.65, 'rgba(255,255,255,0.5)');
      X.save(); X.globalAlpha = 0.07; X.fillStyle = '#fffbe0';
      for (const [a, b] of [[300, 360], [340, 420], [410, 470]]) poly([a, 0, b, 0, b - 200, H, a - 260, H], '#fffbe0');
      X.restore();
    },
    layers: [
      { p: 0.07, paint(rng) {
        fillRidge(ridgeFn(rng, [[1, 14], [2, 9], [3, 6], [7, 3], [13, 1.5]]), 128, '#93c2b1');
        fillRidge(ridgeFn(rng, [[2, 10], [3, 8], [5, 4], [11, 2]]), 148, '#77ab96');
        hazeBand(120, 210, '226,243,226');
      } },
      { p: 0.2, paint(rng) {
        for (let i = 0; i < 9; i++) {
          const x = rng() * TW, h = 60 + rng() * 40;
          wrap(x, 30, xx => { R(xx - 2, 172 - h, 4, h, '#34583c'); circ(xx, 172 - h, 16, '#3c7249'); circ(xx - 6, 176 - h, 10, '#4a8a55'); });
        }
        for (let i = 0; i < 80; i++) {
          const x = rng() * TW, y = 150 + rng() * 26, r = 10 + rng() * 14;
          wrap(x, r + 4, xx => { circ(xx, y, r, '#3d774a'); circ(xx - r * 0.3, y - r * 0.35, r * 0.55, '#4f9058'); });
        }
        R(0, 168, TW, H, '#3a7248');
        hazeBand(168, 214, '205,232,205');
      } },
      { p: 0.48, paint(rng) {
        for (let i = 0; i < 6; i++) {
          const x = i * 160 + rng() * 70, w = 12 + rng() * 10;
          wrap(x, w + 20, xx => {
            X.fillStyle = (() => { const gr = X.createLinearGradient(xx - w / 2, 0, xx + w / 2, 0); gr.addColorStop(0, '#2c2016'); gr.addColorStop(0.4, '#54402c'); gr.addColorStop(1, '#261b12'); return gr; })();
            X.fillRect(xx - w / 2, 0, w, 214);
            poly([xx - w / 2 - 8, 214, xx - w / 2, 190, xx - w / 2, 214], '#2c2016');
            poly([xx + w / 2 + 8, 214, xx + w / 2, 190, xx + w / 2, 214], '#261b12');
            for (let y = 10; y < 210; y += 14) R(xx - w / 2 + 2 + ((y * 7) % (w - 4)), y, 2, 6, '#3a2c1e');
          });
        }
        X.strokeStyle = '#2e5a2e'; X.lineWidth = 2;
        for (let i = 0; i < 16; i++) {
          const x = rng() * TW, len = 40 + rng() * 90;
          wrap(x, 10, xx => {
            X.beginPath(); X.moveTo(xx, 0);
            X.quadraticCurveTo(xx + 8, len / 2, xx - 2, len); X.stroke();
            for (let y = 10; y < len; y += 12) oval(xx + 3 + Math.sin(y) * 3, y, 3, 1.6, '#3f7a3a', 0.6);
          });
        }
        for (let i = 0; i < 40; i++) {
          const x = rng() * TW;
          wrap(x, 30, xx => { circ(xx, rng() * 18 - 4, 16 + rng() * 10, '#1f4a2a'); circ(xx + 6, rng() * 10, 9, '#2c5f35'); });
        }
        for (let i = 0; i < 50; i++) {
          const x = rng() * TW;
          wrap(x, 20, xx => { circ(xx, 206 + rng() * 6, 7 + rng() * 8, '#2b5c31'); circ(xx - 3, 203, 5, '#3c7a40'); });
        }
      } },
    ],
    ground(rng) {
      R(0, 0, 64, 64, '#6d4b2f');
      speckle(rng, 64, 64, ['#5a3d25', '#7c5a38', '#61432a'], 180);
      for (let i = 0; i < 5; i++) { const x = rng() * 60, y = rng() * 60; oval(x, y, 3 + rng() * 2, 2, '#877660'); oval(x - 1, y - 1, 2, 1, '#a8977d'); }
      line(rng() * 64, rng() * 64, rng() * 64, rng() * 64, '#4a3220', 1);
    },
    top(rng) {
      R(0, 4, 64, 5, '#3f8f2e'); R(0, 4, 64, 1, '#6cc44d'); R(0, 8, 64, 1, '#2d6a22');
      for (let x = 0; x < 64; x++) { const h = (rng() * 4) | 0; if (h) R(x, 4 - h, 1, h, rng() < 0.5 ? '#62b845' : '#4fa23a'); }
      X.fillStyle = vgrad(9, 16, [[0, 'rgba(0,0,0,0.3)'], [1, 'rgba(0,0,0,0)']]); X.fillRect(0, 9, 64, 7);
    },
    plat(x, y, w, gy) {
      R(x + 3, y + 3, 3, gy - y - 3, '#5e3b1f'); R(x + w - 6, y + 3, 3, gy - y - 3, '#5e3b1f');
      line(x + 4, y + 8, x + w - 5, gy - 6, '#6b4a2a', 1); line(x + w - 5, y + 8, x + 4, gy - 6, '#6b4a2a', 1);
      R(x, y, w, 5, '#8a5a32'); R(x, y, w, 1, '#b8844f'); R(x, y + 4, w, 1, '#5a381c');
      for (let i = x + 7; i < x + w; i += 8) R(i, y + 1, 1, 3, '#6a4424');
    },
  },

  desert: {
    pal: PAL.desert,
    veh: { hull: '#b8975c', hullL: '#dcc084', hullD: '#7d633a', bunker: '#c9a26a', bunkerL: '#e6c68e', bunkerD: '#8e7043' },
    dark: 0, weather: 'sand', wall: ['#e8c07e', '#8e6636'],
    deco: { palm: 1.5, pillar: 1.8, rock: 1.5, sandbags: 1, wreck: 0.8, barrel: 0.6, sign: 0.4 },
    sky() {
      X.fillStyle = vgrad(0, H, [[0, '#2d2b5e'], [0.35, '#7c4a7c'], [0.6, '#e0785a'], [0.82, '#f7b265'], [1, '#ffd98f']]);
      X.fillRect(0, 0, W, H);
      for (let i = 0; i < 40; i++) R((Math.sin(i * 91.7) * 0.5 + 0.5) * W | 0, (Math.sin(i * 47.3) * 0.5 + 0.5) * 60 | 0, 1, 1, 'rgba(255,240,220,0.6)');
      sunGlow(150, 150, 170, '255,214,150');
      circ(150, 150, 26, '#fff0c4');
      X.fillStyle = 'rgba(220,120,110,0.35)';
      for (let i = 0; i < 4; i++) R(0, 120 + i * 11, W, 2 + i, 'rgba(200,100,110,0.22)');
    },
    layers: [
      { p: 0.05, paint(rng) {
        const f = ridgeFn(rng, [[1, 20], [2, 14], [4, 10], [9, 4]]);
        fillRidge(x => clamp(f(x), -26, 6) * 1.2, 150, '#9c5767');
        hazeBand(140, 200, '240,160,120');
      } },
      { p: 0.17, paint(rng) {
        for (let i = 0; i < 3; i++) {
          const x = 120 + i * 320 + rng() * 80, w = 70 + rng() * 70, h = w * 0.62;
          wrap(x, w, xx => {
            poly([xx - w / 2, 188, xx, 188 - h, xx, 188], '#df9a66');
            poly([xx, 188 - h, xx + w / 2, 188, xx, 188], '#b56847');
            for (let y = 188 - h + 6; y < 188; y += 5) R(xx - (188 - y) * 0.8 + 2, y, (188 - y) * 1.6 - 4, 1, 'rgba(90,40,30,0.18)');
          });
        }
        const f = ridgeFn(rng, [[1, 8], [3, 6], [5, 4]]);
        fillRidge(f, 184, '#cf8a5b');
        fillRidge(x => f(x) * 0.6 + 4, 192, '#d99965');
      } },
      { p: 0.44, paint(rng) {
        for (let i = 0; i < 7; i++) {
          const x = rng() * TW, h = 40 + rng() * 70, broken = rng() < 0.7;
          wrap(x, 20, xx => {
            R(xx - 7, 214 - h, 14, h, '#a86240');
            R(xx - 7, 214 - h, 3, h, '#c77d52'); R(xx + 4, 214 - h, 3, h, '#80472c');
            for (let k = 214 - h + 10; k < 214; k += 12) R(xx - 7, k, 14, 1, '#6e3b24');
            if (broken) poly([xx - 7, 214 - h, xx - 2, 214 - h - 7, xx + 3, 214 - h - 2, xx + 7, 214 - h - 5, xx + 7, 214 - h], '#a86240');
            else { R(xx - 10, 214 - h - 5, 20, 5, '#c07a50'); R(xx - 10, 214 - h - 5, 20, 1, '#dc9868'); }
          });
        }
        for (let i = 0; i < 12; i++) {
          const x = rng() * TW, s = 5 + rng() * 10;
          wrap(x, 20, xx => { poly([xx - s * 1.4, 214, xx - s * 0.6, 214 - s, xx + s * 0.8, 214 - s * 0.8, xx + s * 1.5, 214], '#8a4f33'); poly([xx - s * 0.6, 214 - s, xx + s * 0.8, 214 - s * 0.8, xx, 214 - s * 0.4], '#a8653f'); });
        }
      } },
    ],
    ground(rng) {
      R(0, 0, 64, 64, '#d9a462');
      speckle(rng, 64, 64, ['#c98f4f', '#ecc07e', '#d09652'], 200);
      for (let y = 18; y < 64; y += 22) R(0, y, 64, 1, 'rgba(140,80,40,0.25)');
      for (let i = 0; i < 4; i++) oval(rng() * 64, rng() * 64, 2, 1.3, '#a8784a');
    },
    top(rng) {
      R(0, 4, 64, 5, '#e8b772'); R(0, 3, 64, 2, '#f7d89e');
      for (let x = 0; x < 64; x += 4) R(x + ((rng() * 3) | 0), 6 + ((rng() * 2) | 0), 2, 1, '#d29a57');
      X.fillStyle = vgrad(9, 16, [[0, 'rgba(120,60,20,0.25)'], [1, 'rgba(120,60,20,0)']]); X.fillRect(0, 9, 64, 7);
    },
    plat(x, y, w, gy) {
      R(x + 4, y + 8, 8, gy - y - 8, '#b8794a'); R(x + w - 12, y + 8, 8, gy - y - 8, '#b8794a');
      R(x + 4, y + 8, 2, gy - y - 8, '#d9985f'); R(x + w - 12, y + 8, 2, gy - y - 8, '#d9985f');
      R(x, y, w, 8, '#c98f55'); R(x, y, w, 2, '#ecb97c'); R(x, y + 7, w, 1, '#8e5c34');
      for (let i = x + 14; i < x + w; i += 16) R(i, y + 2, 1, 5, '#a26c40');
    },
  },

  snow: {
    pal: PAL.snow,
    veh: { hull: '#c9d3dd', hullL: '#f1f5f9', hullD: '#8795a6', bunker: '#9aa8b8', bunkerL: '#e8eef4', bunkerD: '#6b7888' },
    dark: 0, weather: 'snow', wall: ['#ffffff', '#8aa0b8'],
    deco: { pine: 4, snowman: 0.6, fence: 1, barrel: 0.8, rock: 1, sandbags: 1 },
    sky() {
      X.fillStyle = vgrad(0, H, [[0, '#5b82b8'], [0.5, '#a8c3e0'], [0.85, '#dfe9f3'], [1, '#eef3f8']]);
      X.fillRect(0, 0, W, H);
      sunGlow(330, 52, 110, '255,255,245', 0.8);
      circ(330, 52, 9, '#fbfdff');
      cloud(90, 70, 1, 'rgba(255,255,255,0.55)');
      cloud(420, 110, 0.7, 'rgba(255,255,255,0.45)');
    },
    layers: [
      { p: 0.06, paint(rng) {
        const f = ridgeFn(rng, [[2, 22], [3, 16], [5, 10], [9, 6], [17, 3]]);
        const y = x => 128 - Math.abs(f(x)) * 1.1;
        fillRidge(x => -Math.abs(f(x)) * 1.1, 128, '#e6eef7');
        for (let x = 0; x < TW; x += 2) if (y(x + 2) > y(x)) R(x, y(x), 2, 70, 'rgba(100,130,175,0.32)');
        hazeBand(120, 200, '220,232,245');
      } },
      { p: 0.2, paint(rng) {
        for (let i = 0; i < 90; i++) {
          const x = rng() * TW, h = 22 + rng() * 22, by = 176 + rng() * 10;
          wrap(x, 14, xx => pine(xx, by, h, '#3b5f73', 'rgba(240,246,252,0.9)'));
        }
        R(0, 182, TW, H, '#3b5f73');
        hazeBand(176, 214, '225,236,248');
      } },
      { p: 0.45, paint(rng) {
        for (let i = 0; i < 8; i++) {
          const x = i * 120 + rng() * 60, h = 90 + rng() * 60;
          wrap(x, 40, xx => pine(xx, 214, h, '#24435a', '#eaf2f9'));
        }
        for (let i = 0; i < 30; i++) {
          const x = rng() * TW;
          wrap(x, 20, xx => oval(xx, 212, 14 + rng() * 10, 6, '#f2f7fc'));
        }
      } },
    ],
    ground(rng) {
      R(0, 0, 64, 64, '#7d93ab');
      speckle(rng, 64, 64, ['#6a8098', '#8fa6bd', '#b9cadb'], 170);
      for (let i = 0; i < 3; i++) line(rng() * 64, rng() * 64, rng() * 64, rng() * 64, '#62788f', 1);
    },
    top(rng) {
      R(0, 2, 64, 8, '#f6fafd'); R(0, 9, 64, 1, '#cddcec');
      for (let x = 0; x < 64; x += 6) oval(x + 3, 3, 4, 2, '#ffffff');
      for (let x = 0; x < 64; x += 3 + ((rng() * 4) | 0)) R(x, 10, 1, 1 + ((rng() * 4) | 0), '#dfeaf5');
    },
    plat(x, y, w, gy) {
      R(x + 5, y + 5, 4, gy - y - 5, '#5a6a7a'); R(x + w - 9, y + 5, 4, gy - y - 5, '#5a6a7a');
      R(x, y, w, 6, '#b7d3ec'); R(x, y + 5, w, 1, '#8fb2d2'); R(x, y - 2, w, 3, '#ffffff');
      for (let i = x + 3; i < x + w - 2; i += 5) R(i, y + 6, 1, 2 + ((i * 7) % 4), '#cfe3f5');
    },
  },

  city: {
    pal: PAL.city,
    veh: { hull: '#5a6272', hullL: '#848ea2', hullD: '#393f4c', bunker: '#6a6d78', bunkerL: '#8e919c', bunkerD: '#45474f' },
    dark: 0.5, darkCol: 'rgba(6,8,24,', weather: 'rain', wall: ['#8d91a3', '#2a2c36'],
    deco: { car: 1.4, lamp: 1.6, firebarrel: 1, fence: 0.8, barrel: 0.6, sandbags: 1, wreck: 0.6 },
    sky() {
      X.fillStyle = vgrad(0, H, [[0, '#060818'], [0.45, '#141638'], [0.75, '#3a1f45'], [1, '#7a3434']]);
      X.fillRect(0, 0, W, H);
      const r = mulberry32(9);
      for (let i = 0; i < 90; i++) R((r() * W) | 0, (r() * 130) | 0, 1, 1, `rgba(255,255,240,${0.3 + r() * 0.6})`);
      sunGlow(96, 50, 70, '230,230,255', 0.5);
      circ(96, 50, 13, '#f1ecd8'); circ(92, 47, 3, '#d8d0b6'); circ(100, 54, 2, '#d8d0b6');
    },
    layers: [
      { p: 0.06, paint(rng) {
        for (let x = 0; x < TW;) {
          const w = 18 + rng() * 30, h = 50 + rng() * 100;
          building(x, 206, w, h, '#131731', 'rgba(230,196,106,0.55)', rng, 0.12, rng() < 0.3);
          if (rng() < 0.3) R(x + w / 2, 206 - h - 12, 1, 12, '#131731');
          x += w + rng() * 4;
        }
        hazeBand(150, 214, '70,34,62');
      } },
      { p: 0.18, paint(rng) {
        for (let x = 0; x < TW;) {
          const w = 30 + rng() * 44, h = 60 + rng() * 70;
          building(x, 214, w, h, '#1c2140', 'rgba(240,200,112,0.7)', rng, 0.16, rng() < 0.6);
          if (rng() < 0.3) {
            const fx = x + w / 2;
            sunGlow(fx, 214 - h, 40, '255,120,40', 0.6);
            for (let k = 0; k < 6; k++) oval(fx + (rng() - 0.5) * 16, 214 - h - 6 - rng() * 8, 3, 6, rng() < 0.5 ? '#ff9a3a' : '#ffd060');
            for (let k = 0; k < 6; k++) oval(fx + k * 6, 214 - h - 20 - k * 14, 10 + k * 3, 6 + k * 2, 'rgba(40,36,50,0.45)');
          }
          x += w + 6 + rng() * 20;
        }
      } },
      { p: 0.44, paint(rng) {
        for (let i = 0; i < 5; i++) {
          const x = i * 200 + rng() * 60, w = 70 + rng() * 50, h = 90 + rng() * 60;
          wrap(x, w, xx => {
            R(xx, 214 - h, w, h, '#232842');
            for (let fy = 214 - h + 18; fy < 214; fy += 22) {
              R(xx, fy, w, 2, '#2e3456');
              for (let wx = xx + 6; wx < xx + w - 10; wx += 16) R(wx, fy - 13, 9, 11, rng() < 0.15 ? '#caa55a' : '#0d0f1c');
            }
            poly([xx + w * 0.3, 214 - h, xx + w * 0.6, 214 - h + 30, xx + w, 214 - h + 12, xx + w, 214 - h], '#141731');
            X.save(); X.globalCompositeOperation = 'destination-out';
            poly([xx + w * 0.35, 214 - h - 1, xx + w * 0.6, 214 - h + 28, xx + w + 1, 214 - h + 10, xx + w + 1, 214 - h - 1], '#000');
            X.restore();
            for (let k = 0; k < 4; k++) line(xx + w * 0.4 + k * 6, 214 - h + 10, xx + w * 0.4 + k * 6 + 3, 214 - h - 2, '#6b5a4a');
          });
        }
        for (let i = 0; i < 4; i++) {
          const x = i * 240 + 60;
          wrap(x, 130, xx => {
            R(xx - 1, 110, 3, 104, '#1a1c2c'); R(xx - 8, 114, 17, 2, '#1a1c2c');
            X.strokeStyle = '#11121c'; X.lineWidth = 1;
            X.beginPath(); X.moveTo(xx - 8, 115); X.quadraticCurveTo(xx + 120, 150, xx + 232, 115); X.stroke();
            X.beginPath(); X.moveTo(xx + 8, 115); X.quadraticCurveTo(xx + 120, 144, xx + 248, 115); X.stroke();
          });
        }
      } },
    ],
    ground(rng) {
      R(0, 0, 64, 64, '#2f3039');
      speckle(rng, 64, 64, ['#3a3b45', '#26272e', '#43444f'], 200);
      line(rng() * 64, 0, rng() * 64, 64, '#1f2026', 1);
      oval(rng() * 64, rng() * 64, 10, 2, 'rgba(90,100,140,0.25)');
    },
    top(rng) {
      R(0, 3, 64, 5, '#777b8c'); R(0, 3, 64, 1, '#a2a6b8'); R(0, 7, 64, 2, '#4a4d5a');
      for (let x = 0; x < 64; x += 16) R(x, 3, 1, 5, '#5a5d6b');
      X.fillStyle = vgrad(9, 16, [[0, 'rgba(0,0,0,0.35)'], [1, 'rgba(0,0,0,0)']]); X.fillRect(0, 9, 64, 7);
    },
    plat(x, y, w, gy) {
      R(x + 4, y + 6, 6, gy - y - 6, '#4d505a'); R(x + w - 10, y + 6, 6, gy - y - 6, '#4d505a');
      R(x, y, w, 6, '#6a6d78'); R(x, y, w, 1, '#9194a0'); R(x, y + 5, w, 1, '#3e4049');
      line(x + w - 2, y + 3, x + w + 5, y - 2, '#7a6450'); line(x + 2, y + 4, x - 4, y + 1, '#7a6450');
      for (let i = x + 9; i < x + w; i += 13) R(i, y + 1, 3, 1, '#565963');
    },
  },

  base: {
    pal: PAL.base,
    veh: { hull: '#4b4d56', hullL: '#72757f', hullD: '#2d2e35', bunker: '#565962', bunkerL: '#7d808a', bunkerD: '#34363d' },
    dark: 0.3, darkCol: 'rgba(26,6,6,', weather: 'embers', wall: ['#9aa1ad', '#23252b'],
    deco: { barrel: 1.4, firebarrel: 0.8, console: 0.8, pipe: 0.8, crates: 1.2, sandbags: 0.8, lamp: 0.6 },
    sky() {
      X.fillStyle = vgrad(0, H, [[0, '#12060a'], [0.45, '#3d0f10'], [0.78, '#8c2c14'], [1, '#d65a1c']]);
      X.fillRect(0, 0, W, H);
      const r = mulberry32(5);
      for (let i = 0; i < 14; i++) oval(r() * W, 30 + r() * 90, 40 + r() * 50, 10 + r() * 10, 'rgba(20,6,8,0.35)');
      sunGlow(260, 230, 220, '255,110,40', 0.5);
    },
    layers: [
      { p: 0.06, paint(rng) {
        for (let x = 0; x < TW;) {
          const kind = rng();
          if (kind < 0.35) {
            const h = 80 + rng() * 60;
            R(x, 206 - h, 8, h, '#2a1312'); R(x - 1, 206 - h, 10, 4, '#3a1a18'); R(x + 3, 206 - h - 2, 2, 2, '#ff3a2a');
            for (let k = 0; k < 5; k++) oval(x + 4 + k * 5, 200 - h - k * 12, 6 + k * 3, 4 + k * 2, 'rgba(40,20,22,0.5)');
            x += 18 + rng() * 20;
          } else {
            const w = 30 + rng() * 40, h = 30 + rng() * 40;
            R(x, 206 - h, w, h, '#2a1312');
            if (kind > 0.7) oval(x + w / 2, 206 - h, w / 2, 8, '#2a1312');
            x += w + rng() * 10;
          }
        }
        hazeBand(160, 214, '140,44,20');
      } },
      { p: 0.18, paint(rng) {
        for (let i = 0; i < 7; i++) {
          const x = rng() * TW, w = 34 + rng() * 30, h = 40 + rng() * 40;
          wrap(x, w, xx => {
            R(xx, 214 - h, w, h, '#3a1a16'); oval(xx + w / 2, 214 - h, w / 2, 6, '#4e241d');
            R(xx + 4, 214 - h, 3, h, '#55291f');
            for (let y = 214 - h + 8; y < 214; y += 10) R(xx, y, w, 1, '#2a120f');
            R(xx + w - 8, 214 - h - 8, 2, 8, '#2a120f'); R(xx + w - 9, 214 - h - 10, 4, 2, '#ff4a2a');
          });
        }
        for (let y of [170, 186]) { R(0, y, TW, 4, '#2e1411'); R(0, y, TW, 1, '#4a221c'); }
        for (let i = 0; i < 3; i++) {
          const x = rng() * TW;
          wrap(x, 60, xx => {
            X.strokeStyle = '#2a1210'; X.lineWidth = 1;
            for (let y = 90; y < 214; y += 8) { line(xx, y, xx + 8, y + 8, '#2a1210'); line(xx + 8, y, xx, y + 8, '#2a1210'); }
            R(xx - 1, 90, 2, 124, '#2a1210'); R(xx + 7, 90, 2, 124, '#2a1210');
            R(xx - 40, 86, 90, 4, '#2a1210'); line(xx + 48, 90, xx + 48, 130, '#1a0a09');
          });
        }
      } },
      { p: 0.44, paint(rng) {
        for (let i = 0; i < 5; i++) {
          const x = i * 200 + rng() * 40;
          wrap(x, 120, xx => {
            R(xx, 0, 12, 214, '#1c0f0e'); R(xx - 3, 0, 18, 3, '#2a1614'); R(xx + 2, 0, 2, 214, '#2c1816');
            for (let y = 8; y < 214; y += 16) { R(xx + 2, y, 1, 1, '#4a2a24'); R(xx + 9, y, 1, 1, '#4a2a24'); }
            X.strokeStyle = '#1c0f0e'; X.lineWidth = 3;
            X.beginPath(); X.moveTo(xx + 12, 20); X.lineTo(xx + 110, 90); X.moveTo(xx + 12, 90); X.lineTo(xx + 110, 20); X.stroke();
            R(xx + 12, 18, 100, 4, '#1c0f0e');
            for (let k = 0; k < 8; k++) R(xx + 60, 22 + k * 5, 1, 3, '#2d1a17');
            R(xx + 57, 62, 7, 3, '#2d1a17');
          });
        }
      } },
    ],
    ground(rng) {
      R(0, 0, 64, 64, '#4a4f59');
      R(0, 0, 64, 1, '#5d636e'); R(0, 31, 64, 2, '#353941'); R(31, 0, 2, 64, '#353941');
      for (const [x, y] of [[3, 3], [28, 3], [3, 28], [28, 28], [36, 3], [60, 3], [36, 36], [60, 60], [3, 60], [28, 60]]) { R(x, y, 2, 2, '#6d7380'); R(x + 1, y + 1, 1, 1, '#2f3239'); }
      speckle(rng, 64, 64, ['#555a65', '#434852'], 60);
      oval(rng() * 64, rng() * 64, 5, 3, 'rgba(120,60,30,0.35)');
    },
    top(rng) {
      R(0, 3, 64, 1, '#b5bcc8');
      for (let x = -8; x < 64; x += 8) poly([x, 4, x + 4, 4, x + 8, 9, x + 4, 9], '#e0b22a');
      for (let x = -4; x < 64; x += 8) poly([x, 4, x + 4, 4, x + 8, 9, x + 4, 9], '#1c1c1c');
      R(0, 9, 64, 1, '#2a2d33');
    },
    plat(x, y, w, gy) {
      R(x + 3, y + 3, 2, gy - y - 3, '#3a3d44'); R(x + w - 5, y + 3, 2, gy - y - 3, '#3a3d44');
      R(x, y - 8, 1, 8, '#6d7380'); R(x + w - 1, y - 8, 1, 8, '#6d7380'); R(x, y - 8, w, 1, '#8a909c');
      R(x, y, w, 3, '#5a606b'); R(x, y, w, 1, '#9aa1ad');
      for (let i = x + 2; i < x + w - 2; i += 4) R(i, y + 1, 2, 1, '#2d3036');
    },
  },
};

/* ============================================================ DECOR */
const DECO = {
  bush(x, y, v) {
    for (const [dx, dy, r, c] of [[-10, -6, 7, '#2f6b35'], [0, -9, 9, '#2f6b35'], [10, -6, 7, '#2f6b35'], [-4, -11, 5, '#3f8a3f'], [5, -12, 5, '#3f8a3f'], [1, -14, 3, '#5aa84c']]) circ(x + dx * (0.8 + v * 0.4), y + dy, r, c);
  },
  palm(x, y, v) {
    const lean = v > 0.5 ? 1 : -1;
    let tx = x, ty = y;
    for (let i = 0; i < 14; i++) {
      tx = x + Math.round(Math.sin(i * 0.16) * 7 * lean);
      ty = y - i * 6 - 6;
      R(tx - 2, ty, 5, 6, i % 2 ? '#8a6a3a' : '#a47d46');
      R(tx - 2, ty, 1, 6, '#6b5028');
    }
    X.strokeStyle = '#3f7a36'; X.lineWidth = 3; X.lineCap = 'round';
    for (const a of [-2.8, -2.2, -1.6, -1.0, -0.4, 0.2]) {
      X.beginPath(); X.moveTo(tx, ty);
      X.quadraticCurveTo(tx + Math.cos(a) * 18, ty + Math.sin(a) * 12 - 4, tx + Math.cos(a) * 30, ty + 10); X.stroke();
    }
    X.lineCap = 'butt';
    circ(tx - 2, ty + 3, 2, '#5a3a1a'); circ(tx + 2, ty + 4, 2, '#5a3a1a');
  },
  hut(x, y) {
    for (const dx of [-20, -6, 8, 20]) R(x + dx, y - 16, 3, 16, '#6b4a26');
    R(x - 24, y - 18, 50, 3, '#8a6034');
    R(x - 20, y - 38, 42, 20, '#b08a4a');
    for (let i = -20; i < 22; i += 4) R(x + i, y - 38, 1, 20, '#8a6a34');
    R(x - 4, y - 32, 9, 14, '#2a1c10');
    poly([x - 30, y - 36, x + 1, y - 58, x + 32, y - 36], '#c9a55a');
    for (let i = 0; i < 6; i++) line(x - 26 + i * 10, y - 37, x - 2 + i * 3, y - 55, '#9a7a3a');
    R(x - 30, y - 37, 62, 2, '#8a6a32');
  },
  sandbags(x, y) {
    for (let row = 0; row < 3; row++) {
      for (let i = 0; i < 4 - row; i++) {
        const bx = x - 15 + row * 5 + i * 10, by = y - 4 - row * 6;
        oval(bx, by, 6, 3.5, '#a8915f'); oval(bx - 1, by - 1, 4, 2, '#c4ad78'); R(bx - 5, by + 2, 10, 1, '#7e6a42');
      }
    }
  },
  rock(x, y, v) {
    const s = 6 + v * 8;
    poly([x - s * 1.5, y, x - s, y - s * 0.9, x - s * 0.1, y - s * 1.3, x + s * 1.1, y - s * 0.7, x + s * 1.5, y], '#7a746a');
    poly([x - s, y - s * 0.9, x - s * 0.1, y - s * 1.3, x + s * 0.2, y - s * 0.6, x - s * 0.8, y - s * 0.4], '#9a948a');
    R(x - s * 1.5, y - 1, s * 3, 1, '#55504a');
  },
  sign(x, y) {
    R(x - 1, y - 24, 3, 24, '#6b4a26');
    R(x - 11, y - 30, 22, 12, '#8a5a32'); R(x - 11, y - 30, 22, 1, '#b07a48'); R(x - 11, y - 19, 22, 1, '#5e3b1f');
    R(x - 3, y - 28, 6, 4, '#eee'); R(x - 2, y - 27, 1, 1, '#222'); R(x + 1, y - 27, 1, 1, '#222'); R(x - 2, y - 24, 4, 2, '#eee');
    line(x - 6, y - 22, x + 6, y - 20, '#eee'); line(x + 6, y - 22, x - 6, y - 20, '#eee');
  },
  wreck(x, y, v) {
    rrect(x - 24, y - 16, 48, 12, 3, '#2e2b28');
    poly([x - 14, y - 16, x - 8, y - 26, x + 10, y - 26, x + 16, y - 16], '#3a3530');
    R(x - 6, y - 24, 6, 6, '#141210'); R(x + 2, y - 24, 6, 6, '#141210');
    R(x - 22, y - 12, 12, 2, '#6a3a24'); R(x + 5, y - 15, 9, 3, '#5a3020');
    circ(x - 14, y - 4, 5, '#1a1817'); circ(x + 14, y - 4, 5, '#1a1817'); circ(x - 14, y - 4, 2, '#3a3633');
    if (v > 0.5) { R(x + 16, y - 20, 12, 2, '#2e2b28'); }
  },
  barrel(x, y, v) {
    const c = v < 0.33 ? ['#3a5a8a', '#5a7aaa', '#2a4064'] : v < 0.66 ? ['#8a2a22', '#b04438', '#5a1a14'] : ['#4a5a2a', '#6a7a3a', '#2e3a18'];
    R(x - 5, y - 14, 10, 14, c[0]); R(x - 5, y - 14, 2, 14, c[1]); R(x + 3, y - 14, 2, 14, c[2]);
    R(x - 5, y - 10, 10, 1, c[2]); R(x - 5, y - 4, 10, 1, c[2]); oval(x, y - 14, 5, 1.5, c[1]);
    if (v > 0.66) { R(x - 2, y - 9, 4, 3, '#e0b22a'); }
  },
  pillar(x, y, v) {
    const h = 26 + v * 30;
    R(x - 6, y - h, 12, h, '#c58a5a'); R(x - 6, y - h, 3, h, '#e0a874'); R(x + 3, y - h, 3, h, '#98643c');
    R(x - 8, y - 4, 16, 4, '#a8704a');
    poly([x - 6, y - h, x - 1, y - h - 6, x + 2, y - h - 1, x + 6, y - h - 4, x + 6, y - h], '#c58a5a');
    R(x + 8, y - 6, 12, 6, '#b27a4f'); R(x + 8, y - 6, 12, 1, '#d69c6a');
  },
  pine(x, y, v) { pine(x, y, 34 + v * 30, '#2e5266', '#eef5fb'); },
  snowman(x, y) {
    circ(x, y - 7, 8, '#f4f8fc'); circ(x, y - 19, 6, '#ffffff'); circ(x + 2, y - 20, 1, '#222'); circ(x - 1, y - 20, 1, '#222');
    poly([x + 2, y - 18, x + 8, y - 17, x + 2, y - 16], '#e8812a');
    R(x - 6, y - 26, 12, 3, PAL.snow.helmetD); R(x - 4, y - 28, 8, 2, PAL.snow.helmet);
    line(x - 7, y - 12, x - 14, y - 18, '#6a4a2a'); line(x + 7, y - 12, x + 13, y - 17, '#6a4a2a');
    circ(x, y - 9, 1, '#333'); circ(x, y - 5, 1, '#333');
  },
  fence(x, y) {
    for (const dx of [-18, 0, 18]) R(x + dx - 1, y - 18, 3, 18, '#4a3a2a');
    R(x - 20, y - 14, 40, 2, '#5a4a3a'); R(x - 20, y - 7, 40, 2, '#5a4a3a');
    X.strokeStyle = '#8a8f96'; X.lineWidth = 1; X.beginPath();
    for (let i = -20; i <= 20; i += 4) X.lineTo(x + i, y - 19 - ((i / 4) & 1) * 2);
    X.stroke();
  },
  car(x, y, v) {
    const c = v < 0.5 ? ['#6a3a3a', '#8a5050', '#4a2626'] : ['#3e4a5e', '#5a6a84', '#2a3342'];
    rrect(x - 26, y - 14, 52, 10, 3, c[0]); R(x - 26, y - 14, 52, 2, c[1]);
    poly([x - 14, y - 14, x - 8, y - 24, x + 12, y - 24, x + 18, y - 14], c[2]);
    R(x - 7, y - 22, 8, 7, '#0e1018'); R(x + 3, y - 22, 8, 7, '#0e1018');
    line(x - 6, y - 21, x - 1, y - 17, '#6a7aa0');
    circ(x - 15, y - 4, 5, '#141414'); circ(x + 15, y - 4, 5, '#141414');
    R(x + 22, y - 12, 4, 2, '#f0d890');
  },
  lamp(x, y) {
    R(x - 1, y - 52, 3, 52, '#2a2d38'); R(x - 3, y - 4, 7, 4, '#2a2d38');
    R(x - 1, y - 54, 14, 2, '#2a2d38'); R(x + 8, y - 53, 8, 3, '#3a3d48'); R(x + 9, y - 50, 6, 1, '#fff2c0');
  },
  firebarrel(x, y) {
    R(x - 6, y - 14, 12, 14, '#5a3a2a'); R(x - 6, y - 14, 2, 14, '#7a5038'); R(x + 4, y - 14, 2, 14, '#3a2418');
    R(x - 6, y - 9, 12, 1, '#3a2418'); R(x - 6, y - 4, 12, 1, '#3a2418');
    oval(x, y - 14, 6, 2, '#2a1810');
  },
  console(x, y, v) {
    R(x - 12, y - 22, 24, 22, '#3a3f48'); R(x - 12, y - 22, 24, 2, '#5d636e');
    R(x - 9, y - 18, 18, 8, '#0e2a1e'); R(x - 8, y - 17, 10, 1, '#3aff8a'); R(x - 8, y - 14, 14, 1, '#2ac870');
    for (let i = 0; i < 4; i++) R(x - 8 + i * 5, y - 7, 2, 2, ['#ff4a3a', '#e0b22a', '#3aff8a', '#2fb6ff'][i]);
  },
  pipe(x, y) {
    R(x - 30, y - 22, 60, 10, '#5a3a30'); R(x - 30, y - 22, 60, 3, '#7a5044'); R(x - 30, y - 14, 60, 2, '#3a241e');
    R(x - 24, y - 12, 4, 12, '#3a3d44'); R(x + 20, y - 12, 4, 12, '#3a3d44');
    R(x - 2, y - 26, 5, 16, '#6a4a3e');
    X.strokeStyle = '#c0392b'; X.lineWidth = 2; X.beginPath(); X.arc(x + 0.5, y - 28, 5, 0, TAU); X.stroke();
  },
  crates(x, y) {
    drawCrate({ x: x - 9, y }); drawCrate({ x: x + 9, y }); drawCrate({ x, y: y - 16 });
  },
};

/* ======================================================== THEME CACHE */
const THEME_CACHE = {};
function getTheme(name) {
  if (THEME_CACHE[name]) return THEME_CACHE[name];
  const T = THEMES[name];
  const th = { name, dark: T.dark, darkCol: T.darkCol, weather: T.weather, pal: T.pal, veh: T.veh, wall: T.wall, deco: T.deco, plat: T.plat, layers: [] };
  const [sky, sx] = mkCanvas(W, H);
  withCtx(sx, () => T.sky());
  th.sky = sky;
  T.layers.forEach((L, i) => {
    const [c, x] = mkCanvas(TW, H);
    withCtx(x, () => L.paint(mulberry32(1000 + i * 77 + name.length * 13)));
    th.layers.push({ c, p: L.p });
  });
  const [gp, gx] = mkCanvas(64, 64);
  withCtx(gx, () => T.ground(mulberry32(31)));
  const [tp, tx] = mkCanvas(64, 16);
  withCtx(tx, () => T.top(mulberry32(47)));
  th.groundPat = g.createPattern(gp, 'repeat');
  th.topPat = g.createPattern(tp, 'repeat');
  th.shade = g.createLinearGradient(0, 214, 0, H);
  th.shade.addColorStop(0, 'rgba(0,0,0,0)');
  th.shade.addColorStop(1, 'rgba(0,0,0,0.45)');
  THEME_CACHE[name] = th;
  return th;
}

/** Small preview used on mission cards. */
function themeThumb(name, canvas) {
  const th = getTheme(name);
  const x = canvas.getContext('2d');
  const s = canvas.width / W;
  x.imageSmoothingEnabled = true;
  x.drawImage(th.sky, 0, 0, canvas.width, canvas.height);
  th.layers.forEach((L, i) => x.drawImage(L.c, i * 120, 0, W, H, 0, 0, canvas.width, canvas.height));
  x.fillStyle = th.groundPat;
  x.save(); x.scale(s, s);
  x.fillRect(0, 214, W, H - 214);
  x.translate(0, 210); x.fillStyle = th.topPat; x.fillRect(0, 0, W, 16);
  x.restore();
}
