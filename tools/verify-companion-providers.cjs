const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),{pathToFileURL}=require('node:url');
const {chromium}=require('C:/Users/99000256/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root=path.resolve(__dirname,'..'),A=require('../docs/demo/companion-v01/providers.js'),M=require('../docs/demo/companion-v01/model.js'),P=require('../docs/demo/companion-v01/personas.js');
const target=path.join(root,'Release/win-x64/Demo/CompanionV01/index.html'),out=path.join(root,'.artifacts/companion-v02-review');
fs.mkdirSync(out,{recursive:true});const checks=[],errors=[],seen=[],unexpected=[];
function check(name,run){run();checks.push(name);console.log('PASS '+name);}
async function group(name,run){await run();checks.push(name);console.log('PASS '+name);}
const cases={
  openai:{base:'https://api.openai.com/v1',url:'https://api.openai.com/v1/chat/completions'},
  claude:{base:'https://api.anthropic.com/v1',url:'https://api.anthropic.com/v1/messages'},
  gemini:{base:'https://generativelanguage.googleapis.com/v1beta',url:'https://generativelanguage.googleapis.com/v1beta/models/demo-model:generateContent'},
  grok:{base:'https://api.x.ai/v1',url:'https://api.x.ai/v1/chat/completions'},
  deepseek:{base:'https://api.deepseek.com',url:'https://api.deepseek.com/chat/completions'},
  qwen:{base:'https://dashscope.aliyuncs.com/compatible-mode/v1',url:'https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions'},
  glm:{base:'https://open.bigmodel.cn/api/paas/v4',url:'https://open.bigmodel.cn/api/paas/v4/chat/completions'},
  kimi:{base:'https://api.moonshot.ai/v1',url:'https://api.moonshot.ai/v1/chat/completions'}
};
const headers={'Access-Control-Allow-Origin':'*','Access-Control-Allow-Headers':'*','Access-Control-Allow-Methods':'POST,OPTIONS'};
function response(provider,text){return provider==='claude'?{content:[{type:'thinking',thinking:'Do not display this'},{type:'text',text}]}:provider==='gemini'?{candidates:[{content:{parts:[{thought:true,text:'Do not display this'},{text}]}}]}:{choices:[{message:{role:'assistant',content:text}}]};}
(async()=>{
  check('eight endpoints accept roots, bases and full paths without duplication',()=>{
    assert.equal(Object.keys(A.catalog).length,8);
    for(const [id,c] of Object.entries(cases))for(const value of [new URL(c.base).origin,c.base+'/',c.url+'/'])assert.equal(A.endpoint(value,id,'demo-model'),c.url,id+' '+value);
    assert.equal(A.endpoint('https://workspace.cn-beijing.maas.aliyuncs.com/compatible-mode/v1','qwen'),'https://workspace.cn-beijing.maas.aliyuncs.com/compatible-mode/v1/chat/completions');
    assert.equal(A.endpoint('https://api.moonshot.cn','kimi'),'https://api.moonshot.cn/v1/chat/completions');
    assert.equal(A.endpoint('https://dashscope-intl.aliyuncs.com/compatible-mode/v1','qwen'),'https://dashscope-intl.aliyuncs.com/compatible-mode/v1/chat/completions');
    for(const value of ['https://api.openai.com/v1/responses','http://remote.example','https://user:key@example.com','https://api.openai.com?key=secret'])assert.throws(()=>A.endpoint(value));
    assert.throws(()=>A.endpoint('https://generativelanguage.googleapis.com/v1beta/models/other:generateContent','gemini','demo-model'));
    assert.throws(()=>A.endpoint('https://{WorkspaceId}.cn-beijing.maas.aliyuncs.com','qwen'));
  });
  check('native and compatible request/response schemas, no reasoning leak and no secret persistence',()=>{
    for(const [id,c] of Object.entries(cases)){
      const config={provider:id,endpoint:c.base,model:'demo-model',workspace:'wrkspc_test'},req=A.request(config,'unit-secret',[{role:'assistant',content:'orphan'},{role:'user',content:'first'},{role:'user',content:'retry'},{role:'assistant',content:'answer'},{role:'user',content:'second'}],'persona');
      const body=JSON.parse(req.options.body);assert.equal(req.url,c.url);assert.equal(A.responseText(id,response(id,'hello')),'hello');assert.throws(()=>A.responseText(id,{}));
      assert.equal(req.options.body.includes('unit-secret'),false);
      if(id==='claude'){assert.equal(body.system,'persona');assert.equal(body.max_tokens,1024);assert.equal(req.options.headers['anthropic-version'],'2023-06-01');assert.equal(req.options.headers['x-api-key'],'unit-secret');assert.equal(req.options.headers['anthropic-workspace-id'],'wrkspc_test');assert.deepEqual(body.messages.map(m=>m.role),['user','assistant','user']);}
      else if(id==='gemini'){assert.equal(body.systemInstruction.parts[0].text,'persona');assert.deepEqual(body.contents.map(m=>m.role),['user','model','user']);assert.equal(req.options.headers['x-goog-api-key'],'unit-secret');assert.equal(new URL(req.url).search,'');}
      else{assert.equal(body.messages[0].role,id==='openai'?'developer':'system');assert.equal(req.options.headers.Authorization,'Bearer unit-secret');assert.equal(body.stream,false);}
      const s=M.create();s.api={...config,key:'must-not-survive',apiKey:'must-not-survive'};assert.equal(JSON.stringify(M.create(s)).includes('must-not-survive'),false);assert.equal(M.create(s).api.provider,id);
    }
    assert.throws(()=>A.responseText('gemini',{candidates:[{content:{parts:[{thought:true,text:'secret reasoning'}]}}]}));
    assert.throws(()=>A.responseText('claude',{content:[{type:'thinking',thinking:'secret reasoning'}]}));
  });
  check('saved timer switch migrates cleanly and presets cover all eight personalities',()=>{
    const old=M.create();old.work={minutes:10,message:'old custom text'};assert.deepEqual(M.create(old).work,{enabled:false,minutes:10});
    const s=M.create(),r=M.createRuntime(0);M.workStart(r,s,0);assert.equal(M.create(s).work.enabled,true);M.workStop(r,s);assert.equal(M.create(s).work.enabled,false);assert.deepEqual(M.tick(r,s,600000,{canHide:false}),[]);
    for(const id of M.families){assert.equal(new Set(P.reminderTasks.map((_,i)=>P.reminder(id,i))).size,6);assert.ok(!P.reminder(id).includes('{'));}
  });
  const browser=await chromium.launch({executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true});
  try{
    const context=await browser.newContext({viewport:{width:1280,height:1000}}),page=await context.newPage();
    page.on('pageerror',e=>errors.push(e.message));let active='openai',fail=false,delay=0;
    await context.route(/^https?:/,async route=>{
      const req=route.request();if(req.method()==='OPTIONS'){await route.fulfill({status:204,headers});return;}
      if(req.url()!==cases[active].url){unexpected.push(req.url());await route.abort();return;}
      seen.push({provider:active,url:req.url(),headers:req.headers(),body:req.postDataJSON()});const provider=active,failed=fail,ms=delay;if(ms)await new Promise(r=>setTimeout(r,ms));
      try{await route.fulfill({status:failed?401:200,headers,contentType:'application/json',body:JSON.stringify(failed?{error:{message:'authorization failed'}}:response(provider,'模拟回复 · '+provider))});}catch{/* The UI may have cancelled this request. */}
    });
    await page.goto(pathToFileURL(target).href);await page.waitForFunction(()=>globalThis.companionDemo);const snap=()=>page.evaluate(()=>companionDemo.snapshot());
    await group('header has no Demo tag, footer tools remain reachable, daily gift entry removed',async()=>{
      assert.equal(await page.locator('.page-header').innerText(),'我的伙伴');assert.equal(await page.locator('[data-version]').count(),1);await page.locator('#review-open').click();assert.equal(await page.locator('#review-dialog').isVisible(),true);await page.locator('[data-close="review-dialog"]').click();
      await page.locator('[data-page="daily"]').click();assert.equal(await page.locator('#gift-draw').count(),0);assert.equal((await page.locator('#page-daily').innerText()).includes('拆份礼物'),false);
      await page.screenshot({path:path.join(out,'daily.png'),fullPage:true,animations:'disabled'});
    });
    await group('work is off by default; enabling reveals interval and recurring presets without user text',async()=>{
      await page.locator('[data-page="settings"]').click();assert.equal(await page.locator('#work-enabled').isChecked(),false);assert.equal(await page.locator('#work-form').isVisible(),false);assert.equal(await page.locator('#work-message').count(),0);await page.screenshot({path:path.join(out,'settings-off.png'),fullPage:true,animations:'disabled'});
      await page.locator('#work-enabled').check();assert.equal(await page.locator('#work-form').isVisible(),true);assert.equal((await snap()).runtime.workActive,true);const next=(await snap()).runtime.nextReminder;
      await page.evaluate(()=>companionDemo.advance(600001));assert.equal((await snap()).runtime.reminderCount,1);assert.equal((await snap()).runtime.nextReminder,next+600000);assert.equal(await page.locator('#reminder-text').innerText(),P.reminder('whale',0));
      await page.evaluate(()=>companionDemo.advance(600001));assert.equal(await page.locator('#reminder-text').innerText(),P.reminder('whale',1));
      const before=(await snap()).runtime.nextReminder;await page.locator('#work-minutes').fill('3');await page.locator('#work-try').click();assert.equal((await snap()).runtime.nextReminder,before);await page.screenshot({path:path.join(out,'work-enabled.png'),fullPage:true,animations:'disabled'});
      await page.locator('#work-enabled').uncheck();assert.equal(await page.locator('#reminder').isVisible(),false);const count=(await snap()).runtime.reminderCount;await page.evaluate(()=>companionDemo.advance(3600000));assert.equal((await snap()).runtime.reminderCount,count);await page.reload();await page.waitForFunction(()=>globalThis.companionDemo);await page.locator('[data-page="settings"]').click();assert.equal(await page.locator('#work-enabled').isChecked(),false);
      await page.locator('#work-enabled').check();await page.reload();await page.waitForFunction(()=>globalThis.companionDemo);assert.equal((await snap()).runtime.workActive,true);await page.locator('[data-page="settings"]').click();await page.locator('#work-enabled').uncheck();
    });
    for(const id of Object.keys(cases))await group(id+' actual UI connects using its protocol and keeps one-second thinking',async()=>{
      active=id;await page.locator('[data-page="settings"]').click();await page.locator('#api-provider').selectOption(id);assert.equal(await page.locator('#api-key').inputValue(),'');assert.equal(await page.locator('#api-endpoint').inputValue(),cases[id].base);await page.locator('#api-model').fill('demo-model');await page.locator('#api-key').fill('ui-secret-'+id);
      if(id==='claude')await page.locator('#api-workspace').fill('wrkspc_test');
      await page.locator('#api-test').click();await page.waitForFunction(()=>document.querySelector('#api-status').textContent.startsWith('连接成功'));await page.locator('#api-save').click();await page.locator('[data-page="partners"]').click();await page.locator('[data-family="gpt"]').click();await page.locator('#chat-input').fill('今天有点累');await page.locator('#chat-send').click();await page.waitForTimeout(200);assert.equal((await snap()).runtime.action,'thinking');await page.waitForFunction(()=>companionDemo.snapshot().runtime.action==='talk');assert.ok((await snap()).chatDuration>=990);assert.equal(await page.locator('#bubble-text').innerText(),'模拟回复 · '+id);
      const request=seen.at(-1);if(id==='claude'){assert.equal(request.headers['x-api-key'],'ui-secret-'+id);assert.equal(request.headers['anthropic-dangerous-direct-browser-access'],'true');assert.equal(request.headers['anthropic-workspace-id'],'wrkspc_test');assert.match(request.body.system,/GPT/);}else if(id==='gemini'){assert.equal(request.headers['x-goog-api-key'],'ui-secret-'+id);assert.match(request.body.systemInstruction.parts[0].text,/GPT/);}else{assert.equal(request.headers.authorization,'Bearer ui-secret-'+id);assert.match(request.body.messages[0].content,/GPT/);}
      assert.equal(await page.evaluate(()=>Object.values(localStorage).some(v=>v.includes('ui-secret-'))),false);
    });
    await group('connection failures are visible; work reminder API reply is discarded after switch off',async()=>{
      await page.locator('[data-page="settings"]').click();fail=true;await page.locator('#api-test').click();await page.waitForFunction(()=>document.querySelector('#api-status').textContent.includes('401'));fail=false;
      await page.locator('#work-enabled').check();await page.locator('#work-try').click();await page.waitForFunction(()=>document.querySelector('#reminder-source').textContent==='模型提醒');assert.equal(await page.locator('#reminder-text').innerText(),'模拟回复 · kimi');
      delay=800;await page.locator('#work-try').click();await page.locator('#work-enabled').uncheck();await page.waitForTimeout(1000);assert.equal(await page.locator('#reminder').isVisible(),false);delay=0;
      await page.locator('#api-key').fill('do-not-carry');await page.locator('#api-endpoint').fill('https://example.com/v1');assert.equal(await page.locator('#api-key').inputValue(),'');
      await page.locator('#api-provider').selectOption('claude');await page.screenshot({path:path.join(out,'api-claude.png'),fullPage:true,animations:'disabled'});await page.locator('#api-provider').selectOption('gemini');await page.screenshot({path:path.join(out,'api-gemini.png'),fullPage:true,animations:'disabled'});
      await page.reload();await page.waitForFunction(()=>globalThis.companionDemo);await page.locator('[data-page="settings"]').click();assert.equal(await page.locator('#api-key').inputValue(),'');assert.equal(await page.locator('#api-provider').inputValue(),'kimi');
    });
    await group('mobile switch and API settings remain usable without overflow',async()=>{
      await page.setViewportSize({width:390,height:844});await page.locator('#api-provider').selectOption('claude');await page.locator('#work-enabled').check();assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false);await page.screenshot({path:path.join(out,'mobile-settings.png'),fullPage:true,animations:'disabled'});await page.locator('#work-enabled').uncheck();
    });
    assert.deepEqual(errors,[]);assert.deepEqual(unexpected,[]);
  }finally{await browser.close();}
  fs.writeFileSync(path.join(out,'providers-report.json'),JSON.stringify({passed:true,checks,errors,requestCount:seen.length,notes:['Eight provider HTTP calls were intercepted in Chromium; no real API key or paid call used.','Native executable and normal user profile were not opened or modified.']},null,2));console.log(JSON.stringify({passed:checks.length,output:out}));
})().catch(error=>{fs.writeFileSync(path.join(out,'providers-failure.json'),JSON.stringify({checks,error:String(error.stack),errors,unexpected},null,2));console.error(error);process.exit(1);});
