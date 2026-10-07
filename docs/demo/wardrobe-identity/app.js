/* Clothing review only. Equal source pixels receive equal scale on both sides.
   Never normalize each pose's bounding box or stretch individual body parts. */
'use strict';
const $=id=>document.getElementById(id),images=new Map();
const state={style:'chibi',mode:'pair',pose:0,frame:0,playing:false,direction:1,reference:'original',old:false,opacity:.5,hold:false,guides:false,ready:false};
const files=['chibi-original.png','chibi-swim-before.webp','chibi-wedding.webp','chibi-sports.png','chibi-walk-original.png','chibi-walk-before.png','deepseek-chibi-swim-v1.png','deepseek-chibi-swim-walk-v1.png','realistic-original.png','realistic-swim.png','realistic-wedding.png','realistic-sports-before.png','deepseek-realistic-sports-v1.png'];
const titles={original:'原装',swim:'泳装',wedding:'婚纱'},poseNames=['待机','开心','揉脸','休息','头晕','难过'];
const frameOf=(file,columns=1,rows=1,index=0)=>{const im=images.get(file),w=im.naturalWidth/columns,h=im.naturalHeight/rows;return {file,im,x:index%columns*w,y:Math.floor(index/columns)*h,w,h};};
function selectedFrames(){
  if(state.style==='realistic')return [frameOf('realistic-'+state.reference+'.png'),frameOf('deepseek-realistic-sports-v1.png')];
  if(state.pose==='walk')return [frameOf('chibi-walk-original.png',3,4,state.frame),frameOf(state.old?'chibi-walk-before.png':'deepseek-chibi-swim-walk-v1.png',3,4,state.frame)];
  return [frameOf('chibi-original.png',3,2,state.pose),frameOf(state.old?'chibi-swim-before.webp':'deepseek-chibi-swim-v1.png',3,2,state.pose)];
}
function context(canvas){const r=canvas.getBoundingClientRect(),dpr=window.devicePixelRatio||1;canvas.width=Math.round(r.width*dpr);canvas.height=Math.round(r.height*dpr);const c=canvas.getContext('2d');c.scale(dpr,dpr);c.imageSmoothingEnabled=true;c.imageSmoothingQuality='high';return {c,w:r.width,h:r.height};}
function paint(c,frame,t,alpha=1,mirror=false){c.save();c.globalAlpha=alpha;c.translate(t.x+t.w/2,t.y);c.scale(mirror?-1:1,1);c.drawImage(frame.im,frame.x,frame.y,frame.w,frame.h,-t.w/2,0,t.w,t.h);c.restore();}
function transform(w,h,frame,mini=false){const padding=mini?4:16,top=mini?2:53,s=Math.min((w-2*padding)/frame.w,(h-top-padding)/frame.h);return {x:(w-frame.w*s)/2,y:top+(h-top-padding-frame.h*s)/2,w:frame.w*s,h:frame.h*s,scale:s};}
function draw(canvas,ref,target,isCandidate){
  if(!canvas.getBoundingClientRect().width)return;
  const {c,w,h}=context(canvas),t=transform(w,h,ref),mirror=state.style==='chibi'&&state.pose==='walk'&&state.direction<0;
  if(isCandidate&&state.mode==='overlay'){paint(c,ref,t,state.hold?1:1-state.opacity,mirror);if(!state.hold)paint(c,target,t,state.opacity,mirror);}else paint(c,isCandidate?target:ref,t,1,mirror);
  if(state.guides){c.save();c.strokeStyle=$('dark').checked?'#b8cbea88':'#8ba1c388';c.lineWidth=1;c.setLineDash([4,5]);for(const ratio of [.2,.5,.75,1]){const y=t.y+t.h*ratio;c.beginPath();c.moveTo(t.x,y);c.lineTo(t.x+t.w,y);c.stroke();}c.beginPath();c.moveTo(w/2,t.y);c.lineTo(w/2,t.y+t.h);c.stroke();c.restore();}
  canvas.dataset.source=isCandidate?target.file:ref.file;canvas.dataset.sourceFrame=state.style==='realistic'?0:state.pose==='walk'?state.frame:state.pose;
  canvas.dataset.scale=t.scale;
}
function render(){if(!state.ready)return;const [ref,target]=selectedFrames();draw($('reference-canvas'),ref,target,false);draw($('candidate-canvas'),ref,target,true);$('frame').value=state.frame;$('frame-label').textContent=(state.frame+1)+' / 12';}
function gallery(){
  if(!state.ready)return;
  $('gallery').replaceChildren();
  const entries=state.style==='chibi'?
    [['原装 · 坐姿',frameOf('chibi-original.png',3,2,0)],['现行泳装 · 坐姿',frameOf('chibi-swim-before.webp',3,2,0)],['婚纱 · 坐姿',frameOf('chibi-wedding.webp')],['运动服 · 站姿',frameOf('chibi-sports.png',4,6,0)]]:
    [['原装',frameOf('realistic-original.png')],['泳装',frameOf('realistic-swim.png')],['婚纱',frameOf('realistic-wedding.png')],['现行运动服',Object.assign(frameOf('realistic-sports-before.png'),{x:116,y:6,w:165,h:383})]];
  for(const [label,frame]of entries){const f=document.createElement('figure'),canvas=document.createElement('canvas'),caption=document.createElement('figcaption');canvas.setAttribute('aria-label',label);caption.textContent=label;f.append(canvas,caption);$('gallery').append(f);const {c,w,h}=context(canvas);paint(c,frame,transform(w,h,frame,true));}
}
function update(){
  $('comparison').dataset.style=state.style;$('comparison').dataset.mode=state.mode;
  $('chibi-controls').hidden=state.style!=='chibi';$('realistic-controls').hidden=state.style!=='realistic';
  $('walk-controls').hidden=state.style!=='chibi'||state.pose!=='walk';$('overlay-controls').hidden=state.mode!=='overlay';$('old-toggle').hidden=state.style!=='chibi';
  document.querySelectorAll('[data-style]').forEach(b=>{if(b.tagName==='BUTTON')b.setAttribute('aria-pressed',b.dataset.style===state.style);});
  document.querySelectorAll('[data-mode]').forEach(b=>{if(b.tagName==='BUTTON')b.setAttribute('aria-pressed',b.dataset.mode===state.mode);});
  document.querySelectorAll('[data-pose]').forEach(b=>b.setAttribute('aria-pressed',b.dataset.pose===String(state.pose)));
  document.querySelectorAll('[data-reference]').forEach(b=>b.setAttribute('aria-pressed',b.dataset.reference===state.reference));
  const adult=state.style==='realistic';
  $('reference-label').textContent=adult?titles[state.reference]:'原装';$('candidate-label').textContent=adult?'运动服 · 换衣样稿':state.old?'现行泳装':'泳装 · 换衣样稿';
  $('candidate-note').textContent=state.mode==='overlay'?'同尺寸叠图':!adult&&state.old?'现有画稿':'待确认';
  $('comparison-note').textContent=adult?'参考与换装保持同一画布。短袖上衣＋膝上短裤。':state.pose==='walk'?'12 帧同步对照；可暂停逐帧查看。':'两侧使用同一画布和缩放比例。';
  $('play').textContent=state.playing?'暂停':'播放';$('play').setAttribute('aria-pressed',state.playing);$('direction').textContent=state.direction<0?'← 朝左':'朝右 →';$('direction').setAttribute('aria-pressed',state.direction<0);
  $('source-image').href='art/'+(adult?'deepseek-realistic-sports-v1.png':state.pose==='walk'?'deepseek-chibi-swim-walk-v1.png':'deepseek-chibi-swim-v1.png');
  render();
}
document.querySelectorAll('button[data-style]').forEach(b=>b.addEventListener('click',()=>{state.style=b.dataset.style;state.playing=false;state.hold=false;update();gallery();}));
document.querySelectorAll('button[data-mode]').forEach(b=>b.addEventListener('click',()=>{state.mode=b.dataset.mode;state.hold=false;update();}));
document.querySelectorAll('[data-pose]').forEach(b=>b.addEventListener('click',()=>{state.pose=b.dataset.pose==='walk'?'walk':Number(b.dataset.pose);state.frame=0;state.playing=false;update();}));
document.querySelectorAll('[data-reference]').forEach(b=>b.addEventListener('click',()=>{state.reference=b.dataset.reference;update();}));
$('old').addEventListener('change',e=>{state.old=e.target.checked;update();});$('guides').addEventListener('change',e=>{state.guides=e.target.checked;render();});$('dark').addEventListener('change',e=>{$('comparison').classList.toggle('dark',e.target.checked);render();});
$('opacity').addEventListener('input',e=>{state.opacity=Number(e.target.value)/100;$('opacity-label').textContent=e.target.value+'%';render();});
const stopHold=()=>{state.hold=false;render();};$('hold-reference').addEventListener('pointerdown',e=>{e.currentTarget.setPointerCapture(e.pointerId);state.hold=true;render();});$('hold-reference').addEventListener('pointerup',stopHold);$('hold-reference').addEventListener('pointercancel',stopHold);$('hold-reference').addEventListener('lostpointercapture',stopHold);$('hold-reference').addEventListener('keydown',e=>{if(e.code==='Space'||e.code==='Enter'){e.preventDefault();state.hold=true;render();}});$('hold-reference').addEventListener('keyup',stopHold);$('hold-reference').addEventListener('blur',stopHold);
$('play').addEventListener('click',()=>{state.playing=!state.playing;last=performance.now();elapsed=0;update();});
function seek(n){state.playing=false;state.frame=(n+12)%12;update();}
$('previous').addEventListener('click',()=>seek(state.frame-1));$('next').addEventListener('click',()=>seek(state.frame+1));$('frame').addEventListener('input',e=>seek(Number(e.target.value)));$('direction').addEventListener('click',()=>{state.direction*=-1;update();});
let last=0,elapsed=0;function tick(t){if(state.ready&&state.playing&&!document.hidden){elapsed+=Math.min(t-last,160);if(elapsed>=80){const frames=Math.floor(elapsed/80);state.frame=(state.frame+frames)%12;elapsed%=80;render();}}last=t;requestAnimationFrame(tick);}requestAnimationFrame(tick);
window.addEventListener('resize',()=>{render();if(state.ready)gallery();});document.addEventListener('visibilitychange',()=>{last=performance.now();elapsed=0;});
window.wardrobeReview=()=>({...state,frames:state.ready?selectedFrames().map(({file,x,y,w,h})=>({file,x,y,w,h})):[],installed:false});
Promise.all(files.map(file=>new Promise((resolve,reject)=>{const im=new Image();im.onload=()=>{images.set(file,im);resolve();};im.onerror=()=>reject(new Error('图片无法读取：'+file));im.src='art/'+file;}))).then(()=>{state.ready=true;$('loading').hidden=true;update();gallery();}).catch(e=>{$('loading').hidden=true;$('error').hidden=false;$('error').textContent=e.message;});
