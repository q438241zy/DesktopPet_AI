/* Fresh browser, GPT character, real elapsed time. Capture only this Demo. */
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict'),{pathToFileURL}=require('node:url');
let chromium;try{({chromium}=require('playwright'));}catch{({chromium}=require(path.join(process.env.USERPROFILE,'.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright')));}
const root=path.resolve(__dirname,'..'),out=path.join(root,'.artifacts/companion-v17-play');
(async()=>{
 const browser=await chromium.launch({executablePath:path.join(process.env.ProgramFiles,'Google/Chrome/Application/chrome.exe'),headless:true});
 const page=await browser.newPage({viewport:{width:1180,height:900}}),errors=[];page.on('pageerror',e=>errors.push(e.message));await page.route(/^https?:/,r=>r.abort());
 try{
  await page.goto(pathToFileURL(path.join(root,'Release/win-x64/Demo/CompanionV01/index.html')).href);await page.waitForFunction(()=>globalThis.companionDemo);
  await page.evaluate(()=>{companionDemo.select('gpt');companionDemo.style('realistic');companionDemo.navigate('interaction');companionDemo.action('butterfly');});await page.waitForFunction(()=>companionDemo.snapshot().play.active&&!companionDemo.snapshot().play.loading);
  const samples=await page.evaluate(()=>new Promise(resolve=>{const points=[],start=performance.now();function sample(){const s=companionDemo.snapshot();points.push({at:performance.now()-start,time:s.play.time,x:s.play.frame?.offsetPixels,lead:s.play.frame?.butterflyPixels?.lead});if(performance.now()-start<1800)requestAnimationFrame(sample);else resolve(points);}sample();}));
  const distinct=new Set(samples.map(s=>s.x?.toFixed(3)));assert(distinct.size>=30,'Walking must not advance only on the 100 ms UI timer');assert(samples.every(s=>s.lead>=40),'Butterfly should lead in front of the character');
  await page.screenshot({path:path.join(out,'10-gpt-real-walking.png'),fullPage:true});
  await page.evaluate(()=>companionDemo.action('gift'));await page.waitForFunction(()=>companionDemo.snapshot().play.active&&!companionDemo.snapshot().play.loading);await page.waitForTimeout(700);await page.locator('#presence').click();
  const frozen=await page.evaluate(()=>companionDemo.snapshot().play.time);await page.waitForTimeout(600);assert.equal(await page.evaluate(()=>companionDemo.snapshot().play.time),frozen);await page.locator('#presence').click();
  const start=Date.now();await page.waitForFunction(()=>Object.values(companionDemo.snapshot().collection).reduce((a,b)=>a+b,0)===1);await page.waitForTimeout(650);await page.screenshot({path:path.join(out,'11-gpt-real-gift.png'),fullPage:true});
  await page.waitForFunction(()=>!companionDemo.snapshot().play.active);assert.equal(await page.evaluate(()=>Object.values(companionDemo.snapshot().collection).reduce((a,b)=>a+b,0)),1);assert.deepEqual(errors,[]);
  fs.writeFileSync(path.join(out,'live-motion.json'),JSON.stringify({samples,distinctPositions:distinct.size,pausedForMs:600,giftResumeToFinishMs:Date.now()-start,errors},null,2));console.log(JSON.stringify({realMotionPositions:distinct.size,realGiftCollectedOnce:true,pauseResumed:true,errors}));
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
