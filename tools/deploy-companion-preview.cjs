/* Update review files only. Preserve native installation and existing base art geometry. */
const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm'),assert=require('node:assert/strict');
const root=path.resolve(__dirname,'..'),src=path.join(root,'docs/demo/companion-v01'),dest=path.join(root,'Release/win-x64/Demo/CompanionV01');
const xml=fs.readFileSync(path.join(root,'Version.props'),'utf8'),version=['Major','Minor'].map(k=>xml.match(new RegExp(`<DesktopPet${k}>([^<]+)</DesktopPet${k}>`))[1]).join('.');
const names=['index.html','style.css','icons.js','personas.js','providers.js','model.js','agenda.js','agenda-ui.js','agenda.css','app.js','idle-art.js','idle-postures.js','preview-art.js','claude-review.html','story-library.js','play-model.js','play-ui.js','play.css','play-art.js'];
for(const name of names)fs.copyFileSync(path.join(src,name),path.join(dest,name));
fs.copyFileSync(path.join(root,'docs/demo/sprite-cells.js'),path.join(dest,'../sprite-cells.js'));
for(const folder of [src,dest]){
 const file=path.join(folder,'data.js'),box={};vm.runInNewContext(fs.readFileSync(file,'utf8'),box);const data=box.CLOUD_DATA;
 // Remove obsolete export payload references, not user-created images.
 for(const f of Object.values(data.families))for(const s of Object.values(f.styles))for(const l of Object.values(s.looks)){delete l.photo;delete l.photoData;}
 const before=JSON.stringify(data.families);data.version=version;data.stories=require('../docs/demo/companion-v01/story-library.js');assert.equal(JSON.stringify(data.families),before);
 fs.writeFileSync(file,'globalThis.CLOUD_DATA='+JSON.stringify(data)+';\n');
}
console.log('Deployed HTML review v'+version+'; native executable and user files untouched.');
