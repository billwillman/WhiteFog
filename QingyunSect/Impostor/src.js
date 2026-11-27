import {
  AmbientLight, AnimationMixer, Box3, Color, DirectionalLight, Group, IcosahedronGeometry,
  LoadingManager, Mesh, MeshLambertMaterial, OrthographicCamera, Scene, Vector3, WebGLRenderer
} from 'three';
import {GLTFLoader} from 'three/addons/loaders/GLTFLoader.js';

const $=id=>document.getElementById(id);
const view=$('view').getContext('2d'),atlas=$('atlas'), sheet=atlas.getContext('2d');
const scene=new Scene(),camera=new OrthographicCamera(-1,1,1,-1,.01,100);
const center=new Vector3();
let renderer,model,clips=[],mixer,originalPose=[],radius=1,name='',manifest=null;
let showAtlas=false,revision=0,busy=false,urls=[];
function status(s){$('status').textContent=s;}
function controls(){
  $('bake').disabled=busy||!model;
  for(const id of ['file','size','elevation','yaw','pose-time','clip','toggle'])$(id).disabled=busy;
  $('angles').disabled=busy;
  $('toggle').disabled=busy||!manifest;
}
function invalidate(){
  for(const url of urls)URL.revokeObjectURL(url);
  urls=[];manifest=null;showAtlas=false;$('result').hidden=true;
  $('preview-heading').textContent='场景组件预览';$('toggle').textContent='查看烘焙面片';controls();
}
function dispose(root){
  if(!root)return;
  const resources=new Set();
  root.traverse(o=>{
    if(o.geometry)resources.add(o.geometry);
    for(const m of [].concat(o.material||[])){
      resources.add(m);
      for(const value of Object.values(m))if(value?.isTexture)resources.add(value);
    }
    if(o.skeleton)resources.add(o.skeleton);
  });
  for(const r of resources)r.dispose();
}
function sampleRockCluster(){
  const group=new Group();
  const geometry=new IcosahedronGeometry(1,0);
  const material=new MeshLambertMaterial({color:0x718078,flatShading:true});
  // Asymmetric placement makes the different baked directions easy to compare.
  for(const [x,y,z,width,height,depth,turn] of [
    [-.55,.85,0,.82,1.35,.76,.3],
    [.52,.42,-.28,.76,.73,.66,1.1],
    [.24,.05,.62,.52,.36,.49,-.4]
  ]){
    const rock=new Mesh(geometry,material);
    rock.position.set(x,y,z);rock.scale.set(width,height,depth);rock.rotation.y=turn;
    group.add(rock);
  }
  return group;
}

function fit(){
  model.updateMatrixWorld(true);
  model.traverse(o=>{if(o.isSkinnedMesh)o.skeleton.update();});
  const box=new Box3().setFromObject(model,true);
  if(box.isEmpty())throw Error('模型中没有可烘焙的几何体');
  box.getCenter(center);radius=Math.max(.001,box.getSize(new Vector3()).length()*.52);
}
function drawModel(yaw,size){
  const a=yaw*Math.PI/180,e=Number($('elevation').value)*Math.PI/180;
  camera.left=camera.bottom=-radius;camera.right=camera.top=radius;
  camera.near=radius*.04;camera.far=radius*8;
  camera.position.set(Math.sin(a)*Math.cos(e),Math.sin(e),Math.cos(a)*Math.cos(e)).multiplyScalar(radius*3).add(center);
  camera.lookAt(center);camera.updateProjectionMatrix();
  renderer.setSize(size,size,false);renderer.render(scene,camera);
}
function preview(){
  if(!model||!renderer)return;
  const yaw=Number($('yaw').value);
  $('angle-out').value=yaw+'°';$('elev-out').value=$('elevation').value+'°';
  view.clearRect(0,0,480,480);
  if(showAtlas&&manifest){
    const f=impostorFrame(manifest,yaw);
    view.drawImage(atlas,f.x,f.y,f.w,f.h,0,0,480,480);
  }else{drawModel(yaw,480);view.drawImage(renderer.domElement,0,0);}
}
function takePose(){
  mixer?.stopAllAction();
  for(const [o,p,q,s,weights] of originalPose){
    o.position.copy(p);o.quaternion.copy(q);o.scale.copy(s);
    if(weights)for(let i=0;i<weights.length;i++)o.morphTargetInfluences[i]=weights[i];
  }
  const clip=clips[Number($('clip').value)];
  const time=Number($('pose-time').value)*(clip?.duration||0);
  $('time-out').value=time.toFixed(2)+' 秒';
  if(clip){mixer.clipAction(clip).reset().play();mixer.setTime(Math.min(time,Math.max(0,clip.duration-.00001)));}
  fit();invalidate();preview();
}
function install(root,animations,title){
  const prev=model,prevClips=clips;
  model=root;
  try{fit();}catch(error){model=prev;dispose(root);throw error;}
  if(prev){mixer?.stopAllAction();for(const clip of prevClips)mixer?.uncacheClip(clip);mixer?.uncacheRoot(prev);scene.remove(prev);dispose(prev);}
  scene.add(root);name=title;clips=animations;originalPose=[];
  root.traverse(o=>originalPose.push([o,o.position.clone(),o.quaternion.clone(),o.scale.clone(),o.morphTargetInfluences?.slice()]));
  mixer=clips.length?new AnimationMixer(root):null;
  $('clip').replaceChildren(new Option('默认状态','-1'));
  clips.forEach((c,i)=>$('clip').add(new Option(c.name||'动画片段 '+(i+1),String(i))));
  $('pose-controls').hidden=!clips.length;$('pose-time').value=0;
  invalidate();preview();status(title+' 已载入');
}
$('file').onchange=async()=>{
  const file=$('file').files[0];if(!file)return;
  const id=++revision;busy=true;controls();status('正在读取场景组件…');
  try{
    const data=await file.arrayBuffer();
    if(new DataView(data).getUint32(0,true)!==0x46546c67)throw Error('请选择 glTF 2.0 的 GLB 文件');
    const manager=new LoadingManager();manager.setURLModifier(url=>{
      if(/^(blob:|data:)/i.test(url))return url;throw Error('请将外部贴图和缓冲区嵌入 GLB');
    });
    const gltf=await new GLTFLoader(manager).parseAsync(data,'');
    if(id!==revision){dispose(gltf.scene);return;}
    install(gltf.scene,gltf.animations,file.name.replace(/\.glb$/i,''));
  }catch(e){status('导入失败 · '+e.message);}finally{if(id===revision){busy=false;controls();}}
};
function download(id,blob,filename){
  const url=URL.createObjectURL(blob);urls.push(url);$(id).href=url;$(id).download=filename;
}
$('bake').onclick=async()=>{
  if(busy||!model)return;
  invalidate();busy=true;controls();
  try{
    const size=Number($('size').value),count=Number($('angles').value),cols=Math.ceil(Math.sqrt(count));
    atlas.width=cols*size;atlas.height=Math.ceil(count/cols)*size;
    const frames=[];
    for(let i=0;i<count;i++){
      const yaw=i*360/count,x=i%cols*size,y=Math.floor(i/cols)*size;
      drawModel(yaw,size);sheet.drawImage(renderer.domElement,x,y);frames.push({yaw,x,y,w:size,h:size});
      status('正在烘焙 '+(i+1)+' / '+count);await new Promise(requestAnimationFrame);
    }
    const png=await new Promise(resolve=>atlas.toBlob(resolve,'image/png'));
    if(!png)throw Error('PNG 编码失败');
    manifest={version:1,image:name+'.png',width:atlas.width,height:atlas.height,elevation:Number($('elevation').value),frames};
    download('png',png,manifest.image);
    download('json',new Blob([JSON.stringify(manifest,null,2)],{type:'application/json'}),name+'.json');
    $('result').hidden=false;showAtlas=true;$('preview-heading').textContent='面片预览';$('toggle').textContent='查看原组件';
    status(count+' 个角度 · '+atlas.width+' × '+atlas.height+' 像素');preview();
  }catch(e){invalidate();status('烘焙失败 · '+e.message);}finally{busy=false;controls();}
};
$('toggle').onclick=()=>{showAtlas=!showAtlas;$('preview-heading').textContent=showAtlas?'面片预览':'场景组件预览';$('toggle').textContent=showAtlas?'查看原组件':'查看烘焙面片';preview();};
$('yaw').oninput=preview;
for(const id of ['elevation','size','angles'])$(id).oninput=()=>{invalidate();preview();};
for(const id of ['clip','pose-time'])$(id).oninput=takePose;
try{
  renderer=new WebGLRenderer({alpha:true,antialias:true,preserveDrawingBuffer:false});
  renderer.setClearColor(new Color(0),0);
  scene.add(new AmbientLight(0xffffff,1.65));
  const sun=new DirectionalLight(0xfff1d8,2.0);sun.position.set(-3,6,4);scene.add(sun);
  install(sampleRockCluster(),[],'山石组合');
}catch(e){busy=true;controls();status('需要支持 WebGL 2 的浏览器 · '+e.message);}
