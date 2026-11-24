const {createVortexRenderer}=require('./renderer')
const canvas=wx.createCanvas()
const sessionSeed=Math.floor(Math.random()*65536)
function sizeCanvas(){
  const screen=wx.getWindowInfo ? wx.getWindowInfo() : wx.getSystemInfoSync()
  const w=screen.windowWidth,h=screen.windowHeight
  const scale=Math.min(screen.pixelRatio||1,960/Math.max(w,h),540/Math.min(w,h))
  canvas.width=Math.max(1,Math.round(w*scale));canvas.height=Math.max(1,Math.round(h*scale))
}
sizeCanvas()
if(wx.onWindowResize)wx.onWindowResize(sizeCanvas)
wx.setPreferredFramesPerSecond(30)
const options={alpha:false,antialias:false,stencil:false,depth:false}
const gl=canvas.getContext('webgl2',options)||canvas.getContext('webgl',options)
function image(path){
  return new Promise((resolve,reject)=>{
    const img=wx.createImage();img.onload=()=>resolve(img);img.onerror=()=>reject(Error('Cannot load '+path));img.src=path
  })
}
let fixedCamera=true
let draw=null,frame=0,active=true,previous=null,seconds=0
function loop(stamp){
  const now=Number.isFinite(stamp)?stamp:Date.now()
  if(previous!==null)seconds+=Math.min(.1,Math.max(0,(now-previous)/1000))
  previous=now;draw(seconds,sessionSeed,1,fixedCamera)
  frame=requestAnimationFrame(loop)
}
function resume(){if(active&&draw&&!frame){previous=null;frame=requestAnimationFrame(loop)}}
wx.onHide(()=>{active=false;if(frame)cancelAnimationFrame(frame);frame=0})
wx.onShow(()=>{active=true;resume()})
async function start(){
  if(!gl)throw Error('WebGL unavailable')
  const fs=wx.getFileSystemManager()
  fixedCamera=JSON.parse(fs.readFileSync('Resources/GoldenCloudCamera.json','utf8')).fixedCamera!==false
  const [painting,flow]=await Promise.all([image('art/CloudPainting.jpg'),image('art/CloudFlow.png')])
  draw=createVortexRenderer(gl,{painting,flow,mesh:fs.readFileSync('art/Funnel.bytes'),
    shader:fs.readFileSync('rolling.txt','utf8'),sites:JSON.parse(fs.readFileSync('art/FlashSites.json','utf8')).points})
  resume()
}
start().catch(e=>{console.error(e);wx.showModal({title:'云洞启动失败',content:String(e.message||e),showCancel:false})})
