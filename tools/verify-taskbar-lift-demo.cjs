const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const { LiftController } = require('../docs/demo/taskbar-lift/lift-logic.js');

let checks = 0;
function check(condition, message) { assert.ok(condition, message); checks++; }
const drop = new LiftController();
drop.down(0, 0, 0); drop.move(0, -150, 200);
check(drop.mode === 'dragging' && drop.originTaskbar && drop.lift === 150, 'ground start stays held');
check(!drop.canPlace(320), 'quick lift remains a drop candidate');
drop.release(350); check(drop.mode === 'dropping', 'quick release drops');
let previous = drop.lift;
for (let t = 366; t < 1250 && drop.mode === 'dropping'; t += 16) {
  drop.tick(t); check(drop.lift <= previous, 'free fall never rises'); previous = drop.lift;
}
check(drop.lift === 0 && drop.mode === 'landing', 'falls onto the taskbar floor');
drop.tick(1700); check(drop.mode === 'grounded', 'landing returns to taskbar idle');

const slow = new LiftController(); slow.down(0, 0, 0);
slow.move(0, -35, 200); slow.move(0, -90, 460); slow.move(0, -150, 730);
check(slow.canPlace(1230), 'paused in-air lift becomes deliberate placement');
slow.release(1230); slow.tick(2000);
check(slow.mode === 'placed' && slow.lift === 150, 'manual placement does not fall later');

const hover = new LiftController(); hover.down(0, 0, 0); hover.move(0, -110, 200);
check(!hover.canPlace(649), '449 ms pause is not enough');
check(hover.canPlace(650), '450 ms in-air pause confirms placement');
hover.release(650); check(hover.mode === 'placed', 'hovered lift stays where released');

const longMove = new LiftController(); longMove.down(0, 0, 0);
for (let t = 100; t <= 1000; t += 100) longMove.move(0, -t * .15, t);
check(longMove.canPlace(1000), 'continuous deliberate slow move confirms placement');
longMove.release(1000); check(longMove.mode === 'placed', 'slow upward move stays elevated');

const shift = new LiftController(); shift.down(0, 0, 0); shift.move(0, -150, 170);
shift.release(260, true); check(shift.mode === 'placed', 'Shift overrides the fast-drop gesture');

const air = new LiftController(); air.down(0, 0, 0); air.move(0, -150, 200); air.release(900);
air.down(0, 0, 1000); check(!air.originTaskbar, 're-grip starts above the taskbar');
air.move(20, -30, 1050); air.release(1100);
check(air.mode === 'placed' && air.lift === 180, 'repositioning an elevated pet stays put');

const catchFall = new LiftController(); catchFall.down(0, 0, 0);
catchFall.move(0, -155, 200); catchFall.release(350); catchFall.tick(570);
check(catchFall.mode === 'dropping' && catchFall.lift > 0, 'free fall can be intercepted');
catchFall.down(0, -75, 580); check(!catchFall.originTaskbar, 'catch does not inherit taskbar origin');
catchFall.move(0, -115, 800); catchFall.release(820); catchFall.tick(1800);
check(catchFall.mode === 'placed' && catchFall.lift > 0, 'caught pet stays after release');

const tiny = new LiftController(); tiny.down(0, 0, 0); tiny.move(0, -20, 100); tiny.release(120);
check(tiny.mode === 'grounded' && tiny.lift === 0, 'tiny movement does not initiate falling');

const reduced = new LiftController(); reduced.down(0, 0, 0); reduced.move(0, -130, 150);
reduced.release(200); reduced.tick(220, true);
check(reduced.mode === 'landing' && reduced.lift === 0, 'reduced motion lands without gravity animation');

const demoRoot = path.resolve(__dirname, '../Release/win-x64/Demo');
const pageRoot = path.join(demoRoot, 'TaskbarLift');
const html = fs.readFileSync(path.join(pageRoot, 'index.html'), 'utf8');
const context = { window: {} };
vm.runInNewContext(fs.readFileSync(path.join(pageRoot, 'data.js'), 'utf8'), context);
const bundle = context.window.TASKBAR_FRAMES;
check(!!bundle && Object.keys(bundle.appearances).length === 6, 'both styles and three outfits are present');
for (const [key, clips] of Object.entries(bundle.appearances)) {
  for (const name of ['idle', 'pickup', 'land']) {
    check(clips[name]?.length > 0, `${key}/${name} has rendered poses`);
    for (const relative of new Set(clips[name])) {
      check(/^\.\.\/frames\/\d{5}\.webp$/.test(relative), 'image path remains local and relative');
      const bytes = fs.readFileSync(path.resolve(pageRoot, relative));
      check(bytes.toString('ascii', 0, 4) === 'RIFF' && bytes.toString('ascii', 8, 12) === 'WEBP' && bytes.includes(Buffer.from('VP8L')), 'rendered image is lossless WebP');
    }
  }
}
for (const script of ['data.js', 'lift-logic.js', 'app.js']) check(html.includes(`src="${script}"`), `${script} linked`);
check(html.includes('../MotionStudy/index.html'), 'permanent Demo navigation works');
console.log(`PASS ${checks} lift decisions, re-grip, fall, reduced-motion and offline artwork checks.`);
