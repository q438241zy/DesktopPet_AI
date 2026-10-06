const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),vm=require('node:vm'),{pathToFileURL}=require('node:url');
const {chromium}=require('C:/Users/99000256/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root=path.resolve(__dirname,'..'),target=path.resolve(process.argv[2]||path.join(root,'docs/demo/interaction-five/index.html'));
const out=path.join(root,'.artifacts/five-roster-browser');fs.mkdirSync(out,{recursive:true});
const index={};vm.runInNewContext(fs.readFileSync(path.join(path.dirname(target),'roster.js'),'utf8'),{globalThis:index});
async function main(){
 const browser=await chromium.launch({executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true});
 const context=await browser.newContext({viewport:{width:1280,height:1100},deviceScaleFactor:2});const page=await context.newPage(),errors=[],reports=[];
 page.on('pageerror',e=>errors.push(e.message));page.on('requestfailed',r=>errors.push(r.url()+': '+r.failure().errorText));
 const state=()=>page.evaluate(()=>interactionFiveDebug());
 const phase=p=>page.waitForFunction(p=>interactionFiveDebug().phase===p,p);
 const select=k=>page.locator(`#actions [data-action="${k}"]`).click();
 try{
  const requested=process.env.FIVE_ROSTER_FILTER;
  for(const entry of index.FIVE_ROSTER.appearances.filter(e=>!requested||new RegExp(requested).test(e.id))){
   const id=entry.id,safe=id.replace('/','-');
   await page.goto(pathToFileURL(target).href+`?style=${entry.style}&family=${entry.family}&outfit=${entry.outfit}`);
   await page.waitForFunction(id=>interactionFiveDebug?.().ready&&interactionFiveDebug().appearance===id,id);
   assert.equal((await state()).decodedSheets,Object.keys(entry.images).length+1);assert.equal((await state()).outfit,entry.outfit);
   await page.locator('#hand-hit').waitFor({state:'visible'});await page.locator('#stage').screenshot({path:path.join(out,safe+'-palm.png')});
   await page.locator('#hand-hit').click();await phase('contact');assert.equal((await state()).highCount,1);
   await select('rps');await page.locator('[data-choice="scissors"]').click();await phase('revealed');assert.equal((await state()).pose,'reveal-'+(await state()).pet);
   await select('gift');await page.locator('#gift-token').focus();await page.keyboard.press('Enter');await phase('holding');await page.locator('#gift-hit').click();await phase('opened');
   await page.locator('#stage').screenshot({path:path.join(out,safe+'-gift.png')});assert((await state()).prize);
   await select('read');await page.locator('#story-play').click();await page.waitForTimeout(200);assert.equal((await state()).pose,'read');
   await select('photo');await page.locator('#shutter').click();await phase('saved');assert((await state()).photoReady);
   const url=await page.locator('#save-photo').getAttribute('href');assert(url.startsWith('data:image/png;base64,'));fs.writeFileSync(path.join(out,safe+'-photo.png'),Buffer.from(url.split(',')[1],'base64'));
   assert.deepEqual((await state()).errors,[]);reports.push(id);console.log('PASS '+id+' five interactions, exact outfit and offline photo');
  }
  assert.deepEqual(errors,[]);fs.writeFileSync(path.join(out,'report.json'),JSON.stringify({passed:reports.length,appearances:reports,errors,verifiedAt:new Date().toISOString()},null,2));
 }finally{await browser.close();}
}
main().catch(e=>{console.error(e);process.exitCode=1;});
