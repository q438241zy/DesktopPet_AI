/* Calendar transactions, saved-state migration, character identity and visible UI. */
const {chromium}=require('C:/Users/99000256/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),{pathToFileURL}=require('node:url');
const root=path.resolve(__dirname,'..'),M=require('../docs/demo/companion-v01/model.js'),P=require('../docs/demo/companion-v01/personas.js');
const target=path.join(root,'Release/win-x64/Demo/CompanionV01/index.html'),out=path.join(root,'.artifacts/companion-v03-review');
fs.mkdirSync(out,{recursive:true});const checks=[],errors=[];
async function group(name,fn){await fn();checks.push(name);console.log('PASS '+name);}
(async()=>{
  await group('old saves gain zero makeup cards without losing valid records',()=>{
    const old=M.create();delete old.makeupCards;delete old.makeupCheckins;old.checkins=['2024-02-29','2026-02-30','2026-02-29','2026-10-06','2026-10-06'];old.relations.gpt.score=27;old.collection.basketball=2;
    const migrated=M.create(old);assert.equal(migrated.makeupCards,0);assert.deepEqual(migrated.makeupCheckins,[]);assert.deepEqual(migrated.checkins,['2024-02-29','2026-10-06']);assert.equal(migrated.relations.gpt.score,27);assert.equal(migrated.collection.basketball,2);
    assert.equal(M.create({...old,makeupCards:-10}).makeupCards,0);assert.equal(M.create({...old,makeupCards:1.9}).makeupCards,1);
  });
  await group('makeup spends exactly one card on a missed past day without inventing affection or time',()=>{
    const s=M.create(),t=new Date(2026,9,7,12).getTime();s.checkins=['2026-10-05'];
    assert.equal(M.makeup(s,'2026-10-06',t),false);s.makeupCards=1;const relations=structuredClone(s.relations);
    for(const key of ['2026-10-05','2026-10-07','2026-10-08','2026-02-31','invalid'])assert.equal(M.makeup(s,key,t),false);
    assert.equal(s.makeupCards,1);assert.equal(M.makeup(s,'2026-10-06',t),true);assert.equal(s.makeupCards,0);assert.equal(M.makeup(s,'2026-10-06',t),false);assert.equal(M.streak(s,t),2);assert.deepEqual(s.relations,relations);assert.deepEqual(M.create(s).makeupCheckins,['2026-10-06']);
    assert.equal(M.checkin(s,t),true);assert.equal(M.checkin(s,t),false);assert.equal(M.streak(s,t),3);assert.equal(s.relations.whale.score,3);
  });
  await group('all eight profiles describe a distinct character; likes are shared by local and API chat',()=>{
    assert.equal(new Set(M.families.map(id=>P.all[id].profile.title)).size,8);
    for(const id of M.families){const p=P.all[id].profile;assert.equal(p.about.length,2);assert.ok(p.about.join('').length>140,id);assert.equal(p.likes.length,4);assert.ok(p.together.length>30);assert.equal(P.reply(id,[{role:'user',content:'你喜欢什么？'}]),p.favorite);assert.equal(P.reply(id,[{role:'user',content:'介紹自己的性格'}]),p.intro);for(const [name] of p.likes)assert.ok(P.prompt(id,0).includes(name));}
  });
  const browser=await chromium.launch({executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true});
  try{
    const context=await browser.newContext({viewport:{width:1400,height:1024},timezoneId:'Asia/Shanghai',deviceScaleFactor:1.5}),page=await context.newPage();
    page.on('pageerror',e=>errors.push(e.message));await context.route(/^https?:/,route=>{errors.push('Unexpected network: '+route.request().url());return route.abort();});
    await page.clock.setFixedTime(new Date('2026-10-07T10:00:00+08:00'));await page.goto(pathToFileURL(target).href);await page.waitForFunction(()=>globalThis.companionDemo);
    const snap=()=>page.evaluate(()=>companionDemo.snapshot()),shot=name=>page.screenshot({path:path.join(out,name+'.png'),fullPage:true,animations:'disabled'});
    await group('home restores static image/name/wardrobe and preserves each character and style selection',async()=>{
      assert.equal(await page.locator('.partner-card').count(),8);assert.equal(await page.locator('.partner-hero').isVisible(),true);assert.equal(await page.locator('#pet-stage').isVisible(),false);
      assert.deepEqual(await page.locator('#hero-outfits button').allTextContents(),['原装','运动服','泳装','婚纱']);
      await page.locator('#presence').click();assert.equal(await page.locator('#hero-presence').innerText(),'暂停陪伴');await page.locator('#presence').click();assert.equal(await page.locator('#hero-presence').innerText(),'正在陪伴');
      for(const style of ['chibi','realistic']){
        await page.locator(`[data-style="${style}"]`).click();
        for(const id of M.families){await page.locator(`.partner-card[data-family="${id}"]`).click();assert.equal(await page.locator('#hero-name').innerText(),P.all[id].name);await page.locator('[data-hero-outfit="wedding"]').click();const expected=await page.evaluate(()=>CLOUD_DATA.families[companionDemo.snapshot().selected].styles[companionDemo.snapshot().style].looks.wedding.idle.file);assert.equal(await page.locator('#hero-canvas').getAttribute('data-file'),expected);assert.equal((await snap()).outfit,'wedding');}
      }
      await page.locator('[data-style="chibi"]').click();await page.locator('.partner-card[data-family="whale"]').click();assert.equal((await snap()).outfit,'wedding');await page.locator('[data-hero-outfit="original"]').click();await page.waitForTimeout(600);await shot('home-original-card');
      await page.evaluate(()=>companionDemo.advance(65000));assert.equal((await snap()).runtime.action,'idle');assert.equal(await page.locator('#pet-stage').isVisible(),false);
      await page.reload();await page.waitForFunction(()=>globalThis.companionDemo);assert.equal((await snap()).outfit,'original');await page.locator('[data-style="realistic"]').click();assert.equal((await snap()).outfit,'wedding');await page.locator('[data-hero-outfit="original"]').click();await page.waitForTimeout(500);await shot('home-original-card-3d');
    });
    await group('calendar highlights today and checks in only from its date cell',async()=>{
      await page.locator('[data-page="daily"]').click();assert.equal(await page.locator('#makeup-count').innerText(),'0');assert.equal(await page.locator('.calendar-day').count(),31);assert.equal(await page.locator('#check-in').getAttribute('data-date'),'2026-10-07');assert.equal(await page.locator('#check-in').getAttribute('aria-current'),'date');assert.equal(await page.locator('#checkin-calendar #check-in').count(),1);
      const colors=await page.evaluate(()=>['#check-in','[data-date="2026-10-06"]'].map(selector=>getComputedStyle(document.querySelector(selector)).backgroundColor));assert.notEqual(colors[0],colors[1]);
      await shot('calendar-before-checkin');await page.locator('#check-in').click();assert.equal(await page.locator('#check-in').isDisabled(),true);assert.equal(await page.locator('#check-in small').innerText(),'已打卡');assert.equal(await page.locator('#check-in>span').innerText(),'7');assert.equal(await page.locator('#total-count').innerText(),'1');assert.equal((await snap()).relations.whale.score,3);
      assert.equal(await page.locator('[data-date="2026-10-08"]').isDisabled(),true);await page.locator('[data-date="2026-10-06"]').click();assert.equal(await page.locator('#makeup-dialog').isVisible(),false);assert.match(await page.locator('#toast').innerText(),/0 张/);assert.equal((await snap()).checkins.length,1);
      await page.locator('#calendar-prev').click();assert.equal(await page.locator('.calendar-day').count(),30);assert.equal(await page.locator('#check-in').count(),0);await page.locator('#calendar-today').click();assert.equal(await page.locator('#calendar-next').isDisabled(),true);await shot('calendar-checked');
      await page.reload();await page.waitForFunction(()=>globalThis.companionDemo);await page.locator('[data-page="daily"]').click();assert.equal(await page.locator('#check-in').isDisabled(),true);assert.equal((await snap()).makeupCards,0);
    });
    await group('calendar makeup confirms spending, cancels safely, persists and blocks duplicates',async()=>{
      await page.addInitScript(()=>{if(sessionStorage.getItem('makeup-fixture'))return;const key='cloud-companions.demo.v01.state',s=JSON.parse(localStorage.getItem(key));s.makeupCards=2;localStorage.setItem(key,JSON.stringify(s));sessionStorage.setItem('makeup-fixture','1');});
      await page.reload();await page.waitForFunction(()=>globalThis.companionDemo);await page.locator('[data-page="daily"]').click();const before=await snap();await page.locator('[data-date="2026-10-06"]').click();assert.equal(await page.locator('#makeup-dialog').isVisible(),true);await page.locator('[data-close="makeup-dialog"]').last().click();assert.equal((await snap()).makeupCards,2);
      await page.locator('[data-date="2026-10-06"]').click();await page.locator('#makeup-confirm').click();const after=await snap();assert.equal(after.makeupCards,1);assert.deepEqual(after.makeupCheckins,['2026-10-06']);assert.equal(after.relations.whale.score,before.relations.whale.score);assert.equal(await page.locator('[data-date="2026-10-06"]').isDisabled(),true);assert.equal(await page.locator('#streak-count').innerText(),'2');
      await page.reload();await page.waitForFunction(()=>globalThis.companionDemo);assert.equal((await snap()).makeupCards,1);assert.deepEqual((await snap()).makeupCheckins,['2026-10-06']);
    });
    await group('today refreshes while open, including a month boundary',async()=>{
      await page.locator('[data-page="daily"]').click();await page.evaluate(()=>companionDemo.advance(28*86400000));assert.equal(await page.locator('#check-in').getAttribute('data-date'),'2026-11-04');assert.equal(await page.locator('#check-in').isEnabled(),true);assert.equal(await page.locator('.calendar-day').count(),30);assert.match(await page.locator('#calendar-month').innerText(),/11月/);
      await page.reload();await page.waitForFunction(()=>globalThis.companionDemo);
    });
    await group('profile explanations and all likes appear for all eight; chat entry keeps one-second thinking',async()=>{
      for(const id of M.families){await page.locator('[data-page="partners"]').click();await page.locator(`.partner-card[data-family="${id}"]`).click({button:'right'});await page.locator('[data-detail="voice"]').click();assert.equal(await page.locator('[data-detail="voice"]').innerText(),'说明');assert.deepEqual(await page.locator('#voice-description p').allTextContents(),P.all[id].profile.about);assert.deepEqual(await page.locator('#profile-likes strong').allTextContents(),P.all[id].profile.likes.map(x=>x[0]));}
      await shot('profile-explanation');await page.locator('#voice-chat').click();assert.equal((await snap()).page,'interaction');assert.equal((await snap()).selected,'kimi');await page.locator('#chat-input').fill('你喜欢什么？');await page.locator('#chat-send').click();assert.equal((await snap()).runtime.action,'thinking');await page.waitForFunction(()=>companionDemo.snapshot().runtime.action==='talk');assert.ok((await snap()).chatDuration>=990);assert.equal(await page.locator('#bubble-text').innerText(),P.all.kimi.profile.favorite);
      await page.locator('#interaction-back').click();assert.equal(await page.locator('.partner-hero').isVisible(),true);
    });
    await group('narrow layouts preserve readable calendar, original card and descriptions',async()=>{
      await page.setViewportSize({width:390,height:844});await page.locator('[data-style="chibi"]').click();await page.locator('.partner-card[data-family="whale"]').click();await shot('mobile-home');assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false);
      await page.locator('[data-page="daily"]').click();await shot('mobile-calendar');assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false);
      await page.locator('[data-page="partners"]').click();await page.locator('.partner-card[data-family="gpt"]').click({button:'right'});await page.locator('[data-detail="voice"]').click();await shot('mobile-profile');assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false);
      assert.deepEqual(errors,[]);assert.deepEqual((await snap()).errors,[]);
    });
  }finally{await browser.close();}
  fs.writeFileSync(path.join(out,'calendar-report.json'),JSON.stringify({passed:true,checks,errors,notes:['All browser storage belongs to isolated test contexts. Makeup card fixtures do not grant cards to the user.','The native app and art were not modified.']},null,2));console.log(JSON.stringify({passed:checks.length,out}));
})().catch(e=>{fs.writeFileSync(path.join(out,'calendar-failure.json'),JSON.stringify({checks,error:String(e.stack),errors},null,2));console.error(e);process.exit(1);});
