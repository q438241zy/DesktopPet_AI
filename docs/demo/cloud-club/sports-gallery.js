(() => {
  'use strict';
  const roster=globalThis.SPORTS_ROSTER;
  if(!Array.isArray(roster)||roster.length!==8)return;
  const root=document.querySelector('#school-study'),cards=root.querySelectorAll('.school-card');
  const tabs=document.createElement('div');tabs.className='sports-families';tabs.setAttribute('role','group');tabs.setAttribute('aria-label','运动服角色');
  root.querySelector('.school-cards').before(tabs);
  let generation=0;
  const loaded=new Map();
  async function picture(src){
    if(!loaded.has(src)){const im=new Image();im.src=src;loaded.set(src,im.decode().then(()=>im));}
    return loaded.get(src);
  }
  async function show(entry){
    const current=++generation;root.dataset.family=entry.family;
    for(const b of tabs.children)b.setAttribute('aria-pressed',String(b.dataset.family===entry.family));
    for(const [i,style] of ['chibi','realistic'].entries()){
      const card=cards[i],look=entry.looks[style],label=style==='chibi'?'Q版':'3D真人';
      card.querySelector('h3').textContent=`${entry.name} · ${label}`;
      card.querySelector('.school-caption p').textContent=style==='chibi'?'短袖、短裤，保留伙伴自己的小特征。':'同款短袖与短裤，自然的运动服剪裁。';
      const box=card.querySelector('.school-art');
      try{
        const im=await picture(look.file);if(current!==generation)return;
        const canvas=document.createElement('canvas');canvas.setAttribute('role','img');canvas.setAttribute('aria-label',`${entry.name} ${label} 短袖上衣与膝上运动短裤`);canvas.dataset.source=look.file;
        const cell=look.cell||{x:look.frame%look.columns*im.naturalWidth/look.columns,y:Math.floor(look.frame/look.columns)*im.naturalHeight/look.rows,width:im.naturalWidth/look.columns,height:im.naturalHeight/look.rows};
        const w=Math.max(1,box.clientWidth-36),h=Math.max(1,box.clientHeight-36),dpr=Math.min(devicePixelRatio||1,2);
        canvas.width=Math.round(w*dpr);canvas.height=Math.round(h*dpr);canvas.style.width='100%';canvas.style.height='100%';
        const ctx=canvas.getContext('2d');ctx.scale(dpr,dpr);ctx.imageSmoothingQuality='high';const scale=Math.min(w/cell.width,h/cell.height);
        ctx.drawImage(im,cell.x,cell.y,cell.width,cell.height,(w-cell.width*scale)/2,(h-cell.height*scale)/2,cell.width*scale,cell.height*scale);
        box.replaceChildren(canvas);
        const link=card.querySelector('a');link.href='../DeepSeek-demo.html?outfit=sports';link.textContent='查看动作 ↗';link.title='DeepSeek 两种风格的完整运动服动作；其他角色可在桌面程序切换';
      }catch(error){if(current===generation){box.textContent='运动服图片未能读取，请重新安装 Demo。';console.error(error);}}
    }
  }
  for(const entry of roster){const b=document.createElement('button');b.type='button';b.dataset.family=entry.family;b.textContent=entry.name;b.onclick=()=>show(entry);tabs.appendChild(b);}
  let resizeTimer;window.addEventListener('resize',()=>{clearTimeout(resizeTimer);resizeTimer=setTimeout(()=>show(roster.find(r=>r.family===root.dataset.family)||roster[0]),120);});
  show(roster[0]);
})();
