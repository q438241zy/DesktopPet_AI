(() => {
  'use strict';
  const $ = selector => document.querySelector(selector), $$ = selector => [...document.querySelectorAll(selector)];
  const M = globalThis.FiveModel, A = globalThis.FIVE_ART, Items=globalThis.FiveItems;
  const stage = $('#stage'), canvas = $('#actor'), ctx = canvas.getContext('2d');
  const paths = {
    cloud: 'M6.5 18.5h11a4 4 0 0 0 .4-8 6 6 0 0 0-11.5-1.4 4.8 4.8 0 0 0 .1 9.4Z M9 13.7h.01 M15 13.7h.01 M10.3 16q1.7 1.4 3.4 0',
    hand: 'M8 11V5a1.5 1.5 0 0 1 3 0v5 M11 9V3.8a1.5 1.5 0 0 1 3 0V10 M14 8V5.1a1.5 1.5 0 0 1 3 0v6 M17 10V8a1.5 1.5 0 0 1 3 0v7c0 4-2 6-6 6h-1c-2 0-3.6-.8-5-2.7L4.8 14a1.5 1.5 0 0 1 2.3-1.9L9 14',
    scissors: 'M9 12 6.5 5a1.4 1.4 0 0 1 2.6-1l3 7 M12 11l3-7a1.4 1.4 0 0 1 2.6 1L15 12 M8 11.8h5.5c2.5 0 4.5 2 4.5 4.5V18c0 2-2 3-5 3s-5-1-5-3v-3 M8 14H6.5A1.5 1.5 0 0 0 5 15.5V18',
    rock: 'M6 11V9a1.5 1.5 0 0 1 3 0v3 M9 10V7.5a1.5 1.5 0 0 1 3 0V12 M12 9V8a1.5 1.5 0 0 1 3 0v4 M15 10a1.5 1.5 0 0 1 3 0v7c0 3-2 4-6 4s-6-1-6-4v-4H5a1.5 1.5 0 0 0-1 2.6L6 18 M9 14h5',
    gift: 'M4 9h16v4H4z M6 13v8h12v-8 M12 9v12 M12 9H8a2.5 2.5 0 1 1 2.3-3.5L12 9Zm0 0h4a2.5 2.5 0 1 0-2.3-3.5L12 9Z',
    book: 'M12 6v15 M12 6c-3-2-6-2-9-1v14c3-1 6-1 9 2 3-3 6-3 9-2V5c-3-1-6-1-9 1Z M6 9l3 .7 M15 9.7l3-.7 M6 13l3 .7 M15 13.7l3-.7',
    camera: 'M8 6 9.5 3h5L16 6h4a2 2 0 0 1 2 2v11a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h4Z M16 13.5a4 4 0 1 1-8 0 4 4 0 0 1 8 0Z M18.5 9h.01',
    restart: 'M4 10a8 8 0 1 1 .7 7 M4 4v6h6',
    close: 'm7 7 10 10 M17 7 7 17',
    check: 'm5 12 4 4 10-11'
  };
  function icon(key) {
    if (key === 'gift-color') return '<svg viewBox="0 0 48 48" aria-hidden="true"><rect x="9" y="21" width="30" height="23" rx="4" fill="#fff8fc" stroke="#d3a5bd"/><rect x="6" y="16" width="36" height="10" rx="3" fill="#f9dce9" stroke="#d3a5bd"/><path d="M23 16c-14 0-17-11-9-11 7 0 10 11 10 11s4-11 10-11c8 0 5 11-10 11" fill="#f0afce" stroke="#ca8fb0"/><path d="M24 18v25" stroke="#d699b7" stroke-width="6"/></svg>';
    return `<svg viewBox="0 0 24 24" aria-hidden="true"><path d="${paths[key] || paths.cloud}"></path></svg>`;
  }
  $$('[data-icon]').forEach(node => node.innerHTML = icon(node.dataset.icon));
  const shelfKey='deepseek.interaction-five.demo.collection.v1';let storageOK=true;
  function readShelf(){try{return JSON.parse(localStorage.getItem(shelfKey)||'null');}catch{storageOK=false;return null;}}
  const images = {}, itemImages={}, errors = [], state = M.create(readShelf());
  const requestedStyle=new URLSearchParams(location.search).get('style');
  state.style=requestedStyle==='chibi'?'chibi':'realistic';
  const roster=globalThis.FIVE_ROSTER;
  let appearance=null,appearanceTicket=0;
  const petName=()=>appearance?.name||'DeepSeek';
  const pendingSources=new Map();
  globalThis.FIVE_SOURCE=(id,payload)=>pendingSources.get(id)?.(payload);
  function loadSources(entry){
    return new Promise((resolve,reject)=>{
      const script=document.createElement('script');let received=false;
      const timer=setTimeout(()=>fail('素材读取超时，请重新选择'),20000);
      function cleanup(){clearTimeout(timer);pendingSources.delete(entry.id);script.remove();}
      function fail(message){cleanup();reject(new Error(message));}
      pendingSources.set(entry.id,payload=>{received=true;cleanup();resolve(payload);});
      script.src=entry.module;script.onerror=()=>fail('找不到这套外观的素材');
      script.onload=()=>{if(!received)fail('素材文件不完整');};document.head.append(script);
    });
  }
  const reduced = matchMedia('(prefers-reduced-motion: reduce)').matches;
  let comparisonPose='';
  let ready = false, W = 0, H = 0, dpr = 1, last = 0, raf = 0, stamp = '', menu = null, drag = null, handDrag=null, handFrom=null, photoUrl = null, draggingClick = false, handClickConsumed=false, shelfRevision=0, handStamp='', storyStamp='', collectionStamp='';
  const clamp = (n, a, b) => Math.max(a, Math.min(b, n));
  const anchors = {
    chibi: {hand: [-90, -231], receive: [-3, -113], gift: [5, -113], book: [0, -126]},
    realistic: {hand: [-50, -287], receive: [0, -190], gift: [2, -197], book: [0, -201]}
  };
  function random() { const bytes = new Uint32Array(1); crypto.getRandomValues(bytes); return bytes[0] / 4294967296; }
  function dimensions() {
    const height = state.style === 'chibi' ? Math.min(294, H-174, W*.82) : Math.min(390, H-150, W*.95);
    return {x: W*.5, floor: H-54, height, scale: height/A.styles[state.style].referenceHeight};
  }
  function anchor(name,poseName=M.pose(state)) {
    const r=dimensions(),pose=A.styles[state.style].poses[poseName],local=pose.anchors?.[name];
    const p=local||anchors[state.style][name],unit=local?(pose.unit||1):1;
    return {x:r.x+p[0]*r.scale*unit,y:r.floor+p[1]*r.scale*unit};
  }
  function place(node, point) {node.style.left=point.x+'px';node.style.top=point.y+'px';}
  function report(error) {errors.push(String(error));$('#load-error').hidden=false;$('#load-error').textContent='预览素材读取失败：'+String(error.message || error);}
  function selectOptions(){
    if(!roster)return;
    const family=appearance?.family||'whale',outfit=appearance?.outfit||'original';
    const families=[...new Map(roster.appearances.map(a=>[a.family,a.name]))];
    $('#family-select').replaceChildren(...families.map(([id,label])=>new Option(label,id,false,id===family)));
    const outfits=[...new Map(roster.appearances.filter(a=>a.family===family&&a.style===state.style).map(a=>[a.outfit,a.outfitName]))];
    $('#outfit-select').replaceChildren(...outfits.map(([id,label])=>new Option(label,id,false,id===outfit)));
    $('#family-select').disabled=$('#outfit-select').disabled=!ready;
    $$('[data-style]').forEach(b=>b.disabled=!ready||!roster.appearances.some(a=>a.family===family&&a.style===b.dataset.style&&a.outfit===outfit));
  }
  async function loadAppearance(family,style,outfit){
    const entry=roster?.appearances.find(a=>a.family===family&&a.style===style&&a.outfit===outfit);
    if(!entry){report(new Error('这套外观尚未安装'));return false;}
    const ticket=++appearanceTicket;cancelDrag();cancelHandDrag();ready=false;selectOptions();sync(true);draw();
    $('#appearance-status').textContent='换装中…';
    try{
      const sources=await loadSources(entry);
      const loaded=await Promise.all(Object.entries(sources).map(async([key,url])=>{const im=new Image();im.src=url;await im.decode();return [key,im];}));
      if(ticket!==appearanceTicket)return false;
      // Retain only the active appearance's decoded sheets, not the whole wardrobe.
      Object.keys(images).forEach(key=>delete images[key]);Object.assign(images,Object.fromEntries(loaded));
      A.styles[style]=entry.art;appearance=entry;state.style=style;comparisonPose='';handFrom=null;
      const c=$('#identity-reference'),out=c.getContext('2d'),r=entry.reference,k=1510/r.height;
      out.clearRect(0,0,c.width,c.height);out.imageSmoothingQuality='high';out.drawImage(images['native-reference'],...r.rect,512+(r.rect[0]-r.foot[0])*k,1515+(r.rect[1]-r.foot[1])*k,r.rect[2]*k,r.rect[3]*k);
      $('#pet-family-name').textContent=entry.name;$('#pet-outfit-name').textContent=entry.outfitName;
      $('.brand small').textContent=entry.name+' 的新互动';$('#opponent-label').textContent=entry.name;
      $('#actor').setAttribute('aria-label',entry.name+' 互动动画');$('.pet-panel').setAttribute('aria-label',entry.name+' 互动舞台');
      $('#save-photo').download=entry.name+'-云朵纪念照.png';$('#photo-image').alt=entry.name+' 与你的云朵头像一起留下的纪念照';
      $('#load-error').hidden=true;$('#appearance-status').textContent='';ready=true;selectOptions();resetPhoto();sync(true);draw();last=0;return true;
    }catch(error){if(ticket===appearanceTicket){report(error);$('#appearance-status').textContent='读取失败';ready=!!appearance;selectOptions();sync(true);}return false;}
  }
  function drawPose(out, style, pose, x, floor, height) {
    const styleArt=A.styles[style], p=styleArt.poses[pose], k=height/styleArt.referenceHeight*(p.unit||1);
    out.drawImage(images[p.atlas||style],...p.rect,x+(p.rect[0]-p.foot[0])*k,floor+(p.rect[1]-p.foot[1])*k,p.rect[2]*k,p.rect[3]*k);
  }
  const ease=t=>{t=clamp(t,0,1);return t*t*(3-2*t);},lerp=(a,b,t)=>a+(b-a)*t;
  function handRest(){return{x:Math.max(64,W*.18),y:H-110};}
  function userHand(){
    const rest=handRest(),r=dimensions();let p={...rest},pose='paper';
    if(state.action==='highfive'){
      if(handDrag)p=handDrag.position;
      else if(state.phase==='approach'){
        const from=handFrom||rest,to=anchor('hand','high-contact'),t=ease(state.time/480);
        p={x:lerp(from.x,to.x,t),y:lerp(from.y,to.y,t)-Math.sin(t*Math.PI)*18};
      }else if(state.phase==='contact'){
        const touch=anchor('hand','high-contact'),t=ease((state.time-280)/920);
        p={x:lerp(touch.x,rest.x,t),y:lerp(touch.y,rest.y,t)};
      }
    }else if(state.action==='rps'){
      const base=r.floor-r.height*(state.style==='chibi'?.37:.61),amplitude=r.height*(state.style==='chibi'?.09:.11);
      p={x:Math.max(62,W*.18),y:base};pose='rock';
      if(state.phase==='countdown')p.y=base-Math.cos(state.time%600/600*Math.PI*2)*amplitude;
      if(state.phase==='shoot'||state.phase==='revealed'){
        const t=state.phase==='revealed'?1:ease(state.time/250);p.x+=22*t;
        pose=state.phase==='revealed'||state.time>=200?state.player:'rock';
      }
    }
    return{position:p,pose,visible:ready&&['highfive','rps'].includes(state.action),contact:state.action==='highfive'&&state.phase==='contact'&&state.time<280};
  }
  function userHandSvg(pose){
    const shapes={
      paper:'M8 13V5.5C8 3.5 10.5 3.5 10.5 5.5V11 3.5C10.5 1.5 13 1.5 13 3.5V11 4.5C13 2.5 15.5 2.5 15.5 4.5V12 7C15.5 5 18 5 18 7V16C18 20 16 22 12.5 22H11C8 22 6.5 20 5 18L2.5 14C1.5 12 3.5 10.5 5 12L8 15Z',
      rock:'M5 12V9C5 7 7.5 7 7.5 9V11 7.5C7.5 5.5 10 5.5 10 7.5V11 7C10 5 12.5 5 12.5 7V11 8C12.5 6 15 6 15 8V12H17C19 12 20 14 20 16V18C20 21 17.5 22 13 22H10C6 22 4 20 4 17V14C4 12.5 5.5 12 6.5 12H12V15H7Z',
      scissors:'M8 13 5.5 5C5 3 7.5 2 8.2 4L11 12 14.5 4C15.2 2 17.8 3 17 5L14.5 13H16C18 13 19 15 19 17V18C19 21 16.5 22 13 22H10C6 22 4 20 4 17V15C4 13 6 12 8 13Z'
    };
    return `<svg viewBox="0 0 24 24" aria-hidden="true"><g transform="translate(0 -3)"><path d="${shapes[pose]}" fill="#fbe1d1" stroke="#c79b8b" stroke-width=".85"/><path d="M9 17q3-1 6 0" fill="none" stroke="#d6b0a0" stroke-width=".65"/></g></svg>`;
  }
  function drawUserHand(){
    const info=userHand(),node=$('#player-hand');node.hidden=!info.visible;$('#opponent-label').hidden=state.action!=='rps';if(!info.visible)return;
    node.disabled=state.action==='rps'||!ready||state.phase!=='offer'||state.time<750;
    node.classList.toggle('guessing',state.action==='rps');node.classList.toggle('holding',!!handDrag);node.style.left=info.position.x+'px';node.style.top=info.position.y+'px';
    node.style.setProperty('--hand-angle',state.action==='rps'?'-12deg':'14deg');
    if(handStamp!==info.pose){handStamp=info.pose;node.querySelector('.hand-art').innerHTML=userHandSvg(info.pose);}
    node.querySelector('.hand-caption').textContent=state.action==='rps'?(state.phase==='choose'?'你的手':state.phase==='countdown'?'一起摇拳':M.names[info.pose]):state.phase==='offer'?'拖我去击掌':'你的手';
    $('#opponent-label').hidden=state.action!=='rps';place($('#opponent-label'),{x:dimensions().x,y:dimensions().floor-dimensions().height*.35});
  }
  function drawPrize(r){
    if(state.action!=='gift'||!['opening','opened'].includes(state.phase)||M.pose(state)!=='gift-empty')return;
    const id=state.prize||state.pendingPrize,pose=A.styles[state.style].poses['gift-empty'],unit=pose.unit||1,k=r.scale*unit;
    const t=state.phase==='opened'?1:ease((state.time-450)/400);if(!id||!itemImages[id]||t<=0)return;
    const slot=anchor('giftSlot'),width=pose.boxWidth*k,front=r.floor+pose.frontY*k,size=Math.min(40,Math.max(25,width*.72))*(.8+.2*t);
    ctx.save();ctx.globalAlpha=t;ctx.drawImage(itemImages[id],slot.x-size/2,front-size+8+(1-t)*10,size,size);ctx.restore();
    // The unmodified character's front box wall and hands occlude the prize.
    ctx.save();ctx.beginPath();ctx.rect(slot.x-width/2-7,front,width+14,r.floor-front+5);ctx.clip();drawPose(ctx,state.style,'gift-empty',r.x,r.floor,r.height);ctx.restore();
  }
  function draw() {
    ctx.clearRect(0,0,W,H);if(!ready)return;
    const r=dimensions(), pose=M.pose(state);
    drawPose(ctx,state.style,pose,r.x,r.floor,r.height);
    stage.dataset.style=state.style;
    if(state.style==='realistic'&&comparisonPose!==pose){
      comparisonPose=pose;
      const compare=$('#identity-current'),out=compare.getContext('2d');out.clearRect(0,0,compare.width,compare.height);
      out.imageSmoothingEnabled=true;out.imageSmoothingQuality='high';drawPose(out,'realistic',pose,512,1515,1510);
      $('#identity-caption').textContent=M.actions[state.action].title+' · 当前姿势';
    }
    drawPrize(r);drawUserHand();
    if(state.action==='highfive' && state.phase==='contact' && state.time<430) {
      const p=anchor('hand','high-contact'),t=state.time/430;
      ctx.save();ctx.globalAlpha=1-t;ctx.strokeStyle='#dba6bf';ctx.lineWidth=2;
      ctx.beginPath();ctx.arc(p.x,p.y,9+t*34,0,Math.PI*2);ctx.stroke();
      for(let i=0;i<5;i++){const a=i*Math.PI*2/5;ctx.beginPath();ctx.moveTo(p.x+Math.cos(a)*(20+t*15),p.y+Math.sin(a)*(20+t*15));ctx.lineTo(p.x+Math.cos(a)*(25+t*18),p.y+Math.sin(a)*(25+t*18));ctx.stroke();}ctx.restore();
    }
    $('#speech').classList.toggle('reading',state.action==='read');
    place($('#speech'),{x:W*.5,y:state.action==='read'?Math.max(23,r.floor-r.height-87):Math.max(49,r.floor-r.height-55)});
    place($('#hand-hit'),anchor('hand'));place($('#gift-hit'),anchor('gift'));place($('#gift-zone'),anchor('receive'));place($('#book-hit'),anchor('book'));
    if(drag) positionDraggedGift(drag.position);
    if(state.action==='read'){
      const length=M.stories[state.storyIndex].sentences.length,done=state.phase==='finished';
      const part=state.phase==='reading'?clamp(state.time/M.sentenceDuration(state),0,1):state.phase==='turning'?1:0;
      $('#read-progress-bar').style.width=(done?100:(state.sentenceIndex+part)/length*100)+'%';
    }
  }
  const descriptions={highfive:'你的手与她真正碰掌',rps:'双方摇拳，一起出手',gift:'随机惊喜，收进收藏袋',read:'选篇小故事，读完即可',photo:'你的云朵头像与她合照'};
  function renderActions() {
    if(!$('#actions').children.length)$('#actions').innerHTML=Object.entries(M.actions).map(([key,a])=>`<button class="action-card" data-action="${key}" aria-pressed="false"><span class="action-icon">${icon(a.icon)}</span><span><strong>${a.title}</strong><small>${descriptions[key]}</small></span><span class="done" hidden>${icon('check')}</span></button>`).join('');
    $$('#actions [data-action]').forEach(button=>{const key=button.dataset.action;button.classList.toggle('active',state.action===key);button.setAttribute('aria-pressed',String(state.action===key));button.querySelector('.done').hidden=!state.completed[key];});
    $('#progress').textContent=`已体验 ${Object.keys(state.completed).length} / 5`;
  }
  function saveShelf(){
    if(shelfRevision===state.collectionRevision)return;shelfRevision=state.collectionRevision;
    try{localStorage.setItem(shelfKey,JSON.stringify(M.collectionSave(state)));storageOK=true;}catch{storageOK=false;}
  }
  function renderCollection(){
    const key=JSON.stringify(state.collection);if(key!==collectionStamp){collectionStamp=key;
      $('#gift-collection').innerHTML=M.gifts.map(gift=>{const n=state.collection[gift.id]||0;return `<div class="collection-item ${n?'':'missing'}" data-collectible="${gift.id}" aria-label="${gift.name}${n?'，收藏 '+n+' 份':'，尚未收藏'}"><span class="collection-art">${Items.svg(gift.id)}</span><span>${gift.name}</span><small>${n?'× '+n:'待发现'}</small></div>`;}).join('');
    }
    const unique=Object.keys(state.collection).length,total=Object.values(state.collection).reduce((a,b)=>a+b,0);
    $('#collection-count').textContent=`${unique} / ${M.gifts.length} 种 · ${total} 份`;
    $('#collection-save-note').textContent=storageOK?'收藏保存在这个 Demo，重开也保留。':'收藏暂保留在这次打开中。';
  }
  function renderReader(){
    const story=M.stories[state.storyIndex],select=$('#story-select');
    if(!select.children.length)select.innerHTML=M.stories.map(x=>`<option value="${x.id}">${x.title} · ${x.sentences.length}句</option>`).join('');
    select.value=story.id;
    if(storyStamp!==story.id){storyStamp=story.id;$('#story-lines').replaceChildren();for(const sentence of story.sentences){const line=document.createElement('li');line.textContent=sentence;$('#story-lines').append(line);}}
    const started=!['ready'].includes(state.phase),finished=state.phase==='finished';
    [...$('#story-lines').children].forEach((line,i)=>{line.classList.toggle('read',finished||started&&i<state.sentenceIndex);line.classList.toggle('active',started&&!finished&&i===state.sentenceIndex);});
    $('#story-title').textContent=story.title;$('#read-progress').textContent=finished?`读完 ${story.sentences.length} / ${story.sentences.length} 句`:started?`第 ${state.sentenceIndex+1} / ${story.sentences.length} 句`:`${story.sentences.length}句小故事`;
    $('#story-play').textContent=finished?'再读一遍':state.phase==='ready'?'一起读':state.phase==='paused'?'继续':'暂停';
    $('#story-play').disabled=!ready||state.phase==='finishing';
    $('#next-sentence').disabled=!ready||!['reading','paused'].includes(state.phase);
    $('#story-finished').hidden=!finished;
  }
  function renderStoryShelf(){
    const shelf=$('#story-shelf');if(!shelf)return;
    const key=JSON.stringify(state.finishedStories);if(shelf.dataset.stamp===key)return;shelf.dataset.stamp=key;
    shelf.innerHTML=M.stories.map(story=>{const count=state.finishedStories[story.id]||0;return `<button class="story-shelf-book ${count?'owned':'unread'}" data-reread="${story.id}" aria-label="${story.title}，${count?'已读 '+count+' 次，重读':'读完收藏'}"><span>${icon('book')}</span><strong>${story.title}</strong><small>${count?'已读 '+count+' 次 · 重读':'读完收藏'}</small></button>`;}).join('');
    $('#story-shelf-count').textContent=Object.keys(state.finishedStories).length+' / '+M.stories.length;
  }
  function sync(force=false) {
    const next=[state.action,state.phase,M.pose(state),M.speech(state),state.style,state.storyIndex,state.sentenceIndex,state.highCount,state.rounds,state.photos,state.gifts,state.collectionRevision,state.frame,menu?'menu':''].join('|');
    if(!force && next===stamp)return;stamp=next;
    $('#speech').textContent=M.speech(state);$('#action-status').textContent=M.actions[state.action].title;
    $('#action-hint').textContent=M.actions[state.action].hint;
    $('#mini-count').textContent={highfive:state.highCount?`默契 ${state.highCount} 次`:'',rps:state.rounds?`第 ${state.rounds} 局`:'',gift:state.gifts?`心意 ${state.gifts} 份`:'',read:state.phase==='finished'?'故事读完啦':`第 ${state.sentenceIndex+1} / ${M.stories[state.storyIndex].sentences.length} 句`,photo:state.photos?`回忆 ${state.photos} 张`:''}[state.action];
    $('#hand-hit').hidden=!(ready && state.action==='highfive' && state.phase==='offer' && state.time>=750);
    $('#gift-hit').hidden=!(ready && state.action==='gift' && state.phase==='holding');
    $('#gift-zone').hidden=!(state.action==='gift' && state.phase==='receive');
    $('#gift-tray').hidden=!(state.action==='gift' && state.phase==='receive');
    $('#book-hit').hidden=!ready || state.action!=='read' || !['ready','reading','paused'].includes(state.phase);$('#gift-token').disabled=!ready;
    $('#book-hit .contact-label').textContent=state.phase==='ready'?'一起读':'下一句';
    for(const key of ['rps','gift','read','photo'])$('#'+key+'-controls').hidden=state.action!==key;
    $$('[data-choice]').forEach(button=>{button.disabled=!ready || state.phase!=='choose';button.classList.toggle('chosen',state.player===button.dataset.choice);});
    $('#rps-again').hidden=state.phase!=='revealed';
    $('#rps-result').textContent=state.action==='rps'&&state.phase==='revealed'?`你：${M.names[state.player]} · ${petName()}：${M.names[state.pet]} · ${{win:'你赢了',lose:'她赢了',draw:'平局'}[state.outcome]}`:state.phase==='countdown'?'双方一起摇拳，3、2、1…':state.phase==='shoot'?'一起出拳！':'选好手势，我们一起猜。';
    $('#gift-message').textContent=state.phase==='opened'?`拆到了${M.giftById[state.prize].name} · ${state.prizeIsNew?'新收藏':'已有 '+state.collection[state.prize]+' 份'} · 已放进收藏袋`:state.phase==='holding'?'礼物已在她手里，点礼物拆开。':state.phase==='opening'?'正在打开，看看这次会是什么。':'把礼物拖到手心；也可以选中礼物，按回车递给她。';
    $('#gift-again').hidden=state.phase!=='opened';
    renderCollection();renderStoryShelf();if(state.action==='read')renderReader();
    $('#identity-review').hidden=state.style!=='realistic';
    $$('[data-style]').forEach(b=>{b.classList.toggle('selected',b.dataset.style===state.style);b.setAttribute('aria-pressed',String(b.dataset.style===state.style));});
    const cameraBusy=state.action==='photo'&&['countdown','capture'].includes(state.phase);
    $('#shutter').disabled=!ready || cameraBusy;$('#shutter').innerHTML=icon('camera')+(cameraBusy?'准备中…':state.phase==='saved'?'再合照一张':'一起合照');
    $$('.frame-options [data-frame]').forEach(button=>{button.disabled=cameraBusy;button.classList.toggle('selected',state.frame===button.dataset.frame);button.setAttribute('aria-pressed',String(state.frame===button.dataset.frame));});
    $('#photo-nickname').disabled=cameraBusy;
    stage.dataset.frame=state.action==='photo'?state.frame:'cloud';
    renderActions();
  }
  function cancelDrag() {
    if(!drag)return;
    const token=$('#gift-token'),pointer=drag.id;drag=null;
    if(token.hasPointerCapture(pointer))token.releasePointerCapture(pointer);
    token.classList.remove('dragging');token.style.transform='';$('#gift-zone').classList.remove('near');
  }
  function cancelHandDrag(){
    if(!handDrag)return;const id=handDrag.id;handDrag=null;const node=$('#player-hand');if(node.hasPointerCapture(id))node.releasePointerCapture(id);node.classList.remove('holding');
  }
  function startHighfive(from){
    const origin=from||userHand().position;if(!ready||!M.highfive(state))return false;handFrom={...origin};cancelHandDrag();sync(true);draw();return true;
  }
  function resetPhoto() {photoUrl=null;$('#photo-result').hidden=true;$('#photo-image').removeAttribute('src');$('#save-photo').removeAttribute('href');$('#flash').hidden=true;}
  function setAction(key) {
    if(!M.actions[key])return;
    cancelDrag();cancelHandDrag();handFrom=null;M.start(state,key,random());menu=null;$('#radial-menu').hidden=true;resetPhoto();stamp='';sync(true);draw();
  }
  function point(event) {const r=stage.getBoundingClientRect();return {x:event.clientX-r.left,y:event.clientY-r.top};}
  function positionDraggedGift(p) {
    const token=$('#gift-token'),tray=$('#gift-tray');
    const sr=stage.getBoundingClientRect(),tr=tray.getBoundingClientRect();
    token.style.transform=`translate(${p.x-(tr.left-sr.left+token.offsetWidth/2)}px,${p.y-(tr.top-sr.top+token.offsetHeight/2)}px)`;
    const q=anchor('receive');$('#gift-zone').classList.toggle('near',Math.abs(p.x-q.x)<54 && Math.abs(p.y-q.y)<42);
  }
  function isDeliveryPoint(p) {const q=anchor('receive');return Math.abs(p.x-q.x)<54 && Math.abs(p.y-q.y)<42;}
  $('#gift-token').addEventListener('pointerdown',event=>{
    if(event.button!==0 || !ready || state.action!=='gift' || state.phase!=='receive')return;
    event.preventDefault();drag={id:event.pointerId,position:point(event)};draggingClick=false;
    $('#gift-token').classList.add('dragging');$('#gift-token').setPointerCapture(event.pointerId);positionDraggedGift(drag.position);
  });
  $('#gift-token').addEventListener('pointermove',event=>{if(drag && event.pointerId===drag.id){drag.position=point(event);positionDraggedGift(drag.position);}});
  $('#gift-token').addEventListener('pointerup',event=>{
    if(!drag || event.pointerId!==drag.id)return;
    const deliver=isDeliveryPoint(point(event));draggingClick=true;cancelDrag();if(deliver)M.deliver(state);sync(true);draw();
  });
  $('#gift-token').addEventListener('pointercancel',cancelDrag);
  $('#gift-token').addEventListener('lostpointercapture',cancelDrag);
  $('#gift-token').addEventListener('click',event=>{if(draggingClick){draggingClick=false;return;}if(event.detail===0 && ready){M.deliver(state);sync(true);draw();}});
  $('#hand-hit').addEventListener('click',()=>startHighfive());
  $('#player-hand').addEventListener('pointerdown',event=>{
    if(event.button!==0||!ready||state.action!=='highfive'||state.phase!=='offer'||state.time<750)return;
    event.preventDefault();const p=point(event),start=userHand().position;
    handDrag={id:event.pointerId,start:{...start},position:{...start},offset:{x:p.x-start.x,y:p.y-start.y},travel:0};
    handClickConsumed=false;$('#player-hand').setPointerCapture(event.pointerId);draw();
  });
  $('#player-hand').addEventListener('pointermove',event=>{
    if(!handDrag||handDrag.id!==event.pointerId)return;const p=point(event);
    handDrag.position={x:clamp(p.x-handDrag.offset.x,28,W-28),y:clamp(p.y-handDrag.offset.y,70,H-55)};
    handDrag.travel=Math.max(handDrag.travel,Math.hypot(handDrag.position.x-handDrag.start.x,handDrag.position.y-handDrag.start.y));
    const goal=anchor('hand');if(Math.hypot(handDrag.position.x-goal.x,handDrag.position.y-goal.y)<24){const from={...handDrag.position};handClickConsumed=true;startHighfive(from);}else draw();
  });
  $('#player-hand').addEventListener('pointerup',event=>{
    if(!handDrag||handDrag.id!==event.pointerId)return;const click=handDrag.travel<8;handClickConsumed=true;cancelHandDrag();if(click)startHighfive();else draw();
  });
  $('#player-hand').addEventListener('pointercancel',()=>{handClickConsumed=true;cancelHandDrag();});
  $('#player-hand').addEventListener('lostpointercapture',cancelHandDrag);
  $('#player-hand').addEventListener('click',event=>{if(event.detail===0){handClickConsumed=false;startHighfive();return;}if(handClickConsumed)handClickConsumed=false;});
  $('#gift-hit').addEventListener('click',()=>{if(M.openGift(state,random())){sync(true);draw();}});
  function readingPrimary(){if(!ready||state.action!=='read')return;if(['ready','finished'].includes(state.phase))M.beginReading(state);else M.pauseReading(state);sync(true);draw();}
  $('#book-hit').addEventListener('click',()=>{if(state.phase==='ready')M.beginReading(state);else M.nextSentence(state);sync(true);draw();});
  $('#story-play').addEventListener('click',readingPrimary);
  $('#next-sentence').addEventListener('click',()=>{M.nextSentence(state);sync(true);draw();});
  $('#story-select').addEventListener('change',event=>{M.selectStory(state,event.target.value);sync(true);draw();});
  $$('[data-choice]').forEach(button=>button.addEventListener('click',()=>{M.choose(state,button.dataset.choice);sync(true);}));
  $('#actions').addEventListener('click',event=>{const button=event.target.closest('[data-action]');if(button)setAction(button.dataset.action);});
  $('#story-shelf').addEventListener('click',event=>{const button=event.target.closest('[data-reread]');if(!button)return;setAction('read');M.selectStory(state,button.dataset.reread);sync(true);draw();});
  for(const id of ['restart','rps-again','gift-again'])$('#'+id).addEventListener('click',()=>setAction(state.action));
  $$('[data-style]').forEach(button=>button.addEventListener('click',()=>{
    if(roster){loadAppearance(appearance?.family||'whale',button.dataset.style,appearance?.outfit||'original');return;}
    cancelDrag();cancelHandDrag();state.style=button.dataset.style;
    $$('[data-style]').forEach(b=>{b.classList.toggle('selected',b===button);b.setAttribute('aria-pressed',String(b===button));});sync(true);draw();
  }));
  $('#family-select').addEventListener('change',event=>loadAppearance(event.target.value,state.style,appearance?.outfit||'original'));
  $('#outfit-select').addEventListener('change',event=>loadAppearance(appearance?.family||'whale',state.style,event.target.value));
  $$('.frame-options [data-frame]').forEach(button=>button.addEventListener('click',()=>{state.frame=button.dataset.frame;sync(true);}));
  $('#shutter').addEventListener('click',()=>{M.shutter(state);sync(true);});
  function toggleMenu(p) {
    const node=$('#radial-menu');if(menu){menu=null;node.hidden=true;sync(true);return;}
    const radius=Math.min(100,(W-80)/2),cx=clamp(p.x,radius+35,W-radius-35),cy=clamp(p.y,radius+40,H-radius-60);
    menu={x:cx,y:cy};node.style.left=cx+'px';node.style.top=cy+'px';node.hidden=false;
    node.innerHTML=Object.entries(M.actions).map(([key,a],i)=>{const angle=-Math.PI/2+i*Math.PI*2/5;return `<button class="radial-item" data-menu-action="${key}" style="left:${Math.cos(angle)*radius}px;top:${Math.sin(angle)*radius}px" aria-label="${a.title}">${icon(a.icon)}<span>${a.title}</span></button>`;}).join('')+`<button class="radial-item radial-center" id="close-menu" aria-label="收起互动圆盘">${icon('close')}</button>`;sync(true);
  }
  stage.addEventListener('contextmenu',event=>{event.preventDefault();toggleMenu(point(event));});
  $('#menu-button').addEventListener('click',()=>toggleMenu({x:W*.73,y:H*.48}));
  $('#radial-menu').addEventListener('click',event=>{const item=event.target.closest('[data-menu-action]');if(item)setAction(item.dataset.menuAction);else if(event.target.closest('#close-menu')){menu=null;$('#radial-menu').hidden=true;}});
  stage.addEventListener('keydown',event=>{
    if(event.target.closest('button,input'))return;
    if(event.key==='Escape'){menu=null;$('#radial-menu').hidden=true;return;}
    if(event.key==='ContextMenu' || (event.shiftKey && event.key==='F10')){event.preventDefault();toggleMenu({x:W*.7,y:H*.48});return;}
    if(state.action==='read' && event.key==='ArrowRight'){event.preventDefault();M.nextSentence(state);sync(true);}
    if(state.action==='read' && event.key===' '){event.preventDefault();readingPrimary();}
  });
  window.addEventListener('blur',()=>{cancelDrag();cancelHandDrag();});
  function roundRect(out,x,y,w,h,r,color){out.fillStyle=color;out.beginPath();out.roundRect(x,y,w,h,r);out.fill();}
  function cloudPortrait(out,x,y,nickname) {
    out.save();out.translate(x,y);roundRect(out,-66,-75,132,132,40,'#ffffff90');
    out.fillStyle='#fffdfa';out.strokeStyle='#d6bed3';out.lineWidth=2.5;
    out.beginPath();out.moveTo(-39,20);out.bezierCurveTo(-65,20,-64,-17,-38,-21);out.bezierCurveTo(-29,-57,12,-55,24,-25);out.bezierCurveTo(57,-31,70,16,44,21);out.closePath();out.fill();out.stroke();
    out.fillStyle='#ac93a6';for(const sx of [-13,20]){out.beginPath();out.ellipse(sx,-5,3.2,4.1,0,0,Math.PI*2);out.fill();}
    out.strokeStyle='#b996ad';out.beginPath();out.arc(3,2,7,0,Math.PI);out.stroke();
    out.fillStyle='#9e8198';out.textAlign='center';out.font='24px "Microsoft YaHei UI", sans-serif';out.fillText(nickname,0,93,180);out.restore();
  }
  function capturePhoto() {
    if(state.phase!=='capture' || !ready)return;
    const shot=document.createElement('canvas');shot.width=720;shot.height=880;const out=shot.getContext('2d');
    const colors={cloud:['#f5eef8','#e6edf6'],peach:['#fff1ef','#f4dfe6'],mint:['#edf8f1','#dcece6']}[state.frame];
    out.fillStyle='#fffdfd';out.fillRect(0,0,720,880);const grad=out.createLinearGradient(25,25,660,750);grad.addColorStop(0,colors[0]);grad.addColorStop(1,colors[1]);roundRect(out,24,24,672,728,22,grad);
    out.fillStyle='#b29ab1';out.textAlign='center';out.font='12px "Segoe UI", sans-serif';out.fillText('A LITTLE MOMENT TOGETHER',360,64);
    out.fillStyle='#c5b9d025';out.beginPath();out.ellipse(463,700,110,10,0,0,Math.PI*2);out.fill();
    drawPose(out,state.style,'photo',466,707,state.style==='chibi'?425:553);
    const nickname=$('#photo-nickname').value.trim() || '你';cloudPortrait(out,160,555,nickname);
    out.font='20px "Segoe UI", sans-serif';out.fillStyle='#a58ca6';out.textAlign='center';out.fillText(petName(),467,743);
    out.fillStyle='#846d84';out.font='21px "Microsoft YaHei UI", sans-serif';out.fillText('今天，也有你陪着。',360,800);
    const now=new Date(),date=now.toLocaleDateString('zh-CN',{year:'numeric',month:'2-digit',day:'2-digit'});
    out.font='12px "Segoe UI", sans-serif';out.fillStyle='#b5a5b9';out.fillText(date,360,835);
    try {
      photoUrl=shot.toDataURL('image/png');$('#photo-image').src=photoUrl;$('#save-photo').href=photoUrl;$('#photo-result').hidden=false;$('#photo-date').textContent=`${nickname} ＋ ${petName()} · ${date}`;
      $('#flash').hidden=reduced;M.photoSaved(state);sync(true);
    } catch(error){report(error);M.start(state,'photo',random());sync(true);}
  }
  function resize() {
    cancelDrag();cancelHandDrag();W=stage.clientWidth;H=stage.clientHeight;dpr=Math.min(devicePixelRatio||1,2);
    canvas.width=Math.round(W*dpr);canvas.height=Math.round(H*dpr);ctx.setTransform(dpr,0,0,dpr,0,0);ctx.imageSmoothingEnabled=true;ctx.imageSmoothingQuality='high';
    if(menu){menu=null;$('#radial-menu').hidden=true;}draw();
  }
  function loop(time) {
    const dt=last?time-last:0;last=time;
    if(ready && !document.hidden)M.advance(state,dt);saveShelf();
    sync();draw();if(state.action==='photo'&&state.phase==='capture')capturePhoto();
    if(state.phase!=='saved' && !$('#flash').hidden)$('#flash').hidden=true;
    raf=requestAnimationFrame(loop);
  }
  document.addEventListener('visibilitychange',()=>{last=0;cancelDrag();cancelHandDrag();});
  window.addEventListener('pagehide',()=>{saveShelf();cancelDrag();cancelHandDrag();cancelAnimationFrame(raf);});
  window.addEventListener('pageshow',event=>{if(event.persisted){last=0;raf=requestAnimationFrame(loop);}});
  new ResizeObserver(resize).observe(stage);
  M.start(state,'highfive',random());resize();sync(true);
  Promise.all([
    ...Object.entries(roster?{}:A.images).map(async([key,source])=>{const image=new Image();image.src=source;await image.decode();images[key]=image;}),
    ...M.gifts.map(async gift=>{const image=new Image();image.src='data:image/svg+xml;charset=utf-8,'+encodeURIComponent(Items.svg(gift.id));await image.decode();itemImages[gift.id]=image;})
  ]).then(async()=>{if(roster){const params=new URLSearchParams(location.search);await loadAppearance(params.get('family')||'whale',state.style,params.get('outfit')||'original');}else{ready=true;state.time=0;sync(true);draw();}}).catch(report);
  raf=requestAnimationFrame(loop);
  globalThis.interactionFiveDebug=()=>({ready,appearance:appearance?.id,family:appearance?.family,outfit:appearance?.outfit,decodedSheets:Object.keys(images).length,action:state.action,style:state.style,phase:state.phase,pose:M.pose(state),time:state.time,revision:state.revision,
    highCount:state.highCount,rounds:state.rounds,player:state.player,pet:state.pet,outcome:state.outcome,gifts:state.gifts,photos:state.photos,
    story:M.stories[state.storyIndex].id,sentenceIndex:state.sentenceIndex,sentenceCount:M.stories[state.storyIndex].sentences.length,books:state.books,finishedStories:{...state.finishedStories},
    prize:state.prize,collection:{...state.collection},collectionRevision:state.collectionRevision,storageOK,playerHand:userHand(),handDragging:!!handDrag,
    completed:{...state.completed},frame:state.frame,actor:dimensions(),anchors:Object.fromEntries(Object.keys(anchors[state.style]).map(k=>[k,anchor(k)])),
    dragging:!!drag,menu:!!menu,photoReady:!!photoUrl,errors:[...errors]});
})();
