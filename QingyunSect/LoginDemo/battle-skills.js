// Cast and hit are presentation cues. Release follows the sampled casting hand.
const swordMoves = {
  entry: {name:'入门剑招',icon:'21020',clip:'jian_attack_1_yj',fx:'',travel:.38},
  pierce: {name:'破空剑法',icon:'25090',clip:'jian_pokong_25090',fx:'pierce',travel:.36},
  rain: {name:'流星剑雨',icon:'25120',clip:'jian_liuxing_25120',fx:'rain',travel:.54}
};
const swordEncounters = {
  ferry: {slots:['entry'],casts:[[2.1,'entry']],ward:4.65,finish:7.65},
  skills: {slots:['pierce','rain'],casts:[[.60,'pierce'],[4.55,'rain']],period:9.0}
};
function encounterFrame(plan,mode,time,local,move,clip,phase){
  const release=clip?clip.releaseFrame/clip.rate:0,duration=clip?(clip.frames-1)/clip.rate:0;
  const arrival=release+(move?.travel||0);
  const impact=clip?smooth(arrival,arrival+.10,phase)*(1-smooth(arrival+.10,arrival+.36,phase)):0;
  const diving=mode==='ferry'?smooth(.7,2.75,local):0;
  const fleeing=mode==='ferry'?smooth(3.51,5.35,local):0;
  const enemy=[.065*Math.sin(time*.85)+.16*diving+.98*fleeing,
    .35+.017*Math.sin(time*2.2)-.17*diving+1.05*fleeing+.06*impact];
  const boat=[.58-.085*smooth(3.9,7.5,local),-.39+.055*smooth(3.9,7.5,local)+.007*Math.sin(time*1.8)];
  const gust=mode==='ferry'?smooth(3.75,4.96,local)*(1-smooth(5.1,6.75,local)):0;
  return {plan,time,local,move,clip,phase,duration,release,arrival,enemy,boat,gust,
    wardAge:local-(plan.ward??Infinity),complete:local>=(plan.finish??Infinity)};
}
function battleFrame(mode,time,clips){
  const plan=swordEncounters[mode],local=plan.period?time%plan.period:time;
  let selected=null;
  for(const cast of plan.casts)if(cast[0]<=local)selected=cast;
  const move=selected?swordMoves[selected[1]]:null,clip=move?clips[move.clip]:null;
  const phase=selected?local-selected[0]:0;
  return encounterFrame(plan,mode,time,local,move,clip,phase);
}
function makeBattleBar(art){
  const bar=document.getElementById('battle-bar'), label=document.getElementById('battle-skill');
  const cells=[];
  bar.style.setProperty('--bar-image','url('+art.worldcombat_bg_skill_group+')');
  for(let i=0;i<6;i++){
    const slot=document.createElement('li'),icon=new Image(),frame=new Image(),timer=document.createElement('span');
    slot.style.backgroundImage='url('+art.worldcombat_skill_state_bg01+')';
    icon.alt='';frame.alt='';slot.append(icon,frame,timer);bar.append(slot);cells.push({slot,icon,frame,timer});
  }
  function configure(plan){
    cells.forEach(({slot,icon,frame,timer},i)=>{
      const move=swordMoves[plan.slots[i]];
      icon.hidden=frame.hidden=!move;
      if(move){icon.src=art[move.icon];frame.src=art[i===0&&move===swordMoves.entry?'quality_skill_new_01':'quality_skill_new_05'];}
      slot.setAttribute('aria-label',move?move.name:'空功法位');timer.textContent='';slot.className='';
    });
  }
  function update(s){
    s.plan.slots.forEach((id,i)=>{
      const {slot,timer}=cells[i],at=s.plan.casts.find(c=>c[1]===id)[0];
      const active=s.move===swordMoves[id]&&s.phase<s.duration;
      const wait=s.plan.period&&s.time>=at&&!active?Math.ceil(s.plan.period-(s.time-at)%s.plan.period):0;
      const status=active?'casting':wait?'cooling':'';
      if(slot.className!==status)slot.className=status;
      const text=wait?String(wait):'';if(timer.textContent!==text)timer.textContent=text;
    });
    let message='';
    if(s.move&&s.phase<s.duration)message=s.move.name;
    if(s.wardAge>=0&&s.wardAge<1.6)message='阵修 · 护舟';
    if(label.textContent!==message)label.textContent=message;label.hidden=!message;
  }
  return {configure,update};
}
function decodeSwordPose(data){
  const bytes=s=>Uint8Array.from(atob(s),c=>c.charCodeAt(0)).buffer;
  const clips={};
  for(const [id,clip] of Object.entries(data.clips))
    clips[id]={...clip,rows:new Float32Array(bytes(clip.rows)),hand:new Float32Array(bytes(clip.castingHand))};
  const parts=data.parts.map(part=>{
    const pos=new Int16Array(bytes(part.positions)),weights=new Uint8Array(bytes(part.weights)),v=new Float32Array(part.vertices*8);
    for(let n=0;n<part.vertices;n++){
      for(let j=0;j<3;j++)v[n*8+j]=pos[n*3+j]/4096;
      for(let j=0;j<5;j++)v[n*8+3+j]=weights[n*5+j]/255;
    }
    return v;
  });
  return {clips,parts};
}
function swordHand(s,aspect,time,release){
  if(!s.clip)return [0,-.35];
  const f=release?s.clip.releaseFrame:Math.min(s.clip.frames-1,Math.floor(s.phase*s.clip.rate));
  const [x,y,z]=s.clip.hand.subarray(f*3,f*3+3),h=z-.78,el=BATTLE_PITCH,yaw=-3.04;
  const c=Math.cos(yaw),v=Math.sin(yaw),ce=Math.cos(el),se=Math.sin(el);
  const d=7.5-(x*v*ce-y*c*ce+h*se);
  return [.009*Math.sin(time*.6)+1.70*(x*c+y*v)/(d*aspect),
    -.345+.012*Math.sin(time*1.55)+1.70*(-x*v*se+y*c*se+h*ce)/d];
}
