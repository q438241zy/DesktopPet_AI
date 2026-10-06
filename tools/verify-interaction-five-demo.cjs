const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto'),{pathToFileURL}=require('node:url');
const root=path.resolve(__dirname,'..'),M=require(path.join(root,'docs/demo/interaction-five/model.js')),Items=require(path.join(root,'docs/demo/interaction-five/items.js'));
const target=process.argv[2]||path.join(root,'Release/win-x64/Demo/InteractionFive/index.html');
const output=path.join(root,'.artifacts/interaction-five-review-I3');fs.mkdirSync(output,{recursive:true});
const reports=[],shots=[],timelines={};
const hash=file=>crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const protectedFiles=[path.join(root,'Release/win-x64/DesktopPet.exe'),'C:/Users/99000256/AppData/Local/DesktopPetAI/state.json'];
const protectedBefore=Object.fromEntries(protectedFiles.map(file=>[file,hash(file)]));
function passed(name){reports.push(name);console.log('PASS '+name);}
function advance(s,ms){for(let elapsed=0;elapsed<ms;elapsed+=20)M.advance(s,Math.min(20,ms-elapsed));}
function modelChecks(){
  assert.equal(Object.keys(M.actions).length,5);
  for(const action of Object.keys(M.actions)){const s=M.create();M.start(s,action,0);advance(s,20000);assert.deepEqual(s.completed,{});}
  const high=M.create();assert.equal(M.highfive(high),false);advance(high,750);assert(M.highfive(high));advance(high,470);assert.equal(high.highCount,0);advance(high,10);assert.equal(high.highCount,1);assert.equal(high.phase,'contact');advance(high,1250);assert.equal(high.phase,'offer');assert.equal(high.highCount,1);
  passed('Waiting performs no interaction; high-five counts only after the hands meet');
  for(let pet=0;pet<3;pet++)for(const user of M.choices){
    const s=M.create();M.start(s,'rps',(pet+.1)/3);assert(M.choose(s,user));assert(!M.choose(s,'paper'));
    const poses=new Set();for(let t=0;t<1780;t+=20){advance(s,20);poses.add(M.pose(s));}
    assert.deepEqual([...poses].sort(),['fist-down','fist-mid','fist-up']);assert.equal(s.pet,null);assert.equal(s.outcome,null);
    advance(s,20);assert.equal(s.phase,'shoot');assert.equal(s.pet,M.choices[pet]);advance(s,200);assert.equal(M.pose(s),'reveal-'+s.pet);assert.equal(s.outcome,null);
    advance(s,600);const expected=user===s.pet?'draw':({rock:'scissors',scissors:'paper',paper:'rock'}[user]===s.pet?'win':'lose');assert.equal(s.outcome,expected);assert.equal(s.rounds,1);advance(s,3000);assert.equal(s.rounds,1);
  }
  passed('Nine fair guessing outcomes; three pumping poses; hands reveal before results');
  assert.equal(M.gifts.length,20);assert.deepEqual(Items.ids,M.gifts.map(g=>g.id));
  const nativeIds=[...fs.readFileSync(path.join(root,'src/DesktopPet.Core/Collectibles.cs'),'utf8').matchAll(/new\("([^"]+)"/g)].map(m=>m[1]);assert.deepEqual(M.gifts.map(g=>g.id),nativeIds);
  const bag=M.create(),draws=[];for(let i=0;i<60;i++){M.start(bag,'gift',0);M.deliver(bag);assert(M.openGift(bag,(i*.317)%1));assert.equal(bag.gifts,i);advance(bag,1000);draws.push(bag.prize);assert.equal(bag.gifts,i+1);}
  for(let i=0;i<3;i++)assert.equal(new Set(draws.slice(i*20,(i+1)*20)).size,20);
  assert.notEqual(draws[19],draws[20]);assert.notEqual(draws[39],draws[40]);assert.equal(Object.keys(bag.collection).length,20);
  assert.deepEqual(M.collectionSave(M.create(M.collectionSave(bag))),M.collectionSave(bag));
  const invalid=M.create({version:1,counts:{rice:2,key:-1,bread:3.7,unknown:50},bag:['constructor'],last:'toString'});assert.deepEqual(invalid.collection,{rice:2});assert.equal(invalid.bag.length,20);assert.equal(invalid.lastGift,null);
  assert.equal(M.create({version:1,bag:['bread','bread']}).bag.length,20);
  passed('20 native-matching random gifts; three complete bags; restore validates IDs and counts');
  assert.equal(M.stories.length,3);
  for(const story of M.stories){
    assert(story.sentences.length>=5&&story.sentences.length<=10);
    const s=M.create();M.start(s,'read',0);assert(M.selectStory(s,story.id));advance(s,20000);assert.equal(s.phase,'ready');assert(M.beginReading(s));advance(s,1300);assert(M.pauseReading(s));const t=s.time;advance(s,12000);assert.equal(s.time,t);assert.equal(s.sentenceIndex,0);assert(M.pauseReading(s));
    const seen=new Set();for(let tick=0;tick<6000&&s.phase!=='finished';tick++){seen.add(s.sentenceIndex);M.advance(s,20);}
    assert.equal(s.phase,'finished');assert.equal(s.books,1);assert.equal(seen.size,story.sentences.length);assert.equal(s.sentenceIndex,story.sentences.length-1);advance(s,60000);assert.equal(s.books,1);assert.equal(s.phase,'finished');assert(!M.nextSentence(s));assert(s.completed.read);assert(s.finishedStories[story.id]);assert(M.beginReading(s));assert.equal(s.sentenceIndex,0);
  }
  passed('Three original 7–8 sentence stories; timed reading, pause, finite completion and explicit replay');
  for(const action of ['highfive','rps','gift','read','photo']){
    const s=M.create();M.start(s,action,0);
    if(action==='highfive'){advance(s,750);M.highfive(s);}if(action==='rps')M.choose(s,'rock');if(action==='gift'){M.deliver(s);M.openGift(s,0);}if(action==='read'){M.beginReading(s);M.nextSentence(s);}if(action==='photo')M.shutter(s);
    advance(s,100);M.start(s,'photo',0);advance(s,4000);assert.deepEqual(s.completed,{});assert.equal(s.gifts,0);assert.equal(s.bag.length,20);assert.equal(s.books,0);assert.equal(s.rounds,0);assert.equal(s.highCount,0);assert.equal(s.photos,0);
  }
  passed('Switching cancels pending hand contact, reveals, unwrapping, reading and capture');
}
function artChecks(){
  const geometry=JSON.parse(fs.readFileSync(path.join(root,'docs/demo/interaction-five/art/geometry.json'),'utf8'));
  for(const [style,data]of Object.entries(geometry.styles)){
    assert.equal(Object.keys(data.poses).length,style==='chibi'?24:20);assert.equal(hash(path.join(root,'docs/demo/interaction-five',data.file)),data.sha256);
    for(const p of Object.values(data.poses)){const source=p.atlas?geometry.supplements[p.atlas]:data,[x,y,w,h]=p.bounds,d=source.dimensions;assert(x>=0&&y>=0&&x+w<=d[0]&&y+h<=d[1]);assert.equal(p.foot[1],y+h);assert.equal(p.unit||1,p.atlas?source.unit:1);if(style==='realistic')assert(Math.abs(h*p.unit/data.referenceHeight-1)<.012,'adult standing height changes by over 1.2%');}
  }
  for(const source of Object.values(geometry.supplements))assert.equal(hash(path.join(root,'docs/demo/interaction-five',source.file)),source.sha256);
  assert.equal(geometry.styles.chibi.sha256,'5aa3cf89f60fc840a6a34c1b860fc99ccf55f913e59b5d347c0c2310b2d8f590');
  assert.equal(hash(path.join(root,'docs/demo/interaction-five',geometry.adultReference.file)),hash(path.join(root,'src/DesktopPet.App/Assets/Characters/deepseek-adult/portrait.png')));
  passed('24 approved Q poses preserved; 20 adult poses in five fixed-scale atlases, standing height variation below 1.2%');
}
let pw;try{pw=require('playwright');}catch{pw=require('C:/Users/99000256/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');}
async function main(){
  modelChecks();artChecks();
  const browser=await pw.chromium.launch({headless:true,executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe'});
  try{
    const context=await browser.newContext({viewport:{width:1360,height:1050},deviceScaleFactor:1,acceptDownloads:true});
    const page=await context.newPage(),errors=[],failed=[],requests=[];
    const observe=p=>{p.on('pageerror',e=>errors.push(e.message));p.on('requestfailed',r=>failed.push(r.url()));p.on('request',r=>{if(/^https?:/.test(r.url()))requests.push(r.url());});};observe(page);
    const state=()=>page.evaluate(()=>interactionFiveDebug());
    const phase=name=>page.waitForFunction(expected=>interactionFiveDebug().phase===expected,name,{timeout:9000});
    const select=async key=>{await page.locator('[data-action="'+key+'"]').click();};
    const shot=async name=>{const file=path.join(output,name+'.png');await page.screenshot({path:file,fullPage:true});shots.push(file);};
    const pixels=()=>page.evaluate(()=>{const c=document.querySelector('#actor'),a=c.getContext('2d').getImageData(0,0,c.width,c.height).data;let count=0,hash=0,bottom=0;for(let i=3;i<a.length;i+=4)if(a[i]>80){count++;hash=((hash*31)^a[i-3]^a[i-2]^a[i-1])|0;bottom=Math.floor(i/4/c.width);}return {count,hash,bottom};});
    const dragHand=async goal=>{const q=await state(),b=await page.locator('#stage').boundingBox();await page.mouse.move(b.x+q.playerHand.position.x,b.y+q.playerHand.position.y);await page.mouse.down();await page.mouse.move(b.x+goal.x,b.y+goal.y,{steps:12});await page.mouse.up();};
    await page.goto(pathToFileURL(target).href+'?review=I3');await page.waitForFunction(()=>globalThis.interactionFiveDebug?.().ready,{timeout:15000});
    for(const style of ['chibi','realistic']){
      await page.locator('[data-style="'+style+'"]').click();await select('highfive');await page.locator('#hand-hit').waitFor({state:'visible'});
      const before=await state(),first=await pixels();assert(first.count>15000);assert(Math.abs(first.bottom-before.actor.floor)<5);await shot(style+'-highfive-ready');
      await dragHand({x:55,y:155});let q=await state();assert.equal(q.phase,'offer');assert.equal(q.highCount,before.highCount);assert.equal(q.handDragging,false);
      await dragHand(q.anchors.hand);await phase('contact');q=await state();assert.equal(q.highCount,before.highCount+1);assert(q.playerHand.contact);assert(Math.hypot(q.playerHand.position.x-q.anchors.hand.x,q.playerHand.position.y-q.anchors.hand.y)<2);assert.equal(q.actor.scale,before.actor.scale);await shot(style+'-highfive-contact');
      await phase('offer');await page.locator('#hand-hit').waitFor({state:'visible'});const origin=(await state()).playerHand.position;
      await page.locator('#hand-hit').click();q=await state();assert.equal(q.phase,'approach');assert.equal(q.highCount,before.highCount+1);await page.waitForTimeout(180);q=await state();assert(Math.hypot(q.playerHand.position.x-origin.x,q.playerHand.position.y-origin.y)>25);await phase('contact');assert.equal((await state()).highCount,before.highCount+2);
      await phase('offer');await page.locator('#hand-hit').waitFor({state:'visible'});await page.locator('#player-hand').focus();await page.keyboard.press('Enter');await phase('contact');assert.equal((await state()).highCount,before.highCount+3);
      passed(style+': missed drag ignored; mutual palm contact, click approach and keyboard high-five');

      await select('rps');await page.waitForTimeout(600);assert.equal((await state()).phase,'choose');
      for(const hand of M.choices){
        await page.locator('[data-choice="'+hand+'"]').click();q=await state();assert.equal(q.phase,'countdown');assert.equal(q.pet,null);assert.equal(q.outcome,null);assert(await page.locator('[data-choice="rock"]').isDisabled());
        const frames=[];for(let i=0;i<14;i++){await page.waitForTimeout(65);q=await state();if(q.phase==='countdown')frames.push({time:q.time,pose:q.pose,userY:q.playerHand.position.y});}
        assert.equal(new Set(frames.map(f=>f.pose)).size,3);assert(Math.max(...frames.map(f=>f.userY))-Math.min(...frames.map(f=>f.userY))>30);assert(frames.every(f=>f.pose.startsWith('fist-')));
        await page.locator('#stage').click({button:'right',position:{x:48,y:205}});q=await state();assert(q.menu);assert.equal(q.action,'rps');assert.equal(q.rounds,before.rounds+M.choices.indexOf(hand));await page.locator('#stage').click({button:'right',position:{x:48,y:205}});
        await phase('revealed');q=await state();assert.equal(q.player,hand);assert.equal(q.playerHand.pose,hand);assert.equal(q.pose,'reveal-'+q.pet);assert.equal(q.actor.scale,before.actor.scale);assert((await page.locator('#rps-result').textContent()).includes(M.names[q.pet]));
        timelines[style+'-rps-'+hand]=frames;await shot(style+'-rps-'+hand);if(hand!=='paper')await page.locator('#rps-again').click();
      }
      passed(style+': both hands visibly pump, three selections reveal together, right click preserves the round');

      await select('gift');const empty=await pixels();let token=await page.locator('#gift-token').boundingBox(),stageBox=await page.locator('#stage').boundingBox();
      await page.mouse.move(token.x+token.width/2,token.y+token.height/2);await page.mouse.down();await page.mouse.move(stageBox.x+40,stageBox.y+150,{steps:12});await page.mouse.up();assert.equal((await state()).phase,'receive');assert.equal((await state()).dragging,false);
      token=await page.locator('#gift-token').boundingBox();q=await state();const giftsBefore=q.gifts;
      await page.mouse.move(token.x+token.width/2,token.y+token.height/2);await page.mouse.down();await page.mouse.move(stageBox.x+q.anchors.receive.x,stageBox.y+q.anchors.receive.y,{steps:14});await page.mouse.up();await phase('holding');assert.equal(await page.locator('#gift-token').isVisible(),false);assert.notEqual((await pixels()).hash,empty.hash);await shot(style+'-gift-holding');
      await page.locator('#gift-hit').click();assert.equal((await state()).gifts,giftsBefore);await phase('opened');q=await state();assert.equal(q.pose,'gift-empty');assert.equal(q.gifts,giftsBefore+1);assert(q.collection[q.prize]>0);assert.equal(q.actor.scale,before.actor.scale);assert.equal(await page.locator('#gift-collection [data-collectible]').count(),20);assert.equal(await page.locator('[data-collectible="'+q.prize+'"]').evaluate(n=>n.classList.contains('missing')),false);await shot(style+'-gift-opened');
      const oldPrize=q.prize;await page.locator('#gift-again').click();await page.locator('#gift-token').focus();await page.keyboard.press('Enter');await phase('holding');await page.locator('#gift-hit').click();await phase('opened');q=await state();assert.notEqual(q.prize,oldPrize);assert.equal(q.gifts,giftsBefore+2);
      const shelf={...q.collection};await page.locator('#gift-again').click();await page.locator('#gift-token').focus();await page.keyboard.press('Enter');await phase('holding');await page.locator('#gift-hit').click();await select('highfive');await page.waitForTimeout(1200);assert.deepEqual((await state()).collection,shelf);
      passed(style+': gift drop, empty-box random prize, collection grid, different next gift and canceled unwrap');

      await select('read');await page.locator('#story-select').selectOption('little-bell');q=await state();assert.equal(q.phase,'ready');assert.equal(q.sentenceCount,8);assert.equal(await page.locator('#story-lines li').count(),8);const booksBefore=q.books;
      await page.locator('#book-hit').click();q=await state();assert.equal(q.phase,'reading');assert.equal(q.sentenceIndex,0);assert.equal(await page.locator('#speech').textContent(),M.stories[1].sentences[0]);
      await page.locator('#story-play').click();q=await state();assert.equal(q.phase,'paused');const pausedTime=q.time;await page.waitForTimeout(450);assert.equal((await state()).time,pausedTime);await page.locator('#story-play').click();
      await page.waitForFunction(()=>interactionFiveDebug().sentenceIndex===1,null,{timeout:6500});q=await state();assert.equal(q.phase,'reading');assert.equal(await page.locator('#speech').textContent(),M.stories[1].sentences[1]);
      await page.locator('#stage').click({button:'right',position:{x:48,y:205}});q=await state();assert(q.menu);assert.equal(q.sentenceIndex,1);await page.locator('#stage').click({button:'right',position:{x:48,y:205}});
      const book=await pixels();await page.locator('#book-hit').click();q=await state();assert.equal(q.phase,'turning');assert.equal(q.pose,'page');assert.notEqual((await pixels()).hash,book.hash);await shot(style+'-read-turning');await phase('reading');
      while((await state()).sentenceIndex<7){await page.locator('#next-sentence').click();await phase('reading');}
      await page.locator('#stage').focus();await page.keyboard.press('ArrowRight');await phase('finished');q=await state();assert.equal(q.books,booksBefore+1);assert.equal(q.sentenceIndex,7);assert(q.completed.read);assert(q.finishedStories['little-bell']);assert.equal(q.pose,'read-finish');assert(await page.locator('#story-finished').isVisible());assert(await page.locator('#next-sentence').isDisabled());assert.equal(await page.locator('#read-progress-bar').evaluate(n=>n.style.width),'100%');await shot(style+'-read-finished');await page.waitForTimeout(500);assert.equal((await state()).books,booksBefore+1);
      await page.locator('#story-select').selectOption('star-seed');assert.equal((await state()).phase,'ready');await page.locator('#stage').focus();await page.keyboard.press('Space');assert.equal((await state()).phase,'reading');await page.keyboard.press('Space');assert.equal((await state()).phase,'paused');
      passed(style+': selectable finite story, timed next sentence, pause/resume, real page turn and stopped completion');

      await select('photo');await page.locator('.frame-options [data-frame="mint"]').click();await page.locator('#photo-nickname').fill('朋友');await page.locator('#shutter').click();await page.waitForTimeout(600);q=await state();assert.equal(q.phase,'countdown');assert.equal(q.photoReady,false);assert.equal(q.pose,'photo');
      await page.locator('#stage').click({button:'right',position:{x:48,y:210}});q=await state();assert.equal(q.phase,'countdown');assert(q.menu);await page.locator('#stage').click({button:'right',position:{x:48,y:210}});
      await phase('saved');q=await state();assert(q.photoReady);assert(q.completed.photo);assert.equal(q.frame,'mint');assert.deepEqual(await page.locator('#photo-image').evaluate(im=>[im.naturalWidth,im.naturalHeight]),[720,880]);assert((await page.locator('#photo-date').textContent()).includes('朋友'));await shot(style+'-photo');
      const downloadPromise=page.waitForEvent('download');await page.locator('#save-photo').click();const download=await downloadPromise,saved=path.join(output,style+'-纪念照.png');await download.saveAs(saved);shots.push(saved);assert(fs.statSync(saved).size>20000);assert.equal(Object.keys(q.completed).length,5);
      passed(style+': preserved three-second photo preparation and offline PNG with user cloud avatar');
    }
    const savedStories=(await state()).finishedStories;assert.equal(savedStories['little-bell'],2);assert.equal(await page.locator('#story-shelf [data-reread]').count(),3);const shelf=(await state()).collection,giftTotal=(await state()).gifts;await page.reload();await page.waitForFunction(()=>globalThis.interactionFiveDebug?.().ready);assert.deepEqual((await state()).collection,shelf);assert.equal((await state()).gifts,giftTotal);assert.deepEqual((await state()).finishedStories,savedStories);await page.locator('[data-reread="little-bell"]').click();assert.equal((await state()).action,'read');assert.equal((await state()).story,'little-bell');assert.equal((await state()).phase,'ready');await select('gift');assert((await page.locator('#collection-count').textContent()).includes(giftTotal+' 份'));
    const reopened=await context.newPage();observe(reopened);await reopened.goto(pathToFileURL(target).href+'?review=I3');await reopened.waitForFunction(()=>globalThis.interactionFiveDebug?.().ready);assert.deepEqual(await reopened.evaluate(()=>interactionFiveDebug().collection),shelf);await reopened.close();
    assert.deepEqual((await state()).finishedStories,savedStories);passed('Gift and story collections survive reload; a collected story opens for explicit rereading');
    await select('photo');await page.locator('#shutter').click();await page.waitForTimeout(300);await select('read');await page.waitForTimeout(3100);assert.equal((await state()).action,'read');assert.equal((await state()).photoReady,false);
    await select('rps');await page.locator('[data-choice="rock"]').click();await page.locator('[data-style="chibi"]').click();await phase('revealed');assert.equal((await state()).style,'chibi');
    passed('Switch cancels old photo capture; style change preserves the guessing phase');
    await page.setViewportSize({width:390,height:900});await select('highfive');await page.locator('#hand-hit').waitFor({state:'visible'});await page.locator('#menu-button').click();
    for(const button of await page.locator('#radial-menu button').all()){const b=await button.boundingBox();assert(b.x>=0&&b.x+b.width<=390);}
    assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));await shot('mobile-radial');await page.locator('[data-menu-action="gift"]').click();await page.locator('#gift-token').focus();await page.keyboard.press('Enter');await phase('holding');await page.locator('#gift-hit').click();await phase('opened');await shot('mobile-gift');assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));
    await select('read');await page.locator('#story-select').selectOption('star-seed');await page.locator('#story-play').click();await shot('mobile-reading');assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));
    passed('390px viewport: palm and radial fit; collection and long story do not overflow');
    const bad=await browser.newContext();const badPage=await bad.newPage();observe(badPage);
    await badPage.addInitScript(()=>localStorage.setItem('deepseek.interaction-five.demo.collection.v1','{invalid json'));await badPage.goto(pathToFileURL(target).href);await badPage.waitForFunction(()=>globalThis.interactionFiveDebug?.().ready);assert.deepEqual(await badPage.evaluate(()=>interactionFiveDebug().collection),{});assert.equal(await badPage.evaluate(()=>interactionFiveDebug().storageOK),false);await bad.close();
    passed('Malformed Demo collection falls back safely without blocking the interactions');
    assert.deepEqual(errors,[]);assert.deepEqual(failed,[]);assert.deepEqual(requests,[]);assert.deepEqual((await state()).errors,[]);
    assert(fs.readFileSync(path.join(root,'Release/win-x64/Demo/CloudClub/index.html'),'utf8').includes('../InteractionFive/index.html?review=I3'));
    passed('Offline file URL: no browser errors, network requests or asset failures; permanent link updated');
    const protectedAfter=Object.fromEntries(protectedFiles.map(file=>[file,hash(file)]));assert.deepEqual(protectedAfter,protectedBefore);
    passed('Native executable and normal pet save remain byte-for-byte unchanged');
    fs.writeFileSync(path.join(output,'report.json'),JSON.stringify({status:'passed',review:'I3',reports,shots,timelines,protectedBefore,protectedAfter,verifiedAt:new Date().toISOString(),demo:target},null,2)+'\n');
    console.log('PASS '+reports.length+' groups; evidence '+output);
  } finally {await browser.close();}
}
main().catch(error=>{console.error(error);process.exitCode=1;});
