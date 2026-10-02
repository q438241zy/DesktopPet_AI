const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict'),{pathToFileURL}=require('node:url');
const pw=require(process.env.CODEX_PLAYWRIGHT_ROOT||path.join(process.env.USERPROFILE,'.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright'));
const root=path.resolve(__dirname,'..'),file=process.argv[2]||path.join(root,'Release/win-x64/Demo/DeepSeek-demo.html');
const out=path.join(root,'.artifacts/cloud-club-native/browser');fs.mkdirSync(out,{recursive:true});
(async()=>{
 const browser=await pw.chromium.launch({headless:true,executablePath:process.env.CLOUD_CLUB_BROWSER||'C:/Program Files/Google/Chrome/Application/chrome.exe'});
 try{
  const page=await browser.newPage({viewport:{width:1440,height:1000}}),errors=[];
  page.on('pageerror',e=>errors.push(e.message));page.on('requestfailed',r=>errors.push(r.url()));
  await page.goto(pathToFileURL(path.resolve(file)).href);await page.waitForFunction(()=>states.every(s=>!s.loading));
  assert.equal(await page.evaluate(()=>D.coverage.length),1728);
  for(const outfit of ['original','swim','wedding'])for(const action of ['comb','wipe','stretch','bubbles','stars','butterfly']){
   await page.evaluate(({o,a})=>{outfit=o;run(a);},{o:outfit,a:action});await page.waitForFunction(()=>states.every(s=>!s.loading));
   assert.deepEqual(await page.evaluate(()=>states.map(s=>s.action)),[action,action]);
   await page.evaluate(()=>{for(const s of states)s.elapsed=700;});await page.waitForTimeout(70);
   const first=await page.evaluate(()=>states.map(s=>s.image.src));await page.waitForTimeout(420);
   const second=await page.evaluate(()=>states.map(s=>s.image.src));assert(second.some((v,i)=>v!==first[i]));
   if(['bubbles','stretch','stars'].includes(action))await page.locator('#cards').screenshot({path:path.join(out,`${outfit}-${action}.png`)});
  }
  await page.selectOption('#memberTier','0');await page.evaluate(()=>run('chat'));assert(await page.evaluate(()=>states.every(s=>!s.composer.hidden)));
  await page.evaluate(()=>run('stars'));await page.waitForFunction(()=>states.every(s=>!s.loading));
  await page.locator('.grip').first().dispatchEvent('contextmenu',{button:2});assert.equal(await page.evaluate(()=>states[0].action),'stars');
  assert.deepEqual(errors,[]);fs.writeFileSync(path.join(out,'export-check.json'),JSON.stringify({appearances:6,actions:6,coverageRows:1728,errors},null,2));
  console.log('PASS: exported native demo plays six interactions in both styles and three outfits; guest-level chat and right-click continuity; no browser errors.');
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
