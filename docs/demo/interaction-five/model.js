/* Only the review's collection is saved; the native pet profile is untouched. */
(function(root,factory){const m=factory();if(typeof module==='object'&&module.exports)module.exports=m;else root.FiveModel=m;})(globalThis,function(){
  'use strict';
  const actions={
    highfive:{title:'击掌',hint:'拖动你的手碰她的手心；点手心或按回车也可以。',icon:'hand'},
    rps:{title:'猜拳',hint:'选好手势，双方一起摇拳，再同时出拳。',icon:'scissors'},
    gift:{title:'拆礼物',hint:'把礼物递到双手，拆开看看这次的小惊喜。',icon:'gift'},
    read:{title:'翻书共读',hint:'选一篇短故事，陪她读到最后。',icon:'book'},
    photo:{title:'合照',hint:'选一个相框，按快门，等她准备好。',icon:'camera'}
  };
  const choices=['rock','scissors','paper'],names={rock:'石头',scissors:'剪刀',paper:'布'},beats={rock:'scissors',scissors:'paper',paper:'rock'};
  const gifts=[
    ['baseball','棒球'],['basketball','篮球'],['football','足球'],['volleyball','排球'],['tennis','网球'],
    ['pingpong','乒乓球'],['badminton','羽毛球'],['rugby','橄榄球'],['golf','高尔夫球'],['bowling','保龄球'],
    ['bread','面包'],['rice','米饭'],['apple','苹果'],['banana','香蕉'],['cookie','饼干'],['milk','牛奶'],
    ['shell','贝壳'],['leaf','树叶'],['pencil','铅笔'],['key','钥匙']
  ].map(([id,name],i)=>({id,name,kind:i<10?'球类':'日常'}));
  const giftIds=gifts.map(g=>g.id),giftById=Object.fromEntries(gifts.map(g=>[g.id,g]));
  const stories=[
    {id:'cloud-post',title:'云朵邮差',sentences:[
      '小鲸在清晨收到一封没有地址的信。',
      '信里写着：“请把今天的快乐送给一个朋友。”',
      '她把信放进小包，沿着软软的云路出发。',
      '路边的小兔正在发愁，因为风吹走了她的花帽子。',
      '小鲸踮起脚，从树枝上取下帽子，轻轻递给小兔。',
      '小兔开心地笑了，邀请她一起吃刚烤好的小饼干。',
      '小鲸这才明白，快乐的地址，就是朋友的笑容。'
    ]},
    {id:'little-bell',title:'丢失的小铃铛',sentences:[
      '小猫最喜欢的铃铛不见了，她在窗边找了很久。',
      '小鲸听见叹气声，便拿着小灯过来帮忙。',
      '她们先看了桌子下面，又翻了柔软的坐垫。',
      '风吹动窗帘，角落里忽然响起一声轻轻的叮当。',
      '原来铃铛滚进了装毛线的小篮子。',
      '小猫把铃铛系好，尾巴开心地摇来摇去。',
      '为了感谢小鲸，她织了一条蓝色的小围巾。',
      '从那以后，每次铃铛响起，两位朋友都会想起这一天。'
    ]},
    {id:'star-seed',title:'星星种子',sentences:[
      '小鲸捡到一颗亮晶晶的种子，决定把它种在窗边。',
      '她每天给种子一点水，再轻轻说一声早安。',
      '过了几天，泥土里冒出一片小小的叶子。',
      '一个下雨的夜晚，小鲸担心叶子会冷，替它撑起小伞。',
      '第二天，叶子中间开出一朵像星星一样的小花。',
      '小鲸没有摘下花，而是把花盆搬到朋友们都能看见的地方。',
      '星星花慢慢长大，把窗边照得暖暖的。',
      '小鲸笑着说，原来耐心和关心，也能种出光。'
    ]}
  ];
  function restore(saved){
    const safe={counts:{},stories:{},bag:[...giftIds],last:null};if(!saved||saved.version!==1)return safe;
    for(const id of giftIds){const n=saved.counts?.[id];if(Number.isSafeInteger(n)&&n>0&&n<=100000)safe.counts[id]=n;}
    for(const {id} of stories){const n=saved.stories?.[id];if(Number.isSafeInteger(n)&&n>0&&n<=100000)safe.stories[id]=n;}
    if(Array.isArray(saved.bag)&&saved.bag.length<=giftIds.length&&saved.bag.every(id=>giftIds.includes(id))&&new Set(saved.bag).size===saved.bag.length)safe.bag=[...saved.bag];
    if(giftIds.includes(saved.last))safe.last=saved.last;return safe;
  }
  function create(saved){const shelf=restore(saved);return{
    action:'highfive',style:'chibi',phase:'offer',time:0,totalTime:0,revision:0,
    highCount:0,rounds:0,player:null,pet:null,nextPet:0,outcome:null,photos:0,frame:'cloud',completed:{},
    collection:shelf.counts,bag:shelf.bag,lastGift:shelf.last,collectionRevision:0,
    gifts:Object.values(shelf.counts).reduce((a,b)=>a+b,0),prize:null,pendingPrize:null,prizeIsNew:false,
    storyIndex:0,sentenceIndex:0,pendingSentence:0,books:Object.values(shelf.stories).reduce((a,b)=>a+b,0),finishedStories:{...shelf.stories},pausePhase:'reading'
  };}
  function start(s,action,randomValue=Math.random()){
    if(!actions[action])return false;s.action=action;s.time=0;s.revision++;s.player=null;s.pet=null;s.outcome=null;s.pendingPrize=null;s.prize=null;
    s.phase={highfive:'offer',rps:'choose',gift:'receive',read:'ready',photo:'compose'}[action];
    s.nextPet=Math.min(2,Math.max(0,Math.floor(randomValue*3)));s.sentenceIndex=0;s.pendingSentence=0;return true;
  }
  function highfive(s){if(s.action!=='highfive'||s.phase!=='offer'||s.time<750)return false;s.phase='approach';s.time=0;return true;}
  function choose(s,hand){if(s.action!=='rps'||s.phase!=='choose'||!choices.includes(hand))return false;s.player=hand;s.phase='countdown';s.time=0;return true;}
  function deliver(s){if(s.action!=='gift'||s.phase!=='receive')return false;s.phase='holding';s.time=0;return true;}
  function openGift(s,randomValue=Math.random()){
    if(s.action!=='gift'||s.phase!=='holding')return false;
    let bag=s.bag.length?[...s.bag]:[...giftIds];if(!s.bag.length&&s.lastGift&&bag.length>1)bag=bag.filter(id=>id!==s.lastGift);
    s.pendingPrize=bag[Math.min(bag.length-1,Math.max(0,Math.floor(randomValue*bag.length)))];s.phase='opening';s.time=0;return true;
  }
  function selectStory(s,id){const index=stories.findIndex(story=>story.id===id);if(s.action!=='read'||index<0)return false;s.storyIndex=index;s.sentenceIndex=0;s.time=0;s.phase='ready';s.revision++;return true;}
  function beginReading(s){if(s.action!=='read'||!['ready','finished'].includes(s.phase))return false;s.sentenceIndex=0;s.phase='reading';s.time=0;return true;}
  function pauseReading(s){if(s.action!=='read')return false;if(s.phase==='paused'){s.phase=s.pausePhase;return true;}if(['reading','turning'].includes(s.phase)){s.pausePhase=s.phase;s.phase='paused';return true;}return false;}
  function sentenceDuration(s){return Math.min(7600,Math.max(4000,stories[s.storyIndex].sentences[s.sentenceIndex].length*125+600));}
  function nextSentence(s){
    if(s.action!=='read'||!['reading','paused'].includes(s.phase))return false;
    if(s.sentenceIndex===stories[s.storyIndex].sentences.length-1){s.phase='finishing';s.time=0;return true;}
    s.pendingSentence=s.sentenceIndex+1;s.phase='turning';s.time=0;return true;
  }
  function shutter(s){if(s.action!=='photo'||!['compose','saved'].includes(s.phase))return false;s.phase='countdown';s.time=0;return true;}
  function advance(s,ms){
    const dt=Math.min(100,Math.max(0,ms));s.totalTime+=dt;if(s.action==='read'&&s.phase==='paused')return;s.time+=dt;
    if(s.action==='highfive'&&s.phase==='approach'&&s.time>=480){s.phase='contact';s.time=0;s.highCount++;s.completed.highfive=true;}
    else if(s.action==='highfive'&&s.phase==='contact'&&s.time>=1200){s.phase='offer';s.time=0;}
    if(s.action==='rps'&&s.phase==='countdown'&&s.time>=1800){s.pet=choices[s.nextPet];s.phase='shoot';s.time=0;}
    else if(s.action==='rps'&&s.phase==='shoot'&&s.time>=800){s.outcome=s.player===s.pet?'draw':beats[s.player]===s.pet?'win':'lose';s.phase='revealed';s.time=0;s.rounds++;s.completed.rps=true;}
    if(s.action==='gift'&&s.phase==='opening'&&s.time>=1000){
      const id=s.pendingPrize;s.prize=id;s.prizeIsNew=!s.collection[id];s.collection[id]=(s.collection[id]||0)+1;s.gifts++;
      if(!s.bag.length)s.bag=[...giftIds];s.bag=s.bag.filter(item=>item!==id);s.lastGift=id;s.collectionRevision++;
      s.pendingPrize=null;s.phase='opened';s.time=0;s.completed.gift=true;
    }
    if(s.action==='read'){
      if(s.phase==='reading'&&s.time>=sentenceDuration(s))nextSentence(s);
      else if(s.phase==='turning'&&s.time>=700){s.sentenceIndex=s.pendingSentence;s.phase='reading';s.time=0;}
      else if(s.phase==='finishing'&&s.time>=850){s.phase='finished';s.time=0;s.books++;const id=stories[s.storyIndex].id;s.finishedStories[id]=Math.min(100000,(s.finishedStories[id]||0)+1);s.collectionRevision++;s.completed.read=true;}
    }
    if(s.action==='photo'&&s.phase==='countdown'&&s.time>=3000){s.phase='capture';s.time=0;}
  }
  function photoSaved(s){if(s.action!=='photo'||s.phase!=='capture')return false;s.phase='saved';s.time=0;s.photos++;s.completed.photo=true;return true;}
  function pumpPose(time){const f=time%600;return f<130?'fist-up':f<250?'fist-mid':f<380?'fist-down':f<480?'fist-mid':'fist-up';}
  function pose(s){
    if(s.action==='highfive')return s.phase==='approach'?(s.time<340?'high-ready':'high-contact'):s.phase==='contact'?(s.time<280?'high-contact':s.time<660?'high-recoil':'high-prep'):(s.time<750?'high-prep':'high-ready');
    if(s.action==='rps')return s.phase==='countdown'?pumpPose(s.time):s.phase==='shoot'?(s.time<200?'reveal-rock':'reveal-'+s.pet):s.phase==='revealed'?'reveal-'+s.pet:'fist-up';
    if(s.action==='gift')return s.phase==='receive'?'receive':s.phase==='holding'||(s.phase==='opening'&&s.time<330)?'gift':'gift-empty';
    if(s.action==='read')return s.phase==='finished'||s.phase==='finishing'?'read-finish':s.phase==='turning'||(s.phase==='paused'&&s.pausePhase==='turning')?'page':'read';
    if(s.action==='photo')return s.phase==='compose'?'neutral':s.time<400&&s.phase==='countdown'?'lift':'photo';return 'neutral';
  }
  function speech(s){
    if(s.action==='highfive')return s.phase==='approach'?'来了！':s.phase==='contact'?'啪！接住你的击掌。':s.time<750?'我也把手举起来。':s.highCount?'再和我击一次掌吧。':'把你的手递过来吧！';
    if(s.action==='rps')return s.phase==='choose'?'我想好了，一起摇拳！':s.phase==='countdown'?`${3-Math.min(2,Math.floor(s.time/600))}…`:s.phase==='shoot'?'一起出拳！':{win:'你赢了！再来一次吗？',lose:'这次我赢啦，换你挑战我。',draw:'一样的！我们好有默契。'}[s.outcome];
    if(s.action==='gift')return s.phase==='opened'?`${giftById[s.prize].name}！${s.prizeIsNew?'收进我们的收藏袋。':'又多一份小心意。'}`:{receive:'准备好双手，递给我吧。',holding:'接住了！里面会是什么呢？',opening:'一起看看这份惊喜…'}[s.phase];
    if(s.action==='read')return s.phase==='ready'?'选一篇故事，我们一起读。':s.phase==='finished'?'读完啦！故事已放进我们的书架。':s.phase==='finishing'?'故事读完了。':s.phase==='paused'?'歇一会儿，故事停在这里。':stories[s.storyIndex].sentences[s.sentenceIndex];
    if(s.action==='photo')return s.phase==='countdown'?`${3-Math.min(2,Math.floor(s.time/1000))}… 看这里！`:s.phase==='saved'?'留下我们今天的小回忆。':s.phase==='capture'?'茄子！':'相框你来挑，我来摆姿势。';return '';
  }
  function collectionSave(s){return {version:1,counts:{...s.collection},stories:{...s.finishedStories},bag:[...s.bag],last:s.lastGift};}
  return {actions,choices,names,gifts,giftById,stories,create,start,advance,highfive,choose,deliver,openGift,selectStory,beginReading,pauseReading,nextSentence,sentenceDuration,shutter,photoSaved,pose,speech,collectionSave,pumpPose};
});
