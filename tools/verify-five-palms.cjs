const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm'),assert=require('node:assert/strict'),{pathToFileURL}=require('node:url');
const {chromium}=require('C:/Users/99000256/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root=path.resolve(__dirname,'..'),file=path.resolve(process.argv[2]),scope={};
vm.runInNewContext(fs.readFileSync(path.join(path.dirname(file),'roster.js'),'utf8'),{globalThis:scope});
const out=path.join(root,'.artifacts/five-palms-final');fs.mkdirSync(out,{recursive:true});
(async()=>{
 const browser=await chromium.launch({executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true});
 const page=await browser.newPage({viewport:{width:1280,height:1100},deviceScaleFactor:2}),errors=[],passed=[];
 page.on('pageerror',e=>errors.push(e.message));page.on('requestfailed',r=>errors.push(r.url()));
 try{
  for(const e of scope.FIVE_ROSTER.appearances.filter(e=>e.style==='chibi')){
   await page.goto(pathToFileURL(file).href+`?family=${e.family}&style=chibi&outfit=${e.outfit}`);
   await page.waitForFunction(id=>interactionFiveDebug().ready&&interactionFiveDebug().appearance===id,e.id);
   await page.locator('#hand-hit').waitFor({state:'visible'});
   await page.locator('#stage').screenshot({path:path.join(out,e.id.replace('/','-')+'-ready.png')});
   await page.locator('#hand-hit').click();
   await page.waitForFunction(()=>interactionFiveDebug().phase==='contact');
   assert.equal(await page.evaluate(()=>interactionFiveDebug().highCount),1);
   await page.locator('#stage').screenshot({path:path.join(out,e.id.replace('/','-')+'-contact.png')});
   passed.push(e.id);console.log('PASS '+e.id+' revised palm contact');
  }
  assert.equal(passed.length,32);assert.deepEqual(errors,[]);
  fs.writeFileSync(path.join(out,'report.json'),JSON.stringify({passed:passed.length,appearances:passed,errors,target:file},null,2));
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
