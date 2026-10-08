// Diagnostic overlays only: source PNG files are never modified.
const fs=require('node:fs'),path=require('node:path'),{pathToFileURL}=require('node:url');
let pw;try{pw=require('playwright');}catch{pw=require('C:/Users/99000256/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');}
const root=path.resolve(__dirname,'..'),out=path.join(root,'.artifacts/sports-identity-review');
(async()=>{const browser=await pw.chromium.launch({headless:true,...(process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE?{executablePath:process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE}:{})});
try{const page=await browser.newPage({viewport:{width:1100,height:800}});
for(const key of process.argv.slice(2)){
 const job=JSON.parse(fs.readFileSync(path.join(root,'artwork/sports-identity/results',key+'.json'),'utf8'));
 const html=path.join(out,key+'-anchors.html');
 fs.writeFileSync(html,'<!doctype html><meta charset="utf-8"><title>接触点复核</title><style>body{background:#f5f2f4;font:14px sans-serif}main{display:grid;grid-template-columns:repeat(4,250px);gap:12px}article{background:white;padding:8px;border-radius:12px}canvas{width:230px;height:230px}</style><h1>'+key+'</h1><main></main><script>window.job='+JSON.stringify({...job,imageUrl:pathToFileURL(path.join(root,job.source)).href})+';</script>');
 await page.goto(pathToFileURL(html).href);
 await page.evaluate(async()=>{const im=new Image();im.src=job.imageUrl;await im.decode();
  for(const [field,points] of Object.entries(job.measurementPixels||{}))points.forEach((p,index)=>{if(!p)return;
   const article=document.createElement('article'),canvas=document.createElement('canvas');canvas.width=canvas.height=460;
   const ctx=canvas.getContext('2d'),extent=field==='hands'?200:130,scale=460/extent;
   ctx.fillStyle='#eee9ed';ctx.fillRect(0,0,460,460);ctx.drawImage(im,p[0]-extent/2,p[1]-extent/2,extent,extent,0,0,460,460);
   ctx.strokeStyle='#ff1676';ctx.lineWidth=1.5;ctx.beginPath();ctx.arc(230,230,7,0,Math.PI*2);ctx.moveTo(215,230);ctx.lineTo(245,230);ctx.moveTo(230,215);ctx.lineTo(230,245);ctx.stroke();
   if(p.length===3){ctx.strokeStyle='#087f8c';ctx.strokeRect(230-p[2]*scale/2,230-p[2]*scale/2,p[2]*scale,p[2]*scale);}
   article.append(canvas);const caption=document.createElement('div');caption.textContent=field+' '+index+' — '+p.join(', ');article.append(caption);document.querySelector('main').append(article);
  });
 });
 await page.screenshot({path:path.join(out,key+'-anchors.png'),fullPage:true});console.log(key+' contact markers rendered');
}
}finally{await browser.close();}})().catch(error=>{console.error(error);process.exit(1);});
