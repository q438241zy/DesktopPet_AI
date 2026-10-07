(function(root,factory){const value=factory();if(typeof module==='object'&&module.exports)module.exports=value;else root.CompanionModel=value;})(globalThis,function(){
  'use strict';
  const A=typeof module==='object'&&module.exports?require('./providers.js'):globalThis.CompanionProviders;
  const families=['whale','gpt','claude','gemini','grok','qwen','zhipu','kimi'];
  const outfits={original:{name:'原装',score:0},sports:{name:'运动服',score:20},swim:{name:'泳装',score:50},wedding:{name:'婚纱',score:80}};
  const clamp=(n,a,b)=>Math.max(a,Math.min(b,Number.isFinite(n)?n:0));
  const day=now=>{const d=new Date(now);return `${d.getFullYear()}-${String(d.getMonth()+1).padStart(2,'0')}-${String(d.getDate()).padStart(2,'0')}`;};
  const validDate=value=>typeof value==='string'&&/^\d{4}-\d{2}-\d{2}$/.test(value)&&day(new Date(value+'T12:00:00'))===value;
  const relation=()=>({score:0,seconds:0,interactions:0,history:[],awards:{},daily:{},companionshipAwarded:0});
  function create(saved){
    const s={schema:1,selected:'whale',style:'chibi',outfits:{},relations:Object.fromEntries(families.map(id=>[id,relation()])),checkins:[],makeupCards:0,makeupCheckins:[],collection:{},stories:{},settings:{autoHide:true,reducedMotion:false,calendarView:'month'},api:A.cleanConfig(),work:{enabled:false,minutes:10}};
    if(!saved||saved.schema!==1)return s;
    if(families.includes(saved.selected))s.selected=saved.selected;if(saved.style==='realistic')s.style='realistic';
    for(const id of families){
      const r=saved.relations?.[id];if(r&&typeof r==='object')s.relations[id]={...relation(),score:clamp(Number(r.score),-100,100),seconds:clamp(Number(r.seconds),0,315360000),interactions:clamp(Number(r.interactions),0,10000000),history:Array.isArray(r.history)?r.history.filter(e=>typeof e.label==='string'&&Number.isFinite(e.at)&&Number.isFinite(e.delta)).slice(-40):[],awards:r.awards&&typeof r.awards==='object'?r.awards:{},daily:r.daily&&typeof r.daily==='object'?r.daily:{},companionshipAwarded:clamp(Number(r.companionshipAwarded),0,1000000)};
      for(const style of ['chibi','realistic']){const k=id+'/'+style;if(Object.hasOwn(outfits,saved.outfits?.[k]))s.outfits[k]=saved.outfits[k];}
    }
    s.checkins=Array.isArray(saved.checkins)?[...new Set(saved.checkins.filter(validDate))].sort():[];
    s.makeupCards=Math.floor(clamp(Number(saved.makeupCards),0,99999));
    s.makeupCheckins=Array.isArray(saved.makeupCheckins)?[...new Set(saved.makeupCheckins.filter(date=>s.checkins.includes(date)))].sort():[];
    for(const [key,value] of Object.entries(saved.collection||{}))if(/^[a-z-]+$/.test(key)&&Number.isFinite(value))s.collection[key]=clamp(value,1,99999);
    for(const [key,value] of Object.entries(saved.stories||{}))if(['cloud-post','little-bell','star-seed'].includes(key)&&Number.isFinite(value))s.stories[key]=clamp(value,1,99999);
    if(saved.settings){s.settings.autoHide=saved.settings.autoHide!==false;s.settings.reducedMotion=!!saved.settings.reducedMotion;s.settings.calendarView=saved.settings.calendarView==='week'?'week':'month';}
    s.api=A.cleanConfig(saved.api);
    s.work.minutes=Math.round(clamp(Number(saved.work?.minutes)||10,1,180));s.work.enabled=saved.work?.enabled===true;
    return s;
  }
  function affect(s,id,delta,label,now,key='',cooldown=0){
    const r=s.relations[id];if(!r)return 0;
    if(key&&r.awards[key]!==undefined&&now-r.awards[key]<cooldown)return 0;
    const date=day(now),earned=Number(r.daily[date])||0;if(delta>0)delta=Math.min(delta,Math.max(0,20-earned));
    const before=r.score;r.score=clamp(r.score+delta,-100,100);const actual=r.score-before;
    if(key)r.awards[key]=now;if(delta>0)r.daily[date]=earned+actual;
    if(actual){r.interactions++;r.history.push({label,delta:actual,at:now});r.history=r.history.slice(-40);}
    for(const dateKey of Object.keys(r.daily))if(dateKey<day(now-32*86400000))delete r.daily[dateKey];
    return actual;
  }
  function checkin(s,now){const date=day(now);if(s.checkins.includes(date))return false;s.checkins.push(date);affect(s,s.selected,3,'一起打卡',now,'checkin-'+date,86400000);return true;}
  function makeup(s,date,now){
    if(!validDate(date)||date>=day(now)||s.checkins.includes(date)||!Number.isInteger(s.makeupCards)||s.makeupCards<1)return false;
    s.makeupCards--;s.checkins.push(date);s.checkins.sort();s.makeupCheckins.push(date);s.makeupCheckins.sort();return true;
  }
  function streak(s,now){const date=new Date(now);date.setHours(12,0,0,0);if(!s.checkins.includes(day(date)))date.setDate(date.getDate()-1);let n=0;while(s.checkins.includes(day(date))){n++;date.setDate(date.getDate()-1);}return n;}
  function companion(s,id,seconds,now){const r=s.relations[id];r.seconds+=clamp(seconds,0,2);const reached=Math.floor(r.seconds/300);if(reached>r.companionshipAwarded){r.companionshipAwarded=reached;affect(s,id,1,'安静陪伴五分钟',now,'time',300000);}}
  function label(score){return score<=-60?'需要一些空间':score<0?'慢慢修复默契':score<20?'初次相遇':score<50?'渐渐熟悉':score<80?'默契伙伴':'亲密搭档';}
  const endpoint=A.endpoint,request=A.request;
  function createRuntime(now){return {paused:false,action:'idle',started:now,until:0,idleSince:now,hideSide:'left',workActive:false,nextReminder:0,reminderCount:0,workRemaining:0};}
  function startHide(r,now,side){if(r.action==='hidden'||r.action==='hiding')return false;r.action='hiding';r.started=now;r.hideSide=side;r.until=now+2200;return true;}
  function tick(r,s,now,{canHide=true}={}){
    if(r.paused)return [];
    const events=[];
    if(r.action==='hiding'&&now>=r.until){r.action='hidden';r.started=now;events.push('hidden');}
    if(['smile','pat','found','talk'].includes(r.action)&&now>=r.until){r.action='idle';r.idleSince=now;events.push('idle');}
    if(s.settings.autoHide&&canHide&&r.action==='idle'&&now-r.idleSince>=60000){startHide(r,now,Math.random()<.5?'left':'right');events.push('auto-hide');}
    if(r.workActive&&now>=r.nextReminder){const interval=s.work.minutes*60000;r.nextReminder+=Math.max(1,Math.floor((now-r.nextReminder)/interval)+1)*interval;r.reminderCount++;events.push('reminder');}
    return events;
  }
  function find(r,now){if(!['hidden','hiding'].includes(r.action))return false;r.action='found';r.started=now;r.until=now+1700;r.idleSince=now;return true;}
  function workStart(r,s,now){s.work.enabled=true;r.workActive=true;r.nextReminder=now+s.work.minutes*60000;r.workRemaining=s.work.minutes*60000;}
  function workStop(r,s){s.work.enabled=false;r.workActive=false;r.nextReminder=0;r.workRemaining=0;}
  function pause(r,now){r.paused=!r.paused;if(r.paused)r.workRemaining=Math.max(0,r.nextReminder-now);else{if(r.workActive)r.nextReminder=now+r.workRemaining;r.idleSince=now;}}
  return {families,outfits,clamp,day,create,affect,checkin,makeup,streak,companion,label,endpoint,request,createRuntime,startHide,tick,find,workStart,workStop,pause};
});
