const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto'),{pathToFileURL}=require('node:url');
let pw;try{pw=require('playwright');}catch{pw=require('C:/Users/99000256/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');}
const root=path.resolve(__dirname,'..'),target=process.argv[2]||path.join(root,'Release/win-x64/Demo/WardrobeIdentity/index.html');
const output=path.join(root,'.artifacts/wardrobe-identity-W1');fs.mkdirSync(output,{recursive:true});
const protectedFiles=[path.join(root,'Release/win-x64/DesktopPet.exe'),path.join(process.env.LOCALAPPDATA,'DesktopPetAI/state.json')];
const hash=file=>crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex'),before=Object.fromEntries(protectedFiles.map(f=>[f,hash(f)]));
const results=[],errors=[],network=[];function pass(name){results.push(name);console.log('PASS '+name);}
(async()=>{
 const browser=await pw.chromium.launch({headless:true,executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe'});
 try{
  const context=await browser.newContext({viewport:{width:1360,height:1080},deviceScaleFactor:1});const page=await context.newPage();
  const observe=p=>{p.on('pageerror',e=>errors.push(e.message));p.on('requestfailed',r=>errors.push(r.url()));p.on('request',r=>{if(/^https?:/.test(r.url()))network.push(r.url());});};observe(page);
  const shot=async(name,p=page)=>p.screenshot({path:path.join(output,name+'.png'),fullPage:true});
  const state=()=>page.evaluate(()=>wardrobeReview());
  const pixels=async()=>crypto.createHash('sha256').update(await page.locator('#candidate-canvas').screenshot()).digest('hex');
  await page.goto(pathToFileURL(target).href);await page.waitForFunction(()=>globalThis.wardrobeReview?.().ready);assert.equal((await state()).installed,false);
  await shot('chibi-idle');
  for(let pose=0;pose<6;pose++){
   await page.locator('[data-pose="'+pose+'"]').click();const s=await state();assert.equal(s.pose,pose);assert.equal(s.frames[0].w,s.frames[1].w);assert.equal(s.frames[0].h,s.frames[1].h);
   const scales=await page.locator('#comparison canvas').evaluateAll(a=>a.map(c=>Number(c.dataset.scale)));assert(Math.abs(scales[0]-scales[1])<.003);
  }pass('Six Q poses: identical source-cell dimensions and paired display scale');
  await page.locator('[data-pose="0"]').click();const revised=await pixels();await page.locator('#old').check();assert.notEqual(revised,await pixels());assert((await state()).frames[1].file.includes('before'));await page.locator('#old').uncheck();assert.equal(revised,await pixels());pass('Existing swimsuit comparison switches source art and restores the review image');
  await page.locator('[data-pose="walk"]').click();let frames=new Set();
  for(let f=0;f<12;f++){await page.locator('#frame').fill(String(f));assert.equal((await state()).frame,f);frames.add(await pixels());}assert.equal(frames.size,12);
  await page.locator('#next').click();assert.equal((await state()).frame,0);await page.locator('#previous').click();assert.equal((await state()).frame,11);
  await page.locator('#direction').click();assert.equal((await state()).direction,-1);await shot('chibi-walk-left');await page.locator('#direction').click();
  await page.locator('#play').click();await page.waitForFunction(()=>wardrobeReview().frame!==11);await page.locator('#play').click();const stopped=(await state()).frame;await page.waitForTimeout(210);assert.equal((await state()).frame,stopped);pass('Twelve distinct walking frames, both directions, playback, pause and wraparound');
  await page.locator('[data-pose="0"]').click();await page.locator('button[data-mode="overlay"]').click();await page.locator('#opacity').fill('100');const onlyNew=await pixels();await page.locator('#opacity').fill('0');const onlyReference=await pixels();assert.notEqual(onlyNew,onlyReference);await page.locator('#opacity').fill('50');const half=await pixels();assert.notEqual(half,onlyNew);assert.notEqual(half,onlyReference);
  const hold=page.locator('#hold-reference');await hold.focus();await page.keyboard.down('Space');assert.equal((await state()).hold,true);assert.equal(await pixels(),onlyReference);await page.keyboard.up('Space');assert.equal((await state()).hold,false);assert.equal(await pixels(),half);pass('Overlay endpoints, blended comparison and keyboard hold-to-view reference');
  await page.locator('#guides').check();await page.locator('#dark').check();await shot('chibi-overlay-dark');await page.locator('#dark').uncheck();await page.locator('#guides').uncheck();
  await page.locator('button[data-style="realistic"]').click();await page.locator('button[data-mode="pair"]').click();
  for(const ref of ['original','swim','wedding']){await page.locator('[data-reference="'+ref+'"]').click();const s=await state();assert.equal(s.reference,ref);assert.deepEqual(s.frames.map(f=>[f.w,f.h]),[[1024,1536],[1024,1536]]);await shot('realistic-'+ref);}
  assert(await page.locator('#walk-controls').isHidden());assert(await page.locator('#old-toggle').isHidden());pass('Adult clothing uses all three full-size references without changing source geometry');
  await page.locator('[data-reference="original"]').click();await page.locator('button[data-mode="overlay"]').click();await shot('realistic-overlay');
  const mobile=await browser.newContext({viewport:{width:390,height:844},deviceScaleFactor:2,isMobile:true,hasTouch:true});const phone=await mobile.newPage();observe(phone);await phone.goto(pathToFileURL(target).href);await phone.waitForFunction(()=>globalThis.wardrobeReview?.().ready);
  for(const style of ['chibi','realistic']){await phone.locator('button[data-style="'+style+'"]').click();assert(await phone.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));assert(await phone.locator('#candidate-canvas').evaluate(c=>Math.abs(c.width-c.getBoundingClientRect().width*2)<1));await shot('mobile-'+style,phone);}pass('390px layout and 2x pixel-density canvases');
  await page.goto(pathToFileURL(path.join(root,'Release/win-x64/Demo/CloudClub/index.html')).href);await page.locator('a[href*="WardrobeIdentity"]').click();await page.waitForFunction(()=>globalThis.wardrobeReview?.().ready);pass('Long-term CloudClub entry opens the new review');
  assert.deepEqual(errors,[]);assert.deepEqual(network,[]);pass('No browser errors, failed assets or remote dependencies');
  const after=Object.fromEntries(protectedFiles.map(f=>[f,hash(f)]));assert.deepEqual(after,before);pass('Daily executable and user save unchanged by Demo deployment and browser checks');
  fs.writeFileSync(path.join(output,'report.json'),JSON.stringify({target,results,protectedBefore:before,protectedAfter:after,errors,network},null,2));
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
