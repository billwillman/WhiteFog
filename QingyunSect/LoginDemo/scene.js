const canvas = document.querySelector('canvas');
const title = document.querySelector('h1');
const enter = document.querySelector('#enter');
const again = document.querySelector('#again');
const notice = document.querySelector('#notice');
const smooth = (a,b,x) => {
  const t = Math.min(1, Math.max(0,(x-a)/(b-a)));
  return t*t*(3-2*t);
};

function makeProgram(gl, vs, fs, attributes) {
  const program = gl.createProgram();
  const shaders = [[gl.VERTEX_SHADER,vs],[gl.FRAGMENT_SHADER,fs]].map(([type,source]) => {
    const shader = gl.createShader(type);
    gl.shaderSource(shader,'precision highp float;\n'+source);
    gl.compileShader(shader);
    if (!gl.getShaderParameter(shader,gl.COMPILE_STATUS)) throw Error(gl.getShaderInfoLog(shader));
    gl.attachShader(program,shader);
    return shader;
  });
  attributes.forEach((name,i)=>gl.bindAttribLocation(program,i,name));
  gl.linkProgram(program);
  shaders.forEach(s=>gl.deleteShader(s));
  if (!gl.getProgramParameter(program,gl.LINK_STATUS)) throw Error(gl.getProgramInfoLog(program));
  return program;
}
function buffer(gl,target,data,usage=gl.STATIC_DRAW) {
  const result=gl.createBuffer();
  gl.bindBuffer(target,result); gl.bufferData(target,data,usage);
  return result;
}
function attribute(gl,index,size,stride=0,offset=0) {
  gl.enableVertexAttribArray(index);
  gl.vertexAttribPointer(index,size,gl.FLOAT,false,stride,offset);
}
async function texture(gl,url,flip,premultiply) {
  const img=new Image();
  await new Promise((resolve,reject)=>{img.onload=resolve;img.onerror=()=>reject(Error('纹理载入失败'));img.src=url;});
  const result=gl.createTexture();
  gl.bindTexture(gl.TEXTURE_2D,result);
  gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL,flip);
  gl.pixelStorei(gl.UNPACK_PREMULTIPLY_ALPHA_WEBGL,premultiply);
  gl.texImage2D(gl.TEXTURE_2D,0,gl.RGBA,gl.RGBA,gl.UNSIGNED_BYTE,img);
  for (const param of [gl.TEXTURE_WRAP_S,gl.TEXTURE_WRAP_T]) gl.texParameteri(gl.TEXTURE_2D,param,gl.CLAMP_TO_EDGE);
  for (const param of [gl.TEXTURE_MIN_FILTER,gl.TEXTURE_MAG_FILTER]) gl.texParameteri(gl.TEXTURE_2D,param,gl.LINEAR);
  return result;
}
function bindTexture(gl,unit,tex) {
  gl.activeTexture(gl.TEXTURE0+unit); gl.bindTexture(gl.TEXTURE_2D,tex);
}

function cloudPass(gl,painting,flow) {
  const shared = `
#define float2 vec2
#define float3 vec3
#define float4 vec4
#define VORTEX_FRAC(x) fract(x)
#define VORTEX_MIX(a,b,t) mix(a,b,t)
varying vec2 uv,relief;
varying float depth;
uniform float aspect;
`;
  const layout=`
const vec2 hub=vec2(.638,.526);
const vec2 originalHub=vec2(.5,.35);
const vec2 orbitScale=vec2(.195,.62);
float visibleWidth(){return min(1.,aspect/(9./16.));}
float leftEdge(float width){return .20*(1.-width);}
`;
  const vs = shared + layout + `
attribute vec3 position;
attribute vec2 texcoord,surface;
void main(){
  uv=hub+(texcoord-originalHub);
  // Isotropic cover. On taller phones keep the crop close to the left cloud bank.
  float width=visibleWidth();
  float sx=(uv.x-leftEdge(width))/width;
  gl_Position=vec4(vec2(sx,uv.y)*2.-1.,0.,1.);
  relief=surface;depth=position.z;
}`;
  const fs=shared+layout+`
uniform sampler2D painting,flowCap;
uniform float cycle,passageMode;
uniform vec4 storm;
vec3 toLinear(vec3 c){return c*(c*(.305306*c+.682171)+.012523);}
vec3 toDisplay(vec3 c){return mix(12.92*c,1.055*pow(max(c,vec3(0)),vec3(1./2.4))-.055,step(vec3(.0031308),c));}
#define VORTEX_ART(uv) toLinear(texture2D(painting,uv).rgb)
#define VORTEX_FLOW(uv) texture2D(flowCap,uv)
`+assets.shader+`
// Reserve painted texels beyond the visible frame, with the opening as pivot.
const float frameScale=.92;
vec2 paintingUV(vec2 p){return hub+(p-hub)*frameScale;}
vec2 sourceUV(vec2 r,float turn,float travel,float offset,float variant,vec2 limit){
  float angle=turn*mix(offset,1.35*offset+.09*variant,passageMode);
  vec2 rotated=SampleClockwiseOrbit(originalHub+r,r,angle)-originalHub;
  vec2 delta=(rotated*exp2(-travel*offset)-r)/orbitScale;
  // Bound the whole vector smoothly so a cloud can cross any screen edge
  // without flattening one coordinate or reflecting a row of texels.
  vec2 relative=delta/limit;
  return uv+delta*inversesqrt(1.+dot(relative,relative));
}
void main(){
  vec4 flow=texture2D(flowCap,clamp(originalHub+(uv-hub),0.,1.));
  float dist=length((uv-hub)*vec2(9./16.,1.));
  float wall=smoothstep(.054,.18,dist);
  // Backtrace the nearer banks towards the opening as the camera advances.
  // The clear sky in the opening stays at its original scale.
  float foreground=passageMode*smoothstep(.076,.34,dist);
  vec2 push=(uv-hub)*(.145*foreground);
  vec2 r=(uv-hub-push)*orbitScale;
  float width=visibleWidth(),x=uv.x-leftEdge(width);
  vec2 edge=vec2(min(x,width-x)*(9./16.),min(uv.y,1.-uv.y));
  float detail=smoothstep(.06,.21,min(edge.x,edge.y));
  vec2 reserve=(1.-frameScale)/frameScale*min(hub,1.-hub);
  vec2 limit=.96*reserve+detail*vec2(.095,.075)+.045*foreground;
  float turn=wall*mix(.10,.24+.38*flow.r,detail);
  float travel=passageMode*wall*mix(.52,.98,detail);
  float rimPhase=.53+dot(uv-hub,vec2(.17,.27));
  float phase=fract(4.*cycle+mix(rimPhase,.37*flow.b+.018*depth,detail));
  vec2 a=sourceUV(r,turn,travel,phase-.5,0.,limit)-push;
  vec2 b=sourceUV(r,turn,travel,fract(phase+.5)-.5,1.,limit)-push;
  float crossfade=smoothstep(.19,.81,abs(2.*phase-1.));
  vec3 color=mix(texture2D(painting,paintingUV(a)).rgb,texture2D(painting,paintingUV(b)).rgb,crossfade);
  if(storm.w>0.){
    // Track the rolling pigment and its shadows, including in passage mode.
    vec2 d=(mix(a,b,crossfade)-storm.xy)*vec2(9./16.,1.)/storm.z+.23*relief;
    d.x+=.28*d.y;
    float halo=max(0.,(exp2(-1.55*dot(d,d))-.045)/.955)*wall;
    if(halo>0.){
      float cap=texture2D(flowCap,relief*.47+.5).a;
      vec3 linear=toLinear(color);
      const vec3 lumaWeights=vec3(.2126,.7152,.0722);
      float luma=dot(linear,lumaWeights);
      float crest=smoothstep(.30,.94,cap+.16*relief.y);
      float strength=halo*storm.w*(.74+.26*crest)*(1.-.44*smoothstep(.62,.97,luma));
      vec3 violet=mix(vec3(.50,.21,.82),vec3(.74,.43,.95),crest);
      violet/=dot(violet,lumaWeights);
      vec3 lit=mix(linear,luma*violet,.95*smoothstep(.012,.85,strength));
      lit+=violet*strength*(.18+.29*sqrt(luma))*(.78+.22*crest);
      lit/=max(1.,max(lit.r,max(lit.g,lit.b)));
      color=mix(color,toDisplay(lit),.68*smoothstep(.004,.27,strength));
    }
  }
  gl_FragColor=vec4(color,1.);
}`;
  const prog=makeProgram(gl,vs,fs,['position','texcoord','surface']);
  const loc = n=>gl.getUniformLocation(prog,n);
  const aspect=loc('aspect'),phase=loc('cycle'),passage=loc('passageMode'),storm=loc('storm');
  gl.useProgram(prog);
  gl.uniform1i(loc('painting'),0);gl.uniform1i(loc('flowCap'),1);
  const bytes=Uint8Array.from(atob(assets.mesh),c=>c.charCodeAt(0)).buffer;
  const header=new DataView(bytes);
  const count=header.getUint32(4,true),indexCount=header.getUint32(8,true);
  if(header.getUint32(0)!==0x47565831 || bytes.byteLength!==12+count*44+indexCount*2) throw Error('云洞网格数据有误');
  const vb=buffer(gl,gl.ARRAY_BUFFER,new Float32Array(bytes,12,count*11));
  const ib=buffer(gl,gl.ELEMENT_ARRAY_BUFFER,new Uint16Array(bytes,12+count*44,indexCount));
  const seed=Math.floor(Math.random()*65536);
  // Portrait cloud banks, in bottom-up UV. Each pair spans opposite sides.
  const stormSites=[[.18,.62],[.80,.32],[.21,.39],[.78,.68],
                    [.26,.76],[.64,.24],[.24,.27],[.72,.80]];
  function hash(i){let v=Math.imul(i+seed,747796405)+2891336453;v=Math.imul(v^(v>>>16),2246822519);return ((v^(v>>>13))>>>0)/4294967296;}
  return (time,cycle,moving)=>{
    gl.useProgram(prog);gl.disable(gl.BLEND);
    gl.bindBuffer(gl.ARRAY_BUFFER,vb);gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER,ib);
    attribute(gl,0,3,44,0);attribute(gl,1,2,44,12);attribute(gl,2,2,44,36);
    bindTexture(gl,0,painting);bindTexture(gl,1,flow);
    gl.uniform1f(aspect,canvas.width/canvas.height);gl.uniform1f(phase,cycle);
    gl.uniform1f(passage,moving);
    const n=Math.floor(time/3.2),age=time%3.2-.48-.29*hash(n*7);
    const pair=Math.floor(hash(n*7+3)*stormSites.length/2);
    const site=stormSites[pair*2+(n+seed)%2];
    const heat=smooth(0,.18,age)*(1-smooth(.43,1.55,age));
    gl.uniform4f(storm,site[0],site[1],.120+.010*hash(n*7+2),heat);
    gl.drawElements(gl.TRIANGLES,indexCount,gl.UNSIGNED_SHORT,0);
  };
}

// Evaluate only the Spine 3.8 channels used by this rig: translate, rotate
// and weighted mesh attachments. Local joint transforms accumulate down each chain.
function skinnedMesh(rig) {
  const mesh=rig.skins[0].attachments.disciple.rider;
  const timeline=rig.animations.hover.bones;
  const parents=rig.bones.map(b=>rig.bones.findIndex(other=>other.name===b.parent));
  const matrices=new Float32Array(rig.bones.length*4);
  const coords=new Float32Array(mesh.uvs.length);
  const duration=timeline.flight.rotate[timeline.flight.rotate.length-1].time;
  // All channels in this asset are sampled on the same evenly spaced time grid.
  function lerpFrame(frames,i,k,key){return (frames[i][key]||0)*(1-k)+(frames[i+1][key]||0)*k;}
  return {uvs:mesh.uvs,indices:mesh.triangles,coordinates:coords,update(time){
    const f=(time%duration)/duration*(timeline.flight.rotate.length-1);
    const i=Math.floor(f), k=f-i;
    rig.bones.forEach((b,j)=>{
      const t=timeline[b.name];
      const angle=(b.rotation||0)+(t?.rotate?lerpFrame(t.rotate,i,k,'angle'):0);
      const x=b.x+(t?.translate?lerpFrame(t.translate,i,k,'x'):0);
      const y=b.y+(t?.translate?lerpFrame(t.translate,i,k,'y'):0);
      const a=angle*Math.PI/180,c=Math.cos(a),s=Math.sin(a),o=j*4;
      const p=parents[j]*4;
      if(p<0){matrices.set([c,s,x,y],o);return;}
      const pc=matrices[p],ps=matrices[p+1];
      matrices[o]=pc*c-ps*s;matrices[o+1]=ps*c+pc*s;
      matrices[o+2]=matrices[p+2]+pc*x-ps*y;matrices[o+3]=matrices[p+3]+ps*x+pc*y;
    });
    let src=0;
    for(let j=0;j<coords.length;j+=2){
      let x=0,y=0;
      const count=mesh.vertices[src++];
      for(let k=0;k<count;k++){
        const b=mesh.vertices[src++]*4,px=mesh.vertices[src++],py=mesh.vertices[src++],w=mesh.vertices[src++];
        x+=(px*matrices[b]-py*matrices[b+1]+matrices[b+2])*w;
        y+=(px*matrices[b+1]+py*matrices[b]+matrices[b+3])*w;
      }
      coords[j]=x;coords[j+1]=y;
    }
  }};
}
function riderPass(gl,tex){
  const mesh=skinnedMesh(assets.rig);
  const prog=makeProgram(gl,`
    attribute vec2 position,uv;
    uniform vec2 framing;
    varying vec2 vUV;
    void main(){
      gl_Position=vec4(vec2(.015,-.355)+position*framing,0.,1.);vUV=uv;
    }`,`
    varying vec2 vUV; uniform sampler2D sprite;
    void main(){gl_FragColor=texture2D(sprite,vUV);}`,['position','uv']);
  const loc=n=>gl.getUniformLocation(prog,n);
  const framing=loc('framing');
  const spriteScale=890/assets.rig.skeleton.width;
  gl.useProgram(prog);gl.uniform1i(loc('sprite'),0);
  const vb=buffer(gl,gl.ARRAY_BUFFER,mesh.coordinates.byteLength,gl.DYNAMIC_DRAW);
  const ub=buffer(gl,gl.ARRAY_BUFFER,new Float32Array(mesh.uvs));
  const ib=buffer(gl,gl.ELEMENT_ARRAY_BUFFER,new Uint16Array(mesh.indices));
  return time=>{
    mesh.update(time);
    gl.useProgram(prog);gl.enable(gl.BLEND);gl.blendFunc(gl.ONE,gl.ONE_MINUS_SRC_ALPHA);
    gl.bindBuffer(gl.ARRAY_BUFFER,vb);gl.bufferSubData(gl.ARRAY_BUFFER,0,mesh.coordinates);attribute(gl,0,2);
    gl.bindBuffer(gl.ARRAY_BUFFER,ub);attribute(gl,1,2);
    gl.disableVertexAttribArray(2);gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER,ib);
    bindTexture(gl,0,tex);
    const designHeight=1080*canvas.height/canvas.width;
    gl.uniform2f(framing,2*spriteScale/1080,2*spriteScale/designHeight);
    gl.drawElements(gl.TRIANGLES,mesh.indices.length,gl.UNSIGNED_SHORT,0);
  };
}
let raf=0,last=null,animTime=0,flightStart=null,cloudCycle=0,lost=false,generation=0;
let drawCloud,drawRider;
const previewMode=new URLSearchParams(location.search).get('preview');
const previewCloud=previewMode==='cloud';
const previewStory=['map','ferry','passage','skills'].includes(previewMode);
let gl=previewStory?null:canvas.getContext('webgl',{alpha:false,antialias:false,depth:false,stencil:false,preserveDrawingBuffer:false});
function tick(stamp){
  raf=0;
  if(firstChapter.finished||document.hidden||(!firstChapter.shown&&(lost||!drawCloud)))return;
  if(last!==null && stamp-last<1000/30-1){raf=requestAnimationFrame(tick);return;}
  const dt=last===null?0:Math.min(.10,(stamp-last)/1000);last=stamp;
  animTime+=dt;
  const flying=flightStart!==null;
  const age=flying?animTime-flightStart:0;
  const move=smooth(0,1.65,age);
  cloudCycle=(cloudCycle+dt/(12.8-1.6*move))%1;
  if(!firstChapter.shown){
  const ratio=Math.min(devicePixelRatio||1,1080/canvas.clientWidth,1920/canvas.clientHeight);
  const w=Math.max(1,Math.round(canvas.clientWidth*ratio)),h=Math.max(1,Math.round(canvas.clientHeight*ratio));
  if(canvas.width!==w||canvas.height!==h){canvas.width=w;canvas.height=h;}
  gl.viewport(0,0,w,h);
  drawCloud(animTime,cloudCycle,move);
  drawRider(animTime);
  }
  const ui=1-smooth(0,.65,age);
  title.style.opacity=ui;enter.style.opacity=ui;
  again.hidden=!previewCloud||!flying||age<8;
  if(flying&&!previewCloud&&firstChapter.transition(age))return;
  raf=requestAnimationFrame(tick);
}
function resume(){
  if(raf)cancelAnimationFrame(raf);
  raf=0;last=null;
  if(!document.hidden&&!firstChapter.finished&&(firstChapter.shown||(!lost&&drawCloud)))raf=requestAnimationFrame(tick);
}
enter.addEventListener('click',()=>{if(flightStart===null){flightStart=animTime;enter.disabled=true;enter.style.pointerEvents='none';notice.hidden=true;if(!previewCloud)firstChapter.prepare();}});
again.addEventListener('click',()=>{flightStart=null;again.hidden=true;enter.disabled=false;enter.style.pointerEvents='';});
document.addEventListener('visibilitychange',resume);
canvas.addEventListener('webglcontextlost',event=>{
  if(firstChapter.shown){event.preventDefault();return;}
  event.preventDefault();lost=true;generation++;drawCloud=null;drawRider=null;resume();
  title.hidden=enter.hidden=again.hidden=true;notice.hidden=false;notice.textContent='正在恢复画面…';
});
canvas.addEventListener('webglcontextrestored',()=>{if(!firstChapter.shown){lost=false;start();}});
async function start(){
  const version=++generation;
  try{
    if(!gl)throw Error('此设备未开启 WebGL');
    const [paint,flow,actor]=await Promise.all([
      texture(gl,assets.painting,true,false),texture(gl,assets.flow,true,false),texture(gl,assets.rider,false,true)
    ]);
    if(version!==generation)return;
    gl.disable(gl.DEPTH_TEST);gl.disable(gl.CULL_FACE);
    const cloud=cloudPass(gl,paint,flow),rider=riderPass(gl,actor);
    drawCloud=cloud;drawRider=rider;
    notice.hidden=true;title.hidden=enter.hidden=false;
    resume();
  }catch(error){
    if(version!==generation)return;
    notice.textContent='画面载入失败：'+error.message;notice.hidden=false;
    console.error(error);
  }
}
if(previewStory) firstChapter.previewMap();
else start();
