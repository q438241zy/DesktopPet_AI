/* Fresh file:// browser, synthetic API replies, no personal profiles or desktop capture. */
const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm'),crypto=require('node:crypto'),assert=require('node:assert/strict'),{pathToFileURL}=require('node:url');
let chromium;try{({chromium}=require('playwright'));}catch{({chromium}=require(path.join(process.env.USERPROFILE,'.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright')));}
const root=path.resolve(__dirname,'..'),target=path.join(root,'Release/win-x64/Demo/CompanionV01'),out=process.env.CHAT_REVIEW_DIRECTORY||path.join(root,'.artifacts/companion-v15-review'),A=require('../docs/demo/companion-v01/providers.js'),P=require('../docs/demo/companion-v01/personas.js');
fs.mkdirSync(out,{recursive:true});const checks=[],errors=[],requests=[],sha=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex'),exe=path.join(root,'Release/win-x64/DesktopPet.exe'),nativeBefore=sha(exe);
(async()=>{
 const browser=await chromium.launch({executablePath:path.join(process.env.ProgramFiles,'Google/Chrome/Application/chrome.exe'),headless:true});
 const context=await browser.newContext({viewport:{width:1440,height:1040},timezoneId:'Asia/Shanghai'}),page=await context.newPage();page.on('pageerror',e=>errors.push(e.message));
 let payload='嗯，我听着。',delay=0,mode='chat';
 await context.route(/^https?:/,async route=>{
  const req=route.request(),body=req.postData()||'',provider=await page.locator('#api-provider').inputValue();
  assert.equal(body.includes('只输出一个 JSON 对象'),mode==='agenda');assert(!body.includes('NOT_A_REAL_API_KEY'));requests.push({provider,mode,body});
  const content=typeof payload==='string'?payload:JSON.stringify(payload),protocol=A.catalog[provider].protocol;
  await new Promise(resolve=>setTimeout(resolve,delay));const data=protocol==='messages'?{content:[{type:'text',text:content}]}:protocol==='gemini'?{candidates:[{content:{parts:[{text:content}]}}]}:{choices:[{message:{content}}]};
  await route.fulfill({status:200,contentType:'application/json',body:JSON.stringify(data)}).catch(()=>{});
 });
 const snap=()=>page.evaluate(()=>companionDemo.snapshot()),go=key=>page.evaluate(key=>companionDemo.navigate(key),key);
 async function check(name,fn){await fn();checks.push(name);console.log('PASS '+name);}
 async function shot(name){await page.waitForTimeout(350);await page.screenshot({path:path.join(out,name+'.png'),fullPage:true,animations:'disabled'});}
 async function send(text){await page.locator('#chat-input').fill(text);await page.locator('#chat-send').click();await page.waitForFunction(()=>companionDemo.snapshot().runtime.action!=='thinking');}
 async function config(id){await go('settings');await page.locator('#api-provider').selectOption(id);await page.locator('#api-model').fill('demo-model');await page.locator('#api-key').fill('NOT_A_REAL_API_KEY');await page.locator('#api-save').click();await go('interaction');}
 try{
  await page.clock.setFixedTime(new Date('2026-10-10T10:00:00+08:00'));await page.goto(pathToFileURL(path.join(target,'index.html')).href);await page.waitForFunction(()=>globalThis.companionDemo);
  await check('eight short greetings, pure chat UI, no calendar buttons or draft in chat',async()=>{
   assert.equal(await page.locator('#hero-chat').innerText(),'聊聊天');assert.equal(await page.locator('#page-interaction #agenda-draft,.agenda-chat-tools,#agenda-chat-open,#agenda-chat-sample').count(),0);
   await go('interaction');for(const id of Object.keys(P.all)){await page.evaluate(id=>companionDemo.select(id),id);const hello=await page.locator('#bubble-text').innerText();assert(hello.length<=8,hello);assert.equal(hello,P.all[id].hello);for(const score of [-100,0,100])assert(P.reply(id,[{role:'user',content:'你好'}],score).length<=9);}
   await page.evaluate(()=>companionDemo.select('gemini'));await shot('01-gemini-simple-chat');
  });
  await check('offline chat thinks at least one second and never diverts reminder wording into the calendar',async()=>{
   const before=(await snap()).agenda.events;await page.locator('#chat-input').fill('明天提醒我去散步');await page.locator('#chat-send').click();assert.equal((await snap()).runtime.action,'thinking');await page.waitForTimeout(500);assert.equal((await snap()).runtime.action,'thinking');await page.waitForFunction(()=>companionDemo.snapshot().runtime.action!=='thinking');
   assert((await snap()).chatDuration>=995);assert(!/API|日程卡|体验记日程|先在设定/.test(await page.locator('#bubble-text').innerText()));assert.deepEqual((await snap()).agenda.events,before);assert.equal((await snap()).agenda.pending,null);
  });
  await check('all eight API adapters return plain natural chat with one-second thinking and no agenda contract',async()=>{
   for(const id of Object.keys(A.catalog)){await config(id);payload='我在，慢慢说。';await send('今天想随便聊聊');assert.equal(await page.locator('#bubble-text').innerText(),payload);assert((await snap()).chatDuration>=995);assert.equal((await snap()).agenda.pending,null);}
  });
  await check('explicit agenda uses separate input and history, keeps confirmations, and cannot overwrite the chat bubble',async()=>{
   await config('deepseek');payload='当然，接着聊吧。';await send('我们在聊咖啡');const chatBefore=await page.locator('#bubble-text').innerText();
   await go('agenda');mode='agenda';payload={kind:'event',reply:'请确认时间。',event:{title:'专属日程审阅',startLocal:'2026-10-11T15:00',leadMinutes:10,repeat:'none',zone:'Asia/Shanghai',reminder:'到时间啦，我们出发吧。'}};
   await page.locator('#agenda-input').fill('明天下午三点专属日程审阅');await page.locator('#agenda-send').click();await page.waitForFunction(()=>!companionDemo.snapshot().agenda.thinking);assert((await snap()).agenda.requestDuration>=995);assert.equal((await snap()).agenda.events.length,0);assert.equal(await page.locator('#agenda-title').inputValue(),'专属日程审阅');assert.equal(await page.locator('#bubble-text').textContent(),chatBefore);
   assert(!requests.at(-1).body.includes('我们在聊咖啡'));await page.locator('#agenda-confirm').click();await page.waitForFunction(()=>companionDemo.snapshot().agenda.events.length===1);await shot('02-separate-agenda');
   await go('interaction');assert.equal(await page.locator('#bubble-text').innerText(),chatBefore);mode='chat';payload='嗯，继续说。';await send('接着聊');assert(!requests.at(-1).body.includes('专属日程审阅'));assert.equal((await snap()).agenda.events.length,1);assert(!JSON.stringify(await page.evaluate(()=>({...localStorage}))).includes('NOT_A_REAL_API_KEY'));
  });
  await check('leaving an in-flight agenda request for ordinary chat cancels the draft without changing the greeting',async()=>{
   await go('agenda');mode='agenda';delay=1600;payload={kind:'event',reply:'请确认。',event:{title:'不应出现',startLocal:'2026-10-11T16:00',leadMinutes:0,repeat:'none',zone:'Asia/Shanghai'}};
   await page.locator('#agenda-input').fill('明天下午四点开会');await page.locator('#agenda-send').click();await page.waitForTimeout(100);await go('interaction');await page.evaluate(()=>companionDemo.select('claude'));await page.waitForTimeout(1800);assert.equal(await page.locator('#bubble-text').innerText(),P.all.claude.hello);assert.equal((await snap()).agenda.pending,null);assert.equal((await snap()).agenda.events.length,1);delay=0;mode='chat';
  });
  await check('only Claude Q sports standing is changed; sitting, all other looks and native program remain intact',async()=>{
   const raw={};vm.runInNewContext(fs.readFileSync(path.join(target,'data.js'),'utf8'),raw);const changes=await page.evaluate(before=>{const changes=[];for(const [family,f] of Object.entries(CLOUD_DATA.families))for(const [style,s] of Object.entries(f.styles))for(const [outfit,look] of Object.entries(s.looks))for(const [pose,d] of Object.entries(look))if(JSON.stringify(d)!==JSON.stringify(before.families[family].styles[style].looks[outfit][pose]))changes.push([family,style,outfit,pose].join('/'));return changes;},raw.CLOUD_DATA);assert.deepEqual(changes,['claude/chibi/sports/stand']);
   await go('partners');await page.evaluate(()=>{companionDemo.select('claude');companionDemo.style('chibi');});await page.locator('[data-hero-outfit=sports]').click();await page.locator('[data-posture=stand]').click();await page.waitForFunction(()=>document.querySelector('#hero-canvas').dataset.file==='art/claude-sports-stand-v2.png');await shot('03-claude-card');
   await page.locator('[data-posture=sit]').click();await page.waitForFunction(()=>document.querySelector('#hero-canvas').dataset.file==='outfits/sports/identity-original-basic.png');assert.equal(await page.locator('#hero-canvas').getAttribute('data-file'),'outfits/sports/identity-original-basic.png');await page.locator('[data-posture=stand]').click();
   await page.goto(pathToFileURL(path.join(target,'claude-review.html')).href);await page.waitForTimeout(700);assert.equal(await page.locator('#error').innerText(),'');await shot('04-claude-art-comparison');await page.locator('#toggle').click();assert.equal(await page.locator('#opacity').inputValue(),'100');assert.equal(sha(exe),nativeBefore);
  });
  await check('phone chat and comparison fit their viewport without script or image errors',async()=>{
   await page.setViewportSize({width:390,height:844});await shot('05-phone-art');assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1));await page.goto(pathToFileURL(path.join(target,'index.html')).href);await page.waitForFunction(()=>globalThis.companionDemo);await go('interaction');await shot('06-phone-chat');assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1));assert.deepEqual((await snap()).errors,[]);assert.deepEqual(errors,[]);
  });
  fs.writeFileSync(path.join(out,'report.json'),JSON.stringify({passed:checks.length,checks,errors,requests:requests.map(({provider,mode})=>({provider,mode})),realApiRequests:0,nativeExeSha256:nativeBefore},null,2));
 }catch(error){await page.screenshot({path:path.join(out,'failure.png'),fullPage:true}).catch(()=>{});throw error;}finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
