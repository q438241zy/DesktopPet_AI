const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm'),assert=require('node:assert/strict');
const file=process.argv[2],html=fs.readFileSync(file,'utf8');
const scripts=[...html.matchAll(/<script(?:\s[^>]*)?>([\s\S]*?)<\/script>/g)].map(x=>x[1]);
const script=scripts.at(-1);new vm.Script(script);
const body=script.slice(script.indexOf('let last=performance.now();'),script.indexOf('renderButtons();matrix();requestAnimationFrame(tick);'));
const elements={loop:{checked:false},timeline:{value:0},timeLabel:{textContent:''}};
let source='';const state={action:'walk',elapsed:0,walkTime:0,turnRemaining:0,x:300,y:300,dir:1,drop:false,arena:{clientWidth:1000},image:{style:{},getAttribute:()=>source,set src(value){source=value;}}};
const data={sampleMs:40,frames:Array.from({length:24},(_,i)=>'frame-'+i)};
const clip={walkSpeed:44,cycleMs:960,duration:3000,frames:Array.from({length:76},(_,i)=>i%24)};
const context={performance:{now:()=>0},states:[state],paused:false,D:data,clip:()=>clip,$:id=>elements[id],place:()=>{},floor:()=>300,setAction:(s,key)=>{s.action=key;},requestAnimationFrame:()=>{}};
vm.createContext(context);vm.runInContext(body,context);
let now=0;function step(dt=40){now+=dt;context.tick(now);}
for(let i=0;i<100;i++)step();const after4s=source;step();assert.notEqual(source,after4s,'walking must continue animating after the 3 second demo clip');
assert.ok(Math.abs(state.x-(300+44*4.04))<1e-8,'distance must match the gait speed');
state.x=934;state.dir=1;step();assert.equal(state.x,935);assert.equal(state.dir,1);assert.equal(state.walkTime,0);
for(let i=0;i<3;i++)step();assert.equal(state.x,935);assert.equal(state.dir,1);
step();assert.equal(state.dir,-1);assert.equal(state.image.style.transform,'scaleX(-1)');step();assert.ok(state.x<935);assert.equal(state.walkTime,40);
context.paused=true;const frozen=state.x,frozenFrame=source;step();assert.equal(state.x,frozen);assert.equal(source,frozenFrame);
context.paused=false;state.loading=true;step();assert.equal(state.x,frozen,'gait must wait for image decoding');assert.equal(source,frozenFrame);
state.loading=false;step();assert.ok(state.x<frozen,'gait resumes when decoding completes');
const match=html.match(/<script id="data" type="application\/json">([\s\S]*?)<\/script>/);
if(match && !match[1].includes('__DEMO_DATA__')){
 const exported=JSON.parse(match[1]);assert.equal(exported.sampleMs,40);assert.equal(exported.frameWidth,560);assert.equal(exported.frameHeight,680);
 assert.equal(Object.keys(exported.clips).length,9);
 for(const appearance of Object.values(exported.clips)){assert.equal(appearance.walk.cycleMs,960);assert.equal(new Set(appearance.walk.frames.slice(0,24)).size,12);}
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
