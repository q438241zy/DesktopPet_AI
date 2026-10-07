// Keep the native embedded profiles identical to the approved HTML personalities.
const fs=require('node:fs'),path=require('node:path');
const root=path.resolve(__dirname,'..'),p=require('../docs/demo/companion-v01/personas.js'),target=path.join(root,'src/DesktopPet.Core/Data/companion-personas.json');
fs.mkdirSync(path.dirname(target),{recursive:true});
fs.writeFileSync(target,JSON.stringify({characters:p.all,reminderTasks:p.reminderTasks},null,2)+'\n');
console.log('Synced eight approved companion profiles.');
