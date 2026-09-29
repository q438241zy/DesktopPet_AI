(function (root, factory) {
  const api = factory();
  if (typeof module === 'object' && module.exports) module.exports = api;
  else root.MotionStudy = api;
})(typeof window === 'object' ? window : globalThis, function () {
  'use strict';
  const clamp = (v, a=0, b=1) => Math.min(b, Math.max(a, v));
  const mix = (a,b,t) => a + (b-a)*t;
  const smooth = t => {t=clamp(t);return t*t*(3-2*t);};
  const length = (a,b) => Math.hypot(a.x-b.x,a.y-b.y);
  const point = (x,y) => ({x,y});
  function choose(items, previous, random=Math.random) {
    const available=items.filter(x=>x!==previous);
    return available[Math.min(available.length-1,Math.floor(random()*available.length))];
  }
  class ShuffleBag {
    constructor(items, random=Math.random) {this.items=items;this.random=random;this.remaining=[];this.last=null;}
    next() {
      if(!this.remaining.length){
        this.remaining=[...this.items];
        for(let i=this.remaining.length-1;i>0;i--){const j=Math.floor(this.random()*(i+1));[this.remaining[i],this.remaining[j]]=[this.remaining[j],this.remaining[i]];}
        if(this.remaining.at(-1)===this.last&&this.remaining.length>1){[this.remaining[0],this.remaining[this.remaining.length-1]]=[this.remaining.at(-1),this.remaining[0]];}
      }
      return this.last=this.remaining.pop();
    }
  }
  class IdleSchedule {
    constructor(random=Math.random) {this.random=random;this.poses=new ShuffleBag([0,1,2,3,4],random);this.lastAction=null;this.reset(0);}
    poseDelay(){return 5000+this.random()*4000;}
    actionDelay(){return 30000+this.random()*20000;}
    reset(now){this.poseAt=now+this.poseDelay();this.actionAt=now+this.actionDelay();}
    poll(now,available=true,auto=true){
      if(!available)return null;
      if(auto&&now>=this.actionAt){
        this.lastAction=choose(['stretch','yawn','greet'],this.lastAction,this.random);
        this.actionAt=now+this.actionDelay();this.poseAt=now+this.poseDelay();
        return {kind:'action',value:this.lastAction};
      }
      if(now>=this.poseAt){this.poseAt=now+this.poseDelay();return {kind:'pose',value:this.poses.next()};}
      return null;
    }
  }
  // A stance foot moves backwards relative to the hips by exactly the distance
  // the body moves forwards. Thus its world-space point is stationary.
  function footAt(phase, height) {
    const p=((phase%1)+1)%1, stance=.62, stride=.23*height, span=stride*stance;
    if(p<stance)return {x:span/2-stride*p,y:0,stance:true,phase:p};
    const u=(p-stance)/(1-stance), u2=u*u,u3=u2*u,m=-stride*(1-stance);
    const x=(2*u3-3*u2+1)*(-span/2)+(u3-2*u2+u)*m+(-2*u3+3*u2)*(span/2)+(u3-u2)*m;
    return {x,y:-height*.027*Math.sin(Math.PI*u)**2,stance:false,phase:p};
  }
  function solveKnee(hip,ankle,upper,lower){
    const d=clamp(length(hip,ankle),.0001,upper+lower-.0001);
    const dx=(ankle.x-hip.x)/length(hip,ankle),dy=(ankle.y-hip.y)/length(hip,ankle);
    const a=(upper*upper-lower*lower+d*d)/(2*d),h=Math.sqrt(Math.max(0,upper*upper-a*a));
    return point(hip.x+dx*a+dy*h,hip.y+dy*a-dx*h);
  }
  class Walker {
    constructor(){this.x=0;this.direction=1;this.phase=0;this.turn=0;this.started=0;this.distance=0;}
    advance(dt, left, right, height){
      let seconds=Math.max(0,dt)/1000;
      const speed=.23*height/1.65;
      while(seconds>1e-9){
        if(this.turn>0){const hold=Math.min(seconds,this.turn);this.turn-=hold;seconds-=hold;if(this.turn<1e-9){this.turn=0;this.direction*=-1;}continue;}
        const distance=Math.max(0,this.direction>0?right-this.x:this.x-left);
        const moving=Math.min(seconds,distance/speed);
        this.x+=this.direction*speed*moving;this.phase+=moving/1.65;this.distance+=speed*moving;seconds-=moving;
        if(distance<=speed*moving+1e-8){this.x=this.direction>0?right:left;this.turn=.32;}else break;
      }
    }
  }
  function clipPose(clip,time){
    let t=clamp(time,0,clip.duration);
    for(let i=0;i<clip.frames.length-1;i++){
      const duration=clip.times[i];
      if(t<duration)return {a:clip.frames[i],b:clip.frames[i+1],t:smooth((t/duration-.13)/.74),index:i};
      t-=duration;
    }
    return {a:clip.frames.at(-1),b:clip.frames.at(-1),t:0,index:clip.frames.length-1};
  }
  return {clamp,mix,smooth,point,length,choose,ShuffleBag,IdleSchedule,Walker,footAt,solveKnee,clipPose};
});
