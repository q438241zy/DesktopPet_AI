/* Isolated browser profile only. No user saves, desktop screenshots or live API calls. */
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),{pathToFileURL}=require('node:url');
let chromium;try{({chromium}=require('playwright'));}catch{({chromium}=require(path.join(process.env.USERPROFILE,'.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright')));}
const root=path.resolve(__dirname,'..'),out=path.resolve(process.env.UNIFIED_CALENDAR_REVIEW_DIRECTORY||path.join(root,'.artifacts/companion-v19-calendar/full-page'));
const target=path.join(root,'Release/win-x64/Demo/CompanionV01/index.html'),groups=[],errors=[],network=[];
fs.mkdirSync(out,{recursive:true});
async function check(name,fn){await fn();groups.push(name);console.log('PASS '+name);}
(async()=>{
  const browser=await chromium.launch({executablePath:process.env.CHROME_PATH||path.join(process.env.ProgramFiles,'Google/Chrome/Application/chrome.exe'),headless:true});
  try{
    const context=await browser.newContext({viewport:{width:1440,height:1100},timezoneId:'Asia/Shanghai',deviceScaleFactor:1}),page=await context.newPage();
    page.on('pageerror',e=>errors.push(e.message));await context.route(/^https?:/,route=>{network.push(new URL(route.request().url()).hostname);return route.abort();});
    await page.clock.setFixedTime(new Date('2026-10-11T10:00:00+08:00'));
    await page.goto(pathToFileURL(target).href+'#agenda');await page.waitForFunction(()=>globalThis.companionDemo);
    const snapshot=()=>page.evaluate(()=>companionDemo.snapshot());
    const day=d=>page.locator(`[data-agenda-date="${d}"]`);
    const reload=async()=>{await page.reload();await page.waitForFunction(()=>globalThis.companionDemo);};
    const screenshot=async name=>{await page.mouse.move(0,0);await page.evaluate(()=>scrollTo(0,0));await page.waitForTimeout(650);await page.screenshot({path:path.join(out,name+'.png'),fullPage:true,animations:'disabled'});};

    await check('one shared calendar replaces the separate daily check-in view',async()=>{
      assert.equal((await snapshot()).page,'agenda');assert.equal(await page.locator('#checkin-calendar').count(),0);
      assert.equal(await page.locator('.agenda-day').count(),42);assert.equal(await page.locator('.agenda-day').first().getAttribute('data-agenda-date'),'2026-09-28');
      assert.equal(await page.locator('#agenda-composer').isVisible(),false);assert.equal(await page.locator('#agenda-total').innerText(),'0');assert.equal(await page.locator('#agenda-makeup-count').innerText(),'0');
    });
    await check('today records once and stays clickable to inspect events',async()=>{
      const before=(await snapshot()).relations.whale.score;await day('2026-10-11').click();assert.deepEqual((await snapshot()).checkins,['2026-10-11']);
      assert.equal((await snapshot()).relations.whale.score,before+3);assert.equal(await day('2026-10-11').getAttribute('data-checked'),'true');assert.equal(await day('2026-10-11').isEnabled(),true);
      await day('2026-10-11').click();assert.equal((await snapshot()).relations.whale.score,before+3);assert.equal(await page.locator('#agenda-streak').innerText(),'1');assert.equal(await page.locator('#agenda-selected-checkin').innerText(),'✓ 已打卡');
    });
    await check('future dates are browsable; zero makeup cards cannot create a record',async()=>{
      await day('2026-10-12').click();assert.equal((await snapshot()).agenda.selectedDate,'2026-10-12');assert.deepEqual((await snapshot()).checkins,['2026-10-11']);
      await day('2026-10-10').click();assert.equal(await page.locator('#makeup-dialog').isVisible(),false);assert.match(await page.locator('#toast').innerText(),/0 张/);assert.deepEqual((await snapshot()).checkins,['2026-10-11']);
    });
    await check('existing makeup dialog cancels safely, spends exactly one card and persists',async()=>{
      await page.addInitScript(()=>{if(sessionStorage.getItem('calendar-v19-makeup-fixture'))return;const key='cloud-companions.demo.v01.state',s=JSON.parse(localStorage.getItem(key));if(!s)return;s.makeupCards=2;localStorage.setItem(key,JSON.stringify(s));sessionStorage.setItem('calendar-v19-makeup-fixture','1');});await reload();
      const score=(await snapshot()).relations.whale.score;await day('2026-10-10').click();assert.equal(await page.locator('#makeup-dialog').isVisible(),true);await page.locator('[data-close="makeup-dialog"]').last().click();assert.equal((await snapshot()).makeupCards,2);
      await day('2026-10-10').click();await page.locator('#makeup-confirm').click();assert.equal((await snapshot()).makeupCards,1);assert.deepEqual((await snapshot()).makeupCheckins,['2026-10-10']);assert.equal((await snapshot()).relations.whale.score,score);
      assert.equal(await page.locator('#agenda-streak').innerText(),'2');assert.equal(await page.locator('#agenda-selected-checkin').innerText(),'✓ 已补签');await day('2026-10-10').click();assert.equal(await page.locator('#makeup-dialog').isVisible(),false);
      await reload();assert.equal((await snapshot()).makeupCards,1);assert.equal(await day('2026-10-10').getAttribute('data-checked'),'true');assert.equal(await page.locator('#agenda-total').innerText(),'2');
    });
    await check('month/week navigation and view preference survive reloading',async()=>{
      await page.locator('[data-agenda-view="week"]').click();assert.equal(await page.locator('.agenda-day').count(),7);assert.equal(await page.locator('.agenda-day').first().getAttribute('data-agenda-date'),'2026-10-05');
      await page.locator('#agenda-prev').click();assert.equal(await page.locator('.agenda-day').first().getAttribute('data-agenda-date'),'2026-09-28');await page.locator('#agenda-today').click();
      await reload();assert.equal((await snapshot()).agenda.view,'week');assert.equal(await page.locator('.agenda-day').count(),7);assert.equal(await page.locator('[data-agenda-view="week"]').getAttribute('aria-pressed'),'true');
      await page.locator('[data-agenda-view="month"]').click();await page.locator('#agenda-prev').click();assert.match(await page.locator('#agenda-period').innerText(),/9月/);await page.locator('#agenda-next').click();assert.match(await page.locator('#agenda-period').innerText(),/10月/);await page.locator('#agenda-today').click();await reload();assert.equal((await snapshot()).agenda.view,'month');
    });
    await check('calendar follows today over a year boundary, but leaves historical browsing alone',async()=>{
      await page.clock.setFixedTime(new Date('2026-12-31T10:00:00+08:00'));await reload();await page.locator('[data-agenda-view="week"]').click();assert.equal(await page.locator('.agenda-day').first().getAttribute('data-agenda-date'),'2026-12-28');assert.equal(await page.locator('.agenda-day').last().getAttribute('data-agenda-date'),'2027-01-03');assert.match(await page.locator('#agenda-period').innerText(),/2027年/);
      await page.evaluate(()=>companionDemo.advance(4*86400000));await page.waitForFunction(()=>companionDemo.snapshot().agenda.selectedDate==='2027-01-04');assert.equal(await page.locator('.agenda-day').first().getAttribute('data-agenda-date'),'2027-01-04');
      await page.locator('#agenda-prev').click();const prior=await page.locator('#agenda-period').innerText();await page.evaluate(()=>companionDemo.advance(86400000));await page.waitForTimeout(1100);assert.equal(await page.locator('#agenda-period').innerText(),prior);
      await page.clock.setFixedTime(new Date('2026-10-11T10:00:00+08:00'));await reload();await page.locator('[data-agenda-view="month"]').click();
    });
    await check('AI shortcut selects the AI tab, while the composer keeps independent history',async()=>{
      await page.locator('#agenda-settings').click();assert.equal((await snapshot()).page,'settings');assert.equal(await page.locator('#settings-tab-api').getAttribute('aria-selected'),'true');assert.equal(await page.locator('#api-provider').isVisible(),true);
      await page.locator('[data-page="agenda"]').click();await page.locator('#agenda-talk').click();assert.equal(await page.locator('#agenda-composer').isVisible(),true);const before=(await snapshot()).chatCount,started=Date.now();
      await page.locator('#agenda-input').fill('明天下午三点开会');await page.locator('#agenda-send').click();await page.waitForFunction(()=>!companionDemo.snapshot().agenda.thinking);assert(Date.now()-started>=990);assert.match(await page.locator('#agenda-compose-reply').innerText(),/接入 AI/);assert.equal((await snapshot()).chatCount,before);assert.equal((await snapshot()).agenda.events.length,0);
      await page.locator('#agenda-talk').click();assert.equal(await page.locator('#agenda-composer').isVisible(),false);
    });
    await check('confirmed events and check-in markers coexist without changing saved ownership',async()=>{
      await page.locator('[data-agenda-sample="meeting"]').click();await page.waitForFunction(()=>!companionDemo.snapshot().agenda.thinking);assert.equal((await snapshot()).agenda.events.length,0);
      await page.locator('#agenda-title').fill('一起读下一篇故事');await page.locator('#agenda-confirm').click();await page.waitForFunction(()=>companionDemo.snapshot().agenda.events.length===1);
      await page.locator('#agenda-saved-open').click();assert.match(await day('2026-10-12').innerText(),/一起读下一篇故事/);assert.equal(await page.locator('.agenda-event-info strong').innerText(),'一起读下一篇故事');assert.equal((await snapshot()).agenda.events[0].family,'whale');
      await reload();await day('2026-10-12').click();assert.equal(await page.locator('.agenda-event-info strong').innerText(),'一起读下一篇故事');assert.equal(await day('2026-10-11').getAttribute('data-checked'),'true');
      await screenshot('calendar-desktop');await page.locator('[data-agenda-view="week"]').click();await screenshot('calendar-week');await page.locator('[data-agenda-view="month"]').click();
    });
    await check('full page fits phone width, with no external requests or browser errors',async()=>{
      await page.setViewportSize({width:390,height:844});await screenshot('calendar-mobile');assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),true);assert.equal(await page.locator('.agenda-day').count(),42);assert.deepEqual(errors,[]);assert.deepEqual(network,[]);
    });
    fs.writeFileSync(path.join(out,'results.json'),JSON.stringify({passed:groups.length,groups,errors,externalRequests:network,realApiCalls:0,scope:'fresh isolated browser storage; no native or user-data changes'},null,2));
  }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
