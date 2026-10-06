const fs = require('node:fs');
const path = require('node:path');

const [, , fullDemo, output] = process.argv;
if (!fullDemo || !output) throw new Error('Usage: node build-taskbar-lift-data.cjs <DeepSeek-demo.html> <data.js>');
const html = fs.readFileSync(fullDemo, 'utf8');
const start = '<script id="data" type="application/json">';
const embedded = html.split(start)[1]?.split('</script>')[0];
if (!embedded) throw new Error('Missing full-style Demo data.');
const source = JSON.parse(embedded);
const demoRoot = path.dirname(fullDemo);
const appearances = {};
const files = new Set();
for (const style of ['chibi', 'realistic']) {
  for (const outfit of ['original', 'swim', 'wedding', 'sports']) {
    const key = `${style}-${outfit}`, clips = source.clips[key];
    if (!clips?.idle || !clips?.pickup || !clips?.place || !clips?.walk) throw new Error(`Incomplete ${key} lift artwork.`);
    const imagePaths = action => {
      const clip = clips[action];
      if (!clip?.frames?.length) throw new Error(`Missing ${key}/${action} frame sequence.`);
      return clip.frames.map(index => {
        const relative = source.frames[index];
        if (!/^frames\/\d{5}\.webp$/.test(relative)) throw new Error(`Unsafe frame: ${relative}`);
        files.add(relative);
        return '../' + relative;
      });
    };
    appearances[key] = { idle: imagePaths('idle'), pickup: imagePaths('pickup'),
      land: imagePaths(clips.land ? 'land' : 'place'),
      walk: imagePaths('walk').slice(0, Math.ceil(clips.walk.cycleMs / source.sampleMs)),
      walkCycleMs: clips.walk.cycleMs, walkSpeed: clips.walk.walkSpeed / .8 * .9 };
  }
}
for (const file of files) {
  if (!fs.statSync(path.join(demoRoot, file)).isFile()) throw new Error(`Missing ${file}`);
}
fs.writeFileSync(output, 'window.TASKBAR_FRAMES=' + JSON.stringify({ appearances }) + ';\n');
console.log(`Taskbar lift Demo: ${Object.keys(appearances).length} appearances, ${files.size} verified rendered images.`);
