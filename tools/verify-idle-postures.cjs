/* Isolated browser exercise: 30-second posture cadence must not postpone idle hiding. */
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict'),crypto=require('node:crypto'),{pathToFileURL}=require('node:url');
let chromium;try{({chromium}=require('playwright'));}catch{({chromium}=require(path.join(process.env.USERPROFILE,'.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright')));}
const root=path.resolve(__dirname,'..'),out=path.join(root,'.artifacts/idle-v13-browser'),target=path.join(root,'Release/win-x64/Demo/CompanionV01/index.html');fs.mkdirSync(out,{recursive:true});
const hash=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex'),exe=path.join(root,'Release/win-x64/DesktopPet.exe'),before=hash(exe);
(async()=>{
 const browser=await chromium.launch({executablePath:path.join(process.env.ProgramFiles,'Google/Chrome/Application/chrome.exe'),headless:true});
 const page=await browser.newPage({viewport:{width:1440,height:1100}}),checks=[],errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.route(/^https?:/,r=>r.abort());await page.clock.setFixedTime(new Date('2026-10-09T10:00:00+08:00'));
 const snap=()=>page.evaluate(()=>companionDemo.snapshot()),advance=ms=>page.evaluate(ms=>companionDemo.advance(ms),ms),render=()=>page.evaluate(()=>companionDemo.render()),go=p=>page.evaluate(p=>companionDemo.navigate(p),p);
 async function check(name,run){await run();checks.push(name);console.log('PASS '+name);}
 async function home(){await go('partners');await page.locator('[data-posture=auto]').click();await render();}
 async function capture(name){await page.waitForTimeout(400);await page.screenshot({path:path.join(out,name+'.png'),fullPage:true,animations:'disabled'});}
 try{
  await page.goto(pathToFileURL(target).href);await page.waitForFunction(()=>globalThis.companionDemo);await home();
  await check('auto posture waits the full 30 seconds, alternates without changing the user-idle timestamp',async()=>{
   const a=await snap();await advance(29999);assert.equal((await snap()).posture,a.posture);await advance(1);const b=await snap();assert.notEqual(b.posture,a.posture);assert.equal(b.runtime.idleSince,a.runtime.idleSince);
   await advance(29999);assert.equal((await snap()).posture,b.posture);await advance(1);assert.equal((await snap()).posture,a.posture);await capture('01-thirty-second-home');
  });
  await check('manual standing/sitting persist; changing back to auto starts a new interval',async()=>{
   for(const pose of ['stand','sit']){await page.locator(`[data-posture=${pose}]`).click();await render();await advance(120000);assert.equal((await snap()).posture,pose);}
   await page.reload();await page.waitForFunction(()=>globalThis.companionDemo);assert.equal((await snap()).postureMode,'sit');await home();const p=(await snap()).posture;await advance(29999);assert.equal((await snap()).posture,p);await advance(1);assert.notEqual((await snap()).posture,p);
  });
  await check('pause, reduced motion, dialogs and hidden tabs do not accrue a surprise posture switch',async()=>{
   await home();let p=(await snap()).posture;await advance(10000);await page.locator('#presence').click();await render();await advance(90000);assert.equal((await snap()).posture,p);await page.locator('#presence').click();await render();await advance(29999);assert.equal((await snap()).posture,p);await advance(1);assert.notEqual((await snap()).posture,p);
   await go('settings');await page.locator('#reduced-motion').check();await render();p=(await snap()).posture;await advance(90000);assert.equal((await snap()).posture,p);await page.locator('#reduced-motion').uncheck();await home();p=(await snap()).posture;await advance(29999);assert.equal((await snap()).posture,p);await advance(1);assert.notEqual((await snap()).posture,p);
   await home();await page.locator('#review-open').click();await render();p=(await snap()).posture;await advance(90000);assert.equal((await snap()).posture,p);await page.locator('[data-close=review-dialog]').click();await render();await advance(29999);assert.equal((await snap()).posture,p);await advance(1);assert.notEqual((await snap()).posture,p);
   await page.evaluate(()=>{Object.defineProperty(document,'hidden',{configurable:true,value:true});document.dispatchEvent(new Event('visibilitychange'));});await render();p=(await snap()).posture;await advance(90000);assert.equal((await snap()).posture,p);
   await page.evaluate(()=>{delete document.hidden;document.dispatchEvent(new Event('visibilitychange'));});await render();await advance(29999);assert.equal((await snap()).posture,p);await advance(1);assert.notEqual((await snap()).posture,p);
  });
  await check('one posture change at 30 seconds still leads to auto-hide at 60 seconds',async()=>{
   await home();await go('interaction');await render();const a=await snap();await advance(30000);assert.notEqual((await snap()).posture,a.posture);assert.equal((await snap()).runtime.idleSince,a.runtime.idleSince);await advance(29999);assert.equal((await snap()).runtime.action,'idle');await advance(1);assert.equal((await snap()).runtime.action,'hiding');
   await advance(2300);assert.equal((await snap()).runtime.action,'hidden');await advance(120000);assert.equal((await snap()).runtime.action,'hidden');await capture('02-hidden-waits-for-user');
   await page.locator('#pet-stage').click({button:'right',position:{x:30,y:30}});assert.equal((await snap()).runtime.action,'hidden');await page.keyboard.press('Escape');const side=(await snap()).runtime.hideSide;await page.locator('#find-'+side).click();assert.equal((await snap()).runtime.action,'found');await advance(1800);assert.equal((await snap()).runtime.action,'idle');
  });
  await check('disabled hiding, user touch and unsent chat protect the expected idle clocks',async()=>{
   await go('settings');await page.locator('#auto-hide').uncheck();await go('interaction');await render();await advance(90000);assert.equal((await snap()).runtime.action,'idle');
   await go('settings');await page.locator('#auto-hide').check();await go('interaction');await render();await advance(29000);await page.locator('#chat-input').fill('还没发送');await render();const p=(await snap()).posture;await advance(90000);assert.equal((await snap()).posture,p);assert.equal((await snap()).runtime.action,'idle');
   await page.locator('#chat-input').fill('');await page.locator('#pet-body').click();await render();assert.equal((await snap()).runtime.action,'pat');await advance(1000);assert.equal((await snap()).runtime.action,'pat');assert.equal((await snap()).posture,p);await advance(1500);assert.equal((await snap()).runtime.action,'idle');await advance(30000);assert.notEqual((await snap()).posture,p);assert.equal((await snap()).runtime.action,'idle');
  });
  await check('all 64 appearances alternate their own unmodified standing and sitting frames',async()=>{
   await home();let count=0;
   for(const style of ['chibi','realistic'])for(const family of ['whale','gpt','claude','gemini','grok','qwen','zhipu','kimi'])for(const clothes of ['original','swim','wedding','sports']){
    await page.evaluate(({family,style})=>{companionDemo.select(family);companionDemo.style(style);},{family,style});await page.locator(`[data-hero-outfit=${clothes}]`).click();await page.locator('[data-posture=auto]').click();await render();await page.waitForTimeout(70);
    const a=await snap();await advance(30000);const b=await snap();assert.notEqual(a.posture,b.posture);assert.equal(b.selected,family);assert.equal(b.style,style);assert.equal(b.outfit,clothes);
    await page.waitForFunction(({family,style,clothes})=>{const c=document.querySelector('#hero-canvas'),d=CLOUD_DATA.families[family].styles[style].looks[clothes][companionDemo.snapshot().posture];return c.dataset.file===d.file&&c.dataset.pose===companionDemo.snapshot().posture;},{family,style,clothes});count++;
   }
   assert.equal(count,64);await page.evaluate(()=>companionDemo.select('whale'));await capture('03-realistic-wardrobe');
  });
  await check('phone layout stays readable and native executable is unchanged',async()=>{
   await page.setViewportSize({width:390,height:844});await page.waitForTimeout(250);assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth+1),false);await capture('04-phone');assert.equal(hash(exe),before);assert.deepEqual(errors,[]);
  });
  fs.writeFileSync(path.join(out,'report.json'),JSON.stringify({passed:checks.length,checks,errors,nativeExeSha256:before,artworkChanged:false},null,2));
 }catch(e){await page.screenshot({path:path.join(out,'failure.png'),fullPage:true}).catch(()=>{});throw e;}finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
