'use strict';
(async function(){
  const $=id=>document.getElementById(id),M=MotionStudy,A=window.MOTION_ASSETS;
  const names=A.idles.map(i=>i.name);
  const spontaneous={stretch:{pose:'idle:5',name:'伸个懒腰',duration:3500},yawn:{pose:'idle:6',name:'掩嘴打哈欠',duration:3200},greet:{pose:'idle:7',name:'和你招招手',duration:2900}};
  let renderer;
  try{renderer=new PetRenderer($('pet'),A);await renderer.load();}catch(error){$('loading').hidden=true;$('error').hidden=false;$('error').textContent=error.message;window.demoError=error.message;return;}
  $('loading').hidden=true;
  let clock=0,waitClock=0,last=performance.now(),paused=false,playlist=false,nextDance=Infinity;
  let state={kind:'idle',pose:'idle:0',from:'idle:0',started:0,duration:0},active='idle',manualScrub=false;
  const scheduler=new M.IdleSchedule(Math.random,A.idles.length),danceBag=new M.ShuffleBag(Object.keys(A.clips).filter(k=>k!=='tickle')),walker=new M.Walker();
  scheduler.poses.last=0;
  let lastAction='自动待机',lastDetail=names.length+' 个站立姿势',notice='每隔几秒换个姿势，久等后会自己活动。';
  let stats={frames:0,events:[],loaded:true,maxSupportError:0},lastFpsAt=last,lastFpsCount=0;
  const actorHeight=()=>Math.min(356,renderer.height-105),floor=()=>renderer.height-50;
  let actorX=0;
  function log(message){notice=message;$('event').textContent=message;stats.events.push({at:Math.round(clock),message});if(stats.events.length>60)stats.events.shift();}
  function buttonState(){
    for(const b of document.querySelectorAll('[data-action]'))b.setAttribute('aria-pressed',String(b.dataset.action===active));
    for(const b of document.querySelectorAll('[data-dance]'))b.setAttribute('aria-pressed',String(state.kind==='clip'&&state.key===b.dataset.dance));
    $('randomDance').setAttribute('aria-pressed',String(playlist));
    for(const b of $('poses').children)b.setAttribute('aria-pressed',String(state.kind==='idle'&&state.pose===A.idles[Number(b.dataset.pose)].pose));
    $('actionLabel').textContent=lastAction;$('detailLabel').textContent=lastDetail;
  }
  function currentPose(){
    if(state.kind==='idle')return clock-state.started<state.duration/2?state.from:state.pose;
    if(state.kind==='clip')return M.clipPose(A.clips[state.key],Math.max(0,clock-state.started-420)).a;
    if(state.kind==='auto')return clock-state.started>500?spontaneous[state.key].pose:'idle:0';
    return 'idle:0';
  }
  function activity(){scheduler.reset(waitClock);manualScrub=false;}
  function idle(pose=0,manual=true){
    const previous=currentPose();if(manual){playlist=false;nextDance=Infinity;activity();}
    state={kind:'idle',pose:A.idles[pose].pose,from:previous,started:clock,duration:850};active='idle';
    scheduler.poses.last=pose;scheduler.poses.remaining=scheduler.poses.remaining.filter(x=>x!==pose);
    lastAction='自动待机';lastDetail=names[pose];buttonState();
  }
  function play(key,manual=true){
    const previous=currentPose();if(manual){playlist=false;nextDance=Infinity;activity();}
    state={kind:'clip',key,from:previous,started:clock};active=key==='tickle'?'tickle':'dance';
    lastAction=A.clips[key].name;lastDetail=({tt:'TWICE 手势样例',nextLevel:'aespa 折臂样例',loveDive:'IVE 镜面样例',tickle:'笑着躲一躲，再慢慢恢复'})[key]||'短动作 · 自然收回';
    log((playlist?'随机抽到：':'正在播放：')+lastAction);buttonState();
  }
  function walk(){playlist=false;nextDance=Infinity;activity();active='walk';state={kind:'walk',started:clock};walker.x=actorX||renderer.width/2;walker.turn=0;
    lastAction='散步';lastDetail='支撑脚落地 · 摆动脚低抬';log('脚步按前进距离推进，走到边缘后驻足换向。');buttonState();}
  function automatic(key,manual=false){
    const previous=currentPose();if(manual){playlist=false;nextDance=Infinity;activity();}
    state={kind:'auto',key,from:previous,started:clock};active='idle';lastAction=spontaneous[key].name;lastDetail='待久了，自己活动一下';log('自发动作：'+lastAction);buttonState();}
  function reset(){paused=false;manualScrub=false;setPauseLabel();actorX=renderer.width/2;walker.x=actorX;walker.phase=0;walker.direction=1;idle(0);}
  function setPauseLabel(){$('pause').setAttribute('aria-label',paused?'播放':'暂停');$('pause').title=paused?'播放':'暂停';$('pause').innerHTML=paused?'<svg viewBox="0 0 24 24"><path d="m9 5 10 7-10 7Z"/></svg>':'<svg viewBox="0 0 24 24"><path d="M9 5v14M15 5v14"/></svg>';}
  $('pause').onclick=()=>{paused=!paused;setPauseLabel();};$('reset').onclick=reset;
  for(const button of document.querySelectorAll('[data-action]'))button.onclick=()=>{paused=false;setPauseLabel();if(button.dataset.action==='walk')walk();else if(button.dataset.action==='tickle')play('tickle');else idle(0);};
  for(const button of document.querySelectorAll('[data-dance]'))button.onclick=()=>{paused=false;setPauseLabel();play(button.dataset.dance);};
  $('randomDance').onclick=()=>{activity();paused=false;setPauseLabel();playlist=true;nextDance=Infinity;play(danceBag.next(),false);};
  $('tryAuto').onclick=()=>{paused=false;setPauseLabel();scheduler.lastAction=M.choose(Object.keys(spontaneous),scheduler.lastAction);automatic(scheduler.lastAction,true);};
  $('autonomous').onchange=()=>scheduler.reset(waitClock);
  $('timeline').oninput=()=>{
    if(state.kind!=='clip')return;paused=true;manualScrub=true;setPauseLabel();state.started=clock-420-Number($('timeline').value)/1000*A.clips[state.key].duration;
  };
  $('pet').onpointerdown=e=>{activity();const r=$('pet').getBoundingClientRect(),x=e.clientX-r.left,y=e.clientY-r.top;
    if(Math.abs(x-actorX)<actorHeight()*.28&&y>floor()-actorHeight()&&y<floor()){paused=false;setPauseLabel();play('tickle');}
  };
  $('pet').onkeydown=e=>{if(e.key==='Enter'){play('tickle');e.preventDefault();}else if(e.code==='Space'){paused=!paused;setPauseLabel();e.preventDefault();}else if(e.key==='Escape')idle(0);};
  for(let i=0;i<A.idles.length;i++){
    const button=document.createElement('button'),thumb=document.createElement('canvas'),label=document.createElement('span');button.type='button';button.dataset.pose=i;button.title=names[i];button.setAttribute('aria-label',names[i]);
    thumb.width=88;thumb.height=128;const pose=A.poses[A.idles[i].pose],img=renderer.images[pose.atlas],ctx=thumb.getContext('2d'),[x,y,w,h]=pose.pixels;
    const scale=122/pose.height;ctx.drawImage(img,x,y,w,h,44-w*scale/2,125-h*scale,w*scale,h*scale);
    label.textContent=A.idles[i].label;button.append(thumb,label);button.onclick=()=>{paused=false;setPauseLabel();idle(i);};$('poses').append(button);
  }
  document.addEventListener('visibilitychange',()=>{last=performance.now();});
  function drawGuides(skeleton){
    const canvas=$('guides'),dpr=Math.min(devicePixelRatio||1,2),w=renderer.width,h=renderer.height;
    if(canvas.width!==Math.round(w*dpr)||canvas.height!==Math.round(h*dpr)){canvas.width=Math.round(w*dpr);canvas.height=Math.round(h*dpr);}
    const c=canvas.getContext('2d');c.setTransform(dpr,0,0,dpr,0,0);c.clearRect(0,0,w,h);if(!$('feet').checked||!skeleton)return;
    for(const leg of [skeleton.far,skeleton.near]){
      c.strokeStyle=leg.stance?'#7b9e93':'#c2a0bb';c.fillStyle=c.strokeStyle;c.lineWidth=1.1;c.setLineDash([3,4]);c.beginPath();c.moveTo(leg.hip.x,leg.hip.y);c.lineTo(leg.knee.x,leg.knee.y);c.lineTo(leg.ankle.x,leg.ankle.y);c.stroke();c.setLineDash([]);
      c.beginPath();c.ellipse(leg.sole.x,leg.sole.y+2,12,3,0,0,Math.PI*2);c.stroke();
    }
    c.font='12px Segoe UI';c.fillStyle='#7b9e93';c.fillText('● 支撑脚',24,29);c.fillStyle='#bd95af';c.fillText('○ 迈步脚',99,29);
  }
  function render(dt=0){
    renderer.clear();const h=actorHeight(),f=floor();if(!actorX)actorX=renderer.width/2;
    let skeleton=null,progress=0,duration=1;
    const sprite=(a,b,t,height=h,context=null)=>renderer.sprite(a,b,t,actorX,f,height,1,$('interpolation').checked,context);
    if(state.kind==='walk'){
      const margin=Math.min(140,renderer.width*.28),left=margin,right=Math.max(left+1,renderer.width-margin);
      walker.x=M.clamp(walker.x,left,right);if(dt)walker.advance(dt,left,right,h);actorX=walker.x;skeleton=renderer.walk(walker,actorX,f,h);
      lastDetail=walker.turn>0?'驻足 · 准备转身':'支撑脚落地 · 摆动脚低抬';progress=walker.phase%1;duration=1.65;
    }else{
      // Returning to idle does not teleport a pet back to the centre of the stage.
      actorX=M.clamp(actorX,Math.min(120,renderer.width*.25),Math.max(121,renderer.width-Math.min(120,renderer.width*.25)));
      const age=Math.max(0,clock-state.started);
      if(state.kind==='idle'){
        const breath=1+Math.sin(clock/1600)*.0012;const blend=M.smooth(age/Math.max(1,state.duration));sprite(state.from,state.pose,blend,h*breath);
      }else if(state.kind==='clip'){
        const clip=A.clips[state.key],t=age-420;duration=clip.duration/1000;progress=M.clamp(t/clip.duration);
        const rigged=!!A.hands[clip.frames[0]]&&$('interpolation').checked;
        if(rigged){
          const blendPoints=(a,b,amount)=>a.map((p,i)=>p.map((v,j)=>M.mix(v,b[i][j],amount)));
          const neutral=[[80,267],[241,267]],restFeet=[[140,460.8],[180,460.8]];
          let hands,feet,head;
          if(age<420){const amount=M.smooth(age/420);hands=blendPoints(neutral,A.hands[clip.frames[0]],amount);feet=blendPoints(restFeet,A.feet[clip.frames[0]],amount);head=blendPoints([[160,96]],A.heads[clip.frames[0]],amount);}
          else if(t<=clip.duration){const p=M.clipPose(clip,t);hands=M.trackPoints(A.hands,p);feet=M.trackPoints(A.feet,p);head=M.trackPoints(A.heads,p);}
          else {const amount=M.smooth((t-clip.duration)/380);hands=blendPoints(A.hands[clip.frames.at(-1)],neutral,amount);feet=blendPoints(A.feet[clip.frames.at(-1)],restFeet,amount);head=blendPoints(A.heads[clip.frames.at(-1)],[[160,96]],amount);}
          skeleton=renderer.dance(hands,feet,actorX,f,h,t,state.key,head);window.demoRig=skeleton;
        }else if(age<420)sprite(state.from,clip.frames[0],M.smooth(age/420));
        else if(t<=clip.duration){const p=M.clipPose(clip,t);sprite(p.a,p.b,p.t,h,p);}
        else sprite(clip.frames.at(-1),'idle:0',M.smooth((t-clip.duration)/380));
        if(t>clip.duration+380&&!paused){idle(0,false);if(playlist)nextDance=clock+180;}
      }else if(state.kind==='auto'){
        const auto=spontaneous[state.key],entrance=850,exit=700;duration=(auto.duration+entrance+exit)/1000;progress=M.clamp(age/(duration*1000));
        if(age<entrance)sprite(state.from,auto.pose,M.smooth(age/entrance));
        else if(age<entrance+auto.duration){const breath=1+Math.sin((age-entrance)/470)*.0013;sprite(auto.pose,auto.pose,0,h*breath);}
        else {sprite(auto.pose,'idle:0',M.smooth((age-entrance-auto.duration)/exit));if(age>duration*1000&&!paused){idle(0,false);scheduler.poseAt=waitClock+scheduler.poseDelay();}}
      }
    }
    drawGuides(skeleton?.near?skeleton:null);$('shadow').style.left=actorX+'px';$('timeline').disabled=state.kind!=='clip';
    if(!manualScrub)$('timeline').value=Math.round(progress*1000);$('timeLabel').textContent=(progress*duration).toFixed(1)+' s';
    const seconds=Math.max(0,(scheduler.actionAt-waitClock)/1000);
    $('waitLabel').textContent=$('autonomous').checked?(state.kind==='idle'?`${Math.ceil(seconds)} 秒后，自己活动一下`:'互动结束后继续等待'):'自发动作已关闭';
    $('detailLabel').textContent=lastDetail;
    window.demoSnapshot={kind:state.kind,action:state.key||state.pose,x:actorX,phase:walker.phase,direction:walker.direction,turn:walker.turn,clock,waitClock,paused,playlist,nextAuto:scheduler.actionAt-waitClock,skeleton};
  }
  function advance(dt){
      clock+=dt;waitClock+=dt*($('fastWait').checked?10:1);
      if(playlist&&state.kind==='idle'&&clock>=nextDance){nextDance=Infinity;play(danceBag.next(),false);}
      if(state.kind==='idle'&&!playlist){
        const event=scheduler.poll(waitClock,true,$('autonomous').checked);
        if(event?.kind==='pose'){idle(event.value,false);log('待机换成：'+names[event.value]);}
        else if(event?.kind==='action')automatic(event.value);
      }
      render(dt);
  }
  function frame(now){
    const realDt=Math.max(0,now-last);last=now;
    if(!document.hidden&&!paused)advance(Math.min(realDt,120)*Number($('speed').value));else render();
    stats.frames++;lastFpsCount++;
    if(now-lastFpsAt>=1000){$('fps').textContent=Math.round(lastFpsCount*1000/(now-lastFpsAt))+' fps';lastFpsCount=0;lastFpsAt=now;}
    requestAnimationFrame(frame);
  }
  // A deterministic clock only enabled by the local QA harness query parameter.
  if(new URLSearchParams(location.search).has('qa'))window.demoTest={
    idle,play,walk,automatic,reset,
    setTime(ms){clock=state.started+ms;paused=true;manualScrub=false;setPauseLabel();render();return window.demoSnapshot;},
    advance(ms){paused=false;advance(ms);paused=true;return window.demoSnapshot;},
    snapshot:()=>window.demoSnapshot,events:()=>stats.events,assets:()=>({poses:Object.keys(A.poses).length,clips:Object.keys(A.clips),sources:A.sources}),
    pause(){paused=true;render();},stats,renderer
  };
  buttonState();log(notice);requestAnimationFrame(frame);window.demoReady=true;
})();
