/* Deterministic behavior + isolated browser review. No native profile or remote API. */
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict'),crypto=require('node:crypto'),{pathToFileURL}=require('node:url');
const root=path.resolve(__dirname,'..'),out=path.join(root,'.artifacts/companion-v17-play'),source=path.join(root,'docs/demo/companion-v01'),target=path.join(root,'Release/win-x64/Demo/CompanionV01');fs.mkdirSync(out,{recursive:true});
const P=require(path.join(source,'play-model.js')),stories=require(path.join(source,'story-library.js')),M=require(path.join(source,'model.js')),items=require('../docs/demo/interaction-five/model.js').gifts;
const checks=[],errors=[],hash=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex'),exe=path.join(root,'Release/win-x64/DesktopPet.exe'),before=hash(exe);
async function check(name,run){await run();checks.push(name);console.log('PASS '+name);}
let chromium;try{({chromium}=require('playwright'));}catch{({chromium}=require(path.join(process.env.USERPROFILE,'.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright')));}
(async()=>{
 await check('ten original stories, five to ten sentences; prior collection IDs preserved',()=>{
  assert.equal(stories.length,10);assert.equal(new Set(stories.map(s=>s.id)).size,10);for(const s of stories)assert(s.sentences.length>=5&&s.sentences.length<=10);
  const old=require('../docs/demo/interaction-five/model.js').stories;for(const s of old)assert.deepEqual(stories.find(x=>x.id===s.id),s);
  const data=M.create();data.stories=Object.fromEntries(stories.map((s,i)=>[s.id,i+1]));assert.deepEqual(M.create(data).stories,data.stories);
 });
 await check('gift advances without responses, collects once, supports interruption and twenty-item random bag',()=>{
  const s=new P.Session({stories,items,random:()=>0});const bag=new Set();
  for(let i=0;i<20;i++){s.start('gift');assert.equal(s.view.phase,'giving');s.advance(1150);assert.equal(s.view.phase,'holding');s.advance(1500);assert.equal(s.view.pose,'gift-empty');assert.equal(s.takeEvents().length,0);s.advance(400);const e=s.takeEvents();assert.equal(e.length,1);bag.add(e[0].id);s.advance(7000);assert.equal(s.takeEvents().length,0);assert(!s.active);}assert.equal(bag.size,20);
  s.start('gift');s.advance(700);s.cancel();s.advance(9000);assert.equal(s.takeEvents().length,0);
 });
 await check('reading starts immediately, turns each page, ends and collects once; random story does not repeat immediately',()=>{
  const s=new P.Session({stories,items});let previous='';
  for(let n=0;n<20;n++){s.start('read');assert.notEqual(s.story.id,previous);previous=s.story.id;assert.equal(s.view.phase,'reading');for(const l of s.lines.slice(0,-1)){s.advance(l.end-s.time+20);assert.equal(s.view.pose,'page');s.advance(650);assert.equal(s.view.pose,'read');}s.advance(s.end-s.time);assert.deepEqual(s.takeEvents(),[{kind:'story',id:s.story.id}]);s.advance(5000);assert.equal(s.takeEvents().length,0);}
  s.start('read');s.advance(1000);s.cancel();s.advance(1000000);assert.equal(s.takeEvents().length,0);
 });
 await check('butterfly proactively moves both directions, uses walking feet and stops on a standing pose',()=>{
  const s=new P.Session({stories,items});s.start('butterfly');let min=0,max=0,last=0;const faces=new Set();
  for(let i=0;i<230;i++){s.advance(50);const v=s.view;assert(Math.abs(v.offset)<=.83);min=Math.min(min,v.offset);max=Math.max(max,v.offset);if(Math.abs(v.offset-last)>.0001)assert.equal(Math.sign(v.offset-last),v.direction);faces.add(v.direction);last=v.offset;assert(['walk','stand'].includes(v.pose));}assert(min<-.7&&max>.7);assert.equal(faces.size,2);assert.equal(s.view.pose,'stand');
 });
 await check('merged menu has all original actions once, renamed meals, no photo or old groups',()=>{
  const all=Object.values(P.groups).flat();assert.equal(new Set(all).size,all.length);assert.equal(P.titles.checkin,'吃饭');assert.equal(P.titles.snack,'吃零食');assert(P.groups.interaction.includes('rest'));assert(!all.includes('photo'));assert(!P.groups.care&&!P.groups.motions);
 });
 const browser=await chromium.launch({executablePath:path.join(process.env.ProgramFiles,'Google/Chrome/Application/chrome.exe'),headless:true});
 const page=await browser.newPage({viewport:{width:1300,height:1000},timezoneId:'Asia/Shanghai'});page.on('pageerror',e=>errors.push(e.message));await page.route(/^https?:/,r=>r.abort());
 const snap=()=>page.evaluate(()=>companionDemo.snapshot()),go=p=>page.evaluate(p=>companionDemo.navigate(p),p),step=ms=>page.evaluate(ms=>companionDemo.playStep(ms),ms);
 async function start(key){await page.evaluate(key=>companionDemo.action(key),key);await page.waitForFunction(()=>companionDemo.snapshot().play.active&&!companionDemo.snapshot().play.loading);}
 async function shot(name){await page.screenshot({path:path.join(out,name+'.png'),fullPage:true});}
 try{
  await page.goto(pathToFileURL(path.join(target,'index.html')).href);await page.waitForFunction(()=>globalThis.companionDemo);await go('interaction');
  await check('review loads, photo UI absent, gift is user-to-pet without delivery/open confirmations',async()=>{
   assert.equal(await page.locator('#photo-dialog,[data-act=photo],#hero-photo').count(),0);assert.equal(await page.locator('script[src*=photo]').count(),0);
   await start('gift');assert.equal(await page.locator('#play-controls button').allTextContents().then(x=>x.join(',')),'结束');assert.equal(await page.locator('#inline-chat').isVisible(),false);await step(450);await shot('01-user-gives-gift');await step(2850);assert.equal(Object.values((await snap()).collection).reduce((a,b)=>a+b,0),1);await shot('02-opened-gift');await step(6000);assert.equal(Object.values((await snap()).collection).reduce((a,b)=>a+b,0),1);
  });
  await check('automatic text-only reading reaches final collection, no dropdown/next button/voice request',async()=>{
   await start('read');let a=await snap();assert.equal(a.play.line,0);const story=stories.find(x=>x.id===a.play.story);assert.equal(await page.locator('#bubble-text').innerText(),story.sentences[0]);assert.equal(await page.locator('#play-controls select,#story-next,#story-dialog').count(),0);await shot('03-auto-reading');await step(100000);assert.equal((await snap()).stories[story.id],1);await go('daily');await page.locator('[data-collection=stories]').click();assert.equal(await page.locator('.collection-card.story').count(),10);assert.equal(await page.locator('#stories-count').innerText(),'1/10');await shot('04-ten-story-collection');await go('interaction');
  });
  await check('filled selection and paged circular menu stay usable; right click does not cancel',async()=>{
   await start('butterfly');await step(1200);const before=await snap();await page.locator('#pet-stage').click({button:'right',position:{x:20,y:180}});assert.equal((await snap()).play.key,'butterfly');assert.equal((await snap()).runtime.action,'play');assert.equal(await page.locator('#pet-menu').getAttribute('data-group'),'root');await shot('05-root-menu');await page.locator('[data-menu-action="group:play"]').click();
   const found=new Set();for(let p=0;p<3;p++){for(const k of await page.locator('#pet-menu [data-menu-action]').evaluateAll(bs=>bs.map(b=>b.dataset.menuAction)))found.add(k);await page.locator('[data-menu-action=next]').click();}for(const key of P.groups.play)assert(found.has(key));await shot('06-play-wheel');await page.locator('#pet-menu [data-menu-action=back]').click();await page.locator('[data-menu-action="group:interaction"]').click();assert.equal(await page.locator('[data-menu-action=checkin]').innerText(),'吃饭');await shot('07-interaction-wheel');
   assert.equal(await page.locator('[data-page=partners] .icon-solid').evaluate(e=>getComputedStyle(e).display),'block');assert.equal(await page.locator('[data-page=daily] .icon-solid').evaluate(e=>getComputedStyle(e).display),'none');
   await page.keyboard.press('Escape');await page.locator('#pet-body').click();assert.equal((await snap()).play.active,false);assert.equal((await snap()).runtime.action,'pat');assert.notEqual(before.play.frame.offsetPixels,0);
  });
  await check('high-five contact is clickable; rock-paper-scissors waits for the user then reveals together',async()=>{
   await start('highfive');await step(800);await page.locator('#highfive-target').click();await step(120);assert.equal((await snap()).play.phase,'contact');await start('rps');await page.locator('[data-throw=paper]').click();await step(600);assert.equal((await snap()).play.phase,'pump');await step(1300);assert.equal((await snap()).play.phase,'reveal');
  });
  await check('64 appearances keep their own gift/read/walk files, ground baseline and bounded movement',async()=>{
   const observations=[];for(const family of M.families)for(const style of ['chibi','realistic'])for(const outfit of Object.keys(M.outfits)){
    await page.evaluate(({family,style})=>{companionDemo.select(family);companionDemo.style(style);companionDemo.navigate('partners');},{family,style});await page.locator(`[data-hero-outfit=${outfit}]`).click();await go('interaction');
    for(const key of ['gift','read','butterfly']){await start(key);await step(key==='gift'?3300:key==='read'?700:1600);const a=await snap();assert(a.pose.file&&a.play.frame);assert.equal(a.pose.family,family);assert.equal(a.pose.style,style);assert.equal(a.pose.outfit,outfit);assert(a.play.frame.scale>0);assert(a.play.frame.offsetPixels<=a.play.frame.range+1);observations.push({family,style,outfit,key,pose:a.pose,frame:a.play.frame});
     if(family==='whale'||family==='gemini'&&outfit==='sports')await page.locator('#pet-stage').screenshot({path:path.join(out,`${family}-${style}-${outfit}-${key}.png`)});
    }
   }fs.writeFileSync(path.join(out,'appearances.json'),JSON.stringify(observations,null,2));assert.equal(observations.length,192);
  });
  await check('phone layout, natural-time movement, cancellation on clothing/character and no delayed rewards',async()=>{
   await page.setViewportSize({width:390,height:844});await page.evaluate(()=>{companionDemo.select('whale');companionDemo.style('chibi');});await start('butterfly');const x=(await snap()).play.frame.offsetPixels;await page.waitForTimeout(1400);assert.notEqual((await snap()).play.frame.offsetPixels,x);await shot('08-phone-butterfly');await page.evaluate(()=>companionDemo.action('group:play'));await shot('09-phone-wheel');const rects=await page.locator('#pet-menu button').evaluateAll(bs=>bs.map(b=>{const r=b.getBoundingClientRect();return{x:r.x,y:r.y,w:r.width,h:r.height};}));for(const r of rects)assert(r.x>=0&&r.x+r.w<=390);for(let i=0;i<rects.length;i++)for(let j=i+1;j<rects.length;j++){const a=rects[i],b=rects[j];assert(a.x+a.w<=b.x||b.x+b.w<=a.x||a.y+a.h<=b.y||b.y+b.h<=a.y);}
   await page.keyboard.press('Escape');await start('gift');const collection=(await snap()).collection;await page.evaluate(()=>companionDemo.select('gpt'));await step(100000);assert.deepEqual((await snap()).collection,collection);assert(!((await snap()).play.active));assert.equal(await page.locator('#highfive-target').isVisible(),false);assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),true);
  });
  assert.deepEqual(errors,[]);assert.deepEqual((await snap()).errors,[]);assert.equal(hash(exe),before);
  fs.writeFileSync(path.join(out,'result.json'),JSON.stringify({checks,errors,nativeUnchanged:true,version:(await snap()).version,appearances:192},null,2));console.log(JSON.stringify({passed:checks.length,out}));
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
