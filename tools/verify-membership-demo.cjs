const fs=require('node:fs'),vm=require('node:vm'),assert=require('node:assert/strict');
const html=fs.readFileSync(process.argv[2],'utf8');
const D=JSON.parse(html.match(/<script id="data" type="application\/json">([\s\S]*?)<\/script>/)[1]);
const script=[...html.matchAll(/<script(?:\s[^>]*)?>([\s\S]*?)<\/script>/g)].at(-1)[1];
assert.deepEqual(D.membership.tiers.map(t=>t.name),['黑金','白金','黄金','白银','黄铜']);
assert.equal(D.membership.requirements.chat,2);
assert.equal(D.coverage.filter(r=>r.action==='chat'&&r.detail.includes('黄金及以上')).length,48);
function node(){return{value:'4',className:'',children:[],attributes:{},setAttribute(k,v){this.attributes[k]=v},appendChild(c){this.children.push(c)},replaceChildren(...c){this.children=c}};}
const elements={memberTier:node(),memberNote:node(),actions:node(),outfits:node()};
const state={composer:{hidden:true},input:{disabled:false},pet:{style:{}},detail:{textContent:''},reply:{textContent:''},arena:{clientWidth:560},y:300,action:'idle'};
const context={D,$:id=>elements[id],states:[state],action:'idle',sequence:0,outfit:'original',clothes:[['original','原服']],
 document:{createElement:()=>node()},button:(label,selected,click)=>({...node(),textContent:label,onclick:click}),
 setAction:(s,key)=>s.action=key,place:()=>{},floor:()=>300,matrix:()=>{}};
vm.createContext(context);
vm.runInContext(script.slice(script.indexOf('function memberAllows'),script.indexOf('function renderButtons')),context);
vm.runInContext(script.match(/^function renderButtons\(\).*$/m)[0],context);
for(const tier of D.membership.tiers){
 elements.memberTier.value=String(tier.value);context.memberPreviewChanged();context.renderButtons();
 const button=elements.actions.children.find(b=>b.textContent==='頭頂聊天');const allowed=tier.value>=2;
 assert.equal(button.className==='locked',!allowed);assert.equal(button.children.some(c=>c.innerHTML?.includes('<svg')),!allowed);
 button.onclick();assert.equal(state.composer.hidden,!allowed);assert.equal(state.action,allowed?'listen':'idle');
 for(const pose of ['headpat','walk','dance','praise','comfort','lullaby'])assert.equal(context.memberAllows(pose),true);
}
elements.memberTier.value='4';context.run('chat');assert.equal(state.composer.hidden,false);
const previous=context.sequence;elements.memberTier.value='0';context.memberPreviewChanged();
assert.equal(state.composer.hidden,true);assert.equal(state.action,'idle');assert.ok(context.sequence>previous,'changing tiers cancels an old reply');
console.log('PASS: five membership tiers, actual locked buttons, chat entry checks, pending-response cancellation and 48 coverage notes.');
