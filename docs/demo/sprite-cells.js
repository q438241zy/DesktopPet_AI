/* Render a source crop with the same ownership mask as the native sprite loader.
   Source PNGs and their original per-pixel alpha remain untouched. */
globalThis.SpriteCells=(()=>{
  const cache=new WeakMap();
  function crop(image,rect,ownership){
    if(!ownership)return null;
    let frames=cache.get(image);if(!frames){frames=new Map();cache.set(image,frames);}
    const key=rect.join(',');if(frames.has(key))return frames.get(key);
    const [x,y,w,h]=rect,canvas=document.createElement('canvas');canvas.width=w;canvas.height=h;
    const ctx=canvas.getContext('2d');ctx.drawImage(image,x,y,w,h,0,0,w,h);
    const mask=document.createElement('canvas');mask.width=w;mask.height=h;
    const mc=mask.getContext('2d'),pixels=mc.createImageData(w,h),bits=Uint8Array.from(atob(ownership),c=>c.charCodeAt(0));
    if(bits.length!==Math.ceil(w*h/8))throw Error('Sprite ownership dimensions do not match its source crop');
    for(let i=0;i<w*h;i++)if(bits[i>>3]&(128>>(i&7)))pixels.data[i*4+3]=255;
    mc.putImageData(pixels,0,0);ctx.globalCompositeOperation='destination-in';ctx.drawImage(mask,0,0);ctx.globalCompositeOperation='source-over';
    frames.set(key,canvas);return canvas;
  }
  return {crop};
})();
