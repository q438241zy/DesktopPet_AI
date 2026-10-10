/* Automatic idle gestures have their own clock: they never count as user activity. */
(function(root){
  'use strict';
  const poses=Object.freeze(['stand','sit','think','stretch','smile']);
  const minInterval=1000,maxInterval=2000;
  class Clock {
    constructor(pose='stand',random=Math.random){
      this.pose=poses.includes(pose)?pose:'stand';this.random=random;this.bag=[];
      this.started=0;this.next=0;this.duration=0;this.running=false;this.changes=0;this.heldProgress=0;
    }
    schedule(now){
      this.duration=minInterval+Math.floor(Math.max(0,Math.min(.999999999,this.random()))*(maxInterval-minInterval+1));
      this.started=now;this.next=now+this.duration;this.running=true;this.heldProgress=0;
    }
    reset(now){this.schedule(now);}
    suspend(now){if(this.running)this.heldProgress=Math.max(0,Math.min(1,(now-this.started)/this.duration));this.running=false;}
    tick(now,eligible){
      if(!eligible){this.suspend(now);return false;}
      if(!this.running){this.schedule(now);return false;}
      if(now<this.next)return false;
      if(!this.bag.length){
        this.bag=poses.filter(p=>p!==this.pose);
        for(let i=this.bag.length-1;i>0;i--){const j=Math.floor(Math.max(0,Math.min(.999999999,this.random()))*(i+1));[this.bag[i],this.bag[j]]=[this.bag[j],this.bag[i]];}
        // Each bag visits the other four poses once; the current pose cannot
        // repeat at its boundary. Delayed tabs advance once, never burst-catch-up.
      }
      this.pose=this.bag.shift();this.changes++;this.schedule(now);return true;
    }
    elapsed(now,desc){
      if(desc.frames.length<2)return 0;
      const clip=desc.frameMs.reduce((a,b)=>a+b,0);
      const progress=this.running?Math.max(0,now-this.started)/this.duration:this.heldProgress;
      return Math.min(clip-1,progress*clip);
    }
    snapshot(now){return {pose:this.pose,started:this.started,next:this.next,duration:this.duration,running:this.running,changes:this.changes,remaining:Math.max(0,this.next-now)};}
  }
  const api=Object.freeze({Clock,poses,minInterval,maxInterval});
  if(typeof module!=='undefined'&&module.exports)module.exports=api;
  root.CompanionIdlePostures=api;
})(globalThis);
