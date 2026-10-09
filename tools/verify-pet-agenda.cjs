/* Isolated agenda model + browser flows. All model calls are intercepted fixtures. */
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),{pathToFileURL}=require('node:url');
let chromium;try{({chromium}=require('playwright'));}catch{({chromium}=require(path.join(process.env.USERPROFILE,'.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright')));}
const G=require('../docs/demo/companion-v01/agenda.js'),Providers=require('../docs/demo/companion-v01/providers.js');
const root=path.resolve(__dirname,'..'),out=path.join(root,'.artifacts/agenda-v11-review'),target=path.join(root,'Release/win-x64/Demo/CompanionV01/index.html');fs.mkdirSync(out,{recursive:true});
const groups=[],errors=[],network=[];let requests=0;
async function check(name,fn){await fn();groups.push(name);console.log('PASS '+name);}
const start=new Date(2026,9,9,10,0,0).getTime(),draft={title:'项目会议',startLocal:'2026-10-10T15:00',leadMinutes:10,repeat:'none',zone:G.zone(),reminder:'会议记在这里啦，我们从容地开始。'};
(async()=>{
  await check('strict dates, future reminder, recurrence, timezone and malformed AI output',()=>{
    assert.equal(G.draft(draft,start).title,'项目会议');
    for(const change of [{startLocal:'2026-02-30T15:00'},{startLocal:'2026-10-09T09:00'},{startLocal:'2026-10-09T10:05'},{leadMinutes:-1},{leadMinutes:1.5},{repeat:'every second'},{zone:'Mars/Local'},{title:''},{startLocal:'2026-10-10T15:00',repeat:'weekdays'}])assert.throws(()=>G.draft({...draft,...change},start));
    for(const raw of ['bad JSON','{}',JSON.stringify({kind:'delete',reply:'已删除'}),JSON.stringify({kind:'event',reply:'待确认',event:null})])assert.throws(()=>G.envelope(raw,start));
    assert.equal(G.envelope('```json\n'+JSON.stringify({kind:'clarify',reply:'明天几点？',event:null})+'\n```',start).event,null);
  });
  await check('confirmation only, duplicates, cancelled records and durable reload',()=>{
    const db=G.create();G.envelope(JSON.stringify({kind:'event',reply:'请确认',event:draft}),start);assert.equal(db.events.length,0);
    const first=G.put(db,draft,{family:'whale',now:start});assert.throws(()=>G.put(db,draft,{family:'gpt',now:start}));assert.equal(db.events.length,1);assert.deepEqual(G.create(JSON.parse(JSON.stringify(db))),db);
    G.remove(db,first.id);assert.equal(G.due(db,Date.now()+1e12).length,0);G.put(db,draft,{family:'gpt',now:start});assert.equal(db.events.length,2);
  });
  await check('exact due boundary, snooze and recurring weekdays preserve clock time',()=>{
    const db=G.create(),v={...draft,startLocal:'2026-10-09T10:20',repeat:'weekdays'},e=G.put(db,v,{family:'kimi',now:start}),at=e.startAt-600000;
    assert.equal(G.due(db,at-1).length,0);assert.equal(G.due(db,at).length,1);G.deliver(db,e.id,at);G.snooze(db,e.id,at);assert.equal(e.startAt,G.parseLocal(v.startLocal));assert.equal(G.due(db,at+599999).length,0);G.deliver(db,e.id,at+600000);G.finish(db,e.id,at+600000);assert.equal(G.local(e.startAt),'2026-10-12T10:20');assert.equal(e.status,'scheduled');
    const nextAt=e.startAt;assert.equal(G.finish(db,e.id,at+600000),false);assert.equal(e.startAt,nextAt);
    const single=G.put(db,{...v,title:'浇水',repeat:'none'},{family:'gpt',now:start});assert.equal(G.finish(db,single.id,start),false);G.deliver(db,single.id,at);G.finish(db,single.id,at);assert.equal(single.status,'done');
  });
  const browser=await chromium.launch({executablePath:process.env.CHROME_PATH||path.join(process.env.ProgramFiles,'Google/Chrome/Application/chrome.exe'),headless:true});
  try{
    const context=await browser.newContext({viewport:{width:1440,height:1100},timezoneId:'Asia/Shanghai',deviceScaleFactor:1}),page=await context.newPage();page.on('pageerror',e=>errors.push(e.message));
    let fixture={kind:'chat',reply:'我在，慢慢说。',event:null},delay=0,httpStatus=200;
    await context.route(/^https?:/,async route=>{
      const req=route.request();network.push(new URL(req.url()).hostname);requests++;const body=req.postData()||'';assert(!body.includes('DEMO_ONLY_NOT_A_REAL_KEY'));assert(body.includes('只输出一个 JSON 对象'));const provider=await page.locator('#api-provider').inputValue(),protocol=Providers.catalog[provider].protocol;
      const content=typeof fixture==='string'?fixture:JSON.stringify(fixture);await new Promise(r=>setTimeout(r,delay));
      const data=protocol==='messages'?{content:[{type:'text',text:content}]}:protocol==='gemini'?{candidates:[{content:{parts:[{text:content}]}}]}:{choices:[{message:{content}}]};await route.fulfill({status:httpStatus,contentType:'application/json',body:JSON.stringify(data)}).catch(()=>{});
    });
    await page.clock.setFixedTime(new Date('2026-10-09T10:00:00+08:00'));await page.goto(pathToFileURL(target).href+'#agenda');await page.waitForFunction(()=>globalThis.companionDemo);
    const snap=()=>page.evaluate(()=>companionDemo.snapshot());
    const shot=async name=>{await page.evaluate(()=>scrollTo(0,0));await page.screenshot({path:path.join(out,name+'.png'),fullPage:true,animations:'disabled'});};
    async function send(message){await page.locator('#chat-input').fill(message);await page.locator('#chat-send').click();await page.waitForFunction(()=>companionDemo.snapshot().runtime.action!=='thinking');}
    async function config(provider){await page.evaluate(()=>companionDemo.navigate('settings'));await page.locator('#api-provider').selectOption(provider);await page.locator('#api-model').fill(provider==='gemini'?'demo-model':'demo-model');await page.locator('#api-key').fill('DEMO_ONLY_NOT_A_REAL_KEY');await page.locator('#api-save').click();await page.evaluate(()=>companionDemo.navigate('interaction'));}
    await check('calendar is empty, no API calls; samples think for one second and clarify',async()=>{
      assert.equal((await snap()).version,'1.1');assert.equal((await snap()).page,'agenda');assert.equal(requests,0);await page.waitForTimeout(600);await shot('01-empty-calendar');
      await page.locator('[data-agenda-sample="unclear"]').click();assert.equal((await snap()).runtime.action,'thinking');await page.waitForTimeout(500);assert.equal((await snap()).runtime.action,'thinking');await page.waitForFunction(()=>companionDemo.snapshot().runtime.action!=='thinking');assert.match(await page.locator('#agenda-question').innerText(),/几点/);assert.equal((await snap()).agenda.events.length,0);await page.locator('#agenda-answer-sample').click();assert.equal(await page.locator('#agenda-title').inputValue(),'买牛奶');assert.equal((await snap()).agenda.events.length,0);await shot('02-confirm-card');
      await page.locator('#agenda-confirm').click();await page.waitForFunction(()=>companionDemo.snapshot().agenda.events.length===1);assert.equal((await snap()).agenda.events[0].source,'demo');assert.equal(requests,0);await page.locator('#agenda-saved-open').click();assert.equal(await page.locator('.agenda-event').count(),1);
    });
    await check('week/month views, preview without mutation, edit and confirmed deletion',async()=>{
      await page.locator('[data-agenda-view="month"]').click();assert.equal(await page.locator('.agenda-day').count(),42);await page.locator('[data-agenda-view="week"]').click();assert.equal(await page.locator('.agenda-day').count(),7);const before=(await snap()).agenda.events[0];await page.locator('.agenda-event').getByText('预览提醒',{exact:true}).click();assert.equal(await page.locator('#agenda-alert').isVisible(),true);await page.waitForTimeout(600);await shot('03-reminder-preview');await page.locator('#agenda-alert-done').click();assert.deepEqual((await snap()).agenda.events[0],before);
      await page.evaluate(()=>companionDemo.select('gpt'));await page.locator('.agenda-event').getByText('修改',{exact:true}).click();assert.equal((await snap()).selected,'whale');await page.locator('#agenda-title').fill('买牛奶与面包');await page.locator('#agenda-confirm').click();await page.waitForFunction(()=>companionDemo.snapshot().agenda.events[0].title==='买牛奶与面包');await page.locator('#agenda-saved-open').click();await page.locator('.agenda-event').getByText('删除',{exact:true}).click();await page.locator('#agenda-delete-cancel').click();assert.equal((await snap()).agenda.events[0].status,'scheduled');await page.locator('.agenda-event').getByText('删除',{exact:true}).click();await page.locator('#agenda-delete-confirm').click();await page.waitForFunction(()=>companionDemo.snapshot().agenda.events[0].status==='cancelled');
    });
    await check('all eight adapters use the same agenda contract; plain chat and clarification do not save',async()=>{
      for(const provider of Object.keys(Providers.catalog)){await config(provider);fixture={kind:'chat',reply:'今天也陪着你。',event:null};await send('你好');assert.equal(await page.locator('#bubble-text').innerText(),'今天也陪着你。');assert((await snap()).chatDuration>=995);}
      fixture={kind:'clarify',reply:'明天上午还是下午？几点提醒你？',event:null};await send('明天提醒我开会');assert.equal(await page.locator('#agenda-draft').isVisible(),false);assert.equal((await snap()).agenda.events.filter(e=>e.status!=='cancelled').length,0);assert.match(await page.locator('#bubble-text').innerText(),/几点/);
    });
    await check('AI event stays a draft until confirmation, escaped title and key excluded from storage',async()=>{
      fixture={kind:'event',reply:'我整理成卡片了，请确认。',event:{...draft,title:'项目会议 <img src=x>'}};await send('明天下午三点项目会议，提前十分钟提醒');assert.equal((await snap()).agenda.events.length,1);assert.equal(await page.locator('#agenda-draft-source').innerText(),'AI 日程卡 · 尚未保存');await page.locator('#agenda-title').fill('项目会议');await page.locator('#agenda-confirm').click();await page.waitForFunction(()=>companionDemo.snapshot().agenda.events.filter(e=>e.status==='scheduled').length===1);assert.equal((await snap()).agenda.events.at(-1).source,'api');assert(!(await page.evaluate(()=>JSON.stringify(localStorage))).includes('DEMO_ONLY_NOT_A_REAL_KEY'));
      await page.locator('#agenda-saved-open').click();await shot('04-calendar-with-event');await page.reload();await page.waitForFunction(()=>globalThis.companionDemo);assert.equal((await snap()).agenda.events.at(-1).title,'项目会议');assert.equal(await page.locator('#api-key').inputValue(),'');
    });
    await check('bad JSON, past time, HTTP failure and cancelled requests never create events',async()=>{
      await config('deepseek');const count=(await snap()).agenda.events.length;fixture='broken';await send('提醒我一件事');assert.match(await page.locator('#bubble-text').innerText(),/没有保存/);fixture={kind:'event',reply:'请确认',event:{...draft,startLocal:'2026-10-08T12:00'}};await send('昨天开会');assert.match(await page.locator('#bubble-text').innerText(),/过去/);httpStatus=503;await send('再试一次');assert.match(await page.locator('#bubble-text').innerText(),/503/);httpStatus=200;
      fixture={kind:'event',reply:'请确认',event:draft};delay=1600;await page.locator('#chat-input').fill('明天开会');await page.locator('#chat-send').click();await page.evaluate(()=>companionDemo.select('gpt'));await page.waitForTimeout(1900);assert.equal(await page.locator('#agenda-draft').isVisible(),false);assert.equal((await snap()).agenda.events.length,count);delay=0;
    });
    await check('two due events, snooze, recurring weekdays and refresh recovery work offline',async()=>{
      const initial=G.create();for(const [title,family,repeat] of [['给花浇水','whale','none'],['收尾检查','kimi','weekdays']])G.put(initial,{...draft,title,repeat,startLocal:'2026-10-09T10:02',leadMinutes:0},{family,now:start});
      await page.evaluate(value=>localStorage.setItem('cloud-companions.demo.agenda.v1',JSON.stringify(value)),initial);await page.reload();await page.waitForFunction(()=>globalThis.companionDemo);await page.evaluate(()=>companionDemo.advance(120000));await page.waitForSelector('#agenda-alert:not([hidden])');assert.equal(await page.locator('#agenda-alert-title').innerText(),'给花浇水');await page.locator('#agenda-alert-snooze').click();await page.waitForFunction(()=>document.querySelector('#agenda-alert-title').textContent==='收尾检查');assert.equal(await page.locator('#agenda-alert-pet').getAttribute('data-pose'),'smile');await page.locator('#agenda-alert-done').click();await page.waitForFunction(()=>!companionDemo.snapshot().agenda.alertId);assert.equal(G.local((await snap()).agenda.events[1].startAt),'2026-10-12T10:02');await page.evaluate(()=>companionDemo.advance(600000));await page.waitForSelector('#agenda-alert:not([hidden])');assert.equal(await page.locator('#agenda-alert-title').innerText(),'给花浇水');await page.locator('#agenda-alert-done').click();await page.waitForFunction(()=>companionDemo.snapshot().agenda.events[0].status==='done');
      const missed={...initial.events[0],id:'overdue-test',title:'未读的提醒',startAt:start-3600000,status:'notified',deliveredAt:start-3600000};await page.evaluate(e=>{const k='cloud-companions.demo.agenda.v1',s=JSON.parse(localStorage.getItem(k));s.events.push(e);localStorage.setItem(k,JSON.stringify(s));},missed);await page.reload();await page.waitForSelector('#agenda-alert:not([hidden])');assert.match(await page.locator('#agenda-alert-label').innerText(),/补提醒/);await page.locator('#agenda-alert-done').click();await page.waitForFunction(()=>companionDemo.snapshot().agenda.events.at(-1).status==='done');await page.reload();await page.waitForFunction(()=>globalThis.companionDemo);await page.waitForTimeout(1200);assert.equal(await page.locator('#agenda-alert').isVisible(),false);
    });
    await check('eight companion smile identities in both styles, narrow viewport and no JS errors',async()=>{
      await page.evaluate(()=>companionDemo.navigate('agenda'));await page.locator('#agenda-all').click();for(const style of ['chibi','realistic']){await page.evaluate(s=>companionDemo.style(s),style);for(const id of G.families){await page.evaluate(id=>companionDemo.select(id),id);await page.evaluate(()=>companionDemo.navigate('agenda'));assert.equal(await page.locator('#agenda-pet').getAttribute('data-pose'),'smile');}}
      await page.evaluate(()=>{companionDemo.select('whale');companionDemo.style('chibi');companionDemo.navigate('agenda');});await page.locator('#agenda-all').click();await page.setViewportSize({width:390,height:844});await page.waitForTimeout(500);assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1));await shot('05-phone-agenda');assert.deepEqual(errors,[]);
    });
    await context.close();
  }finally{await browser.close();}
  const report={groups,passed:groups.length,errors,requests,providers:[...new Set(network)],realApiRequests:0,note:'Fresh browser contexts, synthetic API fixtures only; no personal profile or desktop capture.'};fs.writeFileSync(path.join(out,'report.json'),JSON.stringify(report,null,2));console.log(JSON.stringify(report));
})().catch(error=>{console.error(error);process.exitCode=1;});
