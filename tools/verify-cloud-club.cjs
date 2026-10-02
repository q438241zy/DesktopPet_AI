// Browser regression for the pending HTML proposal. Never launches DesktopPet.exe.
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),{pathToFileURL}=require('node:url');
const root=path.resolve(__dirname,'..'),M=require(path.join(root,'docs/demo/cloud-club/model.js'));
const output=path.join(root,'.artifacts/cloud-club-review');fs.mkdirSync(output,{recursive:true});
const results=[];
function passed(name){results.push(name);console.log('PASS '+name);}
for(const style of ['chibi','realistic']){
  assert(M.menu('root',style).includes('chat'));assert(M.menu('root',style).includes('interaction'));assert(!M.menu('root',style).includes('action'));
  assert.equal(M.menu('interaction',style).filter(k=>k==='peek').length,1);
  for(let tier=0;tier<5;tier++)for(const key of Object.keys(M.actions))assert(M.allowed(key,tier));
}
assert(!M.menu('root','chibi').includes('dance'));
for(const side of [-1,1]){
  const state=M.create(760);M.start(state,'peek',()=>side<0?0:.999);
  for(let i=0;i<120&&state.hideStage!=='hidden';i++)M.tick(state,100,760);
  assert.equal(state.hideStage,'hidden');assert.equal(state.hideSide,side);assert(side<0?state.x<0:state.x>760);
  const before={...state};M.toggleMenu(state,{x:100,y:100});
  for(const key of ['action','elapsed','x','phase','dir','hideStage','hideSide'])assert.equal(state[key],before[key]);
  M.tick(state,100,760);assert.equal(state.hideStage,'hidden');assert.equal(state.action,'peek');
  M.find(state);assert.equal(state.hideStage,'returning');
  for(let i=0;i<60&&state.action!=='found';i++)M.tick(state,100,760);
  assert.equal(state.action,'found');assert(state.x>0&&state.x<760);
}
const walk=M.create(760);M.start(walk,'walk');M.tick(walk,100,760);let before={...walk};M.toggleMenu(walk,{x:50,y:50});M.tick(walk,100,760);assert(walk.elapsed>before.elapsed&&walk.x>before.x&&walk.phase>before.phase);M.toggleMenu(walk,{});assert.equal(walk.action,'walk');
passed('state model: test access, root labels, both hide sides, right-click continuity and left-find return');

async function run(){
  let pw;try{pw=require('playwright');}catch{const fallback=process.env.CODEX_PLAYWRIGHT_ROOT||path.join(process.env.USERPROFILE||'','\.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');pw=require(fallback);}
  const executable=process.env.CLOUD_CLUB_BROWSER||'C:/Program Files/Google/Chrome/Application/chrome.exe';
  const browser=await pw.chromium.launch({headless:true,executablePath:executable});
  try{
    const page=await browser.newPage({viewport:{width:1440,height:1160},deviceScaleFactor:1});
    const browserErrors=[];page.on('pageerror',e=>browserErrors.push(e.message));page.on('requestfailed',r=>browserErrors.push(r.url()+': '+r.failure()?.errorText));
    await page.goto(pathToFileURL(path.join(root,'Release/win-x64/Demo/CloudClub/index.html')).href);
    await page.waitForFunction(()=>window.__clubDemo?.snapshot().frame!=null);
    const snap=()=>page.evaluate(()=>__clubDemo.snapshot());
    const clickAction=async(group,key)=>{await page.locator(`[data-group="${group}"]`).click();await page.locator(`#action-list [data-action="${key}"]`).click();};
    const right=()=>page.locator('#pet-canvas').click({button:'right',position:{x:130,y:200}});
    await page.screenshot({path:path.join(output,'01-jade-overview.png'),fullPage:true});
    await page.locator('[data-palette="emerald"]').click();assert.equal(await page.locator('body').getAttribute('data-palette'),'emerald');
    await page.screenshot({path:path.join(output,'02-emerald-overview.png'),fullPage:true});
    await page.locator('.palette-options [data-palette="jade"]').click();
    passed('both membership palettes render');
    for(let tier=0;tier<5;tier++){
      await page.locator(`[data-tier="${tier}"]`).click();await page.locator('#menu-button').click();
      assert.equal(await page.locator('[data-menu-key="interaction"]').count(),1);
      await page.locator('[data-menu-key="chat"]').click();assert.equal((await snap()).action,'chat');
      await page.locator('#chat-input').fill('你好');const start=Date.now();await page.locator('#chat-form button').click();assert((await snap()).thinking);
      await page.waitForTimeout(450);assert((await snap()).thinking);
      await page.waitForFunction(()=>!__clubDemo.snapshot().thinking);assert(Date.now()-start>=990);
      assert((await snap()).reply.includes('你好呀'));await page.locator('#stop-button').click();
    }
    passed('chat works in all five tiers, inline, with >=1 second thinking');
    await clickAction('play','walk');await page.waitForTimeout(250);before=await snap();await right();await page.waitForTimeout(220);let after=await snap();
    assert.equal(after.action,'walk');assert(after.elapsed>before.elapsed&&after.x>before.x&&after.phase>before.phase);assert.equal(after.menu,'root');
    await page.locator('[data-menu-key="care"]').click();before=await snap();await page.waitForTimeout(200);after=await snap();assert.equal(after.menu,'care');assert(after.x>before.x&&after.elapsed>before.elapsed);
    await page.screenshot({path:path.join(output,'03-transparent-care-wheel.png'),fullPage:true});
    await right();assert.equal((await snap()).menu,null);assert.equal((await snap()).action,'walk');
    passed('real pointer right-click and submenu navigation preserve walking and animation phase');
    // Exact same sequence on two styles and every outfit, without body rescaling.
    const fresh=['comb','wipe','breathe','bubbles','stars','butterfly'];
    for(const style of ['realistic','chibi'])for(const outfit of ['original','swim','wedding']){
      await page.locator(`[data-style="${style}"]`).click();await page.locator(`[data-outfit="${outfit}"]`).click();
      for(const key of fresh){
        await clickAction(['comb','wipe','breathe'].includes(key)?'care':'play',key);
        await page.waitForFunction(expected=>__clubDemo.snapshot().frameLook===expected,`${style}-${outfit}`);
        await page.waitForTimeout(150);before=await snap();await right();await page.waitForTimeout(130);after=await snap();
        assert.equal(after.action,key);assert.equal(after.look,`${style}-${outfit}`);assert.equal(after.frameLook,after.look);assert.equal(after.scale,1.25);assert(after.elapsed>before.elapsed);assert.equal(after.errors.length,0);
        await right();assert.equal((await snap()).action,key);
        if(outfit==='original'){
          await page.waitForTimeout(key==='bubbles'?1300:550);
          await page.locator('#stage').screenshot({path:path.join(output,`${style}-${key}.png`)});
        }
      }
    }
    passed('six new interactions × six appearances: 36 combinations, fixed scale, outfit continuity and right-click');
    await clickAction('play','bubbles');await page.waitForTimeout(900);after=await snap();assert(after.targets.length>0);let p=after.targets[0];
    await page.locator('#pet-canvas').click({position:{x:p.x,y:p.y}});assert.equal((await snap()).score,1);
    await clickAction('play','stars');after=await snap();p=after.targets[0];await page.locator('#pet-canvas').click({position:{x:p.x,y:p.y}});assert.equal((await snap()).score,0);await page.waitForTimeout(900);assert.equal((await snap()).score,1);
    await page.locator('#stage').screenshot({path:path.join(output,'04-star-in-palm.png')});
    await clickAction('play','butterfly');await page.locator('#pet-canvas').click({position:{x:260,y:250}});before=await snap();await page.waitForTimeout(400);after=await snap();assert(Math.abs(after.x-260)<Math.abs(before.x-260));
    await page.waitForFunction(()=>__clubDemo.snapshot().butterfly?.landed,null,{timeout:12000});
    after=await snap();assert(Math.abs(after.butterfly.x-after.anchors.hands.x)<.1);assert(Math.abs(after.butterfly.y-(after.anchors.hands.y-5))<.1);
    await page.locator('#stage').screenshot({path:path.join(output,'05-butterfly-on-hand.png')});
    passed('bubble pop scoring, stars travel to palms, butterfly walks then lands on the hand');
    await clickAction('interaction','peek');await page.waitForFunction(()=>__clubDemo.snapshot().hideStage==='hidden',null,{timeout:16000});
    before=await snap();await right();await page.waitForTimeout(500);after=await snap();assert.equal(after.action,'peek');assert.equal(after.hideStage,'hidden');assert.equal(after.x,before.x);assert(after.elapsed>before.elapsed);assert.equal(after.menu,'root');
    await right();await page.locator('#find-pet').click();assert.equal((await snap()).hideStage,'returning');
    await page.waitForFunction(()=>__clubDemo.snapshot().action==='found',null,{timeout:6000});
    passed('actual hide → right menu stays hidden → left find walks back');
    await clickAction('care','breathe');await page.locator('#pause-button').click();before=await snap();await page.waitForTimeout(350);assert.equal((await snap()).elapsed,before.elapsed);await page.locator('#pause-button').click();await page.waitForTimeout(150);assert((await snap()).elapsed>before.elapsed);
    await page.locator('#stop-button').click();assert.equal((await snap()).action,'idle');
    await page.setViewportSize({width:390,height:844});await page.waitForTimeout(150);assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));
    after=await snap();assert(after.x>=after.edgePadding&&after.x<=after.width-after.edgePadding);
    await page.locator('#menu-button').click();await page.locator('[data-menu-key="care"]').click();
    const bounds=await page.locator('.radial-item').evaluateAll(nodes=>nodes.map(n=>{const r=n.getBoundingClientRect();return {x:r.left,right:r.right};}));assert(bounds.every(r=>r.x>=0&&r.right<=390));
    await page.screenshot({path:path.join(output,'06-mobile-menu.png'),fullPage:true});
    passed('explicit pause/resume/stop and 390px layout with contained radial menu');
    assert.deepEqual(browserErrors,[]);assert.deepEqual((await snap()).errors,[]);passed('no browser exceptions or missing asset requests');
    fs.writeFileSync(path.join(output,'verification.json'),JSON.stringify({verifiedAt:new Date().toISOString(),scope:'HTML Demo only; no native execution or account change',checks:results,errors:browserErrors},null,2));
  }finally{await browser.close();}
}
run().catch(error=>{console.error(error);process.exitCode=1;});
