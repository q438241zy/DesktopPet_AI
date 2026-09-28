const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm'),assert=require('node:assert/strict');
const file=process.argv[2],html=fs.readFileSync(file,'utf8');
const scripts=[...html.matchAll(/<script(?:\s[^>]*)?>([\s\S]*?)<\/script>/g)].map(x=>x[1]);
const script=scripts.at(-1);new vm.Script(script);
const body=script.slice(script.indexOf('let last=performance.now();'),script.indexOf('renderButtons();matrix();requestAnimationFrame(tick);'));
const elements={loop:{checked:false},timeline:{value:0},timeLabel:{textContent:''}};
let source='';const state={action:'walk',elapsed:0,walkTime:0,turnRemaining:0,x:300,y:300,dir:1,drop:false,arena:{clientWidth:1000},image:{style:{},getAttribute:()=>source,set src(value){source=value;}}};
const data={sampleMs:40,frames:Array.from({length:24},(_,i)=>'frame-'+i)};
const clip={walkSpeed:44,cycleMs:960,duration:3000,frames:Array.from({length:76},(_,i)=>i%24)};
const context={performance:{now:()=>0},states:[state],paused:false,D:data,clip:()=>clip,$:id=>elements[id],place:()=>{},floor:()=>300,setAction:(s,key)=>{s.action=key;},updateShakeFeedback:()=>{},requestAnimationFrame:()=>{}};
vm.createContext(context);vm.runInContext(body,context);
let now=0;function step(dt=40){now+=dt;context.tick(now);}
for(let i=0;i<100;i++)step();const after4s=source;step();assert.notEqual(source,after4s,'walking must continue animating after the 3 second demo clip');
assert.ok(Math.abs(state.x-(300+44*4.04))<1e-8,'distance must match the gait speed');
const approaching=state.walkTime;
state.x=934;state.dir=1;step();assert.equal(state.x,935);assert.equal(state.dir,1);
assert.ok(Math.abs(state.walkTime-approaching-1000/44)<1e-8,'the arrival pose follows the last travelled distance');
const arrived=state.walkTime;
for(let i=0;i<3;i++)step();assert.equal(state.x,935);assert.equal(state.dir,1);assert.equal(state.walkTime,arrived,'the turn holds the arrival pose');
step();assert.equal(state.dir,-1);assert.equal(state.image.style.transform,'scaleX(-1)');assert.ok(state.x<935,'remaining frame time advances after the hold');
const beforeDelay=state.x,phaseBeforeDelay=state.walkTime;step(120);
assert.ok(Math.abs(state.x-beforeDelay+44*.12)<1e-8,'a delayed rendering frame must not drop walking time');
assert.ok(Math.abs(state.walkTime-phaseBeforeDelay-120)<1e-8);
context.paused=true;const frozen=state.x,frozenFrame=source;step();assert.equal(state.x,frozen);assert.equal(source,frozenFrame);
context.paused=false;state.loading=true;step();assert.equal(state.x,frozen,'gait must wait for image decoding');assert.equal(source,frozenFrame);
state.loading=false;step();assert.ok(state.x<frozen,'gait resumes when decoding completes');
const match=html.match(/<script id="data" type="application\/json">([\s\S]*?)<\/script>/);
if(match && !match[1].includes('__DEMO_DATA__')){
 const exported=JSON.parse(match[1]);assert.equal(exported.sampleMs,40);assert.equal(exported.frameWidth,560);assert.equal(exported.frameHeight,680);
 assert.equal(Object.keys(exported.clips).length,9);
 for(const [key,appearance] of Object.entries(exported.clips)){
  if(key.startsWith('adult-')){assert.equal(appearance.dance.duration,8000,'dance must finish all sixteen counts');assert.equal(appearance.dance.frames.length,201);assert.ok(new Set(appearance.dance.frames).size>180,'dance must contain the rendered continuous choreography');}
  else assert.equal(appearance.dance,undefined,'dance remains exclusive to adult appearances');
 }
 for(const appearance of Object.values(exported.clips)){assert.equal(appearance.walk.cycleMs,960);assert.equal(new Set(appearance.walk.frames.slice(0,24)).size,12);assert.ok(new Set(appearance.shake.frames).size>8,'all appearances need moving stars while lifted');assert.ok(new Set(appearance.dizzy.frames).size>8,'all appearances need visible recovery feedback');}
 assert.ok(fs.statSync(file).size<5_000_000,'HTML must stay small enough to open promptly');
 for(const image of exported.frames){
  assert.match(image,/^frames\/\d{5}\.webp$/,'offline frames must remain relative to the HTML');
  const bytes=fs.readFileSync(path.join(path.dirname(file),image));
  assert.equal(bytes.toString('ascii',0,4),'RIFF');assert.equal(bytes.toString('ascii',8,12),'WEBP');
  assert.ok(bytes.includes(Buffer.from('VP8L')),'lossless WebP required');
 }
 console.log('PASS: native dimensions, nine appearances, twelve poses per gait, portable relative paths and all lossless WebP files');
}
console.log('PASS: syntax, continuous gait beyond clip duration, travelled distance, planted turn and pause');
function travel(dt,count,start=500){Object.assign(state,{x:start,dir:1,turnRemaining:0,walkTime:0});for(let i=0;i<count;i++)step(dt);return {x:state.x,phase:state.walkTime,dir:state.dir};}
for(const hz of [30,60,75,120,144]){const result=travel(1000/hz,hz*3);assert.ok(Math.abs(result.x-632)<1e-7);assert.ok(Math.abs(result.phase-3000)<1e-7);}
const fastTurn=travel(10,400,930),slowTurn=travel(200,20,930);assert.ok(Math.abs(fastTurn.x-slowTurn.x)<1e-7);assert.ok(Math.abs(fastTurn.phase-slowTurn.phase)<1e-7);assert.equal(fastTurn.dir,slowTurn.dir);
console.log('PASS: 30/60/75/120/144 Hz distance, delayed frames and refresh-independent boundary turns');
const shakeContext={setAction:(s,key)=>{s.action=key;}};vm.createContext(shakeContext);
vm.runInContext(script.slice(script.indexOf('function newShake'),script.indexOf('function run(key)')),shakeContext);
const shake=()=>shakeContext.newShake(0,0,0),move=(s,x,y,t)=>shakeContext.trackShake(s,x,y,t);
let gesture=shake();for(let i=1;i<=12;i++)move(gesture,i%2?8:-8,i%2?9:-9,i*40);assert.equal(gesture.until,0,'jitter must not cause dizziness');
gesture=shake();for(let i=1;i<=10;i++)move(gesture,i*80,i*30,i*50);assert.equal(gesture.until,0,'ordinary travel is not shaking');
gesture=shake();for(let i=1;i<=10;i++)move(gesture,i%2?50:0,0,i*450);assert.equal(gesture.until,0,'slow corrections are ignored');
gesture=shake();for(let i=1;i<=3;i++)move(gesture,i%2?100:0,i%2?100:0,i*100);assert.equal(gesture.until,0,'diagonal reversals cannot double count');move(gesture,0,0,400);assert.equal(move(gesture,100,100,500),true);assert.equal(gesture.until,3500);
gesture=shake();for(let i=1;i<=5;i++)move(gesture,0,i%2?100:0,i*100);assert.equal(gesture.until,3500,'vertical shaking works');
gesture=shake();move(gesture,160,0,100);move(gesture,156,0,105);move(gesture,159,0,110);move(gesture,0,0,200);assert.equal(gesture.axes[0].reversals.length,1,'apex jitter must not erase the stroke');
move(gesture,160,0,300);move(gesture,0,0,400);move(gesture,160,0,1200);assert.equal(gesture.until,0,'a pause starts a fresh gesture');
for(const held of [true,false]){const s={held,action:held?'shake':'dizzy',shake:{until:3500},drop:false,x:200,y:170};shakeContext.updateShakeFeedback(s,3499);assert.equal(s.action,held?'shake':'dizzy');shakeContext.updateShakeFeedback(s,3500);assert.equal(s.action,held?'pickup':'place');assert.equal(s.drop,false);assert.equal(s.y,170);assert.equal(s.shake,null);}
console.log('PASS: horizontal/vertical shaking, jitter, slow drag, diagonal counting, stale gestures and recovery without falling');
const grip={setPointerCapture:()=>{}},pointerState={x:200,y:300,arena:{clientWidth:500},composer:{hidden:true},action:'idle',drop:false};
const pointerContext={card:{querySelector:()=>grip},s:pointerState,performance:{now:()=>pointerContext.now},now:0,
 setAction:(s,key)=>{s.action=key;},floor:()=>300,place:()=>{}};
vm.createContext(pointerContext);
vm.runInContext(script.slice(script.indexOf('function newShake'),script.indexOf('function run(key)'))+
 script.slice(script.indexOf(" const grip=card.querySelector('.grip');"),script.indexOf(" s.composer.querySelector('form').onsubmit")),pointerContext);
function down(){grip.onpointerdown({button:0,pointerId:1,clientX:0,clientY:0,preventDefault:()=>{}});}
function pointerMove(x,y,t){pointerContext.now=t;grip.onpointermove({clientX:x,clientY:y});}
down();pointerMove(70,-100,100);grip.onpointerup();grip.onlostpointercapture();assert.equal(pointerState.action,'place');assert.equal(pointerState.y,200);
pointerContext.now=1000;down();for(let i=1;i<=5;i++){pointerMove(i%2?80:0,0,1000+i*100);assert.equal(pointerState.action,i<5?'pickup':'shake');}
assert.equal(pointerState.y,200);grip.onpointerup();grip.onlostpointercapture();assert.equal(pointerState.action,'dizzy','pointer-up must preserve recovery through automatic capture loss');assert.equal(pointerState.drop,false);
pointerContext.updateShakeFeedback(pointerState,4501);assert.equal(pointerState.action,'place');assert.equal(pointerState.y,200);
pointerContext.now=5000;down();for(let i=1;i<=5;i++)pointerMove(i%2?80:0,0,5000+i*100);assert.equal(pointerState.action,'shake');grip.onpointercancel();assert.equal(pointerState.action,'place');assert.equal(pointerState.shake,null);
console.log('PASS: actual Demo pointer handlers distinguish ordinary drag, repeated shake, release, capture loss and cancellation');
(async()=>{
 const pending=[];
 const preloadContext={D:{frames:['old.webp','current.webp','missing.webp']},Image:class{decode(){return new Promise((resolve,reject)=>pending.push({resolve,reject}));}}};
 vm.createContext(preloadContext);
 vm.runInContext(script.slice(script.indexOf('async function prepareFrames'),script.indexOf('function run(key)')),preloadContext);
 const s={loadGeneration:1,loading:true,preloaded:[],detail:{textContent:''}};
 const old=preloadContext.prepareFrames(s,{frames:[0]},1);s.loadGeneration=2;
 const current=preloadContext.prepareFrames(s,{frames:[1,1]},2);
 assert.equal(pending.length,2,'duplicate animation frames decode only once');
 pending[0].resolve();await old;assert.equal(s.loading,true);assert.equal(s.preloaded.length,0,'stale action must not replace the current preload');
 pending[1].resolve();await current;assert.equal(s.loading,false);assert.equal(s.preloaded[0].src,'current.webp');
 s.loadGeneration=3;s.loading=true;const missing=preloadContext.prepareFrames(s,{frames:[2]},3);
 pending[2].reject(new Error('missing frame'));await missing;assert.equal(s.loading,true);assert.match(s.detail.textContent,/frames/);
 console.log('PASS: preload deduplication, action-switch race and missing-file feedback');
})().catch(error=>{console.error(error);process.exitCode=1;});
