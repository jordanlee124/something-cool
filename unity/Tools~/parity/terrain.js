// Prints the terrain of a seed using the world generator inside ember-vale.html, so the C# port can be checked against it.
//   node terrain.js <seed> [path to ember-vale.html]   ->  "<w> <h> <x0> <y0>\n<digits>"
const fs = require('fs');
const seed = +process.argv[2];
const html = fs.readFileSync(process.argv[3] || require('path').join(__dirname, '../../../ember-vale.html'), 'utf8');
const from = html.indexOf('function hash2');
const to = html.indexOf('// Plain data copy for saving');
const src = html.slice(from, to).replace(/^let G = null;.*$/m, '');
const make = new Function('ROAD_SPEED', 'LUX_KEYS', 'CS', 'G', src + '\nreturn World;');
const World = make([1, 1.4, 1.65, 1.9], ['wine', 'salt', 'silk', 'spice'], 16, null);
const w = new World(seed);
w.growTo(0, 0, 2, 3);
console.log(`${w.w} ${w.h} ${w.x0} ${w.y0}`);
console.log(w.tiles.map(t => t.t).join(''));
