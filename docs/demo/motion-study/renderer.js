'use strict';
class PetRenderer {
  constructor(canvas, assets) {
    this.canvas=canvas;this.assets=assets;this.textures={};this.flowCache=new Map();
    const gl=this.gl=canvas.getContext('webgl',{alpha:true,premultipliedAlpha:true,antialias:true});
    if(!gl)throw new Error('浏览器没有启用 WebGL，无法显示动作 Demo。');
    const vertex=`attribute vec2 position;attribute vec2 uv;varying vec2 q;uniform vec2 resolution;void main(){q=uv;gl_Position=vec4(position.x/resolution.x*2.-1.,1.-position.y/resolution.y*2.,0.,1.);}`;
    const fragment=`precision highp float;
      varying vec2 q;uniform sampler2D imageA;uniform sampler2D imageB;uniform sampler2D flowA;uniform sampler2D flowB;
      uniform vec4 cellA;uniform vec4 cellB;uniform vec4 rectA;uniform vec4 rectB;
      uniform float amount;uniform float opacity;uniform float shade;uniform float useFlow;uniform float raw;
      vec4 getColor(sampler2D img,vec2 pos,vec4 cell,vec4 rect){
        vec2 p=(pos-rect.xy)/rect.zw;
        if(p.x<0.||p.x>1.||p.y<0.||p.y>1.)return vec4(0.);
        vec4 c=texture2D(img,cell.xy+p*cell.zw);return vec4(c.rgb*c.a,c.a);
      }
      void main(){
        if(raw>.5){vec4 c=texture2D(imageA,q);gl_FragColor=vec4(c.rgb*c.a*shade,c.a)*opacity;return;}
        vec2 a=q,b=q;
        if(useFlow>.5){
          for(int i=0;i<3;i++){
            a=q-amount*(texture2D(flowA,clamp(a,0.,1.)).rg-vec2(128./255.))*.6;
            b=q-(1.-amount)*(texture2D(flowB,clamp(b,0.,1.)).rg-vec2(128./255.))*.6;
          }
        }
        // Geometry travels throughout the interval. Change texture over a short
        // overlap only; full-interval dissolves double hands and soften faces.
        float textureMix=useFlow>.5?smoothstep(.46,.54,amount):amount;
        vec4 color=mix(getColor(imageA,a,cellA,rectA),getColor(imageB,b,cellB,rectB),textureMix);
        gl_FragColor=color*opacity;
      }`;
    const shader=(type,source)=>{const s=gl.createShader(type);gl.shaderSource(s,source);gl.compileShader(s);if(!gl.getShaderParameter(s,gl.COMPILE_STATUS))throw Error(gl.getShaderInfoLog(s));return s;};
    this.program=gl.createProgram();gl.attachShader(this.program,shader(gl.VERTEX_SHADER,vertex));gl.attachShader(this.program,shader(gl.FRAGMENT_SHADER,fragment));gl.linkProgram(this.program);
    if(!gl.getProgramParameter(this.program,gl.LINK_STATUS))throw Error(gl.getProgramInfoLog(this.program));
    gl.useProgram(this.program);this.buffer=gl.createBuffer();this.uniform={};
    for(const n of ['resolution','imageA','imageB','flowA','flowB','cellA','cellB','rectA','rectB','amount','opacity','shade','useFlow','raw'])this.uniform[n]=gl.getUniformLocation(this.program,n);
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
  draw(vertices){const g=this.gl;g.bindBuffer(g.ARRAY_BUFFER,this.buffer);g.bufferData(g.ARRAY_BUFFER,new Float32Array(vertices),g.DYNAMIC_DRAW);
    g.enableVertexAttribArray(this.pos);g.vertexAttribPointer(this.pos,2,g.FLOAT,false,16,0);g.enableVertexAttribArray(this.uv);g.vertexAttribPointer(this.uv,2,g.FLOAT,false,16,8);g.drawArrays(g.TRIANGLES,0,vertices.length/4);}
  flow(a,b){if(a===b)return null;const key=[a,b].sort().join('|'),fields=this.fields[key];if(!fields)return null;
    if(!this.flowCache.has(key)){const textures=fields.map(bytes=>this.texture(bytes,...this.assets.flowSize));this.flowCache.set(key,textures);}
    const pair=this.flowCache.get(key);return a<b?pair:[pair[1],pair[0]];}
  sprite(a,b,t,x,floor,height,opacity=1,flowEnabled=true){
    if(!flowEnabled)t=t<.5?0:1;
    const g=this.gl,u=this.uniform,A=this.assets.poses[a],B=this.assets.poses[b],flow=flowEnabled?this.flow(a,b):null;
    this.bind(0,this.textures[A.atlas]);this.bind(1,this.textures[B.atlas]);this.bind(2,flow?.[0]||this.zeroFlow);this.bind(3,flow?.[1]||this.zeroFlow);
    for(const [i,name] of ['imageA','imageB','flowA','flowB'].entries())g.uniform1i(u[name],i);
    g.uniform4fv(u.cellA,A.cell);g.uniform4fv(u.cellB,B.cell);g.uniform4fv(u.rectA,A.rect);g.uniform4fv(u.rectB,B.rect);
    g.uniform1f(u.amount,t);g.uniform1f(u.opacity,opacity);g.uniform1f(u.raw,0);g.uniform1f(u.useFlow,flow?1:0);
    const h=height/.9,w=h*320/480,left=x-w/2,top=floor-h*.96;
    this.draw([left,top,0,0,left+w,top,1,0,left,top+h,0,1,left,top+h,0,1,left+w,top,1,0,left+w,top+h,1,1]);
  }
  mesh(texture,vertices,opacity=1,shade=1){const g=this.gl;this.bind(0,this.textures[texture]);g.uniform1i(this.uniform.imageA,0);g.uniform1f(this.uniform.raw,1);g.uniform1f(this.uniform.opacity,opacity);g.uniform1f(this.uniform.shade,shade);this.draw(vertices);}
  part(rect,mapper,shade=1,columns=1,rows=1){const img=this.images.parts,vertices=[];
    const vertex=(x,y)=>{const p=mapper(x,y);return [p.x,p.y,x/img.width,y/img.height];};
    for(let j=0;j<rows;j++)for(let i=0;i<columns;i++){
      const x=rect[0]+rect[2]*i/columns,y=rect[1]+rect[3]*j/rows,x1=x+rect[2]/columns,y1=y+rect[3]/rows;
      const a=vertex(x,y),b=vertex(x1,y),c=vertex(x,y1),d=vertex(x1,y1);vertices.push(...a,...b,...c,...c,...b,...d);
    }
    this.mesh('parts',vertices,1,shade);
  }
  walk(walker,x,floor,height,debug=false){
    const M=MotionStudy,s=height/1129,phase=walker.phase,dir=walker.direction;
    const sourceHip={x:940,y:113},sourceKnee={x:940,y:356},sourceAnkle={x:940,y:665};
    const upper=243*s,lower=309*s,footHeight=73*s;
    const support=[M.footAt(phase,height),M.footAt(phase+.5,height)].filter(f=>f.stance);
    const reach=upper+lower-height*.0015;
    // Set pelvis height from the planted legs. A fixed, lowered pelvis made
    // both knees bend forward throughout the stride, resembling a crouch.
    const hip={x:0,y:Math.max(...support.map(f=>-footHeight-Math.sqrt(reach*reach-(f.x-28*s)**2)))+height*.0008*(1-Math.cos(phase*Math.PI*4))};
    const world=p=>({x:x+dir*p.x,y:floor+p.y});
    const rotate=(p,angle)=>({x:p.x*Math.cos(angle)-p.y*Math.sin(angle),y:p.x*Math.sin(angle)+p.y*Math.cos(angle)});
    const transform=(p,start,end,restStart,restEnd)=>{
      const angle=Math.atan2(end.y-start.y,end.x-start.x)-Math.atan2(restEnd.y-restStart.y,restEnd.x-restStart.x);
      const r=rotate({x:(p.x-restStart.x)*s,y:(p.y-restStart.y)*s},angle);return {x:start.x+r.x,y:start.y+r.y};};
    const feet=[];
    const leg=(offset,shade)=>{
      const foot=M.footAt(phase+offset,height),sole={x:foot.x,y:foot.y};feet.push(foot);
      const ankle={x:sole.x-28*s,y:sole.y-footHeight};
      const knee=M.solveKnee(hip,ankle,upper,lower);
      const map=(px,py)=>{
        const p={x:px,y:py};
        const a=transform(p,hip,knee,sourceHip,sourceKnee),b=transform(p,knee,ankle,sourceKnee,sourceAnkle);
        // Boots retain their shape and stay flat while in contact with the floor.
        const c={x:ankle.x+(px-sourceAnkle.x)*s,y:ankle.y+(py-sourceAnkle.y)*s};
        const k=M.smooth((py-316)/80),f=M.smooth((py-610)/55);
        return world({x:M.mix(M.mix(a.x,b.x,k),c.x,f),y:M.mix(M.mix(a.y,b.y,k),c.y,f)});
      };
      this.part([869,80,193,669],map,shade,18,72);
      return {hip:world(hip),knee:world(knee),ankle:world(ankle),sole:world(sole),stance:foot.stance};
    };
    const shoulder={x:(388-472)*s,y:hip.y+(285-505)*s};
    const arm=(offset,shade)=>{
      const angle=Math.sin((phase+offset)*Math.PI*2)*.17;
      this.part([332,791,199,449],(px,py)=>{const r=rotate({x:(px-385)*s*.83,y:(py-835)*s*.83},angle);return world({x:shoulder.x+r.x,y:shoulder.y+r.y});},shade,1,1);
    };
    arm(.5,.86);const far=leg(.5,.87),near=leg(0,1);
    // The skirt is drawn over both hip attachments, without stretching the hem.
    this.part([813,928,283,192],(px,py)=>{const a=Math.sin(phase*Math.PI*2)*.045,r=rotate({x:(px-1080)*s*.6,y:(py-1060)*s*.6},a);return world({x:-60*s+r.x,y:hip.y+30*s+r.y});});
    this.part([196,0,476,774],(px,py)=>world({x:(px-472)*s,y:hip.y+(py-505)*s}));
    arm(0,1);
    return {near,far,hip:world(hip),phase:phase%1,dir};
  }
}
