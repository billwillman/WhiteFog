(async function () {
'use strict';
const params=new URLSearchParams(location.search), single=params.get('skill')||String(window.localBattleSkillIds?.[0]||'');
const ids=[...new Set((params.get('skills')||single).split(','))], embedded=parent!==window;
const $=id=>document.getElementById(id), canvas=$('field'),overlay=$('overlay'),ctx=overlay.getContext('2d');
const send=data=>{if(embedded)parent.postMessage({type:'qingyun-local-skill',data},'*');};
if(embedded)document.body.classList.add('embedded');
function failed(error){document.body.classList.add('failed');$('status').textContent=error.message;send({kind:'error',text:error.message});}
function loadScript(path){
  return new Promise((resolve,reject)=>{const s=document.createElement('script');s.src=path;
    s.onload=resolve;s.onerror=()=>reject(Error('无法读取本地资源 '+path));document.head.append(s);});
}
try {
  if(!ids.includes(single)||!ids.every(id=>/^\d+$/.test(id)))throw Error('无效的技能 ID');
  await Promise.all(ids.map(id=>loadScript('battle-art/skills/'+id+'.js')));
  const packages=ids.map(id=>window.localBattlePackages[id]);
  const art=window.localBattleArt,gl=canvas.getContext('webgl',{alpha:false,antialias:true});if(!gl)throw Error('设备无法建立 H5 图形上下文');
  const bytes=s=>Uint8Array.from(atob(s),v=>v.charCodeAt(0));
  async function loadRig(encoded){
    const data=await new Response(new Blob([bytes(encoded)]).stream().pipeThrough(new DecompressionStream('gzip'))).json();
    const clips=new Map(data.clips.map(c=>[c.name,{...c,vertices:c.vertices.map(v=>new Int16Array(bytes(v).buffer))}]));
    const parts=await Promise.all(data.parts.map(async p=>{
      const uv=new Int16Array(bytes(p.uv).buffer),idx=new Uint16Array(bytes(p.indices).buffer);
      return {p,uv:buffer(gl,gl.ARRAY_BUFFER,uv),index:buffer(gl,gl.ELEMENT_ARRAY_BUFFER,idx),count:idx.length,
        frameSize:uv.length/2*3,vertex:gl.createBuffer(),tex:await texture(gl,p.texture,true,false)};
    }));
    return {parts,clips,idle:data.clips[0].name};
  }
  const rigFiles={'独臂木侠':['local-wooden-swordsman.js','localWoodenSwordsman']};
  const rigs=new Map([['player',await loadRig(window.localSwordsman)]]);
  for(const model of new Set(packages.map(p=>p.enemyModel).filter(Boolean))){
    const entry=rigFiles[model];if(!entry)throw Error('未烘焙角色 '+model);
    await loadScript('battle-art/'+entry[0]);rigs.set(model,await loadRig(window[entry[1]]));
  }
  const program=makeProgram(gl,`
    attribute vec3 pos;attribute vec2 coords;uniform float aspect,span,z,facing;varying vec2 uv;
    void main(){vec3 p=pos/2048.;p.xz*=facing;p.z+=z;p.y-=1.05;
      gl_Position=vec4(p.x/(span*aspect),(p.y*.78801075+p.z*.61566148)/span,(p.z*.78801075-p.y*.61566148)*.04,1.);
      uv=coords/4096.;}`,`uniform sampler2D tex;uniform float cutoff;varying vec2 uv;
      void main(){vec4 c=texture2D(tex,uv);if(c.a<cutoff)discard;gl_FragColor=vec4(c.rgb,1.);}`,['pos','coords']);
  const uniform=Object.fromEntries(['aspect','span','z','facing','cutoff'].map(k=>[k,gl.getUniformLocation(program,k)]));
  const clouds=await makeBattleClouds(gl,art),bird=await makeFlyingEnemy(gl,art.enemy);
  const presentation=makeBattlePresentation(gl),drawParticle=makeParticleRenderer(gl);
  const effects=new Map(), requests=new Map();
  const keyFor=(key,reversed)=>key+(reversed?'-back':'-front');
  async function loadFx(key,reverse){
    if(!key)return;
    const k=keyFor(key,reverse);if(requests.has(k))return requests.get(k);
    const task=(async()=>{
      await loadScript('battle-art/effects/'+encodeURIComponent(k)+'.js');
      const info=window.localBattleSheets[k];
      if(!info.light)throw Error('粒子图集需要重新烘焙 '+k);
      const [coverage,radiance]=await Promise.all([info.image,info.light].map(url=>texture(gl,url,false,false)));
      effects.set(k,{...info,coverage,radiance});
    })();requests.set(k,task);return task;
  }
  await Promise.all(packages.flatMap(p=>{
    const rig=rigs.get(p.opponent?p.enemyModel:'player'),nodes=p.graphs.find(g=>g.key===p.entry).nodes;
    return nodes.flatMap(n=>{
      if(n.op==='Animation'&&!rig?.clips.has(n.motion))throw Error('缺少本地动作 '+n.motion);
      if(n.op==='Effect'&&!rig?.parts.some(part=>part.p.attachment===n.fx&&part.p.bone===n.bone))
        return [loadFx(n.fx,p.opponent!==n.onTarget)];
      if(n.op==='Projectile')return [loadFx(n.fx,p.opponent),loadFx(n.trail,p.opponent),loadFx(n.impact,false)];
      return [];
    });
  }));
  const distance=Math.min(20,Math.max(2,Number(params.get('distance')||8))),level=Math.max(1,Number(params.get('level')||1));
  const hero={id:'player',name:'男角色 001',rig:rigs.get('player'),position:[0,0,-distance/2],forward:[0,0,1],hurtCenter:[0,1,0],hurtSize:[.85,1.95,.8],layer:0,tags:['Player']};
  const pair=packages.every(p=>!p.opponent&&!p.enemyModel) &&
    packages.some(p=>p.targetLimit>1) && params.get('targets')!=='1';
  const formation=pair?[{x:-1.5,z:0},{x:1.5,z:0}]:[{x:0,z:0}];
  const enemies=formation.map((slot,index)=>{
    const home=[slot.x,0,distance/2+slot.z];
    return {id:'EnemyBot'+String(index+1).padStart(2,'0'),name:index?'小雕 B':'小雕',
      home,position:[...home],forward:[0,0,-1],hurtCenter:[0,1,0],hurtSize:[1.15,1.4,1.25],layer:0,tags:['Enemy']};
  });
  const enemy=enemies[0],actors=[hero,...enemies];
  let current,span,time=0,last=null,frame=0,particles=[],numbers=[],attachments=[],runtimes,casts=new Map(),buffs=new Set();
  function pose(actor,t){
    const rig=actor.rig,motion=actor.motion;
    let clip=rig.clips.get(motion?.name),age=t-(motion?.start||0);
    if(!clip||age>=clip.duration){clip=rig.clips.get(rig.idle);age=t%clip.duration;}
    return {clip,index:Math.max(0,Math.min(clip.frames-1,Math.floor(age/clip.duration*(clip.frames-1))))};
  }
  for(const actor of actors)actor.anchor=(bone,t)=>{
    let sample;
    if(actor.rig){const {clip,index}=pose(actor,t);sample=clip.anchors?.find(b=>b.name===bone)?.positions[index];}
    const [x,y,z]=actor.position,f=actor.forward[2];return sample?[x+f*sample.x,y+sample.y,z+f*sample.z]:[x,y+(bone==='vfx_p_hit'?1.1:0),z];
  };
  const point=(actor,y)=>[actor.position[0],actor.position[1]+y,actor.position[2]];
  function project(p){return [overlay.width/2+p[0]*overlay.height/(2*span),overlay.height*(.5-((p[1]-1.05)*.78801075+p[2]*.61566148)/(2*span))];}
  function stamp(key,age,p,scale=1,loop=false){
    const fx=effects.get(key);if(!fx||age<0)return;
    let i=Math.floor(age*fx.rate+1e-6);if(loop)i%=fx.count;else if(i>=fx.count)return;
    const [x,y]=project(p),side=overlay.height*fx.metres/(2*span)*scale;
    const eyeOffset=p[2]*.78801075-(p[1]-1.05)*.61566148;
    drawParticle(fx,i,2*x/overlay.width-1,1-2*y/overlay.height,2*side/overlay.width,2*side/overlay.height,eyeOffset,scale);
  }
  function record(pkg,e,source=pkg.opponent?enemy:hero){
    const t=e.time,name=e.kind,recipient=actors.find(a=>a.id===e.target)||(pkg.opponent?hero:enemy);
    if(name==='animation')e.actor.motion={name:e.motion,start:t};
    if(name==='effect'){
      if(e.actor.rig?.parts.some(part=>part.p.attachment===e.effect&&part.p.bone===e.bone))
        attachments.push({actor:e.actor,bone:e.bone,key:e.effect,end:t+e.duration});
      else particles.push({key:keyFor(e.effect,e.actor===enemy),start:t,p:e.point,owner:e.follow?e.actor:null,bone:e.bone,
        scale:1,end:e.duration>0?t+e.duration:null});
    }
    if(name==='contact'&&e.shot){
      const n=e.shot.n;
      if(n.impact)particles.push({key:keyFor(n.impact,false),start:t,p:e.point,scale:n.impactScale});
    }
    if(name==='projectileEnd'&&e.shot){
      const n=e.shot.n,reverse=e.shot.caster===enemy;
      if(n.trail&&n.trailSeconds>0)particles.push({key:keyFor(n.trail,reverse),start:e.shot.born,p:e.point,scale:1,end:t+n.trailSeconds});
    }
    if(name==='statusStart')buffs.add(e.status);if(name==='statusEnd')buffs.delete(e.status);
    if((name==='hit'||name==='statusTick')&&e.loss>0)numbers.push({value:e.loss,start:t,p:point(recipient,1.65),status:name==='statusTick'});
    const data={kind:name,skillId:pkg.id,node:e.node,sourceNode:e.sourceNode,projectileNode:e.projectileNode,cast:e.cast,time:t,elapsed:e.elapsed,
      frameSeconds:time-(t-e.elapsed),text:name==='cast'?pkg.name:(e.target||''),motion:e.motion,effect:e.effect||e.shot?.n.fx,bone:e.bone,
      impact:e.shot?.n.impact,target:e.target,attribute:e.attribute,actor:e.actor?.id||source.id,enemyModel:enemy.name,
      segment:e.segment||0,loss:e.loss||0,shield:e.shield||0,casterHp:source.stats.chp,targetHp:recipient.stats.chp,targetStates:[...buffs].join('、')};
    if(['launch','contact','hit','miss','statusTick'].includes(name)){
      const li=document.createElement('li'),label={launch:'发射',contact:'碰撞',hit:'命中',miss:'落空',statusTick:e.status==='burn'?'灼烧':e.status}[name];
      li.textContent=e.elapsed.toFixed(3)+' 秒 · '+(e.sourceNode?e.sourceNode+' → ':'')+e.node+' · '+label+
        (['hit','statusTick'].includes(name)?'　'+(pair?(recipient===enemies[1]?'B ':'A '):'')+'扣血 '+e.loss+'，剩余 '+recipient.stats.chp:'');$('events').append(li);
    }
    canvas.dispatchEvent(new CustomEvent('skill-readout',{detail:data,bubbles:true}));send(data);
  }
  function drawRig(actor){
    gl.uniform1f(uniform.z,actor.position[2]);gl.uniform1f(uniform.facing,actor.forward[2]);
    const {clip,index}=pose(actor,time);
    for(const [i,part] of actor.rig.parts.entries()){
      if(part.p.attachment&&!attachments.some(a=>a.actor===actor&&a.key===part.p.attachment&&a.bone===part.p.bone))continue;
      const vertices=clip.vertices[i],off=vertices.length===part.frameSize?0:index*part.frameSize;
      gl.bindBuffer(gl.ARRAY_BUFFER,part.vertex);gl.bufferData(gl.ARRAY_BUFFER,vertices.subarray(off,off+part.frameSize),gl.DYNAMIC_DRAW);
      gl.enableVertexAttribArray(0);gl.vertexAttribPointer(0,3,gl.SHORT,false,0,0);
      gl.bindBuffer(gl.ARRAY_BUFFER,part.uv);gl.enableVertexAttribArray(1);gl.vertexAttribPointer(1,2,gl.SHORT,false,0,0);
      gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER,part.index);bindTexture(gl,0,part.tex);gl.uniform1f(uniform.cutoff,Math.max(.01,part.p.cutoff));
      gl.drawElements(gl.TRIANGLES,part.count,gl.UNSIGNED_SHORT,0);
    }
  }
  function draw(){
    const w=Math.max(1,Math.round(canvas.clientWidth*Math.min(devicePixelRatio,1.5))),h=Math.max(1,Math.round(canvas.clientHeight*Math.min(devicePixelRatio,1.5)));
    if(canvas.width!==w||canvas.height!==h){canvas.width=overlay.width=w;canvas.height=overlay.height=h;}
    presentation.begin(w,h);gl.clear(gl.COLOR_BUFFER_BIT|gl.DEPTH_BUFFER_BIT);clouds(time,w/h);
    for(const [i,actor] of enemies.entries()){
      if(actor.rig)continue;
      const b=project(point(actor,1));
      bird(w/h,time,[2*b[0]/w-1,1-2*b[1]/h],1);
    }
    gl.enable(gl.DEPTH_TEST);gl.depthFunc(gl.LESS);gl.useProgram(program);gl.uniform1f(uniform.aspect,w/h);gl.uniform1f(uniform.span,span);
    for(let i=2;i<4;i++)gl.disableVertexAttribArray(i);
    attachments=attachments.filter(a=>a.end>time);drawRig(hero);if(enemy.rig)drawRig(enemy);
    presentation.particles();ctx.clearRect(0,0,w,h);
    particles=particles.filter(f=>time<(f.end??f.start+effects.get(f.key).count/effects.get(f.key).rate));
    for(const f of particles)stamp(f.key,time-f.start,f.owner?f.owner.anchor(f.bone,time):f.p,f.scale);
    for(const rt of runtimes.values())for(const shot of rt.shots){
      const age=time-shot.born,rev=shot.caster===enemy;
      if(shot.n.trail)stamp(keyFor(shot.n.trail,rev),age,shot.position(time),1,true);
      stamp(keyFor(shot.n.fx,rev),age,shot.position(time),1,true);
    }
    presentation.present();
    numbers=numbers.filter(n=>time-n.start<.78);
    for(const n of numbers){const a=time-n.start,[x,y]=project(n.p);ctx.font='bold '+Math.round(h*.033)+'px serif';ctx.textAlign='center';ctx.lineWidth=3;ctx.strokeStyle='#42352b';
      ctx.fillStyle=n.status?'#ffcfa4':'#ffe682';ctx.globalAlpha=Math.min(1,(.78-a)*4);ctx.strokeText('−'+n.value,x,y-a*h*.08);ctx.fillText('−'+n.value,x,y-a*h*.08);}
    ctx.globalAlpha=1;
    const target=current.opponent?hero:enemy;
    $('health').textContent=pair?'A '+enemy.stats.chp+' / 80　B '+enemies[1].stats.chp+' / 80':target.name+'　'+target.stats.chp+' / '+(target===hero?100:80);
  }
  function tick(ts){
    try{if(last!==null&&!document.hidden)time+=Math.min(.12,(ts-last)/1000);
      last=document.hidden?null:ts;
      for(const rt of runtimes.values()){rt.advance(time);}
      draw();frame=requestAnimationFrame(tick);
    }catch(err){failed(err);}
  }
  function select(pkg){
    current=pkg;const model=pkg.enemyModel;enemy.rig=rigs.get(model);enemy.name=model||'小雕';
    span=Math.max(enemy.rig?5.9:3.75,distance*.32+2.3);$('skill-name').textContent=document.title=pkg.name;
  }
  function reset(){
    time=0;last=null;hero.motion=enemy.motion=null;particles=[];numbers=[];attachments=[];buffs.clear();casts.clear();
    hero.stats={atk:10,def:5,chp:100};
    enemies.forEach((actor,index)=>{
      actor.stats={atk:20,def:1,chp:80};
      actor.position=[...actor.home];
    });
    $('events').replaceChildren();
    runtimes=new Map(packages.map(p=>[p.id,LocalSkillRuntime.create(p,actors,e=>record(p,e))]));
  }
  function release(id){
    const p=packages.find(p=>p.id===Number(id));
    if(!p)return;
    select(p);
    const rt=runtimes.get(p.id);
    const targets=enemies.filter(actor=>actor.stats.chp>0);
    const c=rt.cast(p.opponent?enemy:hero,p.opponent?hero:targets[0],level,p.opponent?[]:targets.slice(1));
    if(c){
      casts.set(p.id,c);
    }else send({kind:'denied',skillId:p.id,text:'当前无法施放'});
  }
  function play(id,clear){
    const p=packages.find(p=>p.id===Number(id));if(!p)throw Error('技能未装载 '+id);
    if(clear)reset();release(p.id);
    cancelAnimationFrame(frame);last=null;frame=requestAnimationFrame(tick);
  }
  reset();select(packages.find(p=>p.id===Number(single)));$('replay').onclick=()=>play(current.id,true);
  window.addEventListener('message',({source,data})=>{
    if(!embedded||source!==parent||data?.type!=='qingyun-local-input')return;
    try{if(data.action==='begin'){
        cancelAnimationFrame(frame);reset();frame=requestAnimationFrame(tick);
      }
      if(data.action==='interrupt')runtimes.get(current.id).interrupt(casts.get(current.id));
      if(data.action==='replay'||data.action==='cast')play(data.skill||current.id,data.action==='replay');
      if(data.action==='pause')cancelAnimationFrame(frame);
    }catch(err){failed(err);}
  });
  window.addEventListener('pagehide',()=>cancelAnimationFrame(frame),{once:true});
  $('status').textContent='';$('replay').hidden=false;
  if(params.has('standby'))draw();else{play(single,true);}
  send({kind:'ready'});
}catch(err){failed(err);}
})();
