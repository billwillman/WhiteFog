// The WebGL player runs the same OriginalSkillGraph assets as the Unity workbench.
const sourceMoves = {
  entry: {name:'基础剑法', id:21020, seconds:1.5},
  pierce: {name:'破空剑法', id:25090, seconds:2.1},
  rain: {name:'流星剑雨', id:25120, seconds:2.3},
  boss: {name:'玄铁剑法-下', id:102412, seconds:2.633}
};
const sourceEncounters = {
  ferry: {slots:['entry'], casts:[[2.1,'entry']], finish:7.65},
  skills: {slots:['entry','pierce','rain'], casts:[[.6,'entry'],[3.7,'pierce'],[7.1,'rain'],[10.8,'boss']], period:14.5}
};
function makeBattleEntry(art,onVictory) {
  const byId = id => document.getElementById(id);
  const scene=byId('battle-entry'),canvas=byId('battle-role'),bar=byId('battle-bar');
  const label=byId('battle-skill'),result=byId('battle-result'),props=byId('battle-props');
  const context=props.getContext('2d'),boat=new Image(),ward=new Image(),edges=new Image();
  edges.src=art.edgeClouds;boat.src=assets.story.parts.boat.src; ward.src=art.ward.image;
  let player,loading,packet,closing,raf=0,mode,plan,age=0,last=null,castIndex=0,active=null,victory=false;
  bar.style.setProperty('--bar-image','url('+art.ui.worldcombat_bg_skill_group+')');
  const slots=Array.from({length:6},()=>{
    const li=document.createElement('li'),icon=new Image(),frame=new Image(),timer=document.createElement('span');
    li.style.backgroundImage='url('+art.ui.worldcombat_skill_state_bg01+')';
    icon.alt=frame.alt='';li.append(icon,frame,timer);bar.append(li);return {li,icon,frame,timer};
  });
  function loadPackage(){
    if(window.qingyunPlayerFiles)return Promise.resolve();
    if(!packet)packet=new Promise((resolve,reject)=>{
      const s=document.createElement('script');s.src='battle-art/unity-player.js';
      s.onload=resolve;s.onerror=()=>{packet=null;s.remove();reject(Error('云端战斗文件载入失败'));};
      document.head.append(s);
    });
    return packet;
  }
  async function startPlayer(){
    await closing;
    await loadPackage();
    const urls=[];
    try {
      const config={matchWebGLToCanvasSize:false,devicePixelRatio:1,companyName:'青云宗',
        productName:'云端御剑',productVersion:'1.3.0',streamingAssetsUrl:'StreamingAssets'};
      for(const [key,file] of Object.entries(window.qingyunPlayerFiles)){
        const bytes=Uint8Array.from(atob(file.bytes),c=>c.charCodeAt(0));
        let payload=new Blob([bytes]);
        if(file.gzip)payload=await new Response(payload.stream().pipeThrough(new DecompressionStream('gzip'))).blob();
        const url=URL.createObjectURL(new Blob([payload],{type:file.type}));urls.push(url);config[key]=url;
      }
      canvas.width=540;canvas.height=960;
      const instance=await createUnityInstance(canvas,config);
      if(art.birdSprite)instance.SendMessage('QingyunBattle','SetBirdSprite',art.birdSprite);
      instance.SendMessage('QingyunBattle','Pause','');
      player=instance;
    }finally{urls.forEach(url=>URL.revokeObjectURL(url));}
  }
  function prepare(){
    if(!loading)loading=startPlayer().catch(error=>{loading=null;throw error;});
    return loading;
  }
  function drawFerry(){
    const w=props.width,h=props.height;context.clearRect(0,0,w,h);
    if(edges.naturalWidth){
      const smooth=(lo,hi)=>{const x=Math.min(1,Math.max(0,(age-lo)/(hi-lo)));return x*x*(3-2*x);};
      const reduced=matchMedia('(prefers-reduced-motion: reduce)').matches;
      const t=reduced?0:age,gust=reduced?0:smooth(3.75,4.96)*(1-smooth(5.1,6.75));
      for(let side=0;side<2;side++){
        const width=w*((side ? .47 : .57)+.48*gust*side);
        const cx=(1+(side ? .91 : -.86)-.28*gust*side+.027*Math.sin(t*.24+2*side)*(1-.5*side))*w/2;
        const cy=(1-(side ? .38 : -.33)+.68*gust*side-.014*Math.sin(t*.19+side)*(1-.5*side))*h/2;
        context.globalAlpha=side ? .44 : .58;
        context.drawImage(edges,side*edges.naturalWidth/2,0,edges.naturalWidth/2,edges.naturalHeight,
          cx-width/2,cy-width*.345,width,width*.69);
      }
      context.globalAlpha=1;
    }
    const x=w*(.79-.045*Math.min(1,Math.max(0,(age-3.9)/3.6))), y=h*(.695-.027*Math.min(1,Math.max(0,(age-3.9)/3.6))+.003*Math.sin(age*1.8));
    const bw=w*.22,bh=bw*boat.naturalHeight/boat.naturalWidth;
    if(boat.naturalWidth)context.drawImage(boat,x-bw/2,y-bh/2,bw,bh);
    const fx=art.ward,i=Math.floor((age-4.65)*fx.rate);
    if(i>=0&&i<fx.count&&ward.naturalWidth){
      const [l,t,r,b]=fx.captureBounds,unit=2.7*h/768;
      context.drawImage(ward,(i%fx.cols)*fx.cell,Math.floor(i/fx.cols)*fx.cell,fx.cell,fx.cell,
        x+(l-192)*unit,y+(t-192)*unit,(r-l)*unit,(b-t)*unit);
    }
  }
  function tick(now){
    if(last!==null&&!document.hidden)age+=Math.min(.1,(now-last)/1000);
    last=document.hidden?null:now;
    if(plan.period&&age>=plan.period){age%=plan.period;castIndex=0;active=null;}
    while(castIndex<plan.casts.length&&plan.casts[castIndex][0]<=age){
      const [time,key]=plan.casts[castIndex++];active={time,key};
      player.SendMessage('QingyunBattle','Cast',String(sourceMoves[key].id));
    }
    const casting=active&&age<active.time+sourceMoves[active.key].seconds ? active.key:null;
    const text=mode==='ferry'&&age>=4.65&&age<6.25?'阵修 · 护舟':casting?sourceMoves[casting].name:'';
    if(label.textContent!==text)label.textContent=text;label.hidden=!text;
    plan.slots.forEach((key,i)=>{
      const {li,timer}=slots[i],at=plan.casts.find(c=>c[1]===key)[0];
      const wait=plan.period&&age>at&&casting!==key?Math.ceil(plan.period+at-age):0;
      li.className=casting===key?'casting':wait?'cooling':'';timer.textContent=wait?String(wait):'';
    });
    if(mode==='ferry')drawFerry();
    if(plan.finish&&age>=plan.finish&&!victory){result.hidden=false;victory=true;onVictory();}
    raf=requestAnimationFrame(tick);
  }
  function stop(){
    cancelAnimationFrame(raf);scene.hidden=true;last=null;
    if(player&&mode){closing=player.Quit();player=null;loading=null;mode=null;}
  }
  function show(which='ferry', skillKey){
    cancelAnimationFrame(raf);last=null;mode=which;
    const single=mode==='skills' && sourceMoves[skillKey];
    plan=single ? {slots:[skillKey],casts:[[.6,skillKey]],period:single.seconds+2} : sourceEncounters[mode];
    age=0;castIndex=0;active=null;victory=false;
    result.hidden=label.hidden=true;props.hidden=mode!=='ferry';scene.hidden=false;
    scene.querySelector('small').textContent=mode==='ferry'?'第一章　第三声渡钟':'功法试放';
    scene.querySelector('strong').textContent=mode==='ferry'?'雾里有灯':single?single.name:'剑修与独臂木侠';
    slots.forEach(({li,icon,frame,timer},i)=>{
      const skill=sourceMoves[plan.slots[i]];frame.hidden=!skill;icon.hidden=!skill || !art.ui[skill.id];
      if(skill){if(!icon.hidden)icon.src=art.ui[skill.id];frame.src=art.ui[skill.id===21020?'quality_skill_new_01':'quality_skill_new_05'];}
      li.setAttribute('aria-label',skill?skill.name:'空功法位');li.className='';timer.textContent='';
    });
    player.SendMessage('QingyunBattle','Begin',mode);raf=requestAnimationFrame(tick);
  }
  return {prepare,show,stop};
}
