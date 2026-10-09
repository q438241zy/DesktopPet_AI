/* Export the already-reviewed HTML poses for independent native comparison. */
const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm');
const root=path.resolve(__dirname,'..'),demo=path.join(root,'Release/win-x64/Demo/CompanionV01/data.js');
const output=path.resolve(process.argv[2]||path.join(root,'.artifacts/idle-posture-review'));
if(!output.startsWith(path.join(root,'.artifacts')+path.sep))throw new Error('Use an isolated output directory below .artifacts.');
const context={};vm.runInNewContext(fs.readFileSync(demo,'utf8'),context,{timeout:5000});
const references={};let count=0;
for(const family of Object.values(context.CLOUD_DATA.families))for(const style of Object.values(family.styles)){
 const character=references[style.id]={};
 for(const [name,look] of Object.entries(style.looks)){
  character[name]={};
  for(const pose of ['stand','sit']){
   const drawing=look[pose],frame=drawing.frames[0],cell=drawing.cells[frame];
   character[name][pose]={file:drawing.file,frame,reference:drawing.reference,scale:cell.scale,footX:cell.footX,footY:cell.footY};
  }
  count++;
 }
}
if(count!==64)throw new Error('Expected the approved 64 appearances.');
fs.mkdirSync(output,{recursive:true});fs.writeFileSync(path.join(output,'approved-demo.json'),JSON.stringify(references,null,2));
console.log('Exported 64 appearances / 128 approved idle poses.');
