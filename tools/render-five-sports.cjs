// Arrange untouched native screenshots for visual review of all sports interactions.
const fs=require('node:fs'),path=require('node:path'),{pathToFileURL}=require('node:url');
let pw;try{pw=require('playwright');}catch{pw=require('C:/Users/99000256/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');}
const root=path.resolve(__dirname,'..'),folder=path.resolve(root,process.argv[2]||'.artifacts/sports-final-20261009');
if(!folder.startsWith(path.join(root,'.artifacts')+path.sep))throw Error('Expected an isolated review directory');
(async()=>{const browser=await pw.chromium.launch({headless:true});
try{const page=await browser.newPage({viewport:{width:1340,height:800}});
for(const family of ['whale','gpt','claude','gemini','grok','qwen','zhipu','kimi']){
 const ids=[family,family==='whale'?'deepseek-adult':family+'-adult'];
 const names=ids.flatMap(id=>['highfive','rps','gift','read','photo'].map(action=>id+'-sports-'+action+'.png'));
 for(const name of names)if(!fs.existsSync(path.join(folder,name)))throw Error('Missing native capture: '+name);
 const html=path.join(folder,'five-'+family+'-gallery.html');
 fs.writeFileSync(html,'<!doctype html><meta charset="utf-8"><title>Five sports review</title><style>body{font:14px sans-serif;background:#eee9ed;margin:12px}main{display:grid;grid-template-columns:repeat(5,250px);gap:12px}figure{margin:0;background:#fffcf8;border-radius:10px;overflow:hidden}section{position:relative;width:250px;height:320px;overflow:hidden}img{position:absolute;width:560px;height:680px;max-width:none;left:-155px;top:-180px}figcaption{padding:8px;text-align:center;font-size:12px}</style><h1>'+family+' / sports</h1><main>'+names.map(n=>'<figure><section><img src="'+pathToFileURL(path.join(folder,n)).href+'"></section><figcaption>'+n.replace('.png','')+'</figcaption></figure>').join('')+'</main>');
 await page.goto(pathToFileURL(html).href);await page.evaluate(()=>Promise.all([...document.images].map(i=>i.decode())));
 await page.screenshot({path:path.join(folder,'five-'+family+'-gallery.png'),fullPage:true});
}
console.log('Eight galleries saved: 16 appearances, 80 unmodified native screenshots.');
}finally{await browser.close();}})().catch(e=>{console.error(e);process.exit(1);});
