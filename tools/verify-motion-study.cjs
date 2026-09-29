const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const M=require('../docs/demo/motion-study/motion.js');
const root=path.resolve(__dirname,'..'),out=path.join(root,'artifacts/motion-study');fs.mkdirSync(out,{recursive:true});
let checks=0;const check=(condition,message)=>{assert.ok(condition,message);checks++;};
for(let p=0;p<1;p+=.002){
  const a=M.footAt(p,350),b=M.footAt(p+.5,350);
  check(a.stance||b.stance,'a slow walk always has a support foot');
  check(a.y<=0&&a.y>=-350*.028,'swing foot stays above floor without a high kick');
  if(p<.60){const next=M.footAt(p+.001,350);check(Math.abs((next.x-a.x)+.23*350*.001)<1e-7,'planted foot cannot slide in world coordinates');}
}
let seed=71;const random=()=>{seed=(seed*1664525+1013904223)>>>0;return seed/4294967296;};
const bag=new M.ShuffleBag(['tt','heart','wave'],random);let previous=null;
for(let round=0;round<30;round++){const selected=[];for(let i=0;i<3;i++){const next=bag.next();check(next!==previous,'random dance must not immediately repeat');previous=next;selected.push(next);}check(new Set(selected).size===3,'all three dances should appear in each bag');}
const idle=new M.IdleSchedule(random);let poseChanges=0,auto=0;
for(let now=0;now<60000;now+=100){const e=idle.poll(now);if(e?.kind==='pose')poseChanges++;if(e?.kind==='action')auto++;}
check(poseChanges>=5,'five-second idle changes keep running');check(auto>=1,'idle pose changes must not starve the long-wait action');
idle.reset(100000);check(idle.poll(140000,false)===null,'active interaction prevents idle interruption');
for(const hz of [30,60,120,144]){const w=new M.Walker();w.x=200;for(let i=0;i<hz*8;i++)w.advance(1000/hz,100,1200,350);check(Math.abs(w.x-(200+.23*350/1.65*8))<1e-6,'refresh-rate-independent walk distance');}
const slow=new M.Walker(),fast=new M.Walker();slow.x=fast.x=900;for(let i=0;i<200;i++)slow.advance(100,100,950,350);for(let i=0;i<2000;i++)fast.advance(10,100,950,350);check(Math.abs(slow.x-fast.x)<1e-6&&slow.direction===fast.direction,'turn holds consume remaining time consistently');
console.log('PASS',checks,'motion checks');

async function browser(){
  const tabs=await fetch('http://127.0.0.1:9237/json/list').then(r=>r.json()),target=tabs.find(t=>t.type==='page');
  const socket=new WebSocket(target.webSocketDebuggerUrl);await new Promise((r,j)=>{socket.onopen=r;socket.onerror=j;});
  let id=0;const pending=new Map(),errors=[];
  socket.onmessage=e=>{const m=JSON.parse(e.data);if(m.id){const p=pending.get(m.id);pending.delete(m.id);m.error?p.reject(Error(JSON.stringify(m.error))):p.resolve(m.result);}else if(m.method==='Runtime.exceptionThrown')errors.push(m.params.exceptionDetails);};
  const cdp=(method,params={})=>new Promise((resolve,reject)=>{const n=++id;pending.set(n,{resolve,reject});socket.send(JSON.stringify({id:n,method,params}));});
  const evaluate=async expression=>{const r=await cdp('Runtime.evaluate',{expression,awaitPromise:true,returnByValue:true});if(r.exceptionDetails)throw Error(JSON.stringify(r.exceptionDetails));return r.result.value;};
  await cdp('Runtime.enable');await cdp('Page.enable');await cdp('Emulation.setDeviceMetricsOverride',{width:1400,height:1100,deviceScaleFactor:1,mobile:false});
  await cdp('Page.navigate',{url:'file:///'+path.join(root,'docs/demo/motion-study/index.html').replaceAll('\\','/')+'?qa=1'});
  for(let i=0;i<100;i++){if(await evaluate('!!window.demoReady'))break;await new Promise(r=>setTimeout(r,150));}
  check(await evaluate('!!window.demoReady'),'real Chrome page loaded its offline image bundle');
  const snapshot=async(name,expression)=>{await evaluate(expression);await new Promise(r=>setTimeout(r,80));const shot=await cdp('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});fs.writeFileSync(path.join(out,name+'.png'),Buffer.from(shot.data,'base64'));};
  await snapshot('idle',`demoTest.reset();demoTest.setTime(1000)`);
  for(const key of ['tt','heart','wave','tickle']){
    await snapshot(key+'-mid',`demoTest.play('${key}');demoTest.setTime(${key==='tt'?3100:key==='tickle'?1850:2800})`);
    const s=await evaluate('demoTest.snapshot()');check(s.kind==='clip'&&s.action===key,'action '+key+' remains selected');
  }
  await evaluate("demoTest.walk();document.getElementById('feet').checked=true");
  for(let i=0;i<4;i++)await snapshot('walk-'+i,`demoTest.advance(${i===0?100:350})`);
  await evaluate("document.getElementById('feet').checked=false;demoTest.idle(0);demoTest.advance(51000)");
  check((await evaluate('demoTest.snapshot().kind'))==='auto','browser triggers spontaneous action after long idle');
  await evaluate("document.querySelector('[data-dance=tt]').click();document.querySelector('[data-action=tickle]').click();demoTest.pause()");
  check((await evaluate('demoTest.snapshot().action'))==='tickle','manual interaction cancels a dance immediately');
  await evaluate("document.getElementById('randomDance').click();demoTest.pause()");
  check((await evaluate('demoTest.snapshot().playlist'))===true,'random dance enables the playlist');
  const randomSequence=[];
  for(let i=0;i<6;i++){
    randomSequence.push(await evaluate('demoTest.snapshot().action'));
    await evaluate('demoTest.advance(9000);demoTest.advance(700)');
  }
  check(randomSequence.every((x,i)=>!i||x!==randomSequence[i-1]),'actual browser playlist never repeats immediately');
  check(new Set(randomSequence.slice(0,3)).size===3,'actual browser playlist covers all three dances');
  await evaluate("document.querySelector('[data-action=idle]').click();demoTest.pause()");
  check((await evaluate('demoTest.snapshot().playlist'))===false,'idle cancels random playlist');
  check(errors.length===0,'no browser JavaScript exception');
  if(process.argv.includes('--visuals')){
    for(const key of ['tt','heart','wave','tickle','walk']){
      const expression=`(()=>{
        document.getElementById('proof')?.remove();demoTest.reset();${key==='walk'?'demoTest.walk()':`demoTest.play('${key}')`};
        const proof=document.createElement('section');proof.id='proof';proof.style='position:fixed;inset:0;background:#fdf7fb;z-index:999;display:grid;grid-template-columns:repeat(4,260px);grid-template-rows:repeat(2,450px);gap:8px;padding:15px;align-content:start;';
        for(let i=0;i<8;i++){
          ${key==='walk'?'demoTest.advance(i?206.25:0)':'demoTest.setTime(420+i*'+(key==='tickle'?'380':'1000')+')'};
          const s=demoTest.snapshot(),source=document.getElementById('pet'),box=source.getBoundingClientRect(),dpr=source.width/box.width;
          const cell=document.createElement('div'),label=document.createElement('p'),canvas=document.createElement('canvas');
          label.textContent='${key} · '+i;label.style='font:14px Segoe UI;color:#95788c;margin:5px;text-align:center';canvas.width=260;canvas.height=405;canvas.style='position:static;width:260px;height:405px';
          canvas.getContext('2d').drawImage(source,(s.x-130)*dpr,(box.height-50-395)*dpr,260*dpr,405*dpr,0,0,260,405);
          cell.append(label,canvas);proof.append(cell);
        }
        document.body.append(proof);return true;
      })()`;
      await snapshot(key+'-sheet',expression);
    }
    await evaluate("document.getElementById('proof')?.remove()");
  }
  await cdp('Emulation.setDeviceMetricsOverride',{width:440,height:1000,deviceScaleFactor:1,mobile:false});
  await snapshot('mobile',"demoTest.reset();demoTest.setTime(1000)");
  check(await evaluate('document.documentElement.scrollWidth<=innerWidth'),'narrow layout does not overflow horizontally');
  const report={checks,errors,randomSequence,assets:await evaluate('demoTest.assets()'),browser:'Chrome headless, file:// URL, actual WebGL canvas',createdAt:new Date().toISOString()};
  fs.writeFileSync(path.join(out,'verification.json'),JSON.stringify(report,null,2));
  console.log('PASS browser, total',checks,'checks; screenshots',out);socket.close();
}
if(process.argv.includes('--browser'))browser().catch(e=>{console.error(e);process.exitCode=1;});
