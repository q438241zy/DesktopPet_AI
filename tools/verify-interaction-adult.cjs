const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),{pathToFileURL}=require('node:url');
const pw=require('C:/Users/99000256/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root=path.resolve(__dirname,'..'),output=path.join(root,'.artifacts/interaction-five-review-I3');
async function main(){
  const browser=await pw.chromium.launch({headless:true,executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe'});
  try{
    const context=await browser.newContext({viewport:{width:1360,height:1050},deviceScaleFactor:2});
    const page=await context.newPage(),errors=[];page.on('pageerror',e=>errors.push(e.message));
    await page.goto(pathToFileURL(path.join(root,'Release/win-x64/Demo/InteractionFive/index.html')).href+'?review=I3&style=realistic');
    await page.waitForFunction(()=>globalThis.interactionFiveDebug?.().ready);
    assert.equal(await page.evaluate(()=>interactionFiveDebug().style),'realistic');
    await page.locator('#hand-hit').waitFor({state:'visible'});
    await page.locator('#stage').screenshot({path:path.join(output,'realistic-stage-2x.png')});
    await page.locator('#identity-review').screenshot({path:path.join(output,'realistic-comparison-2x.png')});
    const comparison=async()=>{
      const dims=await page.locator('.identity-grid img,.identity-grid canvas').evaluateAll(nodes=>nodes.map(n=>({w:n.getBoundingClientRect().width,h:n.getBoundingClientRect().height})));
      assert(Math.abs(dims[0].h-dims[1].h)<1);assert(Math.abs(dims[0].w-dims[1].w)<1);assert(Math.abs(dims[1].w/dims[1].h-2/3)<.002);return dims;
    };
    const desktop=await comparison();
    // Actual pointer input still contacts the palm after visual hand-size tuning.
    const start=await page.locator('#player-hand').boundingBox(),stage=await page.locator('#stage').boundingBox();
    const hand=await page.evaluate(()=>interactionFiveDebug().anchors.hand);
    await page.mouse.move(start.x+start.width/2,start.y+start.height/2);await page.mouse.down();await page.mouse.move(stage.x+hand.x,stage.y+hand.y,{steps:12});await page.mouse.up();
    await page.waitForFunction(()=>interactionFiveDebug().phase==='contact');assert.equal(await page.evaluate(()=>interactionFiveDebug().highCount),1);
    await page.locator('#stage').screenshot({path:path.join(output,'realistic-contact-2x.png')});
    await page.locator('[data-action="rps"]').click();await page.locator('[data-choice="rock"]').click();
    const frames=[];
    for(let i=0;i<13;i++){
      await page.waitForTimeout(90);
      frames.push(await page.evaluate(()=>{
        const s=interactionFiveDebug(),c=document.querySelector('#actor'),ctx=c.getContext('2d'),a=ctx.getImageData(0,0,c.width,c.height).data;
        let top=c.height,bottom=0;for(let p=3;p<a.length;p+=4)if(a[p]>100){const y=Math.floor(p/4/c.width);top=Math.min(top,y);bottom=Math.max(bottom,y);}
        return {pose:s.pose,phase:s.phase,top:top/2,bottom:bottom/2,actor:s.actor};
      }));
    }
    assert(new Set(frames.filter(f=>f.phase==='countdown').map(f=>f.pose)).size>=2);
    for(const f of frames){assert(Math.abs(f.bottom-f.actor.floor)<2);assert(Math.abs((f.bottom-f.top)-f.actor.height)<4,JSON.stringify(f));}
    await page.setViewportSize({width:390,height:900});await page.locator('[data-action="highfive"]').click();await page.locator('#hand-hit').waitFor({state:'visible'});
    const mobile=await comparison();assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));
    await page.locator('#stage').screenshot({path:path.join(output,'realistic-mobile-stage-2x.png')});
    await page.locator('#identity-review').screenshot({path:path.join(output,'realistic-mobile-comparison-2x.png')});
    assert.deepEqual(errors,[]);
    fs.writeFileSync(path.join(output,'adult-quality.json'),JSON.stringify({status:'passed',desktop,mobile,frames,checks:['3D query opens corrected character','desktop and mobile comparison identical aspect and size','scaled user hand meets real palm through pointer input','actual 2x canvas standing heights and floor stay stable','no horizontal overflow or browser exceptions']},null,2)+'\n');
    console.log('PASS adult proportion, 2x canvas, palm input, same-size desktop/mobile comparison');
  }finally{await browser.close();}
}
main().catch(e=>{console.error(e);process.exitCode=1;});
