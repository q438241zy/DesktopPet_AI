(function(root,factory){const api=factory();if(typeof module==='object'&&module.exports)module.exports=api;else root.ClubPoseMotion=api;})(globalThis,()=>{
  const clamp=(v,a,b)=>Math.max(a,Math.min(b,v));
  const timelines={
    stars:{offset:0,times:[0,750,1700,2900,4300,5700,7950,9400]},
    bubbles:{offset:8,times:[0,420,820,1120,1800,2400,2970,3600]},
    stretch:{offset:16,times:[0,500,1000,1550,2700,3650,4350,5050]}
  };
  const countTimes=[1800,3050,4450,5850,7000];
  function sample(action,elapsed){
    const line=timelines[action];if(!line)return null;
    const time=action==='bubbles'?elapsed%3600:elapsed;
    let i=0;while(i<7&&time>=line.times[i+1])i++;
    const next=Math.min(i+1,7),start=action==='stars'&&i===5?7100:line.times[i],t=next===i?0:clamp((time-start)/(line.times[next]-start),0,1),smooth=t*t*(3-2*t);
    const count=action==='stars'?countTimes.filter(at=>elapsed>=at).length:0;
    return {a:line.offset+i,b:line.offset+next,t:smooth,cycleTime:time,count,blowing:action==='bubbles'&&time>=1120&&time<2250,
      phase:action==='bubbles'?(time<820?'举起泡泡棒':time<1120?'送到嘴边':time<2250?'轻轻吹气':time<2970?'看看泡泡':'收起泡泡棒'):action==='stars'?(count?`数到 ${count} 颗`:'抬头找星星'):time<1550?'慢慢抬手':time<3000?'向上伸展':'放松收手'};
  }
  return {sample,timelines,countTimes};
});
