// Frozen approved HTML descriptors for independent native rendering comparisons.
const fs=require('fs'),path=require('path'),vm=require('vm');
const root=path.resolve(__dirname,'..'),context={};vm.createContext(context);
const preview=path.resolve(process.argv[3]||path.join(root,'Release/win-x64/Demo/CompanionV01'));
for(const f of ['data.js','preview-art.js','idle-art.js'])vm.runInContext(fs.readFileSync(path.join(preview,f),'utf8'),context);
const output={};
for(const [family,f] of Object.entries(context.CLOUD_DATA.families))for(const [style,variant] of Object.entries(f.styles)){
 const outfits=output[variant.id]={};
 for(const [outfit,look] of Object.entries(variant.looks)){
  const poses=outfits[outfit]={},extra=context.CLOUD_IDLE_ART[family][style][outfit];
  for(const name of ['stand','sit','think','stretch','smile']){
   const d=extra[name]||look[name];if(!d)throw Error([family,style,outfit,name,Object.keys(look),Object.keys(extra)].join('/'));
   poses[name]={file:family==='claude'&&style==='chibi'&&outfit==='sports'&&name==='stand'?'outfits/sports/idle-stand-v18.png':d.file,reference:d.reference,frames:d.frames,frameMs:d.frameMs,cells:d.cells.map(c=>c?{footY:c.footY,scale:c.scale,width:c.width,height:c.height}:null)};
  }
 }
}
const target=path.resolve(process.argv[2]||path.join(root,'.artifacts/v18/idle/approved-demo.json'));fs.mkdirSync(path.dirname(target),{recursive:true});fs.writeFileSync(target,JSON.stringify(output));console.log(target);
