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
drop.release(350, true); check(drop.mode === 'dropping', 'Shift release drops');
let previous = drop.lift;
for (let t = 366; t < 1250 && drop.mode === 'dropping'; t += 16) {
  drop.tick(t); check(drop.lift <= previous, 'free fall never rises'); previous = drop.lift;
}
check(drop.lift === 0 && drop.mode === 'landing', 'falls onto the taskbar floor');
drop.tick(1700); check(drop.mode === 'walking', 'landing resumes walking without any daily check-in');
const walkX=drop.x;drop.tick(1800);check(drop.x>walkX && drop.walkMilliseconds>0,'automatic walking moves with a gait');

const slow = new LiftController(); slow.down(0, 0, 0);
slow.move(0, -35, 200); slow.move(0, -90, 460); slow.move(0, -150, 730);
slow.release(1230); slow.tick(2000);
check(slow.mode === 'placed' && slow.lift === 150, 'manual placement does not fall later');

for (const [heldMs, pauseMs] of [[30,0],[320,0],[650,450],[1000,0],[5000,4000]]) {
  for (const shiftHeld of [false,true]) {
    const gesture = new LiftController(); gesture.down(0,0,0); gesture.move(0,-110,heldMs-pauseMs);
    gesture.release(heldMs,shiftHeld);
    check(gesture.mode === (shiftHeld ? 'dropping' : 'placed'), 'speed and dwell never override release-time Shift');
    if (!shiftHeld) { gesture.tick(10000); check(gesture.lift===110, 'ordinary placement never falls later'); }
  }
}

const longMove = new LiftController(); longMove.down(0, 0, 0);
for (let t = 100; t <= 1000; t += 100) longMove.move(0, -t * .15, t);
longMove.release(1000); check(longMove.mode === 'placed', 'slow upward move stays elevated');

const shift = new LiftController(); shift.down(0, 0, 0); shift.move(0, -150, 170);
shift.release(260, true); check(shift.mode === 'dropping', 'Shift is an explicit drop request');

const air = new LiftController(); air.down(0, 0, 0); air.move(0, -150, 200); air.release(900);
air.down(0, 0, 1000); check(!air.originTaskbar, 're-grip starts above the taskbar');
air.move(20, -30, 1050); air.release(1100);
check(air.mode === 'placed' && air.lift === 180, 'repositioning an elevated pet stays put');
air.down(0,0,1200); air.release(1300,true);
check(air.mode === 'dropping', 'Shift drops from an elevated origin too');

const catchFall = new LiftController(); catchFall.down(0, 0, 0);
catchFall.move(0, -155, 200); catchFall.release(350,true); catchFall.tick(570);
check(catchFall.mode === 'dropping' && catchFall.lift > 0, 'free fall can be intercepted');
catchFall.down(0, -75, 580); check(!catchFall.originTaskbar, 'catch does not inherit taskbar origin');
check(catchFall.pickupStartedAt===0,'catch preserves the lifted animation instead of restarting it');
catchFall.move(0, -115, 800); catchFall.release(820); catchFall.tick(1800);
check(catchFall.mode === 'placed' && catchFall.lift > 0, 'caught pet stays after release');

const tiny = new LiftController(); tiny.down(0, 0, 0); tiny.move(0, -20, 100); tiny.release(120);
check(tiny.mode === 'landing' && tiny.lift === 0, 'tiny movement settles without initiating falling');
for(const height of [0,20,24]) for(const modifier of [false,true]) {
  const near = new LiftController(); near.down(0,0,0); near.move(0,-height,100); near.release(120,modifier); near.tick(1000);
  check(near.mode==='walking' && near.lift===0,'24px floor magnet starts walking without a falling loop');
}
const disabled=new LiftController({autoWalk:false});disabled.down(0,0,0);disabled.move(0,-130,150);disabled.release(200,true);
for(let t=216;t<=1700;t+=16)disabled.tick(t);check(disabled.mode==='grounded','disabled automatic walking remains awake and still');
const cancel=new LiftController();cancel.down(0,0,0);cancel.release(100,false,false);cancel.tick(500);check(cancel.mode==='grounded','floor cancellation pauses automatic walking');
for(const hz of [30,60,75,120,144]) {
  const walker=new LiftController();walker.tick(0);
  for(let i=1;i<=hz*3;i++)walker.tick(i*1000/hz);
  check(Math.abs(walker.x-148.5)<1e-7 && Math.abs(walker.walkMilliseconds-3000)<1e-7,'walk displacement follows presentation time');
}
const edge=new LiftController();edge.x=164;edge.tick(0);edge.tick(100);const arrived=edge.walkMilliseconds;
check(edge.x===165 && edge.direction===1,'edge arrival plants the foot before turning');edge.tick(170);
check(edge.direction===1 && edge.walkMilliseconds===arrived,'edge hold keeps the arriving pose');edge.tick(200);
check(edge.direction===-1 && edge.x<165,'turn changes facing and consumes remaining time');

const reduced = new LiftController(); reduced.down(0, 0, 0); reduced.move(0, -130, 150);
reduced.release(200,true); reduced.tick(220, true);
check(reduced.mode === 'landing' && reduced.lift === 0, 'reduced motion lands without gravity animation');
reduced.tick(380,true);check(reduced.mode==='grounded','reduced-motion landing ends');
reduced.down(0,0,400);reduced.move(0,-130,500);reduced.release(550);reduced.tick(1000,true);
check(reduced.mode==='placed' && reduced.lift===130,'reduced motion preserves ordinary placement');

const demoRoot = path.resolve(__dirname, '../Release/win-x64/Demo');
const pageRoot = path.join(demoRoot, 'TaskbarLift');
const html = fs.readFileSync(path.join(pageRoot, 'index.html'), 'utf8');
const context = { window: {} };
vm.runInNewContext(fs.readFileSync(path.join(pageRoot, 'data.js'), 'utf8'), context);
const bundle = context.window.TASKBAR_FRAMES;
check(!!bundle && Object.keys(bundle.appearances).length === 6, 'both styles and three outfits are present');
for (const [key, clips] of Object.entries(bundle.appearances)) {
  check(clips.walkCycleMs===960 && new Set(clips.walk).size===12, `${key} reuses all twelve walking poses`);
  for (const name of ['idle', 'pickup', 'land', 'walk']) {
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
// Execute the real UI handlers, including release-time modifiers and cancellation.
const elements=new Map(),listeners={};let now=0,tickUI;
function element(id) {
  if(!elements.has(id)) elements.set(id,{style:{},dataset:{},clientWidth:900,clientHeight:760,
    setAttribute(){},replaceChildren(){},setPointerCapture(){}});
  return elements.get(id);
}
const ui={window:{TASKBAR_FRAMES:bundle,TaskbarLift:{LiftController},addEventListener:(key,fn)=>listeners[key]=fn,
    matchMedia:()=>({matches:false})},document:{getElementById:element,createElement:()=>({setAttribute(){}})},
  Image:class{},performance:{now:()=>now},URLSearchParams,location:{search:''},setInterval(fn){tickUI=fn;}};
vm.runInNewContext(fs.readFileSync(path.join(pageRoot,'app.js'),'utf8'),ui);
const grip=element('grip'),stage=element('stage'),pet=element('pet');
const downUI=(shiftKey=false)=>grip.onpointerdown({button:0,pointerId:1,clientX:0,clientY:0,shiftKey,preventDefault(){}});
const moveUI=(shiftKey=false)=>grip.onpointermove({clientX:0,clientY:-130,shiftKey});
downUI(true);now=100;moveUI(true);const placedTop=pet.style.top;
grip.onpointerup({shiftKey:false});grip.onlostpointercapture({shiftKey:true});
check(stage.dataset.mode==='placed' && pet.style.top===placedTop,'releasing Shift before pointer-up stays placed');
now=200;downUI();grip.onpointerup({shiftKey:true});grip.onlostpointercapture({shiftKey:true});
check(stage.dataset.mode==='dropping','pressing Shift only at pointer-up drops; capture loss preserves it');
now=300;downUI(true);grip.onpointercancel({shiftKey:true});
check(stage.dataset.mode==='placed','pointer cancellation with Shift must not initiate gravity');
downUI(true);grip.onlostpointercapture({shiftKey:true});check(stage.dataset.mode==='placed','unexpected capture loss stays placed');
downUI(true);listeners.blur();check(stage.dataset.mode==='placed','losing focus cancels the gesture safely');
downUI();listeners.keydown({shiftKey:true});check(element('outcome').textContent==='松手会下落','live Shift hint reflects keyboard-down');
listeners.keyup({shiftKey:false});check(element('outcome').textContent==='松手会停住','live Shift hint reflects keyboard-up');
grip.onpointerup({shiftKey:false});
grip.onkeydown({key:'Enter',shiftKey:true,preventDefault(){}});check(stage.dataset.mode==='dropping','Shift+Enter keyboard demo drops');
grip.onkeydown({key:' ',shiftKey:false,preventDefault(){}});check(stage.dataset.mode==='placed','ordinary keyboard placement catches and stops');
element('reset').onclick();now+=16;tickUI();check(stage.dataset.mode==='walking' && element('stateLabel').textContent==='任务栏散步','actual Demo starts awake and walks on taskbar');
const firstWalkingImage=element('sprite').src,firstWalkingLeft=pet.style.left;
now+=200;tickUI();check(pet.style.left!==firstWalkingLeft && element('sprite').src!==firstWalkingImage,'real UI timer advances position and rendered walking pose');
for(const file of ['index.html','style.css','lift-logic.js','app.js']) {
  check(fs.readFileSync(path.join(pageRoot,file),'utf8')===fs.readFileSync(path.resolve(__dirname,'../docs/demo/taskbar-lift',file),'utf8'),file+' installed copy matches source');
}
console.log(`PASS ${checks} Shift-only lift decisions, actual input handlers, re-grip, fall, reduced-motion and offline artwork checks.`);
