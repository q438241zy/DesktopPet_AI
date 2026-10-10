(function(root,factory){const value=factory();if(typeof module==='object'&&module.exports)module.exports=value;else root.CompanionPlay=value;})(globalThis,function(){
 'use strict';
 const titles={chat:'聊天',gift:'拆礼物',read:'一起阅读',butterfly:'捉蝴蝶',highfive:'击掌',rps:'猜拳',ball:'玩球',blocks:'堆积木',walk:'散步',peek:'躲藏',bubbles:'吹泡泡',stars:'数星星',think:'发呆',jump:'跳跃',curl:'蜷起',bonk:'轻敲',checkin:'吃饭',snack:'吃零食',headpat:'摸头',poke:'揉脸',tickle:'挠痒',praise:'夸夸',comfort:'安抚',lullaby:'哄睡',comb:'梳头',wipe:'擦脸',stretch:'伸懒腰',rest:'休息'};
 const groups={play:['gift','read','butterfly','highfive','rps','ball','blocks','walk','peek','bubbles','stars','think','jump','curl','bonk'],interaction:['checkin','snack','headpat','poke','tickle','praise','comfort','lullaby','comb','wipe','stretch','rest']};
 const clips={checkin:'meal',snack:'feed',blocks:'build',praise:'happy',comfort:'headpat',lullaby:'curl',rest:'curl',ball:'ball-ready'};
 const clamp=(x,a=0,b=1)=>Math.max(a,Math.min(b,x)),ease=x=>(1-Math.cos(Math.PI*clamp(x)))/2;
 const lineDuration=text=>Math.max(3600,Math.min(7600,Array.from(text).length*155+1100));
 function choose(items,last,random){const pool=items.filter(x=>x.id!==last);return (pool.length?pool:items)[Math.min((pool.length||items.length)-1,Math.floor(random()*(pool.length||items.length)))];}
 class Session{
  constructor({stories,items,random=Math.random}){this.stories=stories;this.items=items;this.random=random;this.lastStory='';this.giftBag=[];this.cancel();}
  cancel(){this.active=false;this.key='';this.time=0;this.events=[];this.rewarded=false;this.done=false;this.distance=0;this.x=0;this.direction=1;this.choice=null;this.contactAt=null;this.revealAt=null;this.randomResult=undefined;this.view=null;this.story=null;this.prize=null;}
  start(key,{storyId}={}){
   this.cancel();if(!titles[key]||key==='peek'||key==='chat')return false;this.active=true;this.key=key;
   if(key==='gift'){
    if(!this.giftBag.length)this.giftBag=[...this.items];
    this.prize=this.giftBag.splice(Math.min(this.giftBag.length-1,Math.floor(this.random()*this.giftBag.length)),1)[0];
   }
   if(key==='read'){
    this.story=this.stories.find(x=>x.id===storyId)||choose(this.stories,this.lastStory,this.random);this.lastStory=this.story.id;
    let start=0;this.lines=this.story.sentences.map(text=>{const line={start,end:start+lineDuration(text),text};start=line.end+650;return line;});this.end=start-650+850;
   }
   this.view=this.frame();return true;
  }
  highfive(){if(!this.active||this.key!=='highfive'||this.time<550||this.contactAt!==null)return false;this.contactAt=this.time;return true;}
  throw(choice){if(!this.active||this.key!=='rps'||this.choice||!['rock','scissors','paper'].includes(choice))return false;this.choice=choice;this.petChoice=['rock','scissors','paper'][Math.min(2,Math.floor(this.random()*3))];this.revealAt=this.time+1800;return true;}
  award(kind,id){if(this.rewarded)return;this.rewarded=true;this.events.push({kind,id});}
  advance(ms){
   if(!this.active||!Number.isFinite(ms)||ms<0)return;
   this.time+=ms;this.view=this.frame();
   if(this.key==='gift'&&this.time>=3050)this.award('gift',this.prize.id);
   if(this.key==='read'&&this.time>=this.end)this.award('story',this.story.id);
   if(this.view.done){this.done=true;this.active=false;}
  }
  takeEvents(){return this.events.splice(0);}
  frame(){
   const t=this.time,key=this.key,v={pose:clips[key]||key,elapsed:t,offset:0,direction:1,phase:'playing',bubble:'',done:false};
   if(key==='gift'){
    v.phase=t<1150?'giving':t<2150?'holding':t<3050?'opening':'opened';
    v.pose=t<1150?'receive':t<2500?'gift':t<4800?'gift-empty':'happy';
    v.giving=ease(t/1150);v.prize=this.prize;v.reveal=clamp((t-3050)/550);v.bubble=t<1150?'我来接住。':t<3050?'拆开看看。':'谢谢你的'+this.prize.name+'！';v.done=t>=6100;
   }else if(key==='read'){
    let i=this.lines.findIndex(l=>t<l.end+650);if(i<0)i=this.lines.length-1;const l=this.lines[i],turn=t>=l.end&&i<this.lines.length-1;
    v.phase=t>=this.end?'finished':turn?'turning':'reading';v.pose=t>=this.end-850?'read-finish':turn?'page':'read';v.bubble=t>=this.end?'读完啦，收进我们的故事区。':l.text;v.line=i;v.title=this.story.title;v.total=this.lines.length;v.progress=clamp(t/this.end);v.done=t>=this.end+2400;
   }else if(key==='butterfly'||key==='walk'){
    const points=key==='butterfly'?[0,.82,-.82,.65,-.6,0]:[0,.85,-.85,0],step=key==='butterfly'?2300:2600,seg=Math.min(points.length-2,Math.floor(t/step)),u=clamp((t-seg*step)/step);
    const pos=points[seg]+(points[seg+1]-points[seg])*ease(u),delta=pos-this.x;this.distance+=Math.abs(delta);this.x=pos;
    v.offset=pos;v.direction=Math.abs(delta)>.0000001?Math.sign(delta):this.direction;this.direction=v.direction;
    const finishing=t>=step*(points.length-1);v.pose=finishing?'stand':'walk';v.gait=this.distance;v.phase=finishing?'resting':'chasing';
    v.butterfly={x:clamp(pos+v.direction*.32,-1.05,1.05),y:Math.sin(t/680)*12,wing:Math.cos(t/80)};
    v.bubble=key==='butterfly'?(finishing?'让它歇一会儿吧。':''):'出去走走。';v.done=t>=step*(points.length-1)+1400;
   }else if(key==='highfive'){
    const c=this.contactAt===null?-1:t-this.contactAt;
    v.pose=c<0?(t<550?'high-prep':'high-ready'):c<200?'high-contact':c<500?'high-recoil':'happy';v.phase=c<0?'waiting':'contact';v.bubble=c<0?'碰一下我的手心。':'啪，默契满分！';v.done=c>=2200;
   }else if(key==='rps'){
    const pending=this.revealAt===null,ready=!pending&&t>=this.revealAt;
    v.pose=ready?'reveal-'+this.petChoice:pending?'fist-up':['fist-up','fist-mid','fist-down','fist-mid'][Math.floor(t/150)%4];v.phase=pending?'choose':ready?'reveal':'pump';v.user=ready?this.choice:'rock';
    const beats={rock:'scissors',scissors:'paper',paper:'rock'};v.bubble=pending?'选好手势，我们一起出。':ready?(this.choice===this.petChoice?'平手！':beats[this.choice]===this.petChoice?'你赢啦！':'这次是我赢啦。'):['石头','剪刀','布'][Math.min(2,Math.floor((t-this.revealAt+1800)/600))];v.done=ready&&t-this.revealAt>=2800;
   }else{
    v.bubble={checkin:'一起好好吃饭。',snack:'小点心，好香。',rest:'歇一会儿。',lullaby:'晚安。',praise:'听到啦，好开心。',comfort:'有你在就安心。'}[key]||'';
    if(key==='ball')v.pose=t<1400?'ball-ready':t<3100?(this.randomResult??=(this.random()<.5))?'ball-hit':'ball-miss':'happy';
    if(key==='stars')v.bubble=['一颗','两颗','三颗','四颗','五颗'][Math.min(4,Math.floor(t/1100))]+'星星。';
    v.done=t>=(['rest','lullaby'].includes(key)?10000:6200);
   }
   return v;
  }
 }
 return {titles,groups,clips,Session,lineDuration};
});
