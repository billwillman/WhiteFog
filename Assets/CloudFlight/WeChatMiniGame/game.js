const {createRenderer}=require('./renderer')
const canvas=wx.createCanvas()
const sys=wx.getWindowInfo?wx.getWindowInfo():wx.getSystemInfoSync()
const density=Math.min(sys.pixelRatio||1,960/Math.max(sys.windowWidth,sys.windowHeight),540/Math.min(sys.windowWidth,sys.windowHeight))
canvas.width=Math.max(1,Math.round(sys.windowWidth*density))
canvas.height=Math.max(1,Math.round(sys.windowHeight*density))
wx.setPreferredFramesPerSecond(30)
const options={alpha:false,antialias:false,depth:true,stencil:false,preserveDrawingBuffer:false}
const gl=canvas.getContext('webgl2',options)||canvas.getContext('webgl',options)
const fs=wx.getFileSystemManager()
function image(path){
  return new Promise((resolve,reject)=>{
    const img=wx.createImage()
    img.onload=()=>resolve(img)
    img.onerror=()=>reject(Error('Cannot load '+path))
    img.src=path
  })
}
let draw=null,elapsed=0,previous=0,frame=0,visible=true,overview=false
function loop(now){
  const ms=Number.isFinite(now)?now:Date.now()
  if(previous)elapsed+=Math.min(.1,Math.max(0,(ms-previous)/1000))
  previous=ms
  draw(elapsed,overview)
  frame=requestAnimationFrame(loop)
}
function resume(){
  if(visible&&draw&&!frame){previous=0;frame=requestAnimationFrame(loop)}
}
wx.onHide(()=>{visible=false;if(frame)cancelAnimationFrame(frame);frame=0})
wx.onShow(()=>{visible=true;resume()})
wx.onTouchStart(()=>{overview=!overview})
async function start(){
  if(!gl)throw Error('WebGL context unavailable')
  const [noise,cap]=await Promise.all([image('art/CloudNoise.png'),image('art/CloudMatCap.png')])
  const text=p=>fs.readFileSync(p,'utf8')
  draw=createRenderer(gl,{noise,cap,sites:JSON.parse(text('art/FlashSites.json')).points,
    flashSeed:Math.floor(Math.random()*65536),aircraft:JSON.parse(text('art/Aircraft.json')),
    sea:fs.readFileSync('art/CloudSea.cloudmesh'),volume:text('shaders/volume.txt')})
  resume()
  console.log('[CloudFlight] ready',gl.getParameter(gl.VERSION),canvas.width,canvas.height)
}
start().catch(error=>{console.error(error);wx.showModal({title:'云团演示启动失败',content:String(error.message||error),showCancel:false})})
