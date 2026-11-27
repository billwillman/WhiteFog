function makeTrainingPanel(access,onPractice,onState,onTrial,onPathChoice){
  const panel=document.getElementById('training-panel'),content=document.getElementById('training-content');
  const status=document.getElementById('training-status'),close=document.getElementById('training-close');
  const materials=document.getElementById('training-materials'),nav=panel.querySelector('nav');
  const samples=new Map();
  const tabs={practice:'修行',partners:'伙伴',gear:'法宝与御剑',paths:'主修'};
  let tab='practice',state,busy=false,version=0,after=null,timer=0;
  function button(text,run,disabled=false){
    const el=document.createElement('button');el.type='button';el.textContent=text;el.disabled=disabled;
    el.addEventListener('click',run);return el;
  }
  function row(title,description,buttons=[]){
    const article=document.createElement('section'),name=document.createElement('h3'),detail=document.createElement('p');
    name.textContent=title;detail.textContent=description;article.append(name,detail);
    if(buttons.length){const controls=document.createElement('div');controls.append(...buttons);article.append(controls);}
    content.append(article);return article;
  }
  function action(text,key,pick,disabled){
    const el=button(text,async()=>{
      if(busy)return;busy=true;status.textContent='正在保存…';
      panel.setAttribute('aria-busy','true');close.disabled=true;
      for(const control of content.querySelectorAll('button'))control.disabled=true;
      let saved=false;
      try{state=await access(key,pick);onState(state);status.textContent='';saved=true;}
      catch(error){status.textContent=error.message;}
      finally{busy=false;close.disabled=false;panel.removeAttribute('aria-busy');if(panel.open)render();}
      if(saved&&(key==='borrowPath'||key==='choosePath')){dismiss(false);onPathChoice(key);}
    },disabled||busy);
    el.dataset.action=key;
    if(pick!=null)el.dataset.value=String(pick);
    return el;
  }
  function render(){
    const s=state,has=key=>s.claimed.includes(key);
    materials.textContent='修为 '+s.cultivation+'　残页 '+s.pages+'　灵砂 '+s.sand+'　云髓 '+s.marrow;
    for(const el of nav.children){
      el.hidden=el.dataset.tab==='paths'&&!has('pathReady');
      el.setAttribute('aria-pressed',String(el.dataset.tab===tab));
    }
    content.replaceChildren();
    if(tab==='practice'){
      row(s.realm===1?'炼气一层':'炼气二层',s.realm===1?'完成云关巡灯后，以 30 修为突破。':'气血上限 +10，攻击 +1。',
        s.realm===1?[action('突破二层','breakthrough',null,!has('lampsDone')||s.cultivation<30)]:[]);
      const n=trainingRules.meditation(s,Date.now());
      row('外院静修',has('outerCourt')?'每分钟积 1 修为，最多暂存 60 点。':'送药回宗后开放蒲团。',has('outerCourt')?[action('领取 '+n+' 修为','meditate',null,!n)]:[]);
      row('行气诀'+(s.breath?' · 第一重':''),s.breath?'受冲击后 0.35 秒可再次出招。':'受冲击后需 1 秒稳住剑身。参悟可缩短到 0.35 秒。',
        has('ferry')&&!s.breath?[action('用 1 残页参悟','breath',null,!s.pages)]:[]);
      if(has('ferry')){
        const trial=(label,baseline)=>button(label,()=>{dismiss(false);onPractice(baseline);});
        const buttons=[trial('试打木人',false)];
        if(s.swordRank||s.breath)buttons.push(trial('对照未温养、未参悟',true));
        const results=[...samples.values()].map(s=>s.label+'（攻击 '+s.attack+'）受冲击后 '+s.secondsToCast.toFixed(2)+' 秒出招，首击扣血 '+Number(s.damage.toFixed(2)));
        row('剑坪试招',results.length?results.join('；'):'同一具木人、同一股云流，记录实际出招时间和首击扣血。',buttons);
      }
    }else if(tab==='paths'){
      for(const [id,path] of Object.entries(trainingRules.paths)){
        const passed=s.triedPaths?.includes(id),chosen=s.mainPath===id;
        const trial=button('试修'+path.name,()=>{dismiss(false);onTrial(id);});
        trial.dataset.pathTrial=id;
        const controls=[trial];
        if(has('pathMentor')&&!s.mainPath)controls.push(action('拜师，学'+path.art,'choosePath',id,!passed));
        else if(!s.deliveryCleared)controls.push(action('借'+path.art+'出山','borrowPath',id,!passed));
        row(path.name+' · '+path.art+(chosen?'（已习得）':passed?'（试修通过）':''),path.detail,controls);
      }
    }else if(tab==='partners'){
      if(!s.recruit)row('外院招募帖',has('invite')?'用帖随机结识巡山剑修或护阵弟子，结识后自动同行。':'与许舟在剑坪校完航图后领取。',[action('用帖招募（'+s.tickets+'）','recruit',null,!s.tickets)]);
      const owned=[...(has('ferry')?['adept']:[]),...(s.recruit?[s.recruit]:[])];
      for(const id of owned){
        const recruited=id===s.recruit,charges=1+(recruited?s.allyRank:0);
        const note=id==='blade'?'每战拦下前 '+charges+' 次雾鸢俯冲。':id==='ward'?'每战前 '+charges+' 次受击为主角各挡 5 点伤害。':'每战首次受击为主角挡 3 点伤害。';
        const buttons=[s.ally===id?action('休整','ally',''):action('设为同行','ally',id)];
        if(recruited&&!s.allyRank)buttons.push(action('用 1 残页指点','teach',null,!s.pages));
        const entry=row(trainingRules.companions[id]+(s.ally===id?' · 同行':''),note+(recruited&&!s.allyRank?' 指点后每战多生效一次。':''),buttons);
        const portrait=new Image();portrait.src='battle-art/pet-'+id+'.webp';portrait.alt='';portrait.className='partner-portrait';entry.prepend(portrait);
      }
    }else{
      if(has('stoneRoad')){
        row('定风珠'+(s.bead?' · 已佩戴':''),'每战首次震地为每位在场队员抵消 '+(s.beadRank?9:5)+' 点伤害。',
          [action(s.bead?'卸下':'佩戴','bead',!s.bead),...(!s.beadRank?[action('用 1 灵砂温养','polish',null,!s.sand)]:[])]);
        for(const key of ['guard','seal']){
          const name=trainingRules.arts[key];
          if(key==='seal'&&!has('rearRoad'))continue;
          row(name+' · 已学会', (key==='guard'?'每战首次震地，为主角额外抵消 5 点伤害。':'每战封住第一次雾鸢俯冲。')+'在战斗中拖到上排生效。');
        }
      }
      row('本命飞剑'+(s.swordRank?' · 已温养':''),s.swordRank?'温养使攻击提高 2 点，可去剑坪试剑。':'送完第三章的尾车可得云髓，温养一次提高 2 点攻击。',
        has('delivery')&&!s.swordRank?[action('用 1 云髓温养','temper',null,!s.marrow)]:[]);
    }
  }
  for(const [key,label] of Object.entries(tabs)){
    const el=button(label,()=>{if(!state)return;tab=key;status.textContent='';render();});el.dataset.tab=key;el.hidden=key==='paths';nav.append(el);
  }
  function dismiss(proceed){
    if(busy)return;
    clearInterval(timer);version++;
    const done=after;after=null;panel.close();
    if(proceed&&done)done();
  }
  close.onclick=()=>dismiss(true);
  panel.addEventListener('cancel',event=>{event.preventDefault();dismiss(false);});
  async function open(section,done=null){
    const request=++version;tab=section;after=done;state=null;content.replaceChildren();materials.textContent='';status.textContent='正在读取修行进度…';
    close.textContent=done?'进入战斗':'返回舆图';panel.showModal();
    try{
      const s=await access();if(!panel.open||request!==version)return;state=s;onState(s);
      status.textContent='';render();
      timer=setInterval(()=>{
        const claim=content.querySelector('[data-action="meditate"]');
        if(!claim||!state||busy)return;
        const n=trainingRules.meditation(state,Date.now());claim.textContent='领取 '+n+' 修为';claim.disabled=!n;
      },10000);
    }catch(error){if(request===version)status.textContent='修行进度读取失败：'+error.message;}
  }
  function recordTrial(sample){samples.set(sample.label,sample);}
  return {open,recordTrial};
}
