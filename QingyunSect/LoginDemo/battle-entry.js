// Choose the exported runtime by skill ID when preparing an encounter.
const sourceMoves = {
  entry: {name:'基础剑法', id:21020, seconds:1.5},
  shaoyang: {name:'少阳指',id:22020,seconds:1.5},
  baihong: {name:'白虹剑',id:22030,seconds:1.5},
  pierce: {name:'破空剑法', id:25090, seconds:2.1},
  rain: {name:'流星剑雨', id:25120, seconds:2.3},
  boss: {name:'玄铁剑法-下', id:102412, seconds:2.633}
};
const sourceEncounters = {
  skills: {slots:['entry','pierce','rain',null,null,'boss'], casts:[[.6,'entry'],[3.7,'pierce'],[7.1,'rain'],[10.8,'boss']], period:14.5}
};
function makeBattleEntry(art,onPracticeHit) {
  const byId = id => document.getElementById(id);
  const scene=byId('battle-entry'),canvas=byId('battle-role'),bar=byId('battle-bar');
  const label=byId('battle-skill'),result=byId('battle-result');
  const controls=byId('battle-controls'),auto=byId('battle-auto'),attack=byId('battle-attack');
  let raf=0,mode,plan,age=0,last=null,castIndex=0,active=null,victory=false;
  let automatic=true, readyAt=0, party=null, nextAutoPoll=0;
  const cooldowns=new Map(),feedback=byId('battle-feedback');
  let feedbackUntil=0,onResult,requestVersion=0;
  const damageBySkill=new Map(),report=scene.querySelector('.duel-report');
  function keyForMove(id){
    const found=Object.keys(sourceMoves).find(k=>sourceMoves[k].id===id);
    if(found)return found;
    const key=String(id);
    sourceMoves[key]={id,name:skillRules.combat.get(id).name,seconds:0};
    return key;
  }
  const trainingReport=byId('training-report'),trainingEvents=new Map();
  function finishBattle(won){
    if(!party||victory)return;
    victory=true;feedback.hidden=true;board.close();controls.hidden=true;result.hidden=false;
    result.querySelector('h2').textContent=won?'胜利':'失败';
    result.dataset.outcome=won?'win':'lose';
    scene.classList.add('settled');
    const details=byId('duel-damage'),rows=details.querySelector('ol');
    const hits=[...damageBySkill].sort((a,b)=>b[1]-a[1]);
    const total=hits.reduce((sum,[,value])=>sum+value,0);
    details.open=false;details.classList.toggle('empty',!hits.length);rows.replaceChildren();
    for(const [id,value] of hits){
      const row=document.createElement('li'),name=document.createElement('span'),track=document.createElement('span'),fill=document.createElement('i'),amount=document.createElement('span');
      name.textContent=sourceMoves[keyForMove(id)].name;track.className='damage-track';fill.style.width=(100*value/total)+'%';track.append(fill);
      amount.textContent=Math.round(value)+' · '+Math.round(100*value/total)+'%';row.append(name,track,amount);rows.append(row);
    }
    details.querySelector('.damage-total').textContent=String(Math.round(total));
    details.querySelector('.damage-share').textContent=total?'100%':'0%';
    details.querySelector('summary i').style.width=total?'100%':'0';
    details.querySelector('summary').setAttribute('aria-label','武学伤害 '+Math.round(total)+(hits.length?'，查看各功法伤害':''));
    details.querySelector('summary').setAttribute('aria-disabled',String(!hits.length));
    details.querySelector('summary').tabIndex=hits.length?0:-1;
    trainingReport.textContent=[...trainingEvents.values()].join('\n');
    trainingReport.hidden=!trainingEvents.size;
    report.hidden=false;
    onResult?.(won);
    byId('battle-return').focus({preventScroll:true});
  }
  byId('duel-damage').querySelector('summary').addEventListener('click',event=>{
    if(!damageBySkill.size)event.preventDefault();
  });
  auto.style.backgroundImage='url('+art.ui.worldcombat_btn_switch+')';
  attack.style.backgroundImage='url('+art.ui.worldcombat_btn_combat+')';
  bar.style.setProperty('--bar-image','url('+art.ui.worldcombat_bg_skill_group+')');
  bar.style.setProperty('--slot-image','url('+art.ui.worldcombat_skill_state_bg01+')');
  const slots=Array.from({length:6},()=>{
    const li=document.createElement('li'),icon=new Image(),frame=new Image(),timer=document.createElement('span');
    const button=document.createElement('button'),name=document.createElement('small');
    button.type='button';button.onclick=()=>{if(!editable())manual(button.dataset.key);};
    icon.alt=frame.alt='';li.append(icon,frame,timer,name,button);bar.append(li);return {li,icon,frame,timer,name,button};
  });
  const editable=()=>!!party&&!party.practice&&!!mode&&mode!=='skills'&&!victory;
  let battleBag,learnedArts=[];
  const board=makeBattleBoard(bar,art,state=>{
    battleBag=state;
    const loadout=skillInventory.loadout(state);
    const updated=resolveBattleParty(mode,skillRules,window.localBattleSkillIds,loadout);
    party.player=updated.player;party.links=updated.links;
    frame.contentWindow.postMessage({type:'qingyun-local-input',action:'board',loadout,arts:activeArts()},'*');
    showSlots();
  });
  const activeArts=()=>battleBag?.slots.filter(id=>learnedArts.includes(id))||[];
  let frame, ready, preparedKey, entryCancel, stageComplete, defeated=false;
  const input=(action,key)=>frame?.contentWindow?.postMessage({type:'qingyun-local-input',action,skill:key?sourceMoves[key].id:undefined},'*');
  async function prepare(which,key,training){
    const version=++requestVersion;
    const arts=training?.learnedArts||[];
    const state=which!=='skills'?await skillInventory.readForBattle(arts,training?.previousArt):null;
    const loadout=state?skillInventory.loadout(state):null;
    if(version!==requestVersion)throw Error('已退出战斗');
    let configuration=null;
    try{if(which!=='skills')configuration=resolveBattleParty(which,skillRules,window.localBattleSkillIds||[],loadout);}
    catch(error){return Promise.reject(error);}
    let ids=configuration?[...configuration.player,...configuration.enemy].map(move=>move.id):
      (key?[key]:[...new Set(sourceEncounters.skills.casts.map(c=>c[1]))]).map(k=>sourceMoves[k].id);
    if(configuration&&!configuration.practice){
      const candidates=[...Object.keys(state.books).map(Number),...window.qingyunBookStart.ordinaryResults];
      ids=[...new Set([...ids,...candidates.map(book=>{
        const item=state.books[book];return skillRules.resolve(book,item?.advance??1,item?.level??0)?.skill;
      }).filter(id=>window.localBattleSkillIds.includes(id))])];
    }
    const missing=ids.find(id=>!window.localBattleSkillIds?.includes(id));
    if(missing)return Promise.reject(Error('缺少 Unity 导出的本地技能包 '+missing));
    const signature=JSON.stringify([which,ids,state,training]);
    if(preparedKey===signature&&ready)return ready;
    entryCancel?.();
    preparedKey=signature;party=configuration;battleBag=state;learnedArts=arts;
    const fightTraining={...training,arts:activeArts()};
    frame=document.createElement('iframe');frame.title='云端战斗';frame.className='battle-world';
    const trial=frame;
    ready=new Promise((resolve,reject)=>{
      const timeout=setTimeout(()=>fail(Error('战场载入超时，请重试。')),30000);
      function dispose(){
        clearTimeout(timeout);window.removeEventListener('message',receive);trial.remove();
        ready=null;preparedKey=null;entryCancel=null;
      }
      function fail(error){dispose();stageComplete?.(error);reject(error);}
      function receive({source,data}){
        if(source!==trial.contentWindow||data?.type!=='qingyun-local-skill')return;
        if(data.data.kind==='ready'){clearTimeout(timeout);resolve();}
        if(data.data.kind==='staged')stageComplete?.();
        if(mode&&data.data.kind==='battleEnd')finishBattle(data.data.winner==='player');
        if(party&&mode&&data.data.kind==='training'){
          const event=data.data;
          trainingEvents.set(event.key,event.text);
          if(!victory){feedback.textContent=event.text;feedback.hidden=false;feedbackUntil=age+1.5;}
          if(party.practice&&event.key==='practiceHit')onPracticeHit?.(training,event.amount);
        }
        if(party&&mode&&data.data.kind==='cast'){
          const move=party.player.find(m=>m.id===data.data.skillId);
          if(move){
            const key=keyForMove(move.id);
            cooldowns.set(key,age+move.cooldown);active={key,time:age};
          }

        }
        if(['hit','statusTick'].includes(data.data.kind)){
          const hit=data.data;
          if(party&&!victory&&hit.actor==='player'&&hit.loss>0&&(hit.kind==='hit'||hit.attribute==='chp')&&hit.skillId){
            damageBySkill.set(hit.skillId,(damageBySkill.get(hit.skillId)||0)+hit.loss);
          }
          if(!party&&hit.targetHp<=0)defeated=true;
        }
        if(data.data.kind==='error')fail(Error(data.data.text));
      }
      entryCancel=()=>fail(Error('已退出战斗'));
      window.addEventListener('message',receive);
      trial.onerror=()=>fail(Error('无法读取本地战斗页面'));
    });
    trial.src='local-battle.html?standby=1&encounter='+encodeURIComponent(which)+'&skill='+ids[0]+'&skills='+ids.join(',')+(loadout?'&loadout='+encodeURIComponent(JSON.stringify(loadout)):'')+
      (training?'&training='+encodeURIComponent(JSON.stringify(fightTraining)):'');
    canvas.hidden=true;canvas.after(trial);return ready;
  }
  function cast(key,time){
    if(mode!=='skills'&&!party?.player.some(m=>m.id===sourceMoves[key]?.id))return;
    if(party){
      if(!victory&&age>=(cooldowns.get(key)||0))input('cast',key);
      return;
    }
    // Reset the workbench fixture between automatic trials and when swapping the enemy model.
    const newOpponent=active&&(active.key==='boss')!==(key==='boss');
    const restart=mode==='skills'&&(automatic||newOpponent);
    if(restart)defeated=false;
    input(restart?'replay':'cast',key);active={time,key};
    readyAt=time+sourceMoves[key].seconds;
  }
  function updateControls(){
    auto.hidden=editable();
    auto.setAttribute('aria-pressed',String(automatic));auto.textContent=automatic?'自动':'手动';
    byId('battle-reset').hidden=mode!=='skills'||automatic;
    attack.hidden=automatic||!party;attack.disabled=age<readyAt||victory;
    bar.setAttribute('aria-label',automatic?'出战功法，自动施放':'出战功法，手动施放');
  }
  function manual(key){
    if(!key||!mode||victory||(mode!=='skills'&&!party?.player.some(m=>m.id===sourceMoves[key]?.id))||
      (party?age<(cooldowns.get(key)||0):age<readyAt)||(defeated&&(active?.key==='boss')===(key==='boss')))return;
    automatic=false;cast(key,age);updateControls();
  }
  auto.addEventListener('click',()=>{
    if(!mode||victory)return;
    automatic=!automatic;
    if(automatic&&mode==='skills'){age=0;castIndex=0;active=null;readyAt=0;}
    updateControls();
  });
  const primarySlot=()=>plan?.slots.find(Boolean);
  attack.addEventListener('click',()=>manual(primarySlot()));
  byId('battle-reset').addEventListener('click',()=>{
    if(mode!=='skills')return;
    defeated=false;readyAt=0;active=null;input('begin');updateControls();
  });
  function tick(now){
    if(last!==null&&!document.hidden)age+=Math.min(.1,(now-last)/1000);
    last=document.hidden?null:now;
    if(party&&!victory&&automatic&&age>=nextAutoPoll&&(!active||age>=active.time+sourceMoves[active.key].seconds)){
      const key=plan.slots.find(k=>k&&age>=(cooldowns.get(k)||0));
      if(key)cast(key,age);
      nextAutoPoll=age+.15;
    }
    if(automatic&&plan.period&&age>=plan.period){age%=plan.period;castIndex=0;active=null;readyAt=0;}
    while(automatic&&castIndex<plan.casts.length&&plan.casts[castIndex][0]<=age){
      const [time,key]=plan.casts[castIndex];
      castIndex++;
      cast(key,time);
    }
    if(age>=feedbackUntil)feedback.hidden=true;
    const casting=active&&age<active.time+sourceMoves[active.key].seconds ? active.key:null;
    const text=casting?sourceMoves[casting].name:'';
    if(label.textContent!==text)label.textContent=text;label.hidden=!!party||!text;
    plan.slots.forEach((key,i)=>{
      if(!key)return;
      const {li,timer,button}=slots[i],wait=Math.max(0,Math.ceil((party?(cooldowns.get(key)||0):readyAt)-age));
      button.disabled=!editable()&&(wait>0||victory||(defeated&&(active?.key==='boss')===(key==='boss')));
      li.classList.toggle('casting',casting===key);li.classList.toggle('cooling',!!wait&&casting!==key);timer.textContent=wait?String(wait):'';
    });
    if(party&&!attack.hidden){const first=primarySlot();attack.disabled=!first||victory||age<(cooldowns.get(first)||0);}
    raf=requestAnimationFrame(tick);
  }
  function stop(){
    board.close();
    cancelAnimationFrame(raf);scene.hidden=true;controls.hidden=true;last=null;
    requestVersion++;entryCancel?.();frame=null;onResult=null;party=null;
    mode=null;canvas.hidden=false;
  }
  function showSlots(){
    if(party){
      const keys=[];party.player.forEach((m,i)=>{keys[m.slot??i]=keyForMove(m.id);});plan.slots=keys;
    }
    slots.forEach(({li,icon,frame,timer,button,name},i)=>{
      const artId=editable()&&learnedArts.includes(battleBag.slots[i])?battleBag.slots[i]:null;
      const skill=sourceMoves[plan.slots[i]];frame.hidden=!skill;icon.hidden=!skill || !art.ui[skill.id];
      if(skill){if(!icon.hidden)icon.src=art.ui[skill.id];const quality=skillRules.books.get(skill.id)?.quality||5;frame.src=art.ui['quality_skill_new_0'+quality]||art.ui.quality_skill_new_05;}
      const enemy=mode==='skills'&&plan.slots[i]==='boss';
      name.textContent=skill?(enemy?'敌方 · ':'')+skill.name:'';
      button.textContent=artId?trainingRules.arts[artId]:skill&&icon.hidden?skill.name.slice(0,2):'';
      button.hidden=!editable()&&!skill;button.disabled=!editable()&&!skill;button.dataset.key=plan.slots[i]||'';
      button.setAttribute('aria-label',editable()?'上排第 '+(i+1)+' 格 · '+(skill?.name||trainingRules.arts[artId]||'空'):skill?(enemy?'敌方试放':'施放')+skill.name:'空功法位');
      li.setAttribute('aria-label',skill?.name||trainingRules.arts[artId]||'空功法位');li.className='';timer.textContent='';
    });
  }
  function show(which,skillKey,settle){
    onResult=settle;
    cancelAnimationFrame(raf);last=null;mode=which;
    const single=mode==='skills' && sourceMoves[skillKey];
    if(party){
      const slots=[];party.player.forEach((m,i)=>{slots[m.slot??i]=keyForMove(m.id);});
      plan={slots,casts:[]};
    }else plan=single?{slots:[skillKey],casts:[[.6,skillKey]],period:single.seconds+2}:sourceEncounters.skills;
    age=0;castIndex=0;active=null;victory=false;cooldowns.clear();nextAutoPoll=.25;feedbackUntil=0;feedback.hidden=true;defeated=false;automatic=true;readyAt=0;
    trainingEvents.clear();trainingReport.hidden=true;
    damageBySkill.clear();report.hidden=true;scene.classList.remove('settled');delete result.dataset.outcome;
    byId('battle-return').textContent=party?'点击继续':'回到舆图';
    attack.setAttribute('aria-label','施放'+(sourceMoves[primarySlot()]?.name||'功法'));
    scene.dataset.encounter=mode;scene.classList.toggle('formal-battle',!!party);controls.hidden=false;byId('skill-records').hidden=mode!=='skills';updateControls();
    result.hidden=label.hidden=true;scene.hidden=false;
    scene.querySelector('.court-chapter small').textContent=party?.heading??'功法试放';
    scene.querySelector('.court-chapter strong').textContent=party?.title??(single?single.name:'剑修与独臂木侠');
    showSlots();if(editable())board.open(battleBag,learnedArts);else board.close();
  }
  function stage(){
    return new Promise((resolve,reject)=>{
      const timeout=setTimeout(()=>finish(Error('战场首帧未能绘制，请重试。')),5000);
      function finish(error){
        clearTimeout(timeout);stageComplete=null;
        if(error)reject(error);else resolve();
      }
      stageComplete=finish;
      input('stage');
    });
  }
  function start(){
    if(!frame?.isConnected)throw Error('云端战场已断开，请重试。');
    input('begin');raf=requestAnimationFrame(tick);
  }
  return {prepare,show,stage,start,stop};
}
