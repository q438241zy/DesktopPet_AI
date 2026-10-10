/* Isolated browser; accelerated clock, no personal data or external APIs. */
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict'),crypto=require('node:crypto'),{pathToFileURL}=require('node:url');
const I=require('../docs/demo/companion-v01/idle-postures.js');
let chromium;try{({chromium}=require('playwright'));}catch{({chromium}=require(path.join(process.env.USERPROFILE,'.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright')));}
const root=path.resolve(__dirname,'..'),out=process.env.IDLE_REVIEW_DIRECTORY||path.join(root,'.artifacts/companion-v16-idle'),target=path.join(root,'Release/win-x64/Demo/CompanionV01/index.html');fs.mkdirSync(out,{recursive:true});
const hash=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex'),exe=path.join(root,'Release/win-x64/DesktopPet.exe'),before=hash(exe);
(async()=>{
 const browser=await chromium.launch({executablePath:path.join(process.env.ProgramFiles,'Google/Chrome/Application/chrome.exe'),headless:true});
 const page=await browser.newPage({viewport:{width:1440,height:1100}}),checks=[],errors=[],sequence=[];page.on('pageerror',e=>errors.push(e.message));
 await page.route(/^https?:/,r=>r.abort());await page.clock.setFixedTime(new Date('2026-10-10T10:00:00+08:00'));
 const snap=()=>page.evaluate(()=>companionDemo.snapshot()),advance=ms=>page.evaluate(ms=>companionDemo.advance(ms),ms),render=()=>page.evaluate(()=>companionDemo.render()),go=p=>page.evaluate(p=>companionDemo.navigate(p),p);
 async function check(name,run){await run();checks.push(name);console.log('PASS '+name);}
 async function home(){await go('partners');await page.locator('[data-posture=auto]').click();await render();}
 async function nextPose(){await advance((await snap()).idleClock.remaining);return snap();}
 async function capture(name){await page.waitForTimeout(150);await page.screenshot({path:path.join(out,name+'.png'),fullPage:true,animations:'disabled'});}
 async function fullWait(){const a=await snap();assert(a.idleClock.remaining>=1000&&a.idleClock.remaining<=2000);await advance(a.idleClock.remaining-1);assert.equal((await snap()).posture,a.posture);await advance(1);assert.notEqual((await snap()).posture,a.posture);}
 try{
  await check('clock endpoints, random cadence and single-step recovery after delayed ticks',async()=>{
   for(const [rng,expected] of [[()=>0,1000],[()=>.999999,2000]]){const c=new I.Clock('stand',rng);c.reset(0);assert.equal(c.next,expected);assert(!c.tick(expected-1,true));assert(c.tick(expected,true));assert.notEqual(c.pose,'stand');}
   const c=new I.Clock();c.reset(0);const durations=new Set();let previous=c.pose;for(let n=0;n<1000;n++){durations.add(c.duration);assert(c.duration>=1000&&c.duration<=2000);c.tick(c.next,true);assert.notEqual(c.pose,previous);previous=c.pose;}assert(durations.size>100);
   const n=c.changes;c.tick(c.next+3600000,true);assert.equal(c.changes,n+1);c.tick(c.next,false);assert(!c.running);c.tick(c.next+10000,true);assert.equal(c.changes,n+1);
  });
  await page.goto(pathToFileURL(target).href);await page.waitForFunction(()=>globalThis.companionDemo);await home();
  await check('five different poses change after 1–2 seconds without counting as user activity',async()=>{
   const a=await snap(),seen=new Set([a.posture]);for(let i=0;i<4;i++){await fullWait();const b=await snap();seen.add(b.posture);assert.equal(b.runtime.idleSince,a.runtime.idleSince);sequence.push(b.idleClock);}assert.equal(seen.size,5);await capture('01-fast-idle-home');
  });
  await check('fixed stand/sit survive reload; returning to auto waits a new interval',async()=>{
   for(const pose of ['stand','sit']){await page.locator(`[data-posture=${pose}]`).click();await render();await advance(120000);assert.equal((await snap()).posture,pose);}
   await page.reload();await page.waitForFunction(()=>globalThis.companionDemo);assert.equal((await snap()).postureMode,'sit');await home();await fullWait();
  });
  await check('pause, reduced motion, modal and hidden page suspend gestures without catch-up',async()=>{
   await home();await page.locator('#presence').click();await render();let p=(await snap()).posture;await advance(90000);assert.equal((await snap()).posture,p);await page.locator('#presence').click();await render();await fullWait();
   await go('settings');await page.locator('#reduced-motion').check();await render();p=(await snap()).posture;await advance(90000);assert.equal((await snap()).posture,p);await page.locator('#reduced-motion').uncheck();await home();await fullWait();
   await page.locator('#review-open').click();await render();p=(await snap()).posture;await advance(90000);assert.equal((await snap()).posture,p);await page.locator('[data-close=review-dialog]').click();await render();await fullWait();
   await page.evaluate(()=>{Object.defineProperty(document,'hidden',{configurable:true,value:true});document.dispatchEvent(new Event('visibilitychange'));});await render();p=(await snap()).posture;await advance(90000);assert.equal((await snap()).posture,p);
   await page.evaluate(()=>{delete document.hidden;document.dispatchEvent(new Event('visibilitychange'));});await render();await fullWait();
  });
  await check('idle gestures do not postpone one-minute hiding; finding remains user-controlled',async()=>{
   await home();await go('interaction');await render();const a=await snap();for(let i=0;i<59;i++)await advance(1000);assert.equal((await snap()).runtime.action,'idle');assert.equal((await snap()).runtime.idleSince,a.runtime.idleSince);assert((await snap()).idleClock.changes>a.idleClock.changes+20);
   await advance(1000);assert.equal((await snap()).runtime.action,'hiding');await advance(2300);assert.equal((await snap()).runtime.action,'hidden');await advance(120000);assert.equal((await snap()).runtime.action,'hidden');await capture('02-hidden-waits-for-user');
   await page.locator('#pet-stage').click({button:'right',position:{x:30,y:30}});assert.equal((await snap()).runtime.action,'hidden');await page.keyboard.press('Escape');await page.locator('#find-'+(await snap()).runtime.hideSide).click();assert.equal((await snap()).runtime.action,'found');await advance(1800);assert.equal((await snap()).runtime.action,'idle');
  });
  await check('right-click menu, unsent chat and petting take priority over idle gestures',async()=>{
   await go('settings');await page.locator('#auto-hide').uncheck();await go('interaction');await render();await advance(90000);assert.equal((await snap()).runtime.action,'idle');
   await page.locator('#pet-stage').click({button:'right',position:{x:30,y:30}});await render();let p=(await snap()).posture;await advance(5000);assert.equal((await snap()).posture,p);await page.keyboard.press('Escape');await render();await fullWait();
   await go('settings');await page.locator('#auto-hide').check();await go('interaction');await page.locator('#chat-input').fill('还没发送');await render();p=(await snap()).posture;await advance(90000);assert.equal((await snap()).posture,p);assert.equal((await snap()).runtime.action,'idle');
   await page.locator('#chat-input').fill('');await page.locator('#pet-body').click();await render();assert.equal((await snap()).runtime.action,'pat');await advance(1500);assert.equal((await snap()).runtime.action,'pat');assert.equal((await snap()).posture,p);await advance(1000);assert.equal((await snap()).runtime.action,'idle');await fullWait();
  });
  await check('all 64 appearances display five outfit-specific poses',async()=>{
   await home();let count=0;fs.mkdirSync(path.join(out,'poses'),{recursive:true});
   for(const style of ['chibi','realistic'])for(const clothes of ['original','swim','wedding','sports'])for(const family of ['whale','gpt','claude','gemini','grok','qwen','zhipu','kimi']){
    await page.evaluate(({family,style})=>{companionDemo.select(family);companionDemo.style(style);},{family,style});await page.locator(`[data-hero-outfit=${clothes}]`).click();await page.locator('[data-posture=auto]').click();await render();
    const initial=await snap(),seen=new Set();let limit=0;
    while(seen.size<5&&limit++<12){
     const s=await snap();assert.equal(s.selected,family);assert.equal(s.style,style);assert.equal(s.outfit,clothes);assert.equal(s.runtime.idleSince,initial.runtime.idleSince);assert(s.idleClock.duration>=1000&&s.idleClock.duration<=2000);
     if(!seen.has(s.posture)){
      await advance(Math.round(s.idleClock.remaining*.52));
      await page.waitForFunction(({family,style,clothes})=>{const c=document.querySelector('#hero-canvas'),pose=companionDemo.snapshot().posture,d=CLOUD_IDLE_ART[family][style][clothes][pose]||CLOUD_DATA.families[family].styles[style].looks[clothes][pose];return c.dataset.file===d.file&&c.dataset.pose===pose&&c.dataset.drawKey?.includes(d.file);},{family,style,clothes});
      await page.locator('#hero-canvas').screenshot({path:path.join(out,'poses',[style,clothes,family,s.posture].join('-')+'.png')});seen.add(s.posture);
     }
     await nextPose();
    }
    assert.equal(seen.size,5);count++;
   }assert.equal(count,64);
  });
  await check('all automatic frames reserve one unclipped body scale; older overlapping cells have ownership',async()=>{
   const result=await page.evaluate(()=>{
    const results=[];let masks=0;
    for(const [family,f] of Object.entries(CLOUD_DATA.families))for(const [style,s] of Object.entries(f.styles))for(const [clothes,base] of Object.entries(s.looks)){
     const appearance={...base,...CLOUD_IDLE_ART[family][style][clothes]},descs=CompanionIdlePostures.poses.map(k=>appearance[k]);let width=0,height=0;
     for(const d of descs)for(const n of d.frames){const c=d.cells[n],b=c.bounds;width=Math.max(width,Math.max(c.footX-b[0],b[2]-c.footX)*c.scale/d.reference);height=Math.max(height,c.visibleHeight*c.scale/d.reference);}
     if(style==='chibi'&&['swim','wedding'].includes(clothes)){for(const k of ['think','smile'])for(const n of appearance[k].frames){if(!appearance[k].cells[n].ownership)throw Error(family+'/'+clothes+'/'+k+' missing ownership');masks++;}if(appearance.smile.file===appearance.sit.file)throw Error('Smile still reuses a single-frame portrait');}
     for(const [w,h] of [[400,320],[296,420]]){const size=Math.min(h*.86,w*.47/width,h*.88/height);for(const d of descs)for(const n of d.frames){const c=d.cells[n],b=c.bounds,k=size*c.scale/d.reference;results.push({family,style,clothes,w,h,left:w*.5+(b[0]-c.footX)*k,right:w*.5+(b[2]-c.footX)*k,top:h*.95+(b[1]-c.footY)*k,bottom:h*.95+(b[3]-c.footY)*k});}}
    }return {results,masks};
   });assert(result.masks>=80);for(const b of result.results)assert(b.left>=0&&b.right<=b.w&&b.top>=0&&b.bottom<=b.h,JSON.stringify(b));
  });
  await check('stretch uses the eight source frames in order, without opacity flashing',async()=>{
   await page.evaluate(()=>{companionDemo.select('gemini');companionDemo.style('chibi');});await page.locator('[data-hero-outfit=sports]').click();await page.locator('[data-posture=auto]').click();await render();await go('interaction');await render();
   for(let i=0;(await snap()).posture!=='stretch'&&i<12;i++)await nextPose();const s=await snap();assert.equal(s.posture,'stretch');const frames=[];
   for(let i=0;i<8;i++){await advance(i?Math.ceil(s.idleClock.duration/8):1);await render();frames.push((await snap()).pose.frame);if(i===3)await capture('03-gemini-chibi-stretch');}assert.equal(new Set(frames).size,8);assert.equal(await page.locator('#hero-canvas').evaluate(el=>el.getAnimations().length),0);
   await page.evaluate(()=>companionDemo.style('realistic'));await render();for(let i=0;(await snap()).posture!=='stretch'&&i<12;i++)await nextPose();await advance(Math.floor((await snap()).idleClock.duration*.5));await capture('04-gemini-realistic-stretch');
  });
  await check('phone layout stays readable, native exe unchanged and browser errors absent',async()=>{
   await page.setViewportSize({width:390,height:844});await page.waitForTimeout(250);assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth+1),false);
   const room=await page.evaluate(()=>{const pet=document.querySelector('#pet-canvas').getBoundingClientRect(),input=document.querySelector('#inline-chat').getBoundingClientRect();return {highest:pet.top+pet.height*.08,inputBottom:input.bottom};});assert(room.highest>=room.inputBottom+8,JSON.stringify(room));
   await capture('05-phone');assert.equal(hash(exe),before);assert.deepEqual(errors,[]);assert.deepEqual((await snap()).errors,[]);
  });
  for(const style of ['chibi','realistic'])for(const clothes of ['original','swim','wedding','sports']){
   const cells=['whale','gpt','claude','gemini','grok','qwen','zhipu','kimi'].flatMap(f=>I.poses.map(p=>`<figure><img src="poses/${style}-${clothes}-${f}-${p}.png"><figcaption>${f} · ${p}</figcaption></figure>`)).join('');
   const file=path.join(out,`${style}-${clothes}.html`);fs.writeFileSync(file,`<!doctype html><meta charset="utf-8"><title>${style} ${clothes}</title><style>body{margin:0;background:#fff9fb;font:13px sans-serif}main{display:grid;grid-template-columns:repeat(5,240px)}figure{margin:0;text-align:center;border:1px solid #efdae4}img{width:238px;height:205px;object-fit:contain}figcaption{padding:4px}</style><main>${cells}</main>`);await page.setViewportSize({width:1200,height:1900});await page.goto(pathToFileURL(file).href);await page.waitForFunction(()=>[...document.images].every(i=>i.complete&&i.naturalWidth));await page.screenshot({path:path.join(out,`${style}-${clothes}.png`),fullPage:true});
  }
  fs.writeFileSync(path.join(out,'report.json'),JSON.stringify({passed:checks.length,checks,errors,sequence,appearances:64,poseCaptures:320,nativeExeSha256:before,nativeExeUnchanged:true},null,2));
 }catch(e){await page.screenshot({path:path.join(out,'failure.png'),fullPage:true}).catch(()=>{});throw e;}finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
