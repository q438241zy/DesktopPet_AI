/* Continuous pose transitions sample the original atlas pixels. No opacity-only
   slide show: forward/backward displacement moves each pose's geometry. */
class ClubPoseRenderer {
  constructor(canvas,data) {
    this.canvas=canvas;this.data=data;this.ready=false;this.images={};this.fields={};this.textures={};this.owners={};this.flowTextures=new Map();
    const gl=this.gl=canvas.getContext('webgl',{alpha:true,premultipliedAlpha:true,antialias:false,preserveDrawingBuffer:true});
    if(!gl)throw Error('浏览器未启用 WebGL，无法播放新的连贯姿势。');
    const vertex='attribute vec2 p;attribute vec2 uv;varying vec2 q;uniform vec2 resolution;void main(){q=uv;gl_Position=vec4(p.x/resolution.x*2.-1.,1.-p.y/resolution.y*2.,0.,1.);}';
    const fragment=`precision highp float;varying vec2 q;
      uniform sampler2D A,B,F,G,owners;uniform vec4 cellA,cellB,rectA,rectB;uniform float amount,ownerA,ownerB;
      vec2 displacement(sampler2D f,vec2 p){return (texture2D(f,clamp(p,0.,1.)).rg-vec2(128./255.))*.6;}
      vec4 colour(sampler2D img,vec2 pos,vec4 cell,vec4 rect,float owner){vec2 p=(pos-rect.xy)/rect.zw;if(p.x<0.||p.x>1.||p.y<0.||p.y>1.)return vec4(0.);vec2 uv=cell.xy+p*cell.zw;if(abs(texture2D(owners,uv).r*255.-owner)>.1)return vec4(0.);vec4 c=texture2D(img,uv);return vec4(c.rgb*c.a,c.a);}
      void main(){vec2 a=q,b=q;for(int i=0;i<3;i++){a=q-amount*displacement(F,a);b=q-(1.-amount)*displacement(G,b);}gl_FragColor=amount<.5?colour(A,a,cellA,rectA,ownerA):colour(B,b,cellB,rectB,ownerB);}`;
    const shader=(type,source)=>{const s=gl.createShader(type);gl.shaderSource(s,source);gl.compileShader(s);if(!gl.getShaderParameter(s,gl.COMPILE_STATUS))throw Error(gl.getShaderInfoLog(s));return s;};
    this.program=gl.createProgram();gl.attachShader(this.program,shader(gl.VERTEX_SHADER,vertex));gl.attachShader(this.program,shader(gl.FRAGMENT_SHADER,fragment));gl.linkProgram(this.program);
    if(!gl.getProgramParameter(this.program,gl.LINK_STATUS))throw Error(gl.getProgramInfoLog(this.program));gl.useProgram(this.program);
    this.buffer=gl.createBuffer();this.u={};for(const n of ['resolution','A','B','F','G','owners','ownerA','ownerB','cellA','cellB','rectA','rectB','amount'])this.u[n]=gl.getUniformLocation(this.program,n);
    this.p=gl.getAttribLocation(this.program,'p');this.uv=gl.getAttribLocation(this.program,'uv');gl.enable(gl.BLEND);gl.blendFunc(gl.ONE,gl.ONE_MINUS_SRC_ALPHA);
    this.zero=this.texture(new Uint8Array([128,128,0,255]),1,1);
  }
  texture(image,w,h){const g=this.gl,t=g.createTexture();g.bindTexture(g.TEXTURE_2D,t);g.pixelStorei(g.UNPACK_PREMULTIPLY_ALPHA_WEBGL,false);
    if(image instanceof Uint8Array)g.texImage2D(g.TEXTURE_2D,0,g.RGBA,w,h,0,g.RGBA,g.UNSIGNED_BYTE,image);else g.texImage2D(g.TEXTURE_2D,0,g.RGBA,g.RGBA,g.UNSIGNED_BYTE,image);
    g.texParameteri(g.TEXTURE_2D,g.TEXTURE_MIN_FILTER,g.LINEAR);g.texParameteri(g.TEXTURE_2D,g.TEXTURE_MAG_FILTER,g.LINEAR);g.texParameteri(g.TEXTURE_2D,g.TEXTURE_WRAP_S,g.CLAMP_TO_EDGE);g.texParameteri(g.TEXTURE_2D,g.TEXTURE_WRAP_T,g.CLAMP_TO_EDGE);return t;}
  async load(){
    await Promise.all(Object.entries(this.data.images).map(async([k,src])=>{const im=new Image();im.src=src;await im.decode();this.images[k]=im;this.textures[k]=this.texture(im);}));
    const decompress=async b64=>{const bytes=Uint8Array.from(atob(b64),c=>c.charCodeAt(0));return new Uint8Array(await new Response(new Blob([bytes]).stream().pipeThrough(new DecompressionStream('gzip'))).arrayBuffer());};
    await Promise.all(Object.entries(this.data.flow).map(async([k,pair])=>{this.fields[k]=await Promise.all(pair.map(decompress));}));
    for(const [key,owner] of Object.entries(this.data.owners)){
      const data=await decompress(owner.data),g=this.gl,t=g.createTexture();g.bindTexture(g.TEXTURE_2D,t);g.texImage2D(g.TEXTURE_2D,0,g.LUMINANCE,...owner.size,0,g.LUMINANCE,g.UNSIGNED_BYTE,data);g.texParameteri(g.TEXTURE_2D,g.TEXTURE_MIN_FILTER,g.NEAREST);g.texParameteri(g.TEXTURE_2D,g.TEXTURE_MAG_FILTER,g.NEAREST);g.texParameteri(g.TEXTURE_2D,g.TEXTURE_WRAP_S,g.CLAMP_TO_EDGE);g.texParameteri(g.TEXTURE_2D,g.TEXTURE_WRAP_T,g.CLAMP_TO_EDGE);this.owners[key]=t;
    }this.ready=true;
  }
  clear(){const w=this.canvas.clientWidth,h=this.canvas.clientHeight,dpr=Math.min(devicePixelRatio||1,2);if(this.canvas.width!==Math.round(w*dpr)||this.canvas.height!==Math.round(h*dpr)){this.canvas.width=Math.round(w*dpr);this.canvas.height=Math.round(h*dpr);}this.gl.viewport(0,0,this.canvas.width,this.canvas.height);this.gl.uniform2f(this.u.resolution,w,h);this.gl.clearColor(0,0,0,0);this.gl.clear(this.gl.COLOR_BUFFER_BIT);}
  draw(a,b,t,x,floor,height){if(!this.ready)return;const g=this.gl,u=this.u,A=this.data.poses[a],B=this.data.poses[b],key=a+'|'+b;
    if(!A||!B)throw Error('Missing dedicated pose '+a);
    if(this.fields[key]&&!this.flowTextures.has(key))this.flowTextures.set(key,this.fields[key].map(f=>this.texture(f,...this.data.flowSize)));
    const fields=this.flowTextures.get(key)||[this.zero,this.zero],textures=[this.textures[A.atlas],this.textures[B.atlas],...fields];
    ['A','B','F','G'].forEach((n,i)=>{g.activeTexture(g.TEXTURE0+i);g.bindTexture(g.TEXTURE_2D,textures[i]);g.uniform1i(u[n],i);});
    g.activeTexture(g.TEXTURE4);g.bindTexture(g.TEXTURE_2D,this.owners[A.atlas]);g.uniform1i(u.owners,4);g.uniform1f(u.ownerA,A.owner);g.uniform1f(u.ownerB,B.owner);
    g.uniform4fv(u.cellA,A.cell);g.uniform4fv(u.cellB,B.cell);g.uniform4fv(u.rectA,A.rect);g.uniform4fv(u.rectB,B.rect);g.uniform1f(u.amount,t);
    const h=height/.7,left=x-h/2,top=floor-h*.92,vertices=new Float32Array([left,top,0,0,left+h,top,1,0,left,top+h,0,1,left,top+h,0,1,left+h,top,1,0,left+h,top+h,1,1]);
    g.bindBuffer(g.ARRAY_BUFFER,this.buffer);g.bufferData(g.ARRAY_BUFFER,vertices,g.DYNAMIC_DRAW);g.enableVertexAttribArray(this.p);g.vertexAttribPointer(this.p,2,g.FLOAT,false,16,0);g.enableVertexAttribArray(this.uv);g.vertexAttribPointer(this.uv,2,g.FLOAT,false,16,8);g.drawArrays(g.TRIANGLES,0,6);
  }
}
globalThis.ClubPoseRenderer=ClubPoseRenderer;
