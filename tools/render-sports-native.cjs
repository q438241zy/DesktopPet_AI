// Review gallery of unmodified native screenshots; no artwork pixels are edited.
const fs=require('node:fs'),path=require('node:path'),{pathToFileURL}=require('node:url');
let pw;try{pw=require('playwright');}catch{pw=require('C:/Users/99000256/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');}
const root=path.resolve(__dirname,'..');
(async()=>{const browser=await pw.chromium.launch({headless:true});
try{const page=await browser.newPage({viewport:{width:1360,height:1000}});
for(const relative of process.argv.slice(2)){
 const folder=path.resolve(root,relative);if(!folder.startsWith(path.join(root,'.artifacts')+path.sep))throw Error('Review folder must be under .artifacts');
 const names=fs.readdirSync(folder).filter(n=>n.endsWith('.png')&&!n.includes('-dark')&&!n.startsWith('gallery'));
 const html=path.join(folder,'gallery.html');
 fs.writeFileSync(html,'<!doctype html><meta charset="utf-8"><title>Native sports review</title><style>body{font:14px sans-serif;background:#eee9ed;margin:12px}main{display:grid;grid-template-columns:repeat(4,320px);gap:12px}figure{margin:0;background:#fffcf8;border-radius:10px;overflow:hidden}section{position:relative;width:320px;height:300px;overflow:hidden}img{position:absolute;width:560px;height:680px;max-width:none;left:-120px;top:-200px}figcaption{padding:8px;text-align:center;font-size:12px}</style><h1>'+path.basename(folder)+'</h1><main>'+names.map(n=>'<figure><section><img src="'+pathToFileURL(path.join(folder,n)).href+'"></section><figcaption>'+n.replace('.png','')+'</figcaption></figure>').join('')+'</main>');
 await page.goto(pathToFileURL(html).href);await page.evaluate(()=>Promise.all([...document.images].map(i=>i.decode())));
 await page.screenshot({path:path.join(folder,'gallery.png'),fullPage:true});console.log(folder+' gallery saved');
}
}finally{await browser.close();}})().catch(error=>{console.error(error);process.exit(1);});
