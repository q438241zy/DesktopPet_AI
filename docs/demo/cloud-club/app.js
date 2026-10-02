(() => {
  'use strict';
  const $ = s => document.querySelector(s), $$ = s => [...document.querySelectorAll(s)];
  const D = globalThis.CLUB_ASSETS, M = globalThis.ClubModel, I = globalThis.ClubIcons;
  const stage = $('#stage'), canvas = $('#pet-canvas'), ctx = canvas.getContext('2d');
  const icon = key => `<svg viewBox="0 0 24 24" aria-hidden="true"><path d="${I[key] || I.cloud}"></path></svg>`;
  $$('[data-icon]').forEach(el => el.innerHTML = icon(el.dataset.icon));
  if (!D || !M) { $('#load-error').hidden = false; $('#load-error').textContent = '素材索引未载入。请从桌面的 DeepSeek-動作Demo 入口打开已安装的 Demo。'; return; }

  let W = stage.clientWidth, H = stage.clientHeight, scale = 1.25, outfit = 'original', group = 'care';
  const s = M.create(W), menuEl = $('#radial-menu');s.tier=3;
  const tiers = [
    {name:'黑金',css:'black',material:'曜石 · 香槟金',level:4},
    {name:'白金',css:'platinum',material:'浅玉 · 拉丝金属',level:3},
    {name:'黄金',css:'gold',material:'香槟 · 柔光金',level:2},
    {name:'白银',css:'silver',material:'月光 · 冷银',level:1},
    {name:'黄铜',css:'brass',material:'暖砂 · 黄铜',level:0}
  ];
  let actionStamp = '', lastTime = 0, lastFrame = null, lastLook = '', poseDebug=null, poseRenderer=null;
  let fx = {bubbles:[],stars:[],pops:[],nextBubble:0,catchGlow:0,landAt:0};
  let talk = {phase:'welcome',started:0,message:'想聊点什么？我在听。',turn:0,until:0};
  const errors = [], cache = new Map();
  const poseMotion=globalThis.ClubPoseMotion;
  try {poseRenderer=new ClubPoseRenderer($('#pose-canvas'),globalThis.CLUB_POSES);poseRenderer.load().catch(reportPoseError);}catch(error){reportPoseError(error);}
  function reportPoseError(error){errors.push(String(error));$('#load-error').hidden=false;$('#load-error').textContent='专用动作加载失败：'+error.message;}
  const clamp = (v,a,b) => Math.max(a,Math.min(b,v));
  const ease = t => { t=clamp(t,0,1); return t*t*(3-2*t); };
  const lerp = (a,b,t) => a+(b-a)*t;
  const look = () => `${s.style}-${outfit}`;
  const feet = () => H-35;
  const edge = () => Math.min(W/2-10,120*scale);
  // Coordinates belong to the whole 560x680 render canvas. Never crop/normalize
  // individual poses: hands, hair and clothing must not change the body's scale.
  const anchorTable = {
    'chibi-original':{face:[274,378],mouth:[274,395],hands:[274,434],hair:[276,322]},
    'chibi-swim':{face:[271,379],mouth:[271,393],hands:[272,440],hair:[274,322]},
    'chibi-wedding':{face:[272,369],mouth:[272,385],hands:[274,428],hair:[274,321]},
    'realistic-original':{face:[278,267],mouth:[277,279],hands:[273,360],hair:[281,244]},
    'realistic-swim':{face:[278,267],mouth:[278,279],hands:[269,364],hair:[282,244]},
    'realistic-wedding':{face:[275,266],mouth:[275,278],hands:[269,356],hair:[278,243]}
  };
  function anchor(name) { const [x,y] = anchorTable[look()][name]; return {x:s.x+(x-280)*scale,y:feet()+(y-468)*scale}; }
  const speed = () => (D.clips[look()].walk.walkSpeed || 44)/.8*scale;
  const walking = () => s.action==='walk' || (s.action==='peek' && s.hideStage!=='hidden') || (s.action==='butterfly' && s.target!=null && Math.abs(s.target-s.x)>1);
  function getFrame(id) {
    if (cache.has(id)) { const entry=cache.get(id);cache.delete(id);cache.set(id,entry);return entry.ready?entry.image:null; }
    const image = new Image(), entry={image,ready:false}; cache.set(id,entry);
    image.onload=()=>entry.ready=true;
    image.onerror=()=>{if(!errors.includes(id)){errors.push(id);$('#load-error').hidden=false;$('#load-error').textContent='有动作图片未能读取，请重新安装 Demo 素材索引。';}};
    image.src=D.frames[id];
    if(cache.size>112){for(const [key,value] of cache){if(value.ready&&key!==id){cache.delete(key);break;}}}
    return null;
  }
  function frameSelection() {
    let clip = M.actions[s.action].clip, time = s.elapsed;
    if(walking()){clip='walk';time=s.phase%(D.clips[look()].walk.cycleMs || 960);}
    else if(['comb','wipe','stretch','bubbles','stars','butterfly'].includes(s.action)||s.action==='peek'){clip='listen';time=0;}
    else if(s.action==='chat') {clip=talk.phase==='thinking'?'thinking':talk.phase==='reply'?'chat':'listen';time=s.age-talk.started;}
    const animation=D.clips[look()][clip] || D.clips[look()].listen;
    const index=Math.min(animation.frames.length-1,Math.floor(Math.max(0,time)/D.sampleMs));
    return {id:animation.frames[index],index,animation,clip};
  }
  function drawActor() {
    const motion=poseMotion.sample(s.action,s.elapsed);poseDebug=null;
    if(motion&&poseRenderer?.ready){
      const height=(s.style==='chibi'?185:237)*scale;
      poseRenderer.draw(`${look()}:${motion.a}`,`${look()}:${motion.b}`,motion.t,s.x,feet(),height);
      poseDebug={...motion,look:look(),height};return;
    }
    const selection=frameSelection(), currentLook=look();
    if(lastLook!==currentLook){lastLook=currentLook;lastFrame=null;}
    const im=getFrame(selection.id);if(im)lastFrame={image:im,id:selection.id,look:currentLook};
    for(let i=1;i<=6;i++)getFrame(selection.animation.frames[(selection.index+i)%selection.animation.frames.length]);
    if(!lastFrame)return;
    ctx.save();ctx.translate(s.x,feet());
    if(walking())ctx.scale(s.dir,1);
    ctx.drawImage(lastFrame.image,-280*scale,-468*scale,560*scale,680*scale);ctx.restore();
  }
  function roundedRect(x,y,w,h,r,fill,stroke) {
    ctx.beginPath();ctx.roundRect(x,y,w,h,r);if(fill){ctx.fillStyle=fill;ctx.fill();}if(stroke){ctx.strokeStyle=stroke;ctx.stroke();}
  }
  function line(points,color,width=1.5) {ctx.beginPath();ctx.strokeStyle=color;ctx.lineWidth=width;points.forEach((p,i)=>i?ctx.lineTo(...p):ctx.moveTo(...p));ctx.stroke();}
  function dot(x,y,r,color) {ctx.beginPath();ctx.arc(x,y,r,0,Math.PI*2);ctx.fillStyle=color;ctx.fill();}
  function star(x,y,r,color='#edc76f',turn=0) {
    ctx.save();ctx.translate(x,y);ctx.rotate(turn);ctx.beginPath();for(let i=0;i<10;i++){let a=i*Math.PI/5-Math.PI/2,rr=i%2?r*.46:r;let p=[Math.cos(a)*rr,Math.sin(a)*rr];i?ctx.lineTo(...p):ctx.moveTo(...p);}ctx.closePath();ctx.fillStyle=color;ctx.fill();ctx.strokeStyle='#fff7dc';ctx.lineWidth=1;ctx.stroke();ctx.restore();
  }
  function twinkle(x,y,r=4,color='#cbb082'){line([[x-r,y],[x+r,y]],color,1.1);line([[x,y-r],[x,y+r]],color,1.1);}
  function textAt(message,x,y,color='#ac91a4'){ctx.fillStyle=color;ctx.font='11px "Segoe UI","Microsoft YaHei",sans-serif';ctx.textAlign='center';ctx.fillText(message,x,y);}
  function combEffect() {
    const h=anchor('hair'),t=s.elapsed,cycle=t%1650;
    const stroke=ease(clamp((cycle-200)/1000,0,1)),side=Math.floor(t/1650)%2?-1:1;
    const fade=Math.min(ease(t/500),1-ease((t-6100)/700));
    const x=h.x+side*(20+8*stroke)*scale,y=h.y+(3+36*stroke)*scale;
    ctx.save();ctx.globalAlpha=fade;ctx.translate(x,y);ctx.rotate(side*-.2);ctx.lineWidth=1.2;
    roundedRect(-12,-5,26,9,4,'#e7bfad','#c69d8a');roundedRect(11,-4,6,27,3,'#efd1bf','#c69d8a');
    for(let i=-9;i<=9;i+=4)roundedRect(i,2,2.5,9,1,'#d7a994');ctx.restore();
    if(cycle>1100){ctx.save();ctx.globalAlpha=fade*(1-(cycle-1100)/550);twinkle(x+side*16,y-8,3,'#d9b2c6');ctx.restore();}
  }
  function wipeEffect() {
    const f=anchor('face'),t=s.elapsed,side=t<2900?-1:1;
    const travel=Math.sin(t/260),fade=Math.min(ease(t/450),1-ease((t-5200)/600));
    const x=f.x+side*(s.style==='chibi'?21:9)*scale+travel*2,y=f.y+8*scale+Math.cos(t/310)*3;
    ctx.save();ctx.globalAlpha=fade;ctx.translate(x,y);ctx.rotate(Math.sin(t/420)*.16);ctx.lineWidth=1;
    roundedRect(-10,-9,20,19,5,'#fcf5ec','#dccbbb');line([[-6,-4],[6,-4]],'#e9d8d8');line([[-6,4],[6,4]],'#e9d8d8');ctx.restore();
    if(t>1400) {ctx.save();ctx.globalAlpha=.6*fade;twinkle(f.x-side*24*scale,f.y+8,3,'#d6b9c8');ctx.restore();}
  }
  function bubblePosition(b) { const age=(s.elapsed-b.born)/1000;return {x:b.x+Math.sin(age*1.5+b.seed)*7+age*b.drift,y:b.y-age*(21+b.seed*3),r:b.r}; }
  function bubbleSource(){const height=(s.style==='chibi'?185:237)*scale;return {x:s.x-height*(s.style==='chibi'?.17:.055),y:feet()-height*(s.style==='chibi'?.43:.80)};}
  function drawBubbles() {
    for(const b of fx.bubbles){const p=bubblePosition(b);ctx.save();ctx.globalAlpha=Math.min(1,(s.elapsed-b.born)/300);const g=ctx.createLinearGradient(p.x-p.r,p.y-p.r,p.x+p.r,p.y+p.r);g.addColorStop(0,'#afcde788');g.addColorStop(.5,'#e2c1da77');g.addColorStop(1,'#b4d9cd99');ctx.fillStyle='#ffffff18';ctx.strokeStyle=g;ctx.lineWidth=1.7;ctx.beginPath();ctx.arc(p.x,p.y,p.r,0,Math.PI*2);ctx.fill();ctx.stroke();ctx.beginPath();ctx.strokeStyle='#fff';ctx.arc(p.x-1,p.y-1,p.r*.65,Math.PI*1.1,Math.PI*1.5);ctx.stroke();ctx.restore();}
  }
  function restingStar(p){const f=anchor('face');return {x:clamp(s.x+p.dx,25,W-25),y:Math.max(48,f.y-p.dy-(s.style==='chibi'?45:0))+Math.sin(s.elapsed/1200+p.seed)*2,r:15};}
  function drawStars() {
    const count=poseMotion.sample('stars',s.elapsed).count;
    for(let i=0;i<fx.stars.length;i++){
      const p=restingStar(fx.stars[i]),active=i===count-1;
      if(active){ctx.save();ctx.globalAlpha=.16;dot(p.x,p.y,27,'#e5b357');ctx.restore();twinkle(p.x+22,p.y-8,3);}
      star(p.x,p.y,active?17:13,i<count?'#e5bf68':'#dbd1ba',Math.sin(s.elapsed/1400+i)*.08);
      if(i<count)textAt(String(i+1),p.x,p.y+33,'#b2955d');
    }
    const f=anchor('face');if(count){const label=count===5?'五颗，数完啦！':['一颗','两颗','三颗','四颗'][count-1],x=s.x+(s.x<=W/2?1:-1)*(s.style==='chibi'?145:110),y=f.y+16;ctx.lineWidth=1;roundedRect(x-47,y-18,94,29,14,'#ffffffdd','#e9dfeb');textAt(label,x,y,'#9b83a5');}
  }
  function butterflyPosition(){
    if(walking())return {x:s.target+Math.sin(s.elapsed/350)*10,y:anchor('face').y-22+Math.sin(s.elapsed/280)*11,landed:false};
    const h=anchor('hands'),t=ease((s.elapsed-fx.landAt)/1300);
    return {x:lerp(s.x+28,h.x,t),y:lerp(anchor('face').y-22,h.y-5,t)+Math.sin(s.elapsed/300)*4*(1-t),landed:t>=1};
  }
  function drawButterfly(){
    const p=butterflyPosition(),flap=.38+Math.abs(Math.sin(s.elapsed/(p.landed?330:90)))*.62;
    ctx.save();ctx.translate(p.x,p.y);ctx.scale(flap,1);ctx.lineWidth=1;
    for(const side of [-1,1]){ctx.beginPath();ctx.ellipse(side*8,-4,9,11,side*.4,0,Math.PI*2);ctx.fillStyle=side<0?'#c4b2e6':'#d9b9e4';ctx.fill();ctx.strokeStyle='#b99acb';ctx.stroke();ctx.beginPath();ctx.ellipse(side*6,7,6,7,-side*.3,0,Math.PI*2);ctx.fillStyle='#e7c9e0';ctx.fill();}ctx.restore();
    line([[p.x,p.y-9],[p.x,p.y+10]],'#9b7da7',1.5);line([[p.x,p.y-8],[p.x-3,p.y-13]],'#9b7da7',.8);line([[p.x,p.y-8],[p.x+3,p.y-13]],'#9b7da7',.8);
    if(!p.landed){ctx.save();ctx.globalAlpha=.24;for(let i=1;i<=3;i++)dot(p.x-s.dir*i*12,p.y+Math.sin(s.elapsed/400+i)*7,2,'#be9fcf');ctx.restore();}
  }
  function updateFx(){
    if(s.action==='bubbles'){
      const motion=poseMotion.sample('bubbles',s.elapsed);
      if(motion.blowing&&poseRenderer?.ready&&s.elapsed>=fx.nextBubble){const m=bubbleSource(),seed=fx.bubbles.length%7;fx.bubbles.push({born:s.elapsed,x:m.x,y:m.y,r:9+seed*1.5,seed,drift:-27-seed*3});fx.nextBubble=s.elapsed+270;}
      fx.bubbles=fx.bubbles.filter(b=>s.elapsed-b.born<9000&&bubblePosition(b).y>-35);
    }
    if(s.action==='stars'){
      s.score=poseMotion.sample('stars',s.elapsed).count;
    }
    if(s.action==='butterfly'){if(walking())fx.landAt=s.elapsed;}
    fx.pops=fx.pops.filter(p=>s.elapsed-p.born<500);
  }
  function seedStars(){fx.stars=Array.from({length:5},(_,i)=>({dx:[-118,-68,-8,68,118][i],dy:[57,95,111,95,57][i],seed:i}));}
  function resetFx(){fx={bubbles:[],stars:[],pops:[],nextBubble:0,catchGlow:0,landAt:0};if(s.action==='stars')seedStars();if(s.action==='butterfly')s.target=clamp(s.x+W*.22,edge(),W-edge());}
  function drawFx(){
    if(s.action==='comb')combEffect();if(s.action==='wipe')wipeEffect();if(s.action==='bubbles')drawBubbles();if(s.action==='stars')drawStars();if(s.action==='butterfly')drawButterfly();
    for(const p of fx.pops){let t=(s.elapsed-p.born)/500;ctx.save();ctx.globalAlpha=1-t;for(let i=0;i<6;i++){let a=i*Math.PI/3;dot(p.x+Math.cos(a)*t*28,p.y+Math.sin(a)*t*28,2,'#bda3c8');}ctx.restore();}
  }
  function renderTiers(){
    $('#tier-cards').innerHTML=tiers.map((t,i)=>`<button class="tier-card ${t.css}" data-tier="${t.level}" aria-pressed="${s.tier===t.level}" aria-label="预览${t.name}，测试全部开放"><span class="card-number">CLOUD / 0${5-i}</span><span class="card-crest">${icon('cloud')}</span><strong>${t.name}</strong><span class="material-name">${t.material}</span><span class="card-open">测试开放</span></button>`).join('');
    $('#tier-note').textContent=`当前预览：${tiers.find(t=>t.level===s.tier).name} · 聊天与互动全部开放`;
  }
  function renderActions(){
    $('#action-list').setAttribute('aria-labelledby',`tab-${group}`);
    $('#action-list').innerHTML=M.menu(group,s.style).map(key=>`<button class="action-card ${M.actions[key].fresh?'fresh':''} ${s.action===key?'active':''}" data-action="${key}" aria-pressed="${s.action===key}">${icon(key)}<span>${M.actions[key].title}</span></button>`).join('');
  }
  function renderMenu(){
    menuEl.hidden=!s.menu;if(!s.menu)return;
    const radius=Math.min(128,(W-78)/2),p=s.menuPoint||{x:W/2,y:H/2};
    const margin=radius+35,cx=clamp(p.x,margin,W-margin),cy=clamp(p.y,margin,H-35-margin);
    menuEl.style.left=cx+'px';menuEl.style.top=cy+'px';
    const keys=M.menu(s.menu,s.style);
    menuEl.innerHTML=keys.map((key,i)=>{const a=-Math.PI/2+i*2*Math.PI/keys.length;return `<button class="radial-item" data-menu-key="${key}" data-fresh="${!!M.actions[key]?.fresh}" style="left:${Math.cos(a)*radius}px;top:${Math.sin(a)*radius}px" aria-label="${M.actions[key]?.title||M.labels[key]}">${icon(key)}<span>${M.actions[key]?.title||M.labels[key]}</span></button>`;}).join('')+`<button class="radial-item radial-center" data-menu-key="${s.menu==='root'?'close':'back'}" aria-label="${s.menu==='root'?'收起菜单':'返回一级菜单'}">${icon(s.menu==='root'?'cloud':'back')}</button>`;
  }
  function setAction(key){
    if(s.action==='peek'&&key!=='peek')s.x=clamp(s.x,edge(),W-edge());
    if(!M.start(s,key))return;s.paused=false;$('#pause-button').innerHTML=icon('pause');$('#pause-button').setAttribute('aria-label','暂停');
    s.menu=null;renderMenu();resetFx();
    if(key==='chat')talk={phase:'welcome',started:s.age,message:'想聊点什么？我在听。',turn:talk.turn,until:0};
    actionStamp='';syncStatus();if(key==='chat')setTimeout(()=>$('#chat-input').focus(),0);
  }
  function syncStatus(){
    const motion=poseMotion.sample(s.action,s.elapsed);
    const stamp=[s.action,s.hideStage,s.score,s.paused,talk.phase,motion?.phase].join('|');
    if(stamp!==actionStamp){
      actionStamp=stamp;let title=M.actions[s.action].title;
      if(s.action==='peek')title=s.hideStage==='hidden'?'藏好了…':s.hideStage==='returning'?'找到啦，走回来':'去藏起来';
      if(s.action==='chat'&&talk.phase==='thinking')title='想一想…';
      if(motion)title=M.actions[s.action].title+' · '+motion.phase;
      $('#action-status').textContent=title+(s.paused?' · 已暂停':'');
      $('#action-hint').textContent=M.actions[s.action].hint || (s.action==='peek'?'每次随机选一边。左键找到；右键开菜单，仍然躲藏。':s.action==='chat'?'直接在角色上方聊天，回复前先思考 1 秒。':'右键只开关菜单，动作继续。试试右边的新互动。');
      $('#score').hidden=!['stars','bubbles'].includes(s.action);$('#score').textContent=s.action==='stars'?`数了 ${s.score} / 5 颗`:`戳破 ${s.score}`;
      $$('#action-list [data-action]').forEach(b=>{b.classList.toggle('active',b.dataset.action===s.action);b.setAttribute('aria-pressed',String(b.dataset.action===s.action));});
      const find=$('#find-pet');find.hidden=!(s.action==='peek'&&s.hideStage==='hidden');find.style.left=s.hideSide<0?'9px':'auto';find.style.right=s.hideSide>0?'9px':'auto';
    }
    const isChat=s.action==='chat',face=anchor('face'),x=clamp(s.x,128,W-128);
    $('#chat-form').hidden=!isChat;$('#speech').hidden=!isChat;
    if(isChat){
      const form=$('#chat-form'),speech=$('#speech');form.style.left=x+'px';form.style.top=Math.max(95,face.y-67)+'px';
      speech.style.left=x+'px';speech.style.bottom=(H-Math.max(95,face.y-67)+9)+'px';speech.textContent=talk.phase==='thinking'?'嗯… 想一想':talk.message;speech.classList.toggle('thinking',talk.phase==='thinking');
      $('#chat-form button').disabled=talk.phase==='thinking';
    }
  }
  function resize(){
    const before=W;W=stage.clientWidth;H=stage.clientHeight;scale=Math.min(1.25,W/380);
    const dpr=Math.min(devicePixelRatio||1,2);canvas.width=Math.round(W*dpr);canvas.height=Math.round(H*dpr);ctx.setTransform(dpr,0,0,dpr,0,0);ctx.lineCap='round';ctx.lineJoin='round';ctx.imageSmoothingEnabled=true;ctx.imageSmoothingQuality='high';
    s.edgePadding=edge();
    if(before!==W){s.x=s.x/before*W;if(s.action!=='peek')s.x=clamp(s.x,edge(),W-edge());if(s.target!=null)s.target=clamp(s.target/before*W,edge(),W-edge());}
    renderMenu();
  }
  function changeLook(){
    s.style=$('.style-options .selected').dataset.style;outfit=$('.outfit-options .selected').dataset.outfit;
    lastFrame=null;lastLook='';if(s.action==='dance'&&s.style==='chibi')setAction('idle');
    // A style/outfit switch selects the same action in its own clip set.
    fx.bubbles=[];fx.nextBubble=s.elapsed;if(s.action==='stars')seedStars();
    renderActions();renderMenu();
  }
  function toggleMenu(point){M.toggleMenu(s,point);renderMenu();}
  function point(e){const r=stage.getBoundingClientRect();return{x:e.clientX-r.left,y:e.clientY-r.top};}
  stage.addEventListener('contextmenu',e=>{e.preventDefault();toggleMenu(point(e));});
  stage.addEventListener('pointerdown',e=>{if(e.button===2)e.preventDefault();});
  stage.addEventListener('click',e=>{
    if(e.target.closest('button,input,form,.radial-menu'))return;
    const p=point(e);
    if(s.action==='peek'){M.find(s);s.menu=null;renderMenu();syncStatus();return;}
    if(s.menu){s.menu=null;renderMenu();return;}
    if(s.paused)return;
    if(s.action==='bubbles'){
      const hit=fx.bubbles.findIndex(b=>{const q=bubblePosition(b);return Math.hypot(q.x-p.x,q.y-p.y)<q.r+10;});
      if(hit>=0){const q=bubblePosition(fx.bubbles[hit]);fx.bubbles.splice(hit,1);fx.pops.push({...q,born:s.elapsed});s.score++;}
    }else if(s.action==='stars'){
      const hit=fx.stars.find(star=>{const q=restingStar(star);return Math.hypot(q.x-p.x,q.y-p.y)<26;});
      if(hit)setAction('stars');
    }else if(s.action==='butterfly'){s.target=clamp(p.x,edge(),W-edge());fx.landAt=s.elapsed;}
    syncStatus();
  });
  $('#find-pet').addEventListener('click',()=>{M.find(s);s.menu=null;renderMenu();syncStatus();});
  menuEl.addEventListener('click',e=>{
    const b=e.target.closest('[data-menu-key]');if(!b)return;const key=b.dataset.menuKey;
    if(key==='close'){s.menu=null;renderMenu();return;}
    if(key==='back'){s.menu='root';renderMenu();return;}
    if(['care','play','interaction'].includes(key)){s.menu=key;renderMenu();return;}
    if(key==='member'){s.menu=null;renderMenu();$('#membership-title').scrollIntoView({behavior:'smooth',block:'center'});return;}
    setAction(key);
  });
  $('#menu-button').addEventListener('click',()=>toggleMenu({x:W*.67,y:H*.45}));
  $('#pause-button').addEventListener('click',()=>{s.paused=!s.paused;$('#pause-button').innerHTML=icon(s.paused?'resume':'pause');$('#pause-button').setAttribute('aria-label',s.paused?'继续':'暂停');syncStatus();});
  $('#stop-button').addEventListener('click',()=>{s.x=clamp(s.x,edge(),W-edge());setAction('idle');});
  $('#loop').addEventListener('change',e=>s.loop=e.target.checked);
  $('#action-list').addEventListener('click',e=>{const b=e.target.closest('[data-action]');if(b)setAction(b.dataset.action);});
  $$('.category-tabs button').forEach(b=>b.addEventListener('click',()=>{group=b.dataset.group;$$('.category-tabs button').forEach(x=>{const selected=x===b;x.classList.toggle('selected',selected);x.setAttribute('aria-selected',selected);});renderActions();}));
  for(const selector of ['.style-options','.outfit-options'])$$(selector+' button').forEach(b=>b.addEventListener('click',()=>{$$(selector+' button').forEach(x=>{x.classList.toggle('selected',x===b);x.setAttribute('aria-pressed',x===b);});changeLook();}));
  $('#tier-cards').addEventListener('click',e=>{const b=e.target.closest('[data-tier]');if(b){s.tier=+b.dataset.tier;renderTiers();}});
  $('#chat-form').addEventListener('submit',e=>{
    e.preventDefault();if(s.action!=='chat'||talk.phase==='thinking')return;
    const message=$('#chat-input').value.trim();if(!message)return;
    talk.phase='thinking';talk.started=s.age;talk.until=s.age+1000;talk.turn++;talk.prompt=message;$('#chat-input').value='';syncStatus();
  });
  function reply(message){
    if(/累|烦|難|难|疲|忙/.test(message))return '辛苦了。先放松一下，今天哪件事最让你累？';
    if(/你好|哈喽|早|嗨/.test(message))return '你好呀！今天想一起玩，还是聊聊你的心情？';
    if(/喜欢|可爱|好看|漂亮/.test(message))return '我收下这份夸奖啦。你今天有没有一件开心的小事？';
    return ['嗯，我在听。后来怎么样了？','原来是这样。你当时是什么心情？','我记着这段小故事了。接下来想怎么做？'][ (talk.turn-1)%3 ];
  }
  document.addEventListener('keydown',e=>{
    if(e.key==='Escape'&&s.menu){s.menu=null;renderMenu();$('#menu-button').focus();}
    if((e.key==='ContextMenu'||(e.key==='F10'&&e.shiftKey))&&!e.target.matches('input')){e.preventDefault();toggleMenu({x:W/2,y:H/2});menuEl.querySelector('button')?.focus();}
    if(s.menu&&['ArrowRight','ArrowDown','ArrowLeft','ArrowUp'].includes(e.key)){
      e.preventDefault();const buttons=[...menuEl.querySelectorAll('button')],i=buttons.indexOf(document.activeElement),step=['ArrowRight','ArrowDown'].includes(e.key)?1:-1;buttons[(i+step+buttons.length)%buttons.length]?.focus();
    }
  });
  function animate(now){
    const dt=lastTime?Math.min(now-lastTime,100):0;lastTime=now;const old=s.action,oldElapsed=s.elapsed;
    if(!poseMotion.sample(s.action,s.elapsed)||poseRenderer?.ready)M.tick(s,dt,W,speed());
    if(old!==s.action||s.elapsed<oldElapsed)resetFx();
    if(!s.paused){updateFx();if(s.action==='chat'&&talk.phase==='thinking'&&s.age>=talk.until){talk.phase='reply';talk.message=reply(talk.prompt);talk.started=s.age;}else if(talk.phase==='reply'&&s.age-talk.started>2600)talk.phase='listen';}
    ctx.clearRect(0,0,W,H);poseRenderer?.clear();drawActor();drawFx();syncStatus();requestAnimationFrame(animate);
  }
  document.addEventListener('visibilitychange',()=>lastTime=0);
  new ResizeObserver(resize).observe(stage);resize();renderTiers();renderActions();syncStatus();requestAnimationFrame(animate);
  // Read-only browser inspection plus explicit action selection for repeatable Demo QA.
  globalThis.__clubDemo={
    snapshot:()=>({action:s.action,elapsed:s.elapsed,x:s.x,phase:s.phase,dir:s.dir,menu:s.menu,hideStage:s.hideStage,hideSide:s.hideSide,hideHistory:[...s.hideHistory],paused:s.paused,look:look(),tier:s.tier,score:s.score,thinking:talk.phase==='thinking',reply:talk.message,frame:poseDebug?`pose:${poseDebug.look}:${poseDebug.a}`:lastFrame?.id,frameLook:poseDebug?.look||lastFrame?.look,pose:poseDebug,poseReady:!!poseRenderer?.ready,cacheSize:cache.size,errors:[...errors],scale,edgePadding:s.edgePadding,width:W,anchors:{face:anchor('face'),hands:anchor('hands'),hair:anchor('hair')},bubbleSource:bubbleSource(),targets:s.action==='bubbles'?fx.bubbles.map(bubblePosition):s.action==='stars'?fx.stars.map(restingStar):[],butterfly:s.action==='butterfly'?butterflyPosition():null}),
    play:setAction,
    seek:(key,ms)=>{setAction(key);s.elapsed=clamp(ms,0,M.actions[key].duration);s.paused=true;resetFx();updateFx();$('#pause-button').innerHTML=icon('resume');$('#pause-button').setAttribute('aria-label','继续');syncStatus();}
  };
})();
