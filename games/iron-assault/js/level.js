'use strict';
/* Iron Assault — difficulty presets, mission definitions and the seeded
   level generator (terrain steps, platforms, set dressing, enemy script). */

const DIFFS = {
  easy: {
    key: 'easy', cn: '新兵', en: 'ROOKIE', lives: 5, hp: 5, hpMul: 0.7, fire: 0.6, bspd: 0.8, spd: 0.85,
    dens: 0.75, bossMul: 0.7, scoreMul: 0.5, drop: 0.2,
    desc: '初次上阵。生命充足，敌人火力稀疏、子弹更慢。',
  },
  normal: {
    key: 'normal', cn: '老兵', en: 'VETERAN', lives: 3, hp: 3, hpMul: 1, fire: 1, bspd: 1, spd: 1,
    dens: 1, bossMul: 1, scoreMul: 1, drop: 0.11,
    desc: '标准战场强度，攻守平衡。',
  },
  hard: {
    key: 'hard', cn: '精英', en: 'ELITE', lives: 3, hp: 2, hpMul: 1.3, fire: 1.4, bspd: 1.2, spd: 1.15,
    dens: 1.25, bossMul: 1.3, scoreMul: 1.5, drop: 0.07,
    desc: '敌军更密集、射得更准、更耐打。',
  },
  hell: {
    key: 'hell', cn: '地狱', en: 'INFERNO', lives: 2, hp: 1, hpMul: 1.6, fire: 1.9, bspd: 1.35, spd: 1.3,
    dens: 1.5, bossMul: 1.6, scoreMul: 2.5, drop: 0.05,
    desc: '一击即死，还原街机最硬核的手感。',
  },
};

const BOSS_INFO = {
  tank: { cn: '重型坦克「铁犀牛」', en: 'IRON RHINO' },
  scorpion: { cn: '机械巨蝎「沙暴」', en: 'SAND SCORPION' },
  heli: { cn: '武装直升机「雪鸮」', en: 'SNOW OWL' },
  mech: { cn: '重装机甲「泰坦」', en: 'TITAN' },
  fortress: { cn: '终焉要塞', en: 'DOOM FORTRESS' },
};

const LEVELS = [
  {
    id: 1, theme: 'jungle', name: '丛林前线', en: 'JUNGLE FRONT', len: 4600, seed: 1201, music: 'jungle', boss: 'tank',
    mix: { rifle: 6, grenadier: 2, bazooka: 1.2, turret: 0.6, tank: 0.35, para: 0.6 },
    brief: '穿过雨林，突破叛军的第一道防线。',
  },
  {
    id: 2, theme: 'desert', name: '沙海遗迹', en: 'DESERT RUINS', len: 5000, seed: 2207, music: 'desert', boss: 'scorpion',
    mix: { rifle: 5, grenadier: 1.5, bazooka: 1.5, shield: 1.5, tank: 0.6, drone: 0.6, para: 0.6 },
    brief: '夕阳下的古城遗迹，盾兵开始出现。',
  },
  {
    id: 3, theme: 'snow', name: '雪原要塞', en: 'FROZEN PEAK', len: 5200, seed: 3301, music: 'snow', boss: 'heli',
    mix: { rifle: 5, grenadier: 1.5, shield: 1, turret: 1.4, para: 1.6, drone: 0.8, tank: 0.5 },
    brief: '风雪中的山地炮台与空降部队。',
  },
  {
    id: 4, theme: 'city', name: '废墟都市', en: 'RUINED CITY', len: 5400, seed: 4409, music: 'city', boss: 'mech',
    mix: { rifle: 5, grenadier: 1.5, bazooka: 1.8, shield: 1.4, drone: 1.2, tank: 0.8, para: 1 },
    brief: '雷雨夜的巷战，注意无人机轰炸。',
  },
  {
    id: 5, theme: 'base', name: '敌军总部', en: 'ENEMY HQ', len: 5600, seed: 5503, music: 'base', boss: 'fortress',
    mix: { rifle: 5, grenadier: 1.5, bazooka: 1.5, shield: 1.8, turret: 1.5, drone: 1.3, tank: 1, para: 1 },
    brief: '最终决战。摧毁要塞核心，结束这场战争。',
  },
];

const RUSH_DEF = {
  id: 6, rush: true, theme: 'base', name: '首领连战', en: 'BOSS RUSH', len: W, seed: 77, music: 'boss', boss: null, mix: {},
  brief: '五大首领车轮战，中途补给有限。',
};
const RUSH_ORDER = ['tank', 'scorpion', 'heli', 'mech', 'fortress'];

const GROUND = 214;

function genLevel(def, diff) {
  const rng = mulberry32(def.seed);
  const rr = (a, b) => a + rng() * (b - a);
  const rp = arr => arr[Math.floor(rng() * arr.length)];
  const L = { def, len: def.len, arenaX: def.len - W, segs: [], plats: [], deco: [], events: [] };

  if (def.rush) {
    L.segs.push([-400, def.len + 600, GROUND]);
    return L;
  }

  // Terrain: flat start, stepped middle, flat boss arena.
  let x = 700, y = GROUND;
  L.segs.push([-400, 700, GROUND]);
  while (x < L.arenaX - 600) {
    const w = Math.round(rr(260, 520));
    y = clamp(y + rp([-16, -12, -8, 0, 0, 8, 12, 16]), 192, 224);
    L.segs.push([x, x + w, y]);
    x += w;
  }
  L.segs.push([x, def.len + 600, GROUND]);

  // Platforms on longer stretches.
  for (const [x0, x1, sy] of L.segs) {
    if (x0 < 600 || x1 > L.arenaX - 80) continue;
    const w = x1 - x0;
    if (w > 300 && rng() < 0.6) {
      const pw = Math.round(rr(64, 112) / 8) * 8;
      const px = Math.round(x0 + rr(40, w - pw - 40));
      L.plats.push({ x: px, y: Math.round(sy - rr(44, 58)), w: pw });
    }
  }

  // Set dressing.
  const th = THEMES[def.theme];
  for (let dx = 120; dx < L.len + 200; dx += rr(70, 170)) {
    const seg = L.segs.find(s => dx >= s[0] && dx < s[1]);
    if (!seg || dx - seg[0] < 30 || seg[1] - dx < 30) continue;
    L.deco.push({ x: Math.round(dx), kind: weighted(th.deco, rng), v: rng() });
  }

  // Enemy script.
  const dens = diff.dens;
  const groundMix = Object.assign({}, def.mix);
  let ex = 560, nextWave = 1500;
  while (ex < L.arenaX - 260) {
    if (ex >= nextWave) {
      L.events.push({ x: ex, type: 'wave', n: Math.round(rr(5, 7) * dens) });
      nextWave = ex + rr(1300, 1700);
      ex += 260;
      continue;
    }
    const type = weighted(groundMix, rng);
    const n = type === 'rifle' ? Math.round(rr(1, 3.4)) : type === 'para' ? Math.round(rr(2, 3.4)) :
      type === 'grenadier' || type === 'shield' || type === 'drone' ? Math.round(rr(1, 2.4)) : 1;
    L.events.push({ x: Math.round(ex), type, n: Math.max(1, Math.round(n * (0.8 + dens * 0.2))), idle: rng() < 0.45 });
    ex += rr(150, 260) / dens;
  }

  // Prisoners and supply crates.
  const powN = 3 + (def.id > 2 ? 1 : 0);
  const loot = ['R', 'F', 'S', 'L', 'H', 'B', 'food', 'gem'];
  for (let i = 0; i < powN; i++) {
    const px = Math.round(620 + (i * (L.arenaX - 1000)) / powN + rr(0, 220));
    L.events.push({ x: px, type: 'pow', item: i === 0 ? 'H' : rp(loot) });
  }
  for (let i = 0; i < 4; i++) {
    L.events.push({ x: Math.round(rr(900, L.arenaX - 300)), type: 'crate', item: rp(['B', 'food', 'gem', 'H', 'S', 'R']) });
  }
  L.events.push({ x: L.arenaX + 70, type: 'crate', item: rp(['H', 'R', 'F', 'L']) });
  L.events.push({ x: L.arenaX + 110, type: 'crate', item: 'B' });
  L.events.sort((a, b) => a.x - b.x);
  return L;
}

function groundY(x) {
  const segs = S.L.segs;
  let lo = 0, hi = segs.length - 1;
  if (x < segs[0][0]) return segs[0][2];
  while (lo < hi) {
    const mid = (lo + hi + 1) >> 1;
    if (segs[mid][0] <= x) lo = mid; else hi = mid - 1;
  }
  return segs[lo][2];
}

/** Highest standing surface under x at or below height y. */
function surfaceY(x, y = -1e9) {
  let best = groundY(x);
  for (const p of S.L.plats) if (x >= p.x && x <= p.x + p.w && p.y >= y && p.y < best) best = p.y;
  return best;
}
