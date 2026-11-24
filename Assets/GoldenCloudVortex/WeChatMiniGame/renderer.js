const common = `
#define float2 vec2
#define float3 vec3
#define float4 vec4
#define VORTEX_FRAC(x) fract(x)
#define VORTEX_MIX(a,b,t) mix(a,b,t)
uniform float cycle;
varying vec2 uv,relief;
varying float depth;
`
const vertex = `
attribute vec3 position;
attribute vec2 texcoord,surface;
uniform vec2 focal;
`
const fragment = `
uniform sampler2D painting,flowCap;
uniform vec4 storm;
vec3 toLinear(vec3 c){return c*(c*(.305306*c+.682171)+.012523);}
vec3 toDisplay(vec3 c){
  return mix(12.92*c,1.055*pow(max(c,vec3(0)),vec3(1.0/2.4))-.055,step(vec3(.0031308),c));
}
#define VORTEX_ART(uv) toLinear(texture2D(painting,uv).rgb)
#define VORTEX_FLOW(uv) texture2D(flowCap,uv)
`
const vertexEnd = `
void main(){
    vec3 p=ShapeCloudBank(position,texcoord);
    uv=texcoord;relief=surface;depth=position.z;
    gl_Position=vec4(p.xy*focal,((100.0+.2)*p.z-2.0*.2*100.0)/(100.0-.2),p.z);
}`
const fragmentEnd = `
void main(){gl_FragColor=vec4(toDisplay(RolledCloud(uv,relief,depth,cycle,storm)),1);}
`

function random01(seed){
  let h=(Math.imul(seed,747796405)+2891336453)>>>0
  h=Math.imul((h>>>((h>>>28)+4))^h,277803737)>>>0
  return ((h^(h>>>22))&65535)/65535
}
function ramp(a,b,x){
  const u=Math.min(1,Math.max(0,(x-a)/(b-a)))
  return u*u*(3-2*u)
}
function createVortexRenderer(gl,assets){
  function compile(type,source){
    const shader=gl.createShader(type)
    gl.shaderSource(shader,source);gl.compileShader(shader)
    if(!gl.getShaderParameter(shader,gl.COMPILE_STATUS))throw Error(gl.getShaderInfoLog(shader))
    return shader
  }
  const vs=compile(gl.VERTEX_SHADER,'precision highp float;\n#define VORTEX_VERTEX\n'+common+vertex+assets.shader+vertexEnd)
  const fs=compile(gl.FRAGMENT_SHADER,'precision highp float;\n'+common+fragment+assets.shader+fragmentEnd)
  const program=gl.createProgram()
  gl.attachShader(program,vs);gl.attachShader(program,fs)
  const attributes=['position','texcoord','surface']
  attributes.forEach((name,index)=>gl.bindAttribLocation(program,index,name))
  gl.linkProgram(program);gl.deleteShader(vs);gl.deleteShader(fs)
  if(!gl.getProgramParameter(program,gl.LINK_STATUS))throw Error(gl.getProgramInfoLog(program))
  gl.useProgram(program)
  const uniform=name=>gl.getUniformLocation(program,name)
  const uCycle=uniform('cycle'),uStorm=uniform('storm'),uFocal=uniform('focal')
  const file=new DataView(assets.mesh)
  if(file.getUint32(0)!==0x47565831)throw Error('Invalid funnel mesh')
  const n=file.getUint32(4,true),indices=file.getUint32(8,true),stride=44
  if(assets.mesh.byteLength!==12+n*stride+indices*2)throw Error('Truncated funnel mesh')
  const vb=gl.createBuffer();gl.bindBuffer(gl.ARRAY_BUFFER,vb)
  gl.bufferData(gl.ARRAY_BUFFER,new Float32Array(assets.mesh,12,n*11),gl.STATIC_DRAW)
  const ib=gl.createBuffer();gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER,ib)
  gl.bufferData(gl.ELEMENT_ARRAY_BUFFER,new Uint16Array(assets.mesh,12+n*stride,indices),gl.STATIC_DRAW)
  // GVX1 reserves floats 5 through 8 for the original mesh displacement.
  ;[[3,0],[2,12],[2,36]].forEach(([size,offset],i)=>{
    gl.enableVertexAttribArray(i);gl.vertexAttribPointer(i,size,gl.FLOAT,false,stride,offset)
  })
  function upload(unit,name,img){
    gl.activeTexture(gl.TEXTURE0+unit);gl.bindTexture(gl.TEXTURE_2D,gl.createTexture())
    gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL,true)
    gl.pixelStorei(gl.UNPACK_PREMULTIPLY_ALPHA_WEBGL,false)
    gl.texImage2D(gl.TEXTURE_2D,0,gl.RGBA,gl.RGBA,gl.UNSIGNED_BYTE,img)
    for(const axis of [gl.TEXTURE_WRAP_S,gl.TEXTURE_WRAP_T])gl.texParameteri(gl.TEXTURE_2D,axis,gl.CLAMP_TO_EDGE)
    for(const filter of [gl.TEXTURE_MIN_FILTER,gl.TEXTURE_MAG_FILTER])gl.texParameteri(gl.TEXTURE_2D,filter,gl.LINEAR)
    gl.uniform1i(uniform(name),unit)
  }
  upload(0,'painting',assets.painting);upload(1,'flowCap',assets.flow)
  gl.disable(gl.DEPTH_TEST);gl.disable(gl.CULL_FACE);gl.disable(gl.BLEND)
  const lens=1/Math.tan(25*Math.PI/180)
  let width=0,height=0
  return function draw(seconds,seed,strength=1){
    if(width!==gl.drawingBufferWidth||height!==gl.drawingBufferHeight){
      width=gl.drawingBufferWidth;height=gl.drawingBufferHeight
      const aspect=width/height,scale=Math.max(1,aspect/(16/9))
      gl.viewport(0,0,width,height);gl.uniform2f(uFocal,lens*scale/aspect,lens*scale)
    }
    const event=Math.floor(seconds/3.2),key=seed+event*7
    const slot=Math.min(assets.sites.length-1,Math.floor(random01(key+3)*assets.sites.length))
    const pos=assets.sites[slot],age=seconds-event*3.2-.48-.29*random01(key+1)
    const heat=ramp(0,.32,age)*(1-ramp(.32,1.26,age))
    gl.uniform1f(uCycle,(seconds/19.2)%1)
    gl.uniform4f(uStorm,pos.x,pos.y,.35+.09*random01(key+2),heat*strength)
    gl.drawElements(gl.TRIANGLES,indices,gl.UNSIGNED_SHORT,0)
  }
}
module.exports={createVortexRenderer}

