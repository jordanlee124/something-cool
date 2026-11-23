#!/usr/bin/env node
// Balance harness for Emberwright. Bots play the real simulation (extracted from emberwright.html)
// at simulated speed and report when each milestone fell.
//
//   node tools/balance.js                 all bots, seed 1
//   node tools/balance.js --seeds 3       average over seeds 1..3
//   node tools/balance.js --bot planner --trace [--limit 900]
//
// Sensible strategies: planner (lookahead, active and idle) and cautious (heat-aware heuristics).
// Floors, reported but not checked: greedy (blind to heat) and random.
// The check: the fastest sensible strategy kindles the Forge in 30-50 minutes, and the slowest
// sensible strategy is within 2x of the fastest, so no style of play dominates.

const Sim = require('./loadsim')();
const { B, NB, N } = Sim;

const args = process.argv.slice(2);
const opt = (k, d) => { const i = args.indexOf('--' + k); return i < 0 ? d : args[i + 1]; };
const flag = k => args.includes('--' + k);
const SEEDS = +opt('seeds', 1);
const ONLY = opt('bot', null);
const TRACE = flag('trace');
const LIMIT = +(opt("limit", 3 * 3600));
const TARGET = [30 * 60, 50 * 60];

// ---------- helpers ----------
const clone = s => { const c = structuredClone(s); c.events = []; return c; };
const empties = s => { const o = []; for (let i = 0; i < N * N; i++) if (Sim.unlocked(s, i) && !s.cells[i].b) o.push(i); return o; };
const nbs = (s, i) => NB[i].filter(j => j >= 0 && Sim.unlocked(s, j));
const adj = (s, i, t) => nbs(s, i).filter(j => s.cells[j].b && s.cells[j].b.t === t && !s.cells[j].b.cr).length;
const all = (s, t) => { const o = []; for (let i = 0; i < N * N; i++) if (s.cells[i].b && s.cells[i].b.t === t) o.push(i); return o; };
const dist = (a, b) => Math.abs(a % N - b % N) + Math.abs(((a / N) | 0) - ((b / N) | 0));
const income = s => s.rate.embers;
const T = (s, i) => s.cells[i].T;

function placeScore(s, t, i) {
  const temp = T(s, i);
  switch (t) {
    case 'pit': return -Math.abs(temp - B.pit.ideal) + 60 * adj(s, i, 'hearth') - (temp > Sim.maxT(s, 'pit') - 80 ? 400 : 0);
    case 'hearth': {
      const pits = adj(s, i, 'pit'), lodges = adj(s, i, 'lodge');
      let near = 0; for (const j of all(s, 'pit')) if (dist(i, j) === 2) near++;
      return pits * 3 + near - lodges * 5 - (temp > 1000 ? 4 : 0);
    }
    case 'lodge': {
      const hs = all(s, 'hearth');
      const d = hs.length ? Math.min(...hs.map(j => dist(i, j))) : 9;
      return -temp + 15 * Math.min(d, 6);
    }
    case 'hut': {
      // Between the woodpiles and the fires, on cool ground.
      const ends = all(s, 'lodge').concat(all(s, 'hearth'), all(s, 'kiln'));
      const d = ends.length ? ends.reduce((a, j) => a + dist(i, j), 0) / ends.length : 0;
      return -d * 40 - Math.max(0, temp - 150);
    }
    case 'bellows': return adj(s, i, 'hearth') * 10 - adj(s, i, 'lodge') * 6 - adj(s, i, 'pit') * 2;
    case 'wall': {
      let v = -1e9;
      for (const j of nbs(s, i)) if (s.cells[j].b && s.cells[j].b.t === 'lodge') v = Math.max(v, temp);
      return v;
    }
    case 'vent': {
      let v = -1e9;
      for (const j of nbs(s, i)) { const b = s.cells[j].b; if (b && (b.t === 'pit' || b.t === 'lodge')) v = Math.max(v, T(s, j) - Sim.maxT(s, b.t) * 0.85); }
      return v > -100 ? v : -1e9;
    }
  }
  return -1e9;
}
function bestTiles(s, t, k) {
  return empties(s).map(i => [i, placeScore(s, t, i)]).filter(([, v]) => v > -1e8).sort((a, b) => b[1] - a[1]).slice(0, k).map(([i]) => i);
}
function costOf(s, a) {
  switch (a.k) {
    case 'build': return Sim.buildCost(s, a.t);
    case 'level': return Sim.levelCost(s, a.i);
    case 'repair': return Sim.repairCost(s, a.i);
    case 'upgrade': return Sim.UMAP[a.id].cost;
    case 'expand': return Sim.expandCost(s);
    default: return {};
  }
}
const affordable = (s, a) => Sim.can(s, costOf(s, a));
function wait(s, c) { // seconds until affordable at current net rates
  let w = 0;
  for (const k in c) {
    const have = s[k], r = k === 'embers' ? s.rate.embers : s.rate.wood;
    if (have >= c[k]) continue;
    if (r <= 0) return Infinity;
    w = Math.max(w, (c[k] - have) / r);
  }
  return w;
}
const UNLOCKS = new Set(['bellows', 'firebrick', 'vents', 'mending', 'fireclay', 'charcoal']);

function emergencies(s, out) {
  // Only repair once the tile has cooled; a building repaired in place at 1600 degrees cracks again at once.
  for (let i = 0; i < N * N; i++) { const b = s.cells[i].b; if (b && b.cr && T(s, i) < Sim.maxT(s, b.t) - 50) out.push({ k: 'repair', i }); }
}

// ---------- bots ----------
// Each bot is called every DECIDE seconds and returns actions to try in order.
const DECIDE = 5;
const lastThrottle = new Map(); // planner: tile -> time of last throttle change, so it cannot flap

// How much a unit of wood is worth in embers right now: dear when hearths are short, near nothing when it piles up.
function woodValue(s) {
  let burn = 0;
  for (const i of all(s, 'hearth')) { const b = s.cells[i].b; if (!b.cr) burn += Sim.hearthBurn(s, b); }
  for (const i of all(s, 'kiln')) { const b = s.cells[i].b; if (!b.cr) burn += Sim.kilnBurn(b); }
  if (s.wood < 30 + 40 * burn || s.rate.wood < 0) return 3;
  if (s.wood > 3000 + 200 * burn) return 0.05;
  return 0.5;
}
const costEq = (c, wv) => (c.embers || 0) + wv * (c.wood || 0) + 1;

// Buying anything once the Forge is in sight only makes sense if it gets you there sooner.
function helpsGoal(s, c, gain) {
  const f = Sim.UMAP.forge;
  if (!Sim.upVisible(s, f)) return true;
  const r = Math.max(1e-9, s.rate.embers), rem = Math.max(0, f.cost.embers - s.embers);
  if ((c.wood || 0) > 0 && s.wood - c.wood < f.cost.wood) return false;
  return (rem + (c.embers || 0)) / (r + gain) < rem / r;
}

// Analytic estimate of the embers/s an action adds, used by the heuristic bots (no lookahead).
function estGain(s, a, wv) {
  let pitInc = 0, kilnInc = 0, lodgeInc = 0, burn = 0;
  for (let i = 0; i < N * N; i++) {
    const b = s.cells[i].b; if (!b) continue;
    if (b.t === 'pit') pitInc += Sim.output(s, i);
    else if (b.t === 'kiln') { kilnInc += Sim.output(s, i); burn += b.cr ? 0 : Sim.kilnBurn(b); }
    else if (b.t === 'lodge') lodgeInc += Sim.output(s, i);
    else if (b.t === 'hearth' && !b.cr) burn += Sim.hearthBurn(s, b);
  }
  const pm = Sim.pitMult(s);
  if (a.k === 'upgrade') {
    switch (a.id) {
      case 'clay': return pitInc + kilnInc;
      case 'tongs': return 2 * (pitInc + kilnInc);
      case 'bloom': return 3 * (pitInc + kilnInc);
      case 'deep': return 0.25 * pitInc;
      case 'axes': return lodgeInc * wv;
      case 'seasoned': return 0.35 * burn * wv;
      case 'hotblast': case 'waterwheel': return 0.15 * pitInc;
      default: return 0;
    }
  }
  if (a.k === 'level') {
    const b = s.cells[a.i].b, o = Sim.output(s, a.i);
    if (b.t === 'pit') return o / b.l;
    if (b.t === 'lodge') return o / b.l * wv;
    if (b.t === 'kiln') return o / b.l - Sim.kilnBurn(b) / b.l * wv;
    return 0;
  }
  if (a.k === 'build') {
    const t = T(s, a.i);
    switch (a.t) {
      case 'pit': return B.pit.rate * pm * Sim.pitEff(s, t);
      case 'lodge': return B.lodge.rate * Sim.lodgeMult(s) * Sim.lodgeEff(s, t) * wv;
      case 'kiln': return B.kiln.rate * pm * Sim.kilnEff(s, t) - B.kiln.burn * wv;
      case 'hearth': {
        let g = -B.hearth.burn * wv;
        for (const j of nbs(s, a.i)) {
          const b = s.cells[j].b;
          if (b && b.t === 'pit' && !b.cr) g += B.pit.rate * b.l * pm * (Sim.pitEff(s, T(s, j) + 180) - Sim.pitEff(s, T(s, j)));
        }
        return g;
      }
    }
  }
  return 0;
}

// Pick the best gain per ember. Buy it if affordable; save for it if it is close; otherwise take the best affordable.
function choose(s, cands, wv) {
  let best = null, bestScore = 0, bestNow = null, bestNowScore = 0;
  for (const a of cands) {
    const c = costOf(s, a); if (!c) continue;
    const g = estGain(s, a, wv);
    if (g <= 0 || !helpsGoal(s, c, g)) continue;
    const sc = g / costEq(c, wv);
    if (sc > bestScore) { bestScore = sc; best = a; }
    if (Sim.can(s, c) && sc > bestNowScore) { bestNowScore = sc; bestNow = a; }
  }
  if (!best) return [];
  if (Sim.can(s, costOf(s, best))) return [best];
  if (wait(s, costOf(s, best)) < 300) return [];
  return bestNow ? [bestNow] : [];
}

// Wood logistics: fires out of wood while woodpiles sit full means too few carters.
function logistics(s, out) {
  let cold = 0, full = 0;
  for (let i = 0; i < N * N; i++) {
    const b = s.cells[i].b; if (!b || !Sim.unlocked(s, i)) continue;
    if (Sim.isFire(b) && !b.cr && b.th > 0 && b.f < 0.01) cold++;
    if (b.t === 'lodge' && b.f >= Sim.storeCap(b) - 0.01) full++;
  }
  if (!(full && cold)) return;
  const opts = [];
  const h = bestTiles(s, 'hut', 1)[0]; if (h !== undefined) opts.push({ k: 'build', t: 'hut', i: h });
  for (const i of all(s, 'hut')) if (s.cells[i].b.l < B.hut.levels) opts.push({ k: 'level', i });
  for (const id of ['barrows', 'cobbles', 'oxcarts']) if (Sim.upVisible(s, Sim.UMAP[id])) opts.push({ k: 'upgrade', id });
  opts.sort((a, b) => costEq(costOf(s, a), 1) - costEq(costOf(s, b), 1));
  out.push(...opts.slice(0, 1));
}

function housekeeping(s, out) {
  emergencies(s, out);
  logistics(s, out);
  const f = Sim.UMAP.forge;
  if (Sim.upVisible(s, f)) {
    out.push({ k: 'upgrade', id: 'forge' });
    // Kilns eat the wood the Forge needs: bank it.
    if (s.wood < f.cost.wood) for (const i of all(s, 'kiln')) if (s.cells[i].b.th > 0) out.push({ k: 'throttle', i, v: 0, free: true });
  }
  const inc = income(s);
  for (const u of Sim.U) if (UNLOCKS.has(u.id) && Sim.upVisible(s, u) && (u.cost.embers || 0) < inc * 150 + 400 && helpsGoal(s, u.cost, 0.1 * inc)) out.push({ k: 'upgrade', id: u.id });
  const ec = Sim.expandCost(s);
  if (ec && (empties(s).length < 3 || ec.embers < inc * 90) && helpsGoal(s, ec, 0.2 * inc)) out.push({ k: 'expand' });
  // A full floor is the real bottleneck: save for the expansion rather than spend on levels.
  return !!(ec && empties(s).length < 3 && wait(s, ec) < 600 && helpsGoal(s, ec, 0.2 * inc));
}

function heuristicCands(s, careful) {
  const cands = [];
  for (const u of Sim.U) if (Sim.upVisible(s, u) && !u.goal && !UNLOCKS.has(u.id)) cands.push({ k: 'upgrade', id: u.id });
  for (const t of ['pit', 'lodge', 'kiln', 'hearth']) {
    if (!Sim.buildable(s, t)) continue;
    const tiles = careful ? bestTiles(s, t, 3) : empties(s).sort((a, b) => Math.abs(T(s, a) - (t === 'lodge' ? 20 : t === 'kiln' ? 380 : 520)) - Math.abs(T(s, b) - (t === 'lodge' ? 20 : t === 'kiln' ? 380 : 520))).slice(0, 3);
    for (const i of tiles) {
      if (careful && t !== 'hearth' && T(s, i) > Sim.maxT(s, t) - 150) continue;
      cands.push({ k: 'build', t, i });
    }
  }
  for (const t of ['pit', 'lodge', 'kiln']) for (const i of all(s, t)) { const b = s.cells[i].b; if (!b.cr && b.l < B[t].levels) cands.push({ k: 'level', i }); }
  return cands;
}

const bots = {
  // Lookahead planner: tries each candidate on a copy of the world for 20 s and buys the best payback per ember.
  planner(s) {
    const out = [];
    const saving = housekeeping(s, out);
    if (out.some(a => a.k !== 'build' || a.t !== 'hut') || saving) return out;
    if (out.length && affordable(s, out[0])) return out;
    const inc = income(s), wv = Math.min(0.5, woodValue(s));
    const value = st => {
      Sim.advance(st, 12);
      const e0 = st.embers, w0 = st.wood;
      Sim.advance(st, 8);
      return (st.embers - e0) / 8 + wv * (st.wood - w0) / 8 - (st.stats.cracks - s.stats.cracks) * 0.1 * (inc + 1);
    };
    const base = value(clone(s));

    // Free actions (throttles) first: take one only if it clearly helps.
    let bestFree = null, bestFreeGain = Math.max(0.03 * Math.abs(base), 0.02);
    for (const i of all(s, 'hearth').concat(all(s, 'kiln'))) for (const v of [0, 0.5, 1]) {
      if (s.cells[i].b.th === v || s.t - (lastThrottle.get(i) ?? -1e9) < 60) continue;
      // Only consider turning down when a neighbour runs past its sweet spot, and up when one runs cold.
      const ns = nbs(s, i).map(j => s.cells[j].b && !s.cells[j].b.cr ? [s.cells[j].b.t, T(s, j)] : null).filter(Boolean);
      const hotN = ns.some(([t, tt]) => (t === 'pit' && tt > B.pit.ideal + 60) || (t !== 'pit' && t !== 'hearth' && t !== 'kiln' && t !== 'wall' && t !== 'vent' && tt > Sim.maxT(s, t) - 100) || (t === 'kiln' && tt > B.kiln.ideal + 60));
      const coldN = ns.some(([t, tt]) => (t === 'pit' && tt < B.pit.ideal - 60) || (t === 'kiln' && tt < B.kiln.ideal - 60));
      if (v < s.cells[i].b.th ? !hotN : !coldN) continue;
      const st = clone(s); Sim.act(st, { k: 'throttle', i, v });
      const g = value(st) - base;
      if (g > bestFreeGain) { bestFreeGain = g; bestFree = { k: 'throttle', i, v, free: true }; }
    }
    if (bestFree) return [bestFree];

    const cands = [];
    for (const t of Sim.ORDER) if (Sim.buildable(s, t)) for (const i of bestTiles(s, t, t === 'pit' ? 3 : 2)) cands.push({ k: 'build', t, i });
    for (const t of ['pit', 'lodge', 'hearth', 'kiln', 'vent', 'hut']) {
      const list = all(s, t).filter(i => !s.cells[i].b.cr && s.cells[i].b.l < B[t].levels);
      list.sort((a, b) => Sim.levelCost(s, a).embers - Sim.levelCost(s, b).embers || 0);
      for (const i of list.slice(0, t === 'pit' ? 2 : 1)) cands.push({ k: 'level', i });
    }
    for (const u of Sim.U) if (Sim.upVisible(s, u) && !u.goal && !UNLOCKS.has(u.id)) cands.push({ k: 'upgrade', id: u.id });

    let best = null, bestScore = 0;
    for (const a of cands) {
      const c = costOf(s, a);
      const w = wait(s, c);
      if (w > 120) continue;
      const st = clone(s);
      for (const k in c) st[k] = Math.max(st[k], c[k]);
      if (!Sim.act(st, a)) continue;
      const gain = value(st) - base;
      if (gain <= 0 || !helpsGoal(s, c, gain)) continue;
      const score = gain / costEq(c, wv) / (1 + w / 60);
      if (score > bestScore) { bestScore = score; best = a; }
    }
    return best && affordable(s, best) ? [best] : [];
  },

  // Greedy: best gain per ember, but blind to heat. Places by temperature alone and never turns anything down.
  greedy(s) {
    const out = [];
    if (housekeeping(s, out)) return out;
    const wv = woodValue(s);
    return out.concat(choose(s, heuristicCands(s, false), wv));
  },

  // Cautious: the same economics, plus heat management: turns hearths down near their limits, vents, firebrick.
  cautious(s) {
    const out = [];
    for (let i = 0; i < N * N; i++) {
      const b = s.cells[i].b; if (!b || b.cr || b.t === 'hearth') continue;
      if (T(s, i) > Sim.maxT(s, b.t) - 60) {
        for (const j of nbs(s, i)) { const h = s.cells[j].b; if (h && (h.t === 'hearth' || h.t === 'kiln') && h.th > 0) { out.push({ k: 'throttle', i: j, v: h.th === 1 ? 0.5 : 0, free: true }); break; } }
        if (Sim.buildable(s, 'vent')) { const v = bestTiles(s, 'vent', 1)[0]; if (v !== undefined) out.push({ k: 'build', t: 'vent', i: v }); }
      }
    }
    // Tune each hearth by the pits around it: down when they run past the sweet spot, up when they run cold.
    for (const i of all(s, 'hearth')) {
      const h = s.cells[i].b;
      const pits = nbs(s, i).filter(j => s.cells[j].b && s.cells[j].b.t === 'pit' && !s.cells[j].b.cr);
      if (!pits.length) continue;
      const avg = pits.reduce((a, j) => a + T(s, j), 0) / pits.length;
      const hot = nbs(s, i).some(j => { const b = s.cells[j].b; return b && b.t !== 'hearth' && T(s, j) > Sim.maxT(s, b.t) - 160; });
      if (h.th > 0 && avg > B.pit.ideal + 120) out.push({ k: 'throttle', i, v: h.th === 1 ? 0.5 : 0, free: true });
      else if (h.th < 1 && avg < B.pit.ideal - 120 && !hot) out.push({ k: 'throttle', i, v: h.th === 0 ? 0.5 : 1, free: true });
    }
    if (Sim.buildable(s, 'wall')) { const w = bestTiles(s, 'wall', 1)[0]; if (w !== undefined && T(s, w) > 150 && helpsGoal(s, Sim.buildCost(s, 'wall'), 0)) out.push({ k: 'build', t: 'wall', i: w }); }
    if (housekeeping(s, out)) return out;
    return out.concat(choose(s, heuristicCands(s, true), woodValue(s)));
  },

  // Random: a floor for comparison. Buys something affordable at random every so often.
  random(s, rng) {
    const out = [];
    for (let i = 0; i < N * N; i++) { const b = s.cells[i].b; if (b && b.cr && rng() < 0.5) out.push({ k: 'repair', i }); }
    if (Sim.upVisible(s, Sim.UMAP.forge)) out.push({ k: 'upgrade', id: 'forge' });
    if (rng() < 0.5) return out;
    const e = empties(s);
    const pool = [];
    for (const t of Sim.ORDER) if (Sim.buildable(s, t) && e.length) pool.push({ k: 'build', t, i: e[Math.floor(rng() * e.length)] });
    for (const u of Sim.U) if (Sim.upVisible(s, u) && !u.goal) pool.push({ k: 'upgrade', id: u.id });
    pool.push({ k: 'expand' });
    const built = []; for (let i = 0; i < N * N; i++) if (s.cells[i].b) built.push(i);
    if (built.length) pool.push({ k: 'level', i: built[Math.floor(rng() * built.length)] });
    out.push(pool[Math.floor(rng() * pool.length)]);
    return out;
  },
};

// ---------- runner ----------
function run(name, seed, active) {
  const s = Sim.newState(seed);
  lastThrottle.clear();
  let r = seed * 9301 + 49297;
  const rng = () => (r = (r * 9301 + 49297) % 233280) / 233280;
  let nextDecide = 0, nextPump = 0, actions = 0;
  const log = [];
  while (!s.done && s.t < LIMIT) {
    if (active) {
      if (s.t < 90 && s.wood < 30) Sim.act(s, { k: 'gather' });
      if (s.up.bellows && s.t >= nextPump) { Sim.act(s, { k: 'pump' }); nextPump = s.t + 2.5; }
    }
    if (s.t >= nextDecide) {
      nextDecide = s.t + DECIDE;
      for (let n = 0; n < 25; n++) {
        const acts = bots[name](s, rng);
        let did = false;
        for (const a of acts) {
          if (a.k === 'throttle') {
            if (s.t - (lastThrottle.get(a.i) ?? -1e9) < 30) continue;
            lastThrottle.set(a.i, s.t);
          }
          if (a.free || affordable(s, a)) {
            if (Sim.act(s, a)) {
              actions++; did = true;
              if (TRACE) log.push(`${Sim.serialize ? '' : ''}${(s.t / 60).toFixed(1).padStart(6)}m  ${desc(s, a)}`);
              break;
            }
          }
        }
        if (!did) break;
      }
    }
    Sim.step(s);
    s.events.length = 0;
  }
  return { s, actions, log };
}
function desc(s, a) {
  if (a.k === 'build') return `build ${a.t} at ${a.i % N},${(a.i / N) | 0} (T=${Math.round(s.cells[a.i].T)})`;
  if (a.k === 'level') return `level ${s.cells[a.i].b.t} at ${a.i % N},${(a.i / N) | 0} -> ${s.cells[a.i].b.l}`;
  if (a.k === 'throttle') return `throttle hearth ${a.i % N},${(a.i / N) | 0} -> ${a.v}`;
  if (a.k === 'upgrade') return `upgrade ${a.id}`;
  return a.k + (a.i !== undefined ? ' ' + a.i : '');
}

const SENSIBLE = new Set(['planner', 'cautious']);
const fm = t => t === undefined ? '   —  ' : (t / 60).toFixed(1).padStart(6);
const RUNS = [['planner', true], ['planner', false], ['greedy', true], ['cautious', true], ['random', true]].filter(([n]) => !ONLY || n === ONLY);
const KEYS = ['lodge', 'e1k', 'bellows', 'ring1', 'e10k', 'ring2', 'e100k', 'ring3', 'e1m', 'forge'];
console.log(`Minutes to each milestone (seed${SEEDS > 1 ? 's 1..' + SEEDS : ' 1'})`);
console.log('bot              ' + KEYS.map(k => k.padStart(6)).join(' ') + '   cracks  actions');
const finish = {};
for (const [name, active] of RUNS) {
  const label = name + (active ? '' : ' (idle)');
  const acc = {}; let cracks = 0, acts = 0, fin = 0, finN = 0;
  for (let seed = 1; seed <= SEEDS; seed++) {
    const t0 = Date.now();
    const { s, actions, log } = run(name, seed, active);
    if (TRACE) console.log(log.join('\n'));
    for (const k of KEYS) if (k in s.ms) (acc[k] = acc[k] || []).push(s.ms[k]);
    cracks += s.stats.cracks; acts += actions;
    if (s.done) { fin += s.ms.forge; finN++; }
    if (TRACE) console.error(`${label} seed ${seed}: ${((Date.now() - t0) / 1000).toFixed(1)}s wall`);
  }
  const avg = k => acc[k] && acc[k].length === SEEDS ? acc[k].reduce((a, b) => a + b, 0) / SEEDS : undefined;
  console.log(label.padEnd(17) + KEYS.map(k => fm(avg(k))).join(' ') + String(Math.round(cracks / SEEDS)).padStart(9) + String(Math.round(acts / SEEDS)).padStart(9));
  if (SENSIBLE.has(name)) finish[label] = finN === SEEDS ? fin / SEEDS : Infinity;
}

const vals = Object.values(finish);
if (vals.length >= 2) {
  const lo = Math.min(...vals), hi = Math.max(...vals);
  const spread = hi / lo;
  console.log(`\nSpread between sensible strategies: ${isFinite(spread) ? spread.toFixed(2) + 'x' : 'a bot never finished'} (limit 2x)`);
  console.log(`Fastest finish: ${(lo / 60).toFixed(1)} min (target ${TARGET[0] / 60}-${TARGET[1] / 60})`);
  const ok = isFinite(spread) && spread <= 2 && lo >= TARGET[0] && lo <= TARGET[1];
  console.log(ok ? 'BALANCE OK' : 'BALANCE OUT OF RANGE');
  process.exitCode = ok ? 0 : 1;
}
