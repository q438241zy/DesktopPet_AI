/* Review-only interaction controller. Uses complete native frames; no limb warping. */
globalThis.CompanionPlayUI={create(h){
 'use strict';
 const $=s=>document.querySelector(s),M=CompanionPlay,session=new M.Session({stories:CompanionStories,items:h.items});
 const menu=$('#pet-menu'),stage=$('#pet-stage'),layer=$('#play-effects'),status=$('#play-status'),controls=$('#play-controls');
 let group='root',menuPage=0,chosen='',ticket=0,loading=false,stamp='',lastFrame=null;
 const names={root:'云朵伙伴',play:'玩耍',interaction:'互动'};
 const iconName=key=>({read:'book',highfive:'hand',rps:'scissors',interaction:'heart',settings:'settings',next:'next',back:'back',chat:'chat'}[key]||key);
 function icon(key){const name=iconName(key),line=CompanionIcons[name]||CompanionIcons.heart,solid=CompanionSolidIcons[name];return `<svg viewBox="0 0 24 24" aria-hidden="true"><path class="icon-outline" d="${line}"></path><path class="icon-solid" fill-rule="evenodd" d="${solid||line}"></path></svg>`;}
 function renderMenu(){
  menu.replaceChildren();menu.dataset.group=group;menu.dataset.page=String(menuPage);menu.setAttribute('aria-label',names[group]);
  const root=['chat','group:play','group:interaction','settings','close'];
  const all=M.groups[group]||[],pages=Math.ceil(all.length/6);
  const keys=group==='root'?root:[...all.slice(menuPage*6,menuPage*6+6),'back',...(pages>1?['next']:[])];
  const center=document.createElement('span');center.className='wheel-title';center.textContent=names[group];if(group!=='root'){const small=document.createElement('small');small.textContent=`${menuPage+1} / ${pages}`;center.append(small);}menu.append(center);
  keys.forEach((key,i)=>{
   const b=document.createElement('button'),id=key.replace('group:',''),label=names[id]||M.titles[id]||({back:'返回',next:'下一页',settings:'设定',close:'收起'}[id]);
   b.type='button';b.dataset.menuAction=key;b.className='wheel-item';b.title=label;b.setAttribute('aria-label',label);b.setAttribute('aria-pressed',String(chosen===key||key.startsWith('group:')&&M.groups[id]?.includes(chosen)));
   const angle=(i/keys.length*Math.PI*2)-Math.PI/2;b.style.setProperty('--wx',String(Math.cos(angle)));b.style.setProperty('--wy',String(Math.sin(angle)));
   b.innerHTML=icon(id)+'<span></span>';b.lastElementChild.textContent=label;b.onclick=()=>{
    h.touch();if(key.startsWith('group:')){group=id;menuPage=0;renderMenu();}else if(key==='back'){group='root';menuPage=0;renderMenu();}else if(key==='next'){menuPage=(menuPage+1)%pages;renderMenu();}else if(key==='close')menu.hidden=true;else if(key==='settings'){menu.hidden=true;h.settings();}else{chosen=key;menu.hidden=true;h.action(key);}
   };menu.append(b);
  });
 }
 function open(which='root'){group=which;menuPage=0;renderMenu();menu.hidden=false;}
 function toggle(){if(menu.hidden)open();else menu.hidden=true;}
 function clear(){ticket++;loading=false;session.cancel();lastFrame=null;chosen='';stamp='';layer.hidden=true;controls.hidden=true;status.hidden=true;$('#reading-progress').hidden=true;$('#highfive-target').hidden=true;stage.classList.remove('playing');h.clear();}
 async function start(key,options={}){
  clear();if(!session.start(key,options))return false;
  h.begin();chosen=key;menu.hidden=true;stage.classList.add('playing');const mine=++ticket;loading=true;
  status.hidden=false;status.textContent='准备中';
  const art=h.art(),list=key==='gift'?['receive','gift','gift-empty','happy']:key==='read'?['read','page','read-finish']:key==='highfive'?['high-prep','high-ready','high-contact','high-recoil','happy']:key==='rps'?['fist-up','fist-mid','fist-down','reveal-rock','reveal-scissors','reveal-paper']:key==='ball'?['ball-ready','ball-hit','ball-miss','happy']:key==='butterfly'||key==='walk'?['walk','stand']:[M.clips[key]||key];
  try{await Promise.all([...new Set(list.map(k=>art[k]?.file))].filter(Boolean).map(file=>h.preload(Object.values(art).find(d=>d.file===file))));if(mine!==ticket)return false;loading=false;sync();h.render();return true;}
  catch(e){if(mine===ticket){clear();h.error(e);}return false;}
 }
 function sync(){
  if(!session.active)return;const v=session.view,k=session.key;
  h.bubble(v.bubble);status.hidden=false;status.textContent=k==='read'?`${v.title} · ${v.line+1} / ${v.total}`:M.titles[k];
  const next=[k,v.phase].join('|');if(next!==stamp){stamp=next;controls.replaceChildren();
   if(k==='rps'&&v.phase==='choose')for(const [choice,label] of [['rock','石头'],['scissors','剪刀'],['paper','布']]){const b=document.createElement('button');b.textContent=label;b.dataset.throw=choice;b.onclick=()=>{h.touch();session.throw(choice);session.view=session.frame();sync();};controls.append(b);}
   const stop=document.createElement('button');stop.className='quiet';stop.textContent='结束';stop.dataset.playStop='true';stop.onclick=()=>{h.touch();clear();h.render();};controls.append(stop);
  }controls.hidden=false;
  $('#reading-progress').hidden=k!=='read';$('#reading-progress i').style.width=k==='read'?v.progress*100+'%':'0';
 }
 function advance(ms,blocked=false){
  if(!session.active||loading||blocked)return;
  session.advance(ms);for(const event of session.takeEvents())h.collect(event);
  if(!session.active){clear();h.render();return;}sync();
 }
 function path(ctx,d,fill,stroke){const p=new Path2D(d);if(fill){ctx.fillStyle=fill;ctx.fill(p);}if(stroke){ctx.strokeStyle=stroke;ctx.lineWidth=1.1;ctx.stroke(p);}}
 function userHand(ctx,x,y,scale=1,gesture='paper'){
  ctx.save();ctx.translate(x,y);ctx.scale(scale,scale);
  const d=gesture==='rock'?'M-19 2Q-20-8-13-9H4Q12-8 12 2V9Q8 15-5 14L-19 10Z':gesture==='scissors'?'M-14 8V-3L-4-17Q0-20 2-17L-3-5 9-17Q13-19 15-15L6 0Q15 1 13 9L5 15H-9Z':'M-21 4Q-22-3-17-2L-6 0H13Q20 1 18 6L6 10-8 11-20 9Z';
  path(ctx,d,'#f2d2c5','#d8ac9c');path(ctx,'M-31 0H-21V13H-31Z','#c8dacd','#a8c2b4');ctx.restore();
 }
 function gift(ctx,x,y,size){ctx.save();ctx.translate(x,y);ctx.scale(size/36,size/36);path(ctx,'M-16-12H16V16H-16Z','#fff9fc','#c7aab8');path(ctx,'M-18-16H18V-9H-18Z','#fceff5','#c7aab8');path(ctx,'M-3-16H3V16H-3Z','#ec97b7');path(ctx,'M0-16C-20-15-12-32 0-16ZM0-16C20-15 12-32 0-16Z','#eca5c0','#ce84a2');ctx.restore();}
 function effects(v,info){
  const {w,h:stageHeight,x,floor,height,desc,frame,k,ctx:petContext,img}=info,dpr=Math.min(3,devicePixelRatio||1);
  if(layer.width!==Math.round(w*dpr)||layer.height!==Math.round(stageHeight*dpr)){layer.width=Math.round(w*dpr);layer.height=Math.round(stageHeight*dpr);}
  layer.hidden=false;const c=layer.getContext('2d');c.setTransform(dpr,0,0,dpr,0,0);c.clearRect(0,0,w,stageHeight);
  const anchor=name=>{const a=desc.anchors?.[name]||{x:0,y:-desc.reference*.55};return {x:x+a.x*k,y:floor+a.y*k};};
  $('#highfive-target').hidden=true;
  if(session.key==='gift'){
   if(v.phase==='giving'){
    const dest=anchor('receive'),start={x:Math.max(40,x-150),y:Math.min(stageHeight-50,dest.y+40)},p=v.giving,gx=start.x+(dest.x-start.x)*p,gy=start.y+(dest.y-start.y)*p-Math.sin(p*Math.PI)*12;
    userHand(c,gx-24,gy+18,.88);gift(c,gx,gy,Math.min(38,height*.17));
    c.fillStyle='#a28f99';c.font='11px "Microsoft YaHei UI"';c.textAlign='center';c.fillText('你',start.x-18,start.y+42);
   }else if(session.time<1550){const p=(session.time-1150)/400,dest=anchor('gift');c.globalAlpha=1-p;userHand(c,dest.x-24-p*65,dest.y+22,.88);c.globalAlpha=1;}
   if(v.pose==='gift-empty'&&v.reveal>0){
    // Draw the item inside the native box, then restore its unchanged front wall.
    const a=desc.anchors.giftSlot||{x:0,y:-desc.reference*.4},image=h.item(session.prize.id);
    const localX=info.cw*.5+a.x*info.pixelK,localY=info.ch*.96+a.y*info.pixelK,size=Math.min(44*dpr,Math.max(20*dpr,desc.boxWidth*info.pixelK*.72));
    if(image.complete&&image.naturalWidth){petContext.save();petContext.globalAlpha=v.reveal;petContext.drawImage(image,localX-size/2,localY-size*.7+(1-v.reveal)*12*dpr,size,size);petContext.globalAlpha=1;
     petContext.beginPath();petContext.rect(localX-desc.boxWidth*info.pixelK/2-3,info.ch*.96+desc.frontY*info.pixelK,desc.boxWidth*info.pixelK+6,info.ch);petContext.clip();h.redraw(petContext,img,desc,frame,info);petContext.restore();}
   }
  }
  if(session.key==='butterfly'){
   // Lead in front of the visible body, not a fraction of its travel range:
   // the latter puts the butterfly on the face on a narrow phone viewport.
   const lead=Math.max(64,Math.min(105,height*.43)),bx=Math.max(20,Math.min(w-20,x+v.direction*lead)),by=floor-height*.92+v.butterfly.y;
   c.save();c.translate(bx,by);c.rotate(v.direction*.22);const wing=Math.max(.2,Math.abs(v.butterfly.wing));c.scale(wing*.7,.7);path(c,'M0 0C-24-28-30 10-3 9C-18 29 0 28 0 5C0 28 18 29 3 9C30 10 24-28 0 0Z','#bcafe4','#9681ba');c.restore();path(c,`M${bx} ${by-2}v11`,null,'#796982');
   info.butterflyPixels={x:bx,y:by,lead:(bx-x)*v.direction};
  }
  if(session.key==='highfive'&&v.phase==='waiting'&&session.time>=550){
   const a=anchor('hand'),b=$('#highfive-target');b.hidden=false;b.style.left=a.x+'px';b.style.top=a.y+'px';b.onclick=()=>{h.touch();session.highfive();session.view=session.frame();sync();};
  }
  if(session.key==='rps'&&v.phase!=='choose'){userHand(c,Math.max(38,x-105),floor-height*.6+(v.phase==='pump'?Math.sin(session.time/95)*9:0),1.25,v.user);}
  if(session.key==='bubbles')for(let i=0;i<5;i++){const p=((session.time+i*460)%2500)/2500,bx=x+25+Math.sin(i)*p*75,by=floor-height*.6-p*90;c.strokeStyle=`rgba(181,169,218,${(1-p)*.8})`;c.fillStyle=`rgba(217,226,246,${(1-p)*.2})`;c.beginPath();c.arc(bx,by,6+p*12,0,Math.PI*2);c.fill();c.stroke();}
  if(session.key==='stars')for(let i=0;i<Math.min(5,Math.floor(session.time/1100)+1);i++){c.save();c.translate(x-76+i*38,floor-height-20+Math.sin(session.time/380+i)*3);c.scale(.5,.5);path(c,'M0-14 4-4 15-4 7 3 10 14 0 8-10 14-7 3-15-4-4-4Z','#e5c78d');c.restore();}
  lastFrame={...v,offsetPixels:v.offset*info.range,range:info.range,file:desc.file,frame,scale:k,butterflyPixels:info.butterflyPixels};
 }
 return {start,clear,advance,open,toggle,renderMenu,effects,icon,get active(){return session.active;},get loading(){return loading;},get view(){return session.view;},get key(){return session.key;},snapshot:()=>({active:session.active,loading,key:session.key,time:session.time,phase:session.view?.phase,story:session.story?.id,line:session.view?.line,prize:session.prize?.id,menu:group,frame:lastFrame})};
}};
