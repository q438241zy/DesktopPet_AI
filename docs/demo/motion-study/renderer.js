'use strict';
class PetRenderer {
  constructor(canvas, assets) {
    this.canvas=canvas;this.assets=assets;this.textures={};this.flowCache=new Map();this.partCache=new Map();
    const gl=this.gl=canvas.getContext('webgl',{alpha:true,premultipliedAlpha:true,antialias:true});
    if(!gl)throw new Error('浏览器没有启用 WebGL，无法显示动作 Demo。');
    const vertex=`attribute vec2 position;attribute vec2 uv;varying vec2 q;uniform vec2 resolution;void main(){q=uv;gl_Position=vec4(position.x/resolution.x*2.-1.,1.-position.y/resolution.y*2.,0.,1.);}`;
    const fragment=`precision highp float;
      varying vec2 q;uniform sampler2D imageA;uniform sampler2D imageB;uniform sampler2D flowA;uniform sampler2D flowB;uniform sampler2D flowPrevious;uniform sampler2D flowNext;
      uniform vec4 cellA;uniform vec4 cellB;uniform vec4 rectA;uniform vec4 rectB;
      uniform float amount;uniform float opacity;uniform float shade;uniform float useFlow;uniform float raw;
      uniform float cubic;uniform float previousRatio;uniform float nextRatio;
      vec2 displacement(sampler2D field,vec2 p){return (texture2D(field,clamp(p,0.,1.)).rg-vec2(128./255.))*.6;}
      vec2 boundedTangent(vec2 v,vec2 delta){return clamp(v,-abs(delta)*1.6-vec2(.012),abs(delta)*1.6+vec2(.012));}
      vec4 getColor(sampler2D img,vec2 pos,vec4 cell,vec4 rect){
        vec2 p=(pos-rect.xy)/rect.zw;
        if(p.x<0.||p.x>1.||p.y<0.||p.y>1.)return vec4(0.);
        vec4 c=texture2D(img,cell.xy+p*cell.zw);return vec4(c.rgb*c.a,c.a);
      }
      void main(){
        if(raw>.5){vec4 c=texture2D(imageA,q);gl_FragColor=vec4(c.rgb*c.a*shade,c.a)*opacity;return;}
        vec2 a=q,b=q;
        if(useFlow>.5){
          float t=amount,t2=t*t,t3=t2*t;
          float h01=-2.*t3+3.*t2,h10=t3-2.*t2+t,h11=t3-t2;
          for(int i=0;i<3;i++){
            vec2 da=displacement(flowA,a),db=displacement(flowB,b);
            if(cubic>.5){
              vec2 va=previousRatio>0.?boundedTangent((da-displacement(flowPrevious,a)*previousRatio)*.5,da):vec2(0.);
              vec2 vb=nextRatio>0.?boundedTangent((-db+displacement(flowNext,b)*nextRatio)*.5,db):vec2(0.);
              a=q-(h01*da+h10*va+h11*vb);
              b=q-((1.-h01)*db+h10*va+h11*vb);
            }else {a=q-amount*da;b=q-(1.-amount)*db;}
          }
        }
        // Geometry travels throughout the interval. Change texture over a short
        // overlap only; full-interval dissolves double hands and soften faces.
        float textureMix=useFlow>.5?smoothstep(.47,.53,amount):amount;
        vec4 color=mix(getColor(imageA,a,cellA,rectA),getColor(imageB,b,cellB,rectB),textureMix);
        gl_FragColor=color*opacity;
      }`;
    const shader=(type,source)=>{const s=gl.createShader(type);gl.shaderSource(s,source);gl.compileShader(s);if(!gl.getShaderParameter(s,gl.COMPILE_STATUS))throw Error(gl.getShaderInfoLog(s));return s;};
    this.program=gl.createProgram();gl.attachShader(this.program,shader(gl.VERTEX_SHADER,vertex));gl.attachShader(this.program,shader(gl.FRAGMENT_SHADER,fragment));gl.linkProgram(this.program);
    if(!gl.getProgramParameter(this.program,gl.LINK_STATUS))throw Error(gl.getProgramInfoLog(this.program));
    gl.useProgram(this.program);this.buffer=gl.createBuffer();this.uniform={};
    for(const n of ['resolution','imageA','imageB','flowA','flowB','flowPrevious','flowNext','cellA','cellB','rectA','rectB','amount','opacity','shade','useFlow','raw','cubic','previousRatio','nextRatio'])this.uniform[n]=gl.getUniformLocation(this.program,n);
    this.pos=gl.getAttribLocation(this.program,'position');this.uv=gl.getAttribLocation(this.program,'uv');
    gl.enable(gl.BLEND);gl.blendFunc(gl.ONE,gl.ONE_MINUS_SRC_ALPHA);
    this.zeroFlow=this.texture(new Uint8Array([128,128,0,255]),1,1);
  }
  texture(data,w,h){const g=this.gl,t=g.createTexture();g.bindTexture(g.TEXTURE_2D,t);g.pixelStorei(g.UNPACK_PREMULTIPLY_ALPHA_WEBGL,false);
    if(data instanceof Uint8Array)g.texImage2D(g.TEXTURE_2D,0,g.RGBA,w,h,0,g.RGBA,g.UNSIGNED_BYTE,data);
    else g.texImage2D(g.TEXTURE_2D,0,g.RGBA,g.RGBA,g.UNSIGNED_BYTE,data);
    g.texParameteri(g.TEXTURE_2D,g.TEXTURE_MIN_FILTER,g.LINEAR);g.texParameteri(g.TEXTURE_2D,g.TEXTURE_MAG_FILTER,g.LINEAR);
    g.texParameteri(g.TEXTURE_2D,g.TEXTURE_WRAP_S,g.CLAMP_TO_EDGE);g.texParameteri(g.TEXTURE_2D,g.TEXTURE_WRAP_T,g.CLAMP_TO_EDGE);return t;}
  async load(){
    this.images={};this.fields={};
    await Promise.all(Object.entries(this.assets.images).map(async([key,src])=>{const image=new Image();image.src=src;await image.decode();this.images[key]=image;this.textures[key]=this.texture(image);}));
    await Promise.all(Object.entries(this.assets.flow).map(async([key,pair])=>{
      this.fields[key]=await Promise.all(pair.map(async encoded=>{
        const bytes=Uint8Array.from(atob(encoded),c=>c.charCodeAt(0));
        if(this.assets.flowEncoding!=='gzip')return bytes;
        const stream=new Blob([bytes]).stream().pipeThrough(new DecompressionStream('gzip'));
        return new Uint8Array(await new Response(stream).arrayBuffer());
      }));
    }));
  }
  resize(){const dpr=Math.min(window.devicePixelRatio||1,2),w=this.canvas.clientWidth,h=this.canvas.clientHeight;
    if(this.canvas.width!==Math.round(w*dpr)||this.canvas.height!==Math.round(h*dpr)){this.canvas.width=Math.round(w*dpr);this.canvas.height=Math.round(h*dpr);}
    this.width=w;this.height=h;this.gl.viewport(0,0,this.canvas.width,this.canvas.height);this.gl.uniform2f(this.uniform.resolution,w,h);}
  clear(){this.resize();this.gl.clearColor(0,0,0,0);this.gl.clear(this.gl.COLOR_BUFFER_BIT);}
  bind(slot,texture){const g=this.gl;g.activeTexture(g.TEXTURE0+slot);g.bindTexture(g.TEXTURE_2D,texture);}
  draw(vertices){const g=this.gl;g.bindBuffer(g.ARRAY_BUFFER,this.buffer);g.bufferData(g.ARRAY_BUFFER,vertices instanceof Float32Array?vertices:new Float32Array(vertices),g.DYNAMIC_DRAW);
    g.enableVertexAttribArray(this.pos);g.vertexAttribPointer(this.pos,2,g.FLOAT,false,16,0);g.enableVertexAttribArray(this.uv);g.vertexAttribPointer(this.uv,2,g.FLOAT,false,16,8);g.drawArrays(g.TRIANGLES,0,vertices.length/4);}
  flow(a,b){if(a===b)return null;const key=[a,b].sort().join('|'),fields=this.fields[key];if(!fields)return null;
    if(!this.flowCache.has(key)){const textures=fields.map(bytes=>this.texture(bytes,...this.assets.flowSize));this.flowCache.set(key,textures);}
    const pair=this.flowCache.get(key);return a<b?pair:[pair[1],pair[0]];}
  sprite(a,b,t,x,floor,height,opacity=1,flowEnabled=true,context=null){
    if(!flowEnabled)t=t<.5?0:1;
    const g=this.gl,u=this.uniform,A=this.assets.poses[a],B=this.assets.poses[b],flow=flowEnabled?this.flow(a,b):null;
    this.bind(0,this.textures[A.atlas]);this.bind(1,this.textures[B.atlas]);this.bind(2,flow?.[0]||this.zeroFlow);this.bind(3,flow?.[1]||this.zeroFlow);
    const previous=context?.previous?this.flow(a,context.previous):null,next=context?.next?this.flow(b,context.next):null;
    this.bind(4,previous?.[0]||this.zeroFlow);this.bind(5,next?.[0]||this.zeroFlow);
    for(const [i,name] of ['imageA','imageB','flowA','flowB','flowPrevious','flowNext'].entries())g.uniform1i(u[name],i);
    g.uniform4fv(u.cellA,A.cell);g.uniform4fv(u.cellB,B.cell);g.uniform4fv(u.rectA,A.rect);g.uniform4fv(u.rectB,B.rect);
    g.uniform1f(u.amount,t);g.uniform1f(u.opacity,opacity);g.uniform1f(u.raw,0);g.uniform1f(u.useFlow,flow?1:0);
    g.uniform1f(u.cubic,context&&flow?1:0);g.uniform1f(u.previousRatio,previous?context.previousRatio:0);g.uniform1f(u.nextRatio,next?context.nextRatio:0);
    const h=height/.9,w=h*320/480,left=x-w/2,top=floor-h*.96;
    this.draw([left,top,0,0,left+w,top,1,0,left,top+h,0,1,left,top+h,0,1,left+w,top,1,0,left+w,top+h,1,1]);
  }
  mesh(texture,vertices,opacity=1,shade=1){const g=this.gl;this.bind(0,this.textures[texture]);g.uniform1i(this.uniform.imageA,0);g.uniform1f(this.uniform.raw,1);g.uniform1f(this.uniform.opacity,opacity);g.uniform1f(this.uniform.shade,shade);this.draw(vertices);}
  part(rect,mapper,shade=1,columns=1,rows=1,texture='parts',opacity=1){const img=this.images[texture],key=[texture,...rect,columns,rows].join(',');
    if(!this.partCache.has(key)){
      const points=[],indices=[];
      for(let j=0;j<=rows;j++)for(let i=0;i<=columns;i++)points.push([rect[0]+rect[2]*i/columns,rect[1]+rect[3]*j/rows]);
      for(let j=0;j<rows;j++)for(let i=0;i<columns;i++){const a=j*(columns+1)+i,b=a+1,c=a+columns+1,d=c+1;indices.push(a,b,c,c,b,d);}
      this.partCache.set(key,{points,indices});
    }
    const {points,indices}=this.partCache.get(key),mapped=points.map(([px,py])=>{const p=mapper(px,py);return [p.x,p.y,px/img.width,py/img.height];});
    const vertices=new Float32Array(indices.length*4);for(let i=0;i<indices.length;i++)vertices.set(mapped[indices[i]],i*4);
    this.mesh(texture,vertices,opacity,shade);
  }
  dance(hands,feet,x,floor,height,time,kind,head){
    const M=MotionStudy,s=height/1136,legScale=s*.6,unit=height/432;
    const weight=(feet[0][0]+feet[1][0])/2-160;
    const root={x:x+M.clamp(weight,-14,14)*unit*.3,y:floor-621*s};
    const desiredHead=x+((head?.[0]?.[0]||160)-160)*unit;
    const angle=M.clamp(Math.asin(M.clamp((desiredHead-root.x)/(340*s),-.4,.4)),-.24,.24);
    const bodyPoint=(px,py)=>{const p=M.rotate({x:(px-336)*s,y:(py-530)*s},angle);return {x:root.x+p.x,y:root.y+p.y};};
    const shoulders=[bodyPoint(202,320),bodyPoint(475,320)],hips=[bodyPoint(300,785),bodyPoint(372,785)];
    const sourceX=(py,a,b,ay,by)=>M.mix(a,b,(py-ay)/(by-ay));
    const limbMapper=(start,joint,end,rest,scale,widthScale=scale)=>{
      const [sx,jx,ex,sy,jy,ey]=rest;
      const firstAngle=Math.atan2(joint.y-start.y,joint.x-start.x)-Math.PI/2,secondAngle=Math.atan2(end.y-joint.y,end.x-joint.x)-Math.PI/2;
      const scanlines=new Map();
      return (px,py)=>{
        if(!scanlines.has(py)){
          const a={x:M.mix(start.x,joint.x,(py-sy)/(jy-sy)),y:M.mix(start.y,joint.y,(py-sy)/(jy-sy))};
          const b={x:M.mix(joint.x,end.x,(py-jy)/(ey-jy)),y:M.mix(joint.y,end.y,(py-jy)/(ey-jy))};
          const blend=M.smooth((py-jy+25)/50),centre={x:M.mix(a.x,b.x,blend),y:M.mix(a.y,b.y,blend)};
          const restX=M.mix(sourceX(py,sx,jx,sy,jy),sourceX(py,jx,ex,jy,ey),blend),normal=M.rotate({x:widthScale,y:0},M.mix(firstAngle,secondAngle,blend));
          scanlines.set(py,{centre,restX,normal});
        }
        const row=scanlines.get(py);return {x:row.centre.x+(px-row.restX)*row.normal.x,y:row.centre.y+(px-row.restX)*row.normal.y};
      };
    };
    const legs=[];
    for(let i=0;i<2;i++){
      const sole={x:x+(feet[i][0]-160)*unit,y:floor+(feet[i][1]-460.8)*unit};
      const cuff={x:sole.x-7*legScale,y:sole.y-220*legScale},hip=hips[i];
      // In front view knee flexion mainly travels in depth. A side-view IK
      // branch made both knees bow sideways even during a quiet standing beat.
      const knee={x:M.mix(hip.x,cuff.x,200/390)+(i?1:-1)*height*.003,y:M.mix(hip.y,cuff.y,200/390)};
      const skin=limbMapper(hip,knee,cuff,[334,324,330,900,1100,1290],legScale);
      this.part([254,848,160,676],(px,py)=>{
        const upper=skin(px,py);
        const boot={x:sole.x+(px-337)*legScale,y:sole.y+(py-1510)*legScale},t=M.smooth((py-1250)/40);
        return {x:M.mix(upper.x,boot.x,t),y:M.mix(upper.y,boot.y,t)};
      },i?.98:1,14,96,'frontParts');legs.push({hip,knee,cuff,sole});
    }
    this.part([42,0,590,838],bodyPoint,1,1,1,'frontParts');
    const arms=[];
    const averageY=(hands[0][1]+hands[1][1])/2;
    const heartWeight=kind==='heart'?M.smooth((180-averageY)/25)*M.smooth((60-Math.abs(hands[1][0]-hands[0][0]))/18)*M.smooth((16-Math.abs(hands[1][1]-hands[0][1]))/8):0;
    const pairScale=Math.max(18,Math.abs(hands[1][0]-hands[0][0]))*unit/325;
    const pairOrigin={x:x+((hands[0][0]+hands[1][0])/2-160)*unit,y:floor+(averageY-460.8)*unit};
    const pairPoint=(px,py)=>({x:pairOrigin.x+(px-402.5)*pairScale,y:pairOrigin.y+(py-950)*pairScale});
    for(let i=0;i<2;i++){
      const shoulder=shoulders[i],palm={x:x+(hands[i][0]-160)*unit,y:floor+(hands[i][1]-460.8)*unit},side=i?1:-1;
      const elbow={x:shoulder.x+side*Math.max(15*unit,18*unit+side*(palm.x-shoulder.x)*.35),y:shoulder.y+40*unit+(palm.y-shoulder.y)*.25};
      const forearmAngle=Math.atan2(palm.y-elbow.y,palm.x-elbow.x);
      const faceWeight=M.smooth((150-hands[i][1])/25)*M.smooth((hands[i][0]-90)/20)*M.smooth((230-hands[i][0])/20);
      const gestureWeight=kind==='tt'?M.smooth((180-hands[i][1])/35):kind==='nextLevel'&&i===0?M.smooth((145-hands[i][1])/35):0;
      const relaxed=M.smooth((hands[i][1]-165)/35);
      const definitions=[
        {texture:'frontParts',rect:[706,966,211,345],wrist:[810,1251],palm:[805,1160],scale:s*.4,mirror:!!i,direction:M.mix(forearmAngle,-Math.PI/2+side*.16,faceWeight),weight:(1-relaxed)*(1-gestureWeight)},
        {texture:'frontParts',rect:[794,569,106,271],wrist:[833,637],palm:[844,711],scale:s*.46,mirror:!!i,direction:forearmAngle,weight:relaxed*(1-gestureWeight)}
      ];
      if(kind==='tt')definitions.push(i?
        {texture:'gestures',rect:[744,142,314,478],wrist:[904,245],palm:[896,366],scale:s*.29,direction:Math.PI/2,weight:gestureWeight}:
        {texture:'gestures',rect:[215,142,282,478],wrist:[321,246],palm:[332,364],scale:s*.29,direction:Math.PI/2,weight:gestureWeight});
      if(kind==='nextLevel'&&i===0)definitions.push({texture:'gestures',rect:[928,620,305,542],wrist:[1035,1040],palm:[1030,911],scale:s*.29,direction:-Math.PI/2,weight:gestureWeight});
      let wrist={x:0,y:0};
      for(const definition of definitions){
        const mirror=definition.mirror?-1:1,offset={x:(definition.palm[0]-definition.wrist[0])*definition.scale*mirror,y:(definition.palm[1]-definition.wrist[1])*definition.scale};
        definition.angle=definition.direction-Math.atan2(offset.y,offset.x);
        const p=M.rotate(offset,definition.angle);definition.position={x:palm.x-p.x,y:palm.y-p.y};
        wrist.x+=definition.position.x*definition.weight;wrist.y+=definition.position.y*definition.weight;
      }
      const pairWrist=pairPoint(i?669:137,951);wrist={x:M.mix(wrist.x,pairWrist.x,heartWeight),y:M.mix(wrist.y,pairWrist.y,heartWeight)};
      const skin=limbMapper(shoulder,elbow,wrist,[789,808,839,215,400,635],s,s*.5);
      this.part([718,157,202,478],(px,py)=>skin(i?2*(789+(py-215)/420*50)-px:px,py),1,16,72,'frontParts');
      for(const definition of definitions){
        const opacity=definition.weight*(1-heartWeight);if(opacity<.001)continue;
        this.part(definition.rect,(px,py)=>{const p=M.rotate({x:(px-definition.wrist[0])*definition.scale*(definition.mirror?-1:1),y:(py-definition.wrist[1])*definition.scale},definition.angle);return {x:definition.position.x+p.x,y:definition.position.y+p.y};},1,1,1,definition.texture,opacity);
      }
      arms.push({shoulder,elbow,wrist,palm});
    }
    if(heartWeight>.001)this.part([0,780,809,289],pairPoint,1,1,1,'gestures',heartWeight);
    return {arms,legs,root,method:'separate original PNG parts; continuous joint tracks'};
  }
  walk(walker,x,floor,height,debug=false){
    const M=MotionStudy,rig=M.walkRig(walker.phase,height),s=rig.scale,hip=rig.hip,phase=walker.phase,dir=walker.direction;
    const world=p=>({x:x+dir*p.x,y:floor+p.y});
    const rotate=(p,angle)=>({x:p.x*Math.cos(angle)-p.y*Math.sin(angle),y:p.x*Math.sin(angle)+p.y*Math.cos(angle)});
    const leg=(index,shade)=>{
      const bone=rig.legs[index],scanlines=new Map();
      this.part([869,80,193,669],(px,py)=>{
        if(!scanlines.has(py)){const centre=M.skinLeg(940,py,bone,s),edge=M.skinLeg(941,py,bone,s);scanlines.set(py,{centre,dx:edge.x-centre.x,dy:edge.y-centre.y});}
        const row=scanlines.get(py);return world({x:row.centre.x+(px-940)*row.dx,y:row.centre.y+(px-940)*row.dy});
      },shade,20,144);
      return {hip:world(hip),knee:world(bone.knee),ankle:world(bone.cuff),sole:world(bone.sole),stance:bone.stance,pitch:bone.pitch};
    };
    const shoulder={x:(388-472)*s,y:hip.y+(285-505)*s};
    const arm=(offset,shade)=>{
      const angle=Math.sin((phase+offset)*Math.PI*2)*.17;
      this.part([332,791,199,449],(px,py)=>{const r=rotate({x:(px-385)*s*.83,y:(py-835)*s*.83},angle);return world({x:shoulder.x+r.x,y:shoulder.y+r.y});},shade,1,1);
    };
    arm(.5,.86);const far=leg(1,.87),near=leg(0,1);
    // The skirt is drawn over both hip attachments, without stretching the hem.
    this.part([813,928,283,192],(px,py)=>{const a=Math.sin(phase*Math.PI*2)*.045,r=rotate({x:(px-1080)*s*.6,y:(py-1060)*s*.6},a);return world({x:-60*s+r.x,y:hip.y+30*s+r.y});});
    this.part([196,0,476,774],(px,py)=>world({x:(px-472)*s,y:hip.y+(py-505)*s}));
    arm(0,1);
    return {near,far,hip:world(hip),phase:phase%1,dir};
  }
}
