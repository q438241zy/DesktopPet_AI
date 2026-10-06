(function(root,factory){const api=factory();if(typeof module==='object'&&module.exports)module.exports=api;else root.ClubModel=api;})(globalThis,()=>{
  const actions={
    idle:{title:'待机',clip:'idle',duration:2600},chat:{title:'聊天',clip:'listen',duration:Infinity},
    checkin:{title:'早餐',clip:'checkin',duration:4200},snack:{title:'零食',clip:'snack',duration:3000},
    headpat:{title:'摸头',clip:'headpat',duration:2600},poke:{title:'揉脸',clip:'poke',duration:2600},tickle:{title:'挠痒',clip:'tickle',duration:2200},
    praise:{title:'夸夸',clip:'praise',duration:4800},comfort:{title:'安抚',clip:'comfort',duration:4200},lullaby:{title:'哄睡',clip:'lullaby',duration:5300},
    comb:{title:'梳头',clip:'comb',duration:4800,fresh:true,group:'care',hint:'慢慢理顺头发，最后轻轻收起梳子。'},
    wipe:{title:'擦脸',clip:'wipe',duration:4200,fresh:true,group:'care',hint:'拿起柔软的小毛巾，轻轻擦脸。'},
    stretch:{title:'伸懒腰',clip:'listen',duration:5200,fresh:true,group:'care',hint:'抬起双手，向上伸展，再慢慢放下来。'},
    ball:{title:'玩球',clip:'ball',duration:6200},blocks:{title:'积木',clip:'blocks',duration:4400},walk:{title:'散步',clip:'walk',duration:Infinity},peek:{title:'躲藏',clip:'walk',duration:Infinity},
    bubbles:{title:'吹泡泡',clip:'listen',duration:14400,fresh:true,group:'play',hint:'举起泡泡棒，轻轻吹气，再收手。点泡泡可以戳破。'},
    stars:{title:'数星星',clip:'listen',duration:9600,fresh:true,group:'play',hint:'跟着手指一颗颗数：1、2、3、4、5。点星星也能请她再数一遍。'},
    butterfly:{title:'捉蝴蝶',clip:'walk',duration:15000,fresh:true,group:'play',hint:'点击空处引导蝴蝶，伙伴会追过去；停下后让它落在手心。'},
    think:{title:'发呆',clip:'think',duration:3200},jump:{title:'跳跃',clip:'jump',duration:1400},curl:{title:'蜷起',clip:'curl',duration:3000},bonk:{title:'轻敲',clip:'bonk',duration:1900},rest:{title:'休息',clip:'rest',duration:Infinity},
    found:{title:'找到啦',clip:'found',duration:2000},pickup:{title:'提起',clip:'pickup',duration:Infinity},place:{title:'放置',clip:'place',duration:Infinity}
  };
  const menus={root:['chat','care','play','interaction','rest','member','close'],
    care:['comb','wipe','stretch','checkin','snack','headpat','poke','tickle','praise','comfort','lullaby'],
    play:['bubbles','stars','butterfly','ball','blocks','walk','peek'],interaction:['think','jump','peek','curl','bonk']};
  const labels={care:'照顾',play:'玩耍',interaction:'互动',member:'会员',close:'收起',back:'返回'};
  function menu(group,style){return [...(menus[group]||menus.root)];}
  function allowed(){return true;} // This proposal is deliberately open for every tier during testing.
  function create(width=760){return{action:'idle',elapsed:0,age:0,x:width/2,dir:1,phase:0,menu:null,menuPoint:null,hideStage:null,hideSide:null,hiddenFor:0,hideHistory:[],walkTurn:0,paused:false,style:'realistic',tier:0,loop:false,target:null,score:0,edgePadding:110};}
  function start(s,key,random=Math.random){
    if(!actions[key])return false;
    Object.assign(s,{action:key,elapsed:0,phase:0,hideStage:null,hiddenFor:0,walkTurn:0,target:null,score:0});
    if(key==='peek'){s.hideSide=random()<.5?-1:1;s.dir=s.hideSide;s.hideStage='going';s.hideHistory.push(s.hideSide);if(s.hideHistory.length>30)s.hideHistory.shift();}
    return true;
  }
  function toggleMenu(s,point){s.menu=s.menu?null:'root';if(s.menu)s.menuPoint={...point};}
  function find(s){if(s.action!=='peek')return false;s.hideStage='returning';s.dir=-s.hideSide;s.hiddenFor=0;return true;}
  function move(s,target,dt,speed=68){const before=s.x,delta=target-s.x;if(Math.abs(delta)>.1)s.dir=Math.sign(delta);s.x+=Math.sign(delta)*Math.min(Math.abs(delta),speed*dt/1000);s.phase+=Math.abs(s.x-before)/speed*1000;return Math.abs(target-s.x)<.1;}
  function tick(s,dt,width,speed=68){
    if(s.paused)return;dt=Math.max(0,Math.min(dt,250));s.elapsed+=dt;s.age+=dt;
    if(s.action==='peek'){
      if(s.hideStage==='going'&&move(s,s.hideSide<0?-36:width+36,dt,speed))s.hideStage='hidden';
      else if(s.hideStage==='hidden')s.hiddenFor+=dt;
      else if(s.hideStage==='returning'&&move(s,s.hideSide<0?s.edgePadding:width-s.edgePadding,dt,speed))start(s,'found');
    }else if(s.action==='walk'){
      if(s.walkTurn>0){s.walkTurn=Math.max(0,s.walkTurn-dt);if(!s.walkTurn)s.dir*=-1;return;}
      if(move(s,s.dir<0?s.edgePadding:width-s.edgePadding,dt,speed))s.walkTurn=150;
    }else if(s.action==='butterfly'&&s.target!=null){move(s,s.target,dt*.82,speed);}
    const duration=actions[s.action].duration;
    if(s.elapsed>duration){const old=s.action;if(s.loop&&actions[old].fresh)start(s,old);else start(s,old==='lullaby'?'rest':'idle');}
  }
  return{actions,menus,labels,menu,allowed,create,start,toggleMenu,find,tick};
});
