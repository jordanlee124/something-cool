// Loads the simulation section of emberwright.html so Node tools run exactly the code the game ships.
const fs = require('fs');
const path = require('path');
module.exports = function loadSim() {
  const html = fs.readFileSync(path.join(__dirname, '..', 'emberwright.html'), 'utf8');
  const m = html.match(/\/\/ ==SIM-BEGIN==([\s\S]*?)\/\/ ==SIM-END==/);
  if (!m) throw new Error('SIM markers not found in emberwright.html');
  return new Function('module', m[1] + '\nreturn Sim;')(undefined);
};
