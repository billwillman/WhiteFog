const {pose}=require('./flight')
const cameraSource=`
uniform vec3 eye,forward,right,up;
uniform vec2 focal;
vec4 projectPoint(vec3 world){
  vec3 v=world-eye;
  float z=dot(v,forward);
  return vec4(dot(v,right)*focal.x,dot(v,up)*focal.y,((115.0+.2)*z-2.0*.2*115.0)/(115.0-.2),z);
}`
const solidVert=cameraSource+`
attribute vec3 position,normal,paint;
uniform mat4 model;
varying vec3 color;
void main(){
  float light=.63+.38*max(0.0,dot(normalize(mat3(model)*normal),normalize(vec3(-.3,.8,-.48))));
  color=paint*paint*light; gl_Position=projectPoint((model*vec4(position,1)).xyz);
}`
const solidFrag=`precision mediump float;
varying vec3 color;
void main(){gl_FragColor=vec4(sqrt(max(color,vec3(0))),1);}`
const cloudVert=cameraSource+`
attribute vec3 position;
attribute vec2 texcoord;
attribute vec4 paint;
uniform float shift;
varying vec3 world;
varying vec2 uv;
varying vec4 bakedColor;
varying float depth;
void main(){
  world=position+vec3(0,0,shift);uv=texcoord;bakedColor=paint;
  gl_Position=projectPoint(world);depth=gl_Position.w;
}`
const cloudHeader=`precision highp float;
#define float2 vec2
#define float3 vec3
#define float4 vec4
#define lerp mix
uniform sampler2D atlas,capTex;
uniform vec3 right,up;
uniform vec4 pulse;
uniform float core;
varying vec3 world;
varying vec2 uv;
varying vec4 bakedColor;
varying float depth;
float matcap(vec3 n){return texture2D(capTex,vec2(dot(n,right),dot(n,up))*.475+.5).r;}
#define CLOUD_CAP(n) matcap(n)
#define CLOUD_DETAIL(uv) texture2D(capTex,uv)
`
const cloudEnd=`
void main(){
  vec4 sampleData=bakedColor;
  if(core<.5)sampleData=texture2D(atlas,uv);
  vec4 c=SeaCloud(sampleData,world,depth,core,pulse);
  gl_FragColor=vec4(sqrt(max(c.rgb,vec3(0))),c.a);
}`

function readSea(bytes){
  const file=new DataView(bytes)
  if(file.getUint32(0,false)!==0x43534d31 || file.getUint32(4,true)!==8 || file.getUint32(8,true)!==192)
    throw Error('Invalid cloud geometry')
  let offset=12
  function take(Type,n){
    const start=offset
    offset+=Type.BYTES_PER_ELEMENT*n
    return start%Type.BYTES_PER_ELEMENT ? new Type(bytes.slice(start,offset)) : new Type(bytes,start,n)
  }
  function mesh(){
    const n=file.getUint32(offset,true),k=file.getUint32(offset+4,true)
    offset+=8
    return {positions:take(Float32Array,3*n),uv:take(Float32Array,2*n),colors:take(Uint8Array,4*n),indices:take(Uint16Array,k)}
  }
  return Array.from({length:8},()=>({core:mesh(),mist:mesh()}))
}

function createRenderer(gl,data) {
  function compile(type,source){
    const shader=gl.createShader(type);gl.shaderSource(shader,source);gl.compileShader(shader)
    if(!gl.getShaderParameter(shader,gl.COMPILE_STATUS))throw Error(gl.getShaderInfoLog(shader))
    return shader
  }
  function program(v,f){
    const p=gl.createProgram(),vs=compile(gl.VERTEX_SHADER,v),fs=compile(gl.FRAGMENT_SHADER,f)
    gl.attachShader(p,vs);gl.attachShader(p,fs)
    ;['position','normal','paint','texcoord'].forEach((k,i)=>gl.bindAttribLocation(p,i,k))
    gl.linkProgram(p);gl.deleteShader(vs);gl.deleteShader(fs)
    if(!gl.getProgramParameter(p,gl.LINK_STATUS))throw Error(gl.getProgramInfoLog(p))
    const u={}
    for(let i=0;i<gl.getProgramParameter(p,gl.ACTIVE_UNIFORMS);i++){
      const name=gl.getActiveUniform(p,i).name;u[name]=gl.getUniformLocation(p,name)
    }
    return {p,u}
  }
  const planeProgram=program(solidVert,solidFrag),cloud=program(cloudVert,cloudHeader+data.volume+cloudEnd)
  function buffer(target,values){
    const b=gl.createBuffer();gl.bindBuffer(target,b);gl.bufferData(target,values,gl.STATIC_DRAW);return b
  }
  function mesh(m,plane=false){
    return {v:buffer(gl.ARRAY_BUFFER,m.positions),n:m.normals&&buffer(gl.ARRAY_BUFFER,m.normals),
      c:buffer(gl.ARRAY_BUFFER,m.colors),uv:m.uv&&buffer(gl.ARRAY_BUFFER,m.uv),
      faces:buffer(gl.ELEMENT_ARRAY_BUFFER,m.indices),count:m.indices.length,channels:plane?3:4}
  }
  function attribute(index,b,size,type,normalized=false){
    if(!b){gl.disableVertexAttribArray(index);return}
    gl.bindBuffer(gl.ARRAY_BUFFER,b);gl.enableVertexAttribArray(index);gl.vertexAttribPointer(index,size,type,normalized,0,0)
  }
  function draw(m){
    attribute(0,m.v,3,gl.FLOAT);attribute(1,m.n,3,gl.FLOAT);attribute(2,m.c,m.channels,gl.UNSIGNED_BYTE,true);attribute(3,m.uv,2,gl.FLOAT)
    gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER,m.faces);gl.drawElements(gl.TRIANGLES,m.count,gl.UNSIGNED_SHORT,0)
  }
  function texture(unit,img){
    gl.activeTexture(gl.TEXTURE0+unit);gl.bindTexture(gl.TEXTURE_2D,gl.createTexture())
    gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL,true);gl.texImage2D(gl.TEXTURE_2D,0,gl.RGBA,gl.RGBA,gl.UNSIGNED_BYTE,img)
    for(const p of [gl.TEXTURE_WRAP_S,gl.TEXTURE_WRAP_T])gl.texParameteri(gl.TEXTURE_2D,p,unit===1?gl.REPEAT:gl.CLAMP_TO_EDGE)
    for(const p of [gl.TEXTURE_MAG_FILTER,gl.TEXTURE_MIN_FILTER])gl.texParameteri(gl.TEXTURE_2D,p,gl.LINEAR)
  }
  const a=data.aircraft
  const plane=mesh({positions:new Float32Array(a.positions),normals:new Float32Array(a.normals),colors:new Uint8Array(a.colors),indices:new Uint16Array(a.indices)},true)
  const sections=readSea(data.sea).map(s=>({core:mesh(s.core),mist:mesh(s.mist)}))
  texture(0,data.noise);texture(1,data.cap);gl.useProgram(cloud.p);gl.uniform1i(cloud.u.atlas,0);gl.uniform1i(cloud.u.capTex,1)
  const lens=1/Math.tan(61*Math.PI/360)
  function camera(shader,s){
    gl.useProgram(shader.p)
    const u=shader.u
    gl.uniform3fv(u.eye,s.eye);gl.uniform3fv(u.forward,s.forward);gl.uniform3fv(u.right,s.viewRight);gl.uniform3fv(u.up,s.viewUp)
    gl.uniform2f(u.focal,lens*gl.drawingBufferHeight/gl.drawingBufferWidth,lens)
  }
  return function render(t,wide=false,strength=1){
    const s=pose(t,wide,data.sites,data.flashSeed)
    gl.viewport(0,0,gl.drawingBufferWidth,gl.drawingBufferHeight);gl.enable(gl.DEPTH_TEST);gl.depthMask(true)
    gl.disable(gl.BLEND);gl.disable(gl.CULL_FACE);gl.clearColor(.31,.33,.43,1);gl.clear(gl.COLOR_BUFFER_BIT|gl.DEPTH_BUFFER_BIT)
    camera(planeProgram,s);gl.uniformMatrix4fv(planeProgram.u.model,false,s.model);draw(plane)
    camera(cloud,s);s.pulse[3]*=strength;gl.uniform4fv(cloud.u.pulse,s.pulse)
    const nearest=Math.floor((s.eye[2]-3)/24),farthest=Math.floor((s.eye[2]+96)/24)
    function section(k,dense){
      const i=((k%8)+8)%8
      gl.uniform1f(cloud.u.shift,(k-i)*24);draw(dense?sections[i].core:sections[i].mist)
    }
    gl.uniform1f(cloud.u.core,1)
    for(let k=nearest;k<=farthest;k++)section(k,true)
    gl.enable(gl.BLEND);gl.blendFunc(gl.SRC_ALPHA,gl.ONE_MINUS_SRC_ALPHA);gl.depthMask(false);gl.uniform1f(cloud.u.core,0)
    for(let k=farthest;k>=nearest;k--)section(k,false)
  }
}
module.exports={createRenderer}
