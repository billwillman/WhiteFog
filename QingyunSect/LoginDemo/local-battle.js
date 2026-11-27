(async function () {
'use strict';
const params=new URLSearchParams(location.search), single=params.get('skill')||'25090';
const ids=[...new Set((params.get('skills')||single).split(','))], embedded=parent!==window;
const $=id=>document.getElementById(id), canvas=$('field'),overlay=$('overlay'),ctx=overlay.getContext('2d');
const send=data=>{if(embedded)parent.postMessage({type:'qingyun-local-skill',data},'*');};
if(embedded)document.body.classList.add('embedded');
const standby=embedded&&params.has('standby');
const entrance=standby?null:makeBattleTransition($('battle-transition'));
if(entrance)entrance.wait('正在载入本地技能包…');
function failed(error){entrance?.close();document.body.classList.add('failed');$('status').textContent=error.message;send({kind:'error',text:error.message});}
function loadScript(path){
  return new Promise((resolve,reject)=>{const s=document.createElement('script');s.src=path;
    s.onload=resolve;s.onerror=()=>reject(Error('无法读取本地资源 '+path));document.head.append(s);});
}
try {
  if(!ids.includes(single)||!ids.every(id=>/^\d+$/.test(id)))throw Error('无效的技能 ID');
  await Promise.all(ids.map(id=>loadScript('battle-art/skills/'+id+'.js')));
  const packages=ids.map(id=>window.localBattlePackages[id]);
  const encounter=params.get('encounter'),formal=encounter!==null&&encounter!=='skills';
  // The chapter HUD occupies the lower third of the portrait reference.
  const camera=makeBattleCamera(formal?21:38,formal ? .28 : 0);
  let party=null;
  const graphLevels=new Map(),nextReady=new Map();
  if(formal){
    await loadScript('battle-art/skill-tables.js');
    await Promise.all([loadScript('skill-rules.js'),loadScript('battle-art/encounters.js')]);await loadScript('battle-parties.js');
    party=resolveBattleParty(encounter,skillRules,packages.map(p=>p.id),
      embedded&&params.has('loadout')?JSON.parse(params.get('loadout')):null);
    bindLevels();
  }
  function bindLevels(){
    for(const slot of [...party.player,...party.enemy]){
      const p=packages.find(p=>p.id===slot.id),isEnemy=party.enemy.includes(slot);
      if(p.opponent!==isEnemy||(isEnemy&&party.waves.some(w=>w.enemy.some(m=>m.id===p.id)&&w.model!==p.enemyModel)))throw Error('技能包施放方与出战配置不符 '+p.id);
      if(isEnemy){graphLevels.set(p.id,1);continue;}
      const impacts=p.graphs.find(g=>g.key===p.entry).nodes.filter(n=>n.op==='FormulaImpact');
      const match=n=>n.levels.find(row=>{
        const fields=Object.fromEntries(row.fields.map(f=>[f.key,f.value]));
        if(fields['source.advance']!==undefined)return fields['source.advance']===slot.advance&&fields['source.level']===slot.level;
        return slot.advance===1&&slot.level===0&&row.level===1;
      });
      const levels=impacts.map(n=>match(n)?.level);
      if(levels.some(v=>v===undefined||v!==levels[0]))throw Error('工作台待导出对应阶级 '+p.name);
      graphLevels.set(p.id,levels[0]??1);
    }
  }
  const training=Object.assign(trainingRules.snapshot(trainingRules.initial()),formal&&embedded?JSON.parse(params.get('training')||'null'):null);
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
    const idle=data.clips[0].name,rest=clips.get(idle);
    let height=0;
    parts.forEach((part,i)=>{
      if(part.p.attachment)return;
      const vertices=rest.vertices[i];
      for(let y=1;y<part.frameSize;y+=3)height=Math.max(height,vertices[y]/2048);
    });
    return {parts,clips,idle,height};
  }
  const rigFiles={'独臂木侠':['local-wooden-swordsman.js','localWoodenSwordsman']};
  const rigs=new Map([['player',await loadRig(window.localSwordsman)]]);
  for(const model of new Set([...packages.map(p=>p.enemyModel),...(party?.waves??[]).filter(w=>!w.slam&&!party.practice).map(w=>w.model)].filter(Boolean))){
    const entry=rigFiles[model];if(!entry)throw Error('未烘焙角色 '+model);
    await loadScript('battle-art/'+entry[0]);rigs.set(model,await loadRig(window[entry[1]]));
  }
  let companionRig;
  if(training.ally && Object.hasOwn(trainingRules.companions,training.ally)){
    await loadScript('battle-art/pet-'+training.ally+'.js');
    companionRig=await loadRig(window.localCompanionSkin);
  }
  const program=makeProgram(gl,`
    attribute vec3 pos;attribute vec2 coords;uniform float facing,size;uniform vec3 origin;uniform mat4 clipFromWorld;varying vec2 uv;
    void main(){vec3 p=pos*size/2048.;p.xz*=facing;
      gl_Position=clipFromWorld*vec4(p+origin,1.);
      uv=coords/4096.;}`,`uniform sampler2D tex;uniform float cutoff;varying vec2 uv;
      void main(){vec4 c=texture2D(tex,uv);if(c.a<cutoff)discard;gl_FragColor=vec4(c.rgb,1.);}`,['pos','coords']);
  const uniform=Object.fromEntries(['origin','facing','size','cutoff','clipFromWorld'].map(k=>[k,gl.getUniformLocation(program,k)]));
  const clouds=await makeBattleClouds(gl,art),bird=await makeFlyingEnemy(gl,art.enemy);
  const presentation=makeBattlePresentation(gl),drawParticle=makeParticleRenderer(gl);
  const drawWard=training.ally||training.bead?makeWardRenderer(gl):null;
  const drawStone=party?.practice||party?.waves.some(w=>w.slam)?makeStoneGuardian(gl,party.practice):null;
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
  const headLayer=document.querySelector('.duel-heads');
  const distance=Math.min(20,Math.max(2,Number(params.get('distance')||8))),level=Math.max(1,Number(params.get('level')||1));
  const hero={id:'player',name:'男角色 001',rig:rigs.get('player'),position:[0,0,-distance/2],forward:[0,0,1],hurtCenter:[0,1,0],hurtSize:[.85,1.95,.8],layer:0,tags:['Player']};
  const pair=!formal && packages.every(p=>!p.opponent&&!p.enemyModel) &&
    packages.some(p=>p.targetLimit>1) && params.get('targets')!=='1';
  const waves=party?.waves??[{}];
  let enemyCount=0;
  const groups=waves.map(wave=>{
    const formation=wave.flock?.formation??(pair?[{x:-1.5,z:0},{x:1.5,z:0}]:[{x:0,z:0}]);
    return formation.map((slot,index)=>{
      const home=[slot.x,0,distance/2+slot.z];
      return {id:'EnemyBot'+String(++enemyCount).padStart(2,'0'),name:wave.model||wave.flock?.name||(index?'小雕 B':'小雕'),
        rig:rigs.get(wave.model),slam:wave.slam,firstDive:slot.firstDive,home,position:[...home],forward:[0,0,-1],
        hurtCenter:[0,wave.slam?1.15:1,0],hurtSize:wave.slam?[1.9,2.3,1.25]:[1.15,1.4,1.25],layer:0,tags:['Enemy']};
    });
  });
  const ally=companionRig?{id:'companion',name:trainingRules.companions[training.ally],rig:companionRig,size:1,
    position:[-1.65,0,hero.position[2]-.65],forward:[0,0,1],hurtCenter:[0,.9,0],hurtSize:[.75,1.6,.7],layer:0,tags:['Player']}:null;
  const actors=[hero,...groups.flat(),...(ally?[ally]:[])];
  let waveIndex=0,enemies=groups[0],enemy=enemies[0],flock=waves[0].flock;
  const heads=[];
  if(formal){
    const template=headLayer.children[1];
    heads.push(headLayer.children[0]);template.remove();
    for(const [i,wave] of waves.entries()){
      groups[i].forEach((actor,j)=>{
        const head=template.cloneNode(true);
        if(wave.flock)head.className='duel-head player-head flock-head';
        head.querySelector('.head-name').textContent=actor.name;
        head.setAttribute('aria-label',actor.name+(wave.flock?' '+(j+1):'')+' 生命');
        heads.push(head);headLayer.append(head);
      });
    }
    if(ally){
      const head=heads[0].cloneNode(true);head.querySelector('.head-name').textContent=ally.name;
      head.setAttribute('aria-label',ally.name+' 生命');heads.push(head);headLayer.append(head);
    }
    for(const [i,actor] of actors.entries()){
      actor.head=heads[i];
      actor.castTip=document.createElement('span');actor.castTip.className='world-cast-tip';actor.castTip.hidden=true;
      headLayer.append(actor.castTip);
    }
    headLayer.hidden=false;document.body.classList.add('world-duel');
  }
  let begun=false,ended=false,current,span,time=0,last=null,frame=0,particles=[],numbers=[],attachments=[],runtimes,casts=new Map(),buffs=new Set();
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
  function project(p){
    const [x,y]=camera.project(p);
    return [(1+x)*overlay.width/2,(1-y)*overlay.height/2];
  }
  // Place the nearest bars first. Use the actual DOM size so clamping also works after rotation.
  function updateHead(actor,occupied){
    const element=actor.head,modelTop=actor.rig?actor.rig.height*(actor.size??1):actor.hurtCenter[1]+actor.hurtSize[1]/2;
    const anchor=camera.project(point(actor,modelTop+.08));
    if(actor.headSizeAt!==canvas.width||actor.headHeightAt!==canvas.height){
      const rect=element.getBoundingClientRect();
      actor.headWidth=rect.width/canvas.clientWidth;actor.headHeight=rect.height/canvas.clientHeight;
      actor.headSizeAt=canvas.width;actor.headHeightAt=canvas.height;
    }
    const w=actor.headWidth,h=actor.headHeight,gap=.004;
    const x=Math.max(w/2,Math.min(1-w/2,(anchor[0]+1)/2));
    const wanted=Math.max(h+gap,Math.min(1-gap,(1-anchor[1])/2));
    const neighbors=occupied.filter(r=>Math.abs(r.x-x)<(r.w+w)/2+gap);
    let bottom=wanted;
    for(const r of neighbors.sort((a,b)=>b.bottom-a.bottom)){
      if(bottom>r.bottom-r.h-gap&&bottom-h<r.bottom+gap)bottom=r.bottom-r.h-gap;
    }
    if(bottom<h+gap){
      bottom=wanted;
      for(const r of neighbors.reverse()){
        if(bottom>r.bottom-r.h-gap&&bottom-h<r.bottom+gap)bottom=r.bottom+gap+h;
      }
    }
    occupied.push({x,w,h,bottom});
    const left=(100*x).toFixed(3)+'%',top=(100*bottom).toFixed(3)+'%';
    if(element.style.left!==left)element.style.left=left;
    if(element.style.top!==top)element.style.top=top;
    const tip=actor.castTip;
    tip.hidden=!(time<actor.tipUntil);
    if(!tip.hidden){
      const center=camera.project(point(actor,actor.hurtCenter[1]));
      const side=actor===hero||actor===ally?-.18:.18;
      tip.style.left=(100*Math.max(.035,Math.min(.965,(1+center[0])/2+side)))+'%';
      tip.style.top=(100*(1-center[1])/2)+'%';
    }
    const hp=actor.stats.chp;
    if(Number(element.getAttribute('aria-valuenow'))!==hp||!element.hasAttribute('aria-valuenow')){
      element.setAttribute('aria-valuenow',hp);
      element.style.setProperty('--missing',(100-100*Math.max(0,Math.min(1,hp/actor.maxHp)))+'%');
    }
  }
  function stamp(key,age,p,scale=1,loop=false){
    const fx=effects.get(key);if(!fx||age<0)return;
    let i=Math.floor(age*fx.rate+1e-6);if(loop)i%=fx.count;else if(i>=fx.count)return;
    const [x,y]=project(p),side=overlay.height*fx.metres/(2*span)*scale;
    const clipDepth=camera.depth(p);
    drawParticle(fx,i,2*x/overlay.width-1,1-2*y/overlay.height,2*side/overlay.width,2*side/overlay.height,clipDepth,camera.depthScale*fx.metres*scale);
  }
  function record(pkg,e,source=pkg.opponent?enemy:hero){
    const t=e.time,name=e.kind,recipient=actors.find(a=>a.id===e.target)||(pkg.opponent?hero:enemy);
    if(name==='animation'){
      e.actor.motion={name:e.motion,start:t};
      const clip=e.actor.rig?.clips.get(e.motion);
      if(formal&&clip&&e.actor===source&&source.tipUntil>t)source.tipUntil=t+clip.duration;
    }
    if(name==='effect'){
      if(e.actor.rig?.parts.some(part=>part.p.attachment===e.effect&&part.p.bone===e.bone))
        attachments.push({actor:e.actor,bone:e.bone,key:e.effect,end:t+e.duration});
      else particles.push({key:keyFor(e.effect,e.actor!==hero),start:t,p:e.point,owner:e.follow?e.actor:null,bone:e.bone,
        scale:1,end:e.duration>0?t+e.duration:null});
    }
    if(name==='contact'&&e.shot){
      const n=e.shot.n;
      if(n.impact)particles.push({key:keyFor(n.impact,false),start:t,p:e.point,scale:n.impactScale});
    }
    if(name==='projectileEnd'&&e.shot){
      const n=e.shot.n,reverse=e.shot.caster!==hero;
      if(n.trail&&n.trailSeconds>0)particles.push({key:keyFor(n.trail,reverse),start:e.shot.born,p:e.point,scale:1,end:t+n.trailSeconds});
    }
    if(name==='statusStart')buffs.add(e.status);if(name==='statusEnd')buffs.delete(e.status);
    if((name==='hit'||name==='statusTick')&&e.loss>0)numbers.push({value:e.loss,start:t,p:point(recipient,recipient.hurtCenter[1]+.28),status:name==='statusTick'});
    const data={kind:name,skillId:pkg.id,node:e.node,sourceNode:e.sourceNode,projectileNode:e.projectileNode,cast:e.cast,time:t,elapsed:e.elapsed,
      frameSeconds:time-(t-e.elapsed),text:name==='cast'?pkg.name:(e.target||''),motion:e.motion,effect:e.effect||e.shot?.n.fx,bone:e.bone,
      impact:e.shot?.n.impact,target:e.target,attribute:e.attribute,actor:e.actor?.id||source.id,enemyModel:enemy.name,
      segment:e.segment||0,loss:e.loss||0,shield:e.shield||0,casterHp:source.stats.chp,targetHp:recipient.stats.chp,targetStates:[...buffs].join('、')};
    if(party?.practice&&source===hero&&name==='cast'&&practiceImpactAt!==null){
      const seconds=t-practiceImpactAt;
      practiceImpactAt=null;
      trainingLog('practiceResume','云流冲击后 '+seconds.toFixed(2)+' 秒，基础剑法实际出手',seconds);
    }
    if(party?.practice&&name==='hit'&&source===hero&&e.loss>0&&!practiceHit){
      practiceHit=true;trainingLog('practiceHit','基础剑法首次命中木人，实际扣血 '+Number(e.loss.toFixed(2))+' 点',e.loss);
    }
    if(name==='cast'&&formal){
      source.castTip.textContent=pkg.name;source.tipUntil=t+(e.duration??1.5);
      data.duration=e.duration;
      const [x,y]=project(point(source,source.hurtCenter[1]));
      data.hudPoint=[100*x/overlay.width,100*y/overlay.height];
    }
    if(['launch','contact','hit','miss','statusTick'].includes(name)){
      const li=document.createElement('li'),label={launch:'发射',contact:'碰撞',hit:'命中',miss:'落空',statusTick:e.status==='burn'?'灼烧':e.status}[name];
      li.textContent=e.elapsed.toFixed(3)+' 秒 · '+(e.sourceNode?e.sourceNode+' → ':'')+e.node+' · '+label+
        (['hit','statusTick'].includes(name)?'　'+(pair?(recipient===enemies[1]?'B ':'A '):'')+'扣血 '+e.loss+'，剩余 '+recipient.stats.chp:'');$('events').append(li);
    }
    canvas.dispatchEvent(new CustomEvent('skill-readout',{detail:data,bubbles:true}));send(data);
    if(formal&&!ended&&(hero.stats.chp<=0||(waveIndex===waves.length-1&&enemies.every(actor=>actor.stats.chp<=0)))){
      ended=true;send({kind:'battleEnd',winner:hero.stats.chp>0?'player':'enemy'});
    }
  }
  function drawRig(actor){
    gl.uniform3fv(uniform.origin,actor.position);gl.uniform1f(uniform.size,actor.size??1);
    gl.uniform1f(uniform.facing,actor.forward[2]);
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
    camera.resize(w/h,span);presentation.begin(w,h);gl.clear(gl.COLOR_BUFFER_BIT|gl.DEPTH_BUFFER_BIT);clouds(time,w/h);
    for(const [i,actor] of enemies.entries()){
      if(actor.rig||actor.slam||party?.practice||(formal&&actor.stats.chp<=0))continue;
      bird(camera.matrix,time+(flock?i*.43:0),point(actor,1),flock && enemies.length>1 ? .82 : 1.22);
    }
    gl.enable(gl.DEPTH_TEST);gl.depthFunc(gl.LESS);gl.useProgram(program);gl.uniformMatrix4fv(uniform.clipFromWorld,false,camera.matrix);
    for(let i=2;i<4;i++)gl.disableVertexAttribArray(i);
    attachments=attachments.filter(a=>a.end>time);drawRig(hero);if(ally&&ally.stats.chp>0)drawRig(ally);if(enemy.rig)drawRig(enemy);
    if(party?.practice)drawStone(camera.matrix,enemy.position,0,1,1);
    if(enemy.slam&&enemy.stats.chp>0)drawStone(camera.matrix,enemy.position,time,enemy.strikeAt,enemy.slam.windup);
    presentation.particles();ctx.clearRect(0,0,w,h);
    particles=particles.filter(f=>time<(f.end??f.start+effects.get(f.key).count/effects.get(f.key).rate));
    const transparent=particles.map(f=>({key:f.key,age:time-f.start,at:f.owner?f.owner.anchor(f.bone,time):f.p,scale:f.scale}));
    for(const rt of runtimes.values())for(const shot of rt.shots){
      const age=time-shot.born,rev=shot.caster!==hero;
      const at=shot.position(time);
      if(shot.n.trail)transparent.push({key:keyFor(shot.n.trail,rev),age,at,loop:true});
      transparent.push({key:keyFor(shot.n.fx,rev),age,at,loop:true});
    }
    for(const [actor,until] of wards){
      if(until<=time){wards.delete(actor);continue;}
      transparent.push({ward:Math.min(.8,(until-time)*1.7),at:point(actor,1.05)});
    }
    for(const item of transparent)item.depth=camera.depth(item.at);
    transparent.sort((a,b)=>b.depth-a.depth);
    for(const item of transparent){
      if(item.ward)drawWard(camera,item.at,item.ward);
      else stamp(item.key,item.age,item.at,item.scale,item.loop);
    }
    presentation.present();
    if(formal){
      const visible=[];
      for(const actor of actors){
        actor.head.hidden=actor.active===false||actor.stats.chp<=0;
        if(actor.head.hidden)actor.castTip.hidden=true;
        else visible.push(actor);
      }
      visible.sort((a,b)=>camera.depth(a.position)-camera.depth(b.position));
      const occupied=[];for(const actor of visible)updateHead(actor,occupied);
    }
    numbers=numbers.filter(n=>time-n.start<.78);
    for(const n of numbers){
      const a=time-n.start,[x,y]=project(n.p),font=Math.round(Math.min(h*.033,w*.055));
      ctx.font='bold '+font+'px serif';ctx.textAlign='center';ctx.lineWidth=Math.max(2,font*.1);ctx.strokeStyle='#42352b';
      const text='−'+n.value,half=ctx.measureText(text).width/2+ctx.lineWidth;
      const screenX=Math.min(w-half,Math.max(half,x+font*1.5)),screenY=y-a*h*.05;
      ctx.fillStyle=n.status?'#ffcfa4':'#ffe682';ctx.globalAlpha=Math.min(1,(.78-a)*4);
      ctx.strokeText(text,screenX,screenY);ctx.fillText(text,screenX,screenY);
    }
    ctx.globalAlpha=1;
    if(time>=cueUntil)trainingCue.hidden=true;
    const target=current.opponent?hero:enemy;
    $('health').textContent=formal?[hero,...enemies].map(a=>(a===hero?'我方':a.name)+' '+a.stats.chp+' / '+a.maxHp).join('　'):
      pair?'A '+enemy.stats.chp+' / 80　B '+enemies[1].stats.chp+' / 80':target.name+'　'+target.stats.chp+' / '+(target===hero?100:80);
  }
  // Each bird commits to its own dive. Damage is resolved once at contact;
  // the return leg neither hits again nor survives a kill on the approach.
  const trainingCue=$('training-cue');
  const allyImpact=packages.flatMap(p=>p.opponent?[]:p.graphs.find(g=>g.key===p.entry).nodes)
    .find(n=>n.op==='Projectile'&&n.impact&&effects.has(keyFor(n.impact,false)));
  const wards=new Map();
  let cueUntil=0,defenses,recoverAt=0,gustDone=false,practiceHit=false,practiceImpactAt=null;
  function trainingLog(key,text,amount){
    send({kind:'training',key,text,amount,time});
    if(!embedded){trainingCue.textContent=text;trainingCue.hidden=false;cueUntil=time+1.25;}
  }
  function recoverFromImpact(){
    const seconds=training.breath?0.35:1;
    recoverAt=time+seconds;
    if(party?.practice)practiceImpactAt=time;
    trainingLog('recovery',(training.breath?'行气诀':'平息云流')+' · 受冲击后 '+seconds+' 秒可再次出招',seconds);
  }
  function guardDamage(damage){
    if(!ally||ally.stats.chp<=0||defenses.wards<=0||damage<=0)return damage;
    const blocked=Math.min(damage,training.ally==='adept'?3:5);
    ally.motion={name:'attack',start:time};defenses.wards--;wards.set(hero,time+.7);
    trainingLog('allyWard',ally.name+'护持 · 本次为主角抵消 '+blocked+' 点伤害',blocked);
    return damage-blocked;
  }
  const diveMove={name:'俯冲',opponent:true};
  function advanceDives(){
    const approach=.85,recover=1.05;
    for(const actor of enemies){
      if(ended||actor.stats.chp<=0)continue;
      if(!actor.dive&&time>=actor.nextDive){
        actor.nextDive=time+flock.diveInterval;
        if(!defenses.seal&&training.arts?.includes('seal')){defenses.seal=true;trainingLog('seal','镇行符 · 封住 '+actor.name+' 的俯冲',1);continue;}
        if(ally&&ally.stats.chp>0&&defenses.intercepts>0){
          defenses.intercepts--;
          ally.motion={name:'attack',start:time};
          if(allyImpact)particles.push({key:keyFor(allyImpact.impact,false),start:time,p:point(actor,1),scale:allyImpact.impactScale});
          trainingLog('intercept',ally.name+' · 截住第 '+(++defenses.intercepted)+' 次俯冲',defenses.intercepted);continue;
        }
        actor.dive={start:time,target:[...hero.position],hit:false};
        record(diveMove,{kind:'cast',time,elapsed:0,duration:approach,target:hero.id},actor);
      }
      const dive=actor.dive;
      if(!dive)continue;
      const elapsed=time-dive.start;
      const progress=elapsed<approach?elapsed/approach:Math.max(0,1-(elapsed-approach)/recover);
      const t=progress*progress*(3-2*progress);
      actor.position=actor.home.map((v,i)=>v+(dive.target[i]-v)*t);
      if(!dive.hit&&elapsed>=approach){
        dive.hit=true;
        const contact=hero.position.every((v,i)=>Math.abs(v-dive.target[i])<=(hero.hurtSize[i]+actor.hurtSize[i])/2);
        if(contact){
          const loss=Math.min(hero.stats.chp,guardDamage(Math.max(1,actor.stats.atk-hero.stats.def)));
          hero.stats.chp-=loss;if(loss>0)recoverFromImpact();
          record(diveMove,{kind:'hit',node:'俯冲',time,elapsed,target:hero.id,loss},actor);
        }
      }
      if(elapsed>=approach+recover)actor.dive=null;
    }
  }
  const slamMove={name:'震地',opponent:true};
  function advanceSlam(){
    const slam=enemy.slam;
    if(!slam||enemy.stats.chp<=0)return;
    if(time>=enemy.strikeAt){
      const bead=defenses.bead,guard=!defenses.guard&&training.arts?.includes('guard');defenses.bead=0;if(guard)defenses.guard=true;
      for(const target of (ally&&ally.stats.chp>0?[ally,hero]:[hero])){
        let damage=Math.max(1,enemy.stats.atk-target.stats.def);
        if(bead){
          const blocked=Math.min(damage,bead);damage-=blocked;wards.set(target,time+.85);
          trainingLog('bead-'+target.id,'定风珠 · 震地为'+(target===hero?'主角':target.name)+'抵消 '+blocked+' 点伤害',blocked);
        }
        if(target===hero&&guard){
          const blocked=Math.min(5,damage);damage-=blocked;trainingLog('guard','护脉 · 震地为主角抵消 '+blocked+' 点伤害',blocked);
        }
        if(target===hero)damage=guardDamage(damage);
        const loss=Math.min(target.stats.chp,damage);target.stats.chp-=loss;
        if(target===hero&&loss>0)recoverFromImpact();
        record(slamMove,{kind:'hit',node:'震地',time,elapsed:slam.windup,target:target.id,loss},enemy);
      }
      enemy.strikeAt+=slam.interval;enemy.winding=false;
    }else if(!enemy.winding&&time>=enemy.strikeAt-slam.windup){
      enemy.winding=true;
      record(slamMove,{kind:'cast',time,elapsed:0,duration:slam.windup,target:hero.id},enemy);
    }
  }
  function tick(ts){
    try{if(last!==null&&!document.hidden)time+=Math.min(.12,(ts-last)/1000);
      last=document.hidden?null:ts;
      for(const rt of runtimes.values()){if(!ended)rt.advance(time);}
      if(begun&&flock)advanceDives();
      if(begun&&!ended&&party){
        if(waveIndex+1<waves.length&&enemies.every(actor=>actor.stats.chp<=0))enterWave(waveIndex+1);
        if(party.practice&&!gustDone&&time>=.1){gustDone=true;recoverFromImpact();}
        advanceSlam();
        const move=waves[waveIndex].enemy.find(m=>time>=(nextReady.get(m.id)||0));
        if(move)release(move.id,true);
      }
      draw();if(!ended)frame=requestAnimationFrame(tick);
    }catch(err){failed(err);}
  }
  function select(pkg){
    current=pkg;
    if(!formal){enemy.rig=rigs.get(pkg.enemyModel);enemy.name=pkg.enemyModel||'小雕';}
    span=formal?Math.max(7,distance*.55):Math.max(enemy.rig||enemy.slam||party?.practice||flock?5.9:3.75,distance*.32+2.3);$('skill-name').textContent=document.title=pkg.name;
  }
  function enterWave(index){
    for(const actor of enemies)actor.active=false;
    waveIndex=index;enemies=groups[index];enemy=enemies[0];flock=waves[index].flock;
    for(const actor of enemies){
      actor.active=true;
      if(flock)actor.nextDive=time+actor.firstDive;
      if(actor.slam){actor.strikeAt=time+actor.slam.first;actor.winding=false;}
    }
    if(formal&&begun)send({kind:'waveStart',wave:index+1,total:waves.length,
      enemies:enemies.map(actor=>({id:actor.id,name:actor.name,health:actor.stats.chp})),playerHp:hero.stats.chp});
  }
  function reset(){
    ended=false;nextReady.clear();time=0;last=null;hero.motion=enemy.motion=null;particles=[];numbers=[];attachments=[];buffs.clear();casts.clear();
    hero.stats={atk:training.attack,def:5,chp:training.hp};
    hero.guardDamage=guardDamage;
    if(ally){ally.motion=null;ally.stats={atk:0,def:4,chp:65};}
    defenses={bead:training.bead===2?9:training.bead===1?5:0,guard:false,seal:false,
      wards:training.ally==='adept'?1:training.ally==='ward'?1+training.allyRank:0,
      intercepts:training.ally==='blade'?1+training.allyRank:0,intercepted:0};
    recoverAt=0;gustDone=false;practiceHit=false;practiceImpactAt=null;cueUntil=0;trainingCue.hidden=true;wards.clear();
    groups.forEach((group,index)=>{
      const wave=waves[index];
      for(const actor of group){
        actor.stats={atk:wave.flock?.attack??wave.attack??20,def:1,chp:wave.flock?.health??wave.health??80};
        actor.position=[...actor.home];actor.motion=null;actor.dive=null;actor.active=false;
      }
    });
    enterWave(0);
    $('events').replaceChildren();
    for(const [index,actor] of actors.entries()){
      actor.tipUntil=0;actor.maxHp=actor.stats.chp;
      if(heads.length)heads[index].setAttribute('aria-valuemax',actor.maxHp);
    }
    // Keep raw link attributes in the actor snapshot for formulas that explicitly read these source IDs.
    for(const link of party?.links||[])for(const [id,amount] of link.attribute||[]){
      const key='zszs.attr.'+id;hero.stats[key]=(hero.stats[key]||0)+amount;
    }
    runtimes=new Map(packages.map(p=>[p.id,LocalSkillRuntime.create(p,actors,e=>record(p,e))]));
  }
  function release(id,ai=false){
    const p=packages.find(p=>p.id===Number(id));
    if(!p||ended||(!ai&&time<recoverAt))return;
    const move=party&&(ai?waves[waveIndex].enemy:party.player).find(slot=>slot.id===p.id);
    if(party&&(!begun||!move||p.opponent!==ai||time<(nextReady.get(p.id)||0)))return;
    select(p);
    const rt=runtimes.get(p.id);
    const targets=flock?enemies.filter(actor=>actor.stats.chp>0):enemies;
    const c=rt.cast(p.opponent?enemy:hero,p.opponent?hero:targets[0],graphLevels.get(p.id)??level,p.opponent?[]:targets.slice(1));
    if(c){
      casts.set(p.id,c);
      if(move)nextReady.set(p.id,time+move.cooldown);
    }else send({kind:'denied',skillId:p.id,text:'当前无法施放'});
  }
  function play(id,clear){
    const p=packages.find(p=>p.id===Number(id));if(!p)throw Error('技能未装载 '+id);
    if(clear)reset();release(p.id);
    cancelAnimationFrame(frame);last=null;frame=requestAnimationFrame(tick);
  }
  async function preview(id,entry=false){
    const replay=$('replay');
    replay.disabled=true;
    try{
      if(!entry){cancelAnimationFrame(frame);await entrance.cover();}
      begun=false;reset();select(packages.find(p=>p.id===Number(id)));draw();
      await entrance.reveal();
      begun=true;
      // A formal start emits the initial wave event with the battle clock still at zero.
      play(id,formal);
    }finally{replay.disabled=false;}
  }
  if(entrance)$('replay').onclick=()=>preview(current.id).catch(failed);
  window.addEventListener('message',({source,data})=>{
    if(!embedded||source!==parent||data?.type!=='qingyun-local-input')return;
    try{
      if(data.action==='stage'&&standby&&!begun){draw();send({kind:'staged'});return;}
      if(data.action==='begin'&&(!formal||!begun)){
        cancelAnimationFrame(frame);begun=true;reset();frame=requestAnimationFrame(tick);
      }
      if(formal){
        if(data.action==='board'&&begun&&!ended&&!party.practice){
          const updated=resolveBattleParty(encounter,skillRules,packages.map(p=>p.id),data.loadout);
          for(const link of party.links)for(const [id,amount] of link.attribute||[])hero.stats['zszs.attr.'+id]-=amount;
          for(const link of updated.links)for(const [id,amount] of link.attribute||[]){
            const key='zszs.attr.'+id;hero.stats[key]=(hero.stats[key]||0)+amount;
          }
          party=updated;bindLevels();training.arts=data.arts.filter(id=>training.learnedArts?.includes(id));
        }
        if(data.action==='cast')release(data.skill,false);
        return;
      }
      if(data.action==='interrupt')runtimes.get(current.id).interrupt(casts.get(current.id));
      if(data.action==='replay'||data.action==='cast')play(data.skill||current.id,data.action==='replay');
      if(data.action==='pause')cancelAnimationFrame(frame);
    }catch(err){failed(err);}
  });
  window.addEventListener('pagehide',()=>cancelAnimationFrame(frame),{once:true});
  $('status').textContent='';$('replay').hidden=standby;
  if(standby){reset();select(packages.find(p=>p.id===Number(single)));draw();}
  else await preview(single,true);
  send({kind:'ready'});
}catch(err){failed(err);}
})();
