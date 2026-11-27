const firstChapter=(()=>{
  const $=id=>document.getElementById(id);
  const art=assets.story,storeKey=trainingRules.storeKey;
  const preview=new URLSearchParams(location.search).get('preview');
  const ferryPreview=preview==='ferry'||preview==='passage';
  const isolated=ferryPreview||preview==='skills'||preview==='battle';
  const medicine=[
    {speaker:'外院师兄',actor:'senior',expression:'kind',text:'来得正好。替我按住这头绳，别让药包散了。'},
    {speaker:'我 · 心声',actor:'hero',expression:'thoughtful',text:'他还是爱多绕半圈。上一世头一回下山，我一路攥着的，就是这个结。'},
    {speaker:'我',actor:'hero',expression:'warm',text:'我记得路。沿河走，到云关交给船上的人。'},
    {speaker:'外院师兄',actor:'senior',expression:'earnest',text:'药别沾水。到渡口，记得听回钟。'},
    {speaker:'我',actor:'hero',expression:'warm',text:'好。等最后一船靠了岸，我再回来。',action:'收好药包'}
  ];
  const ferryLines=[
    {speaker:'旧渡',text:'有人蹲在石阶边，往火盆里添了根湿柴。火苗矮下去，青烟贴着水面飘。雾里传来一声桨响。',action:'看向火盆'},
    {speaker:'我 · 心声',actor:'hero',expression:'intent',text:'他把火盆推到水边了……是怕船上的人看不见岸。'},
    {speaker:'船上阵修',actor:'adept',expression:'searching',text:'岸上可还有人？我只瞧得见烟，栈桥在哪一边？'},
    {speaker:'我',actor:'hero',expression:'calling',text:'有，我看见火了！船头往左一点，我来给你引路。'},
    {speaker:'船上阵修',actor:'adept',expression:'relieved',text:'瞧见了，左边那点火。多谢！船身我来护，你在前头带路。',action:'护送药舟'}
  ];
  const tasks=window.qingyunChapterTasks;
  const ferryTask=tasks.find(task=>task.stageId==='1-3');
  let firstClears={},sceneLine=0;
  let progress=ferryPreview?3:0,line=0,ferryLine=preview==='passage'?ferryLines.length-1:0;
  try{
    const saved=!isolated&&JSON.parse(localStorage.getItem(storeKey));
    if(saved&&Number.isInteger(saved.progress)&&saved.progress>=0&&saved.progress<tasks.length){
      progress=saved.progress;
      if(Number.isInteger(saved.sceneLine)&&tasks[progress].lines?.[saved.sceneLine])sceneLine=saved.sceneLine;
      if(saved.firstClears&&typeof saved.firstClears==='object'&&!Array.isArray(saved.firstClears))firstClears=saved.firstClears;
      // Early chapter saves only recorded the task reached after the ferry.
      if(progress>tasks.indexOf(ferryTask)&&!Object.hasOwn(firstClears,ferryTask.stageId))
        firstClears[ferryTask.stageId]=ferryTask.firstClear;
      if(Number.isInteger(saved.line)&&saved.line>=0&&saved.line<medicine.length)line=saved.line;
      if(Number.isInteger(saved.ferryLine)&&saved.ferryLine>=0&&saved.ferryLine<ferryLines.length)ferryLine=saved.ferryLine;
    }
  }catch(_){}
  function save(){if(!isolated)try{localStorage.setItem(storeKey,JSON.stringify({progress,line,ferryLine,sceneLine,firstClears}));}catch(_){}}
  const dialogue=createDialogue($('dialogue'),$('dialogue-template'),art.portraits);
  const battle=makeBattleEntry(assets.battle,(snapshot,damage)=>training.recordTrial({
    label:snapshot.baseline?'初始对照':'当前修行',attack:snapshot.attack,recovery:snapshot.breath ? .35 : 1,damage
  }));
  const previewTraining=trainingRules.initial();
  async function trainingAccess(action,pick){
    if(!isolated)return skillInventory.cultivate(action,pick);
    const now=Date.now();
    trainingRules.award(previewTraining,tasks,progress,firstClears,now);
    if(action)trainingRules.change(previewTraining,action,pick,now);
    return previewTraining;
  }
  function updateIdentity(s){document.querySelector('.identity b').textContent=s.realm===2?'炼气二层':'炼气一层';}
  const training=makeTrainingPanel(trainingAccess,baseline=>openBattle('yard',null,baseline),updateIdentity);
  async function prepareBattle(encounter,key,baseline,request){
    const state=encounter==='skills'?null:await trainingAccess();
    if(request&&request!==pendingBattle)throw Error('已取消进场');
    const snapshot=state?trainingRules.snapshot(state):null;
    if(baseline&&encounter==='yard'){snapshot.baseline=true;snapshot.breath=0;snapshot.attack-=2*state.swordRank;}
    return battle.prepare(encounter,key,snapshot);
  }
  function showChapterBattle(task){
    battle.show(task.encounter,null,won=>{
      if(!won)return;
      if(!Object.hasOwn(firstClears,task.stageId))firstClears[task.stageId]=task.firstClear??{};
      if(tasks[progress]===task)advanceTask();else save();
    });
  }
  window.addEventListener('storage',event=>{
    if(event.key===storeKey&&event.newValue===null)location.reload();
  });
  function advanceTask(){
    sceneLine=0;
    if(progress+1<tasks.length)progress++;
    save();
  }
  async function clearProgress(){
    await skillInventory.reset();
    localStorage.removeItem(storeKey);
    // Leave isolated previews as well so a reset always opens the normal login.
    const login=new URL(location.href);
    login.search='';login.hash='';location.replace(login.href);
  }
  let enteringBattle=false;
  let motion,mapReady=false,shown=false,finished=false,revealStart=null,prepared=false;
  const veil=$('veil'),ctx=veil.getContext('2d');
  veil.width=art.mistWidth;veil.height=art.mistHeight;
  const mask=Uint8Array.from(atob(art.mist),c=>c.charCodeAt(0));
  const overlay=ctx.createImageData(veil.width,veil.height);
  function fog(age,opening){
    veil.hidden=false;
    const coverTime=(age-1.32)/1.18;
    const openTime=opening===null?0:opening/1.48;
    const warm=smooth(0,1,Math.max(0,(age-1.4)/2.3));
    for(let i=0,p=0;i<mask.length;i+=3,p+=4){
      const cover=mask[i]*1.4/255-.2, radius=mask[i+1]*1.2/255-.12;
      const shade=(mask[i+2]-155)*.27;
      overlay.data[p]=239-6*warm+shade;overlay.data[p+1]=223+5*warm+shade;
      overlay.data[p+2]=198+21*warm+shade;
      overlay.data[p+3]=255*(opening===null?smooth(0,.22,1.55*coverTime-.25-cover):
        1-smooth(0,.25,openTime*1.2-radius+.09));
    }
    ctx.putImageData(overlay,0,0);
  }
  function map(){
    if(enteringBattle)return;
    pendingBattle=null;battle.stop();dialogue.hide();$('court').hidden=true;$('region').hidden=false;
    $('task').textContent=tasks[progress].text;
    trainingAccess().then(updateIdentity,()=>{});
    $('home-seal').hidden=!progress;
    const places={home:['36%','68%'],pass:['74%','22%'],ferry:['79%','54%'],road:['21%','34%']};
    const position=places[tasks[progress].place];
    $('route-seal').hidden=!progress||!position;
    if(position)Object.assign($('route-seal').style,{left:position[0],top:position[1]});
    motion.setVisible(true);
  }
  function caption(speaker,text,action='回到舆图',done=map){
    dialogue.show({speaker,text,action},done);
  }
  function courtLine(){
    dialogue.show(medicine[line],()=>{
      if(line<medicine.length-1){line++;save();courtLine();return;}
      progress=1;line=0;save();map();$('task').focus({preventScroll:true});
    });
  }
  function backdrop(place,task){
    const ferry=place==='ferry';
    const title=ferry?(progress===4?'雾里有灯':'两响之后'):'外院的药包';
    const scene=$('court'),picture=$('court-art'),src=ferry?art.ferry:art.court;
    if(picture.getAttribute('src')!==src)picture.src=src;
    picture.alt=ferry?'旧渡岸边有人往火盆里添湿柴，雾中药舟正等着引路':'青云宗外院的石阶与廊庑';
    scene.setAttribute('aria-label',ferry?'旧渡':'青云宗外院');
    const chapters=['第三声渡钟','云路上的灯','山外有人'];
    const chapter=Number(task?.section.split('-')[0]??1);
    scene.querySelector('.court-chapter small').textContent='第'+'一二三'[chapter-1]+'章　'+chapters[chapter-1];
    scene.querySelector('.court-chapter strong').textContent=task?.title??title;
    $('region').hidden=true;scene.hidden=false;motion.setVisible(false);
  }
  function atFerry(offerReplay=false){
    backdrop('ferry');
    if(Object.hasOwn(firstClears,ferryTask.stageId)){
      const claim=tasks[progress].afterStage===ferryTask.stageId&&progress+1<tasks.length;
      const replay=offerReplay&&!claim;
      caption('旧渡 · 首通所得','阵修把药舟稳稳护进了避风水面。这一趟所得的修为和通用残页，已记在历练簿上。',
        replay?'重战旧渡':'回到舆图',replay?beginPassage:()=>{if(claim)advanceTask();map();});return;
    }
    dialogue.show(ferryLines[ferryLine],()=>{
      if(ferryLine+1===ferryLines.length){beginPassage();return;}
      ferryLine++;save();atFerry();
    });
  }
  function beginPassage(){
    if(enteringBattle)return;
    enteringBattle=true;
    const story=$('story'),court=$('court'),painting=$('court-art'),speaker=$('dialogue');
    const reduced=matchMedia('(prefers-reduced-motion: reduce)').matches;
    let ready=false,age=0,last=null,openedAt=null,frame;
    story.inert=true;
    function reset(){
      enteringBattle=false;story.inert=false;veil.hidden=true;
      painting.style.transform='';speaker.style.opacity='';document.removeEventListener('visibilitychange',resetClock);
    }
    function resetClock(){last=null;}
    document.addEventListener('visibilitychange',resetClock);
    prepareBattle(ferryTask.encounter).then(()=>{ready=true;},()=>{
      cancelAnimationFrame(frame);reset();
      caption('旧渡 · 药舟','云路画面载入失败，可重新进入。','重试',beginPassage);
    });
    function advance(now){
      if(!document.hidden){
        if(last!==null)age+=Math.min(.10,(now-last)/1000)*(reduced?3:1);
        last=now;
        if(openedAt===null){
          const push=smooth(0,1.65,age);
          painting.style.transform='scale('+(1+(reduced?0:.13)*push)+')';
          speaker.style.opacity=1-smooth(0,.75,age);
          if(age>=1.32)fog(age,null);
          if(age>=2.55&&ready){
            dialogue.hide();court.hidden=true;
            const stageIndex=tasks.indexOf(ferryTask);
            if(progress===stageIndex-1){progress=stageIndex;save();}
            showChapterBattle(ferryTask);openedAt=age;
          }
        }else{
          const opening=age-openedAt;
          fog(age,opening);
          if(opening>=1.55){
            reset();($('battle-result').hidden?$('battle-map'):$('battle-return')).focus({preventScroll:true});return;
          }
        }
      }
      frame=requestAnimationFrame(advance);
    }
    frame=requestAnimationFrame(advance);
  }
  function playStoryTask(task){
    if(task.place==='home'||task.place==='ferry')backdrop(task.place,task);
    const line=task.lines[sceneLine];
    dialogue.show(line,()=>{
      if(sceneLine+1<task.lines.length){sceneLine++;save();playStoryTask(task);return;}
      if(tasks[progress]!==task){map();return;}
      advanceTask();
      const next=tasks[progress];
      const fight=next.encounter&&next.place===task.place?()=>openBattle(next.encounter,next):null;
      if(task.trainingStop){map();training.open(task.trainingStop,fight);}
      else if(fight)fight();else map();
    });
  }
  function visit(place,fromMap=false){
    if(!finished||pendingBattle||enteringBattle)return;
    const task=tasks[progress];
    if(task.lines&&task.place===place){playStoryTask(task);return;}
    if(task.encounter&&task.place===place){openBattle(task.encounter,task);return;}
    if(place==='home'){
      backdrop('home');
      if(progress===0)courtLine();
      else dialogue.show({speaker:'外院师兄',actor:'senior',expression:'kind',text:'绳结紧着呢。过河时记得把药包收在怀里。',action:'回到舆图'},map);
    }else if(place==='pass'&&progress===1){
      dialogue.show({speaker:'云关',text:'当——当——。关楼敲过两声收渡钟。下游那座旧渡，本该回一声。',action:'再听一听'},()=>{
        dialogue.show({speaker:'我 · 心声',actor:'hero',expression:'thoughtful',text:'上一世，第三声没响。……钟亭旁边，好像有烟。',action:'循烟寻渡'},()=>{
          progress=2;save();map();
        });
      });
    }else if(place==='road'){
      caption('古道 · 云端历练','独臂木侠正在云上候战。','挑战独臂木侠',()=>openBattle('duel'));
    }else if(place==='ferry'&&progress>=2){
      if(progress===2){progress=3;ferryLine=0;save();}
      atFerry(fromMap);
    }else{
      const notes={pass:['云关','关楼管着这段水路，船来船往都听这里的收渡钟。'],
        ferry:['旧渡','乌篷船系在木桩上，钟亭正对着上游的关楼。'],
        road:['古道','石桥那头的松阴里有间驿亭，远行的同道常在这里歇脚。']};
      caption(...notes[place]);
    }
  }
  document.querySelectorAll('[data-place]').forEach(el=>el.addEventListener('click',()=>visit(el.dataset.place,true)));
  $('task').addEventListener('click',()=>visit(tasks[progress].place));
  $('to-map').addEventListener('click',map);
  function returnFromBattle(){
    if(enteringBattle)return;
    map();$('task').focus({preventScroll:true});
    const start=performance.now(),reduced=matchMedia('(prefers-reduced-motion: reduce)').matches;
    if(reduced)return;
    function uncover(now){
      const age=(now-start)/1000;
      if(age>=1.55){veil.hidden=true;return;}
      fog(4,age);requestAnimationFrame(uncover);
    }
    uncover(start);
  }
  $('battle-map').addEventListener('click',returnFromBattle);
  $('battle-return').addEventListener('click',returnFromBattle);
  let pendingBattle=null;
  function openBattle(encounter='skills',task,baseline=false){
    if(pendingBattle||enteringBattle)return;
    const id=preview==='skills' ? new URLSearchParams(location.search).get('skill') : null;
    const key=id&&Object.keys(sourceMoves).find(k=>String(sourceMoves[k].id)===id);
    if(id && !key){caption('功法试放','当前战场版本尚未收录这门功法。','回到舆图',map);return;}
    const request={};
    pendingBattle=request;
    const title=battleParties[encounter]?.heading??'功法试放';
    caption(title,'正在载入云端战场……','回到舆图',()=>{pendingBattle=null;map();});
    prepareBattle(encounter,key,baseline,request).then(()=>{
      if(pendingBattle!==request)return;
      pendingBattle=null;dialogue.hide();$('region').hidden=true;$('court').hidden=true;motion.setVisible(false);
      if(task)showChapterBattle(task);else battle.show(encounter,key);
    },error=>{
      if(pendingBattle!==request)return;
      pendingBattle=null;caption(title,error.message||'云端战场载入失败。','重试',()=>openBattle(encounter,task,baseline));
    });
  }
  document.querySelectorAll('[data-note]').forEach(el=>el.addEventListener('click',()=>{
    if(el.dataset.note==='功法')openBattle();else if(el.dataset.note==='背包')location.href='skill-records.html#bag';else training.open(el.dataset.note==='伙伴'?'partners':'practice');
  }));
  function prepare(onReady){
    if(prepared)return;prepared=true;
    if(!motion)motion=makeMapMotion(art);
    $('region-art').src=art.region;$('avatar').src=art.face;$('court-art').src=art.court;
    const img=$('region-art');
    function failed(){prepared=false;flightStart=null;veil.hidden=true;enter.disabled=false;enter.style.pointerEvents='';notice.hidden=false;notice.textContent='舆图载入失败，请重新点击登录。';}
    function ready(){mapReady=true;if(onReady)onReady();}
    if(img.decode)img.decode().then(ready,failed);
    else if(img.complete&&img.naturalWidth)ready();
    else{img.onload=ready;img.onerror=failed;}
  }
  function previewMap(){
    prepare(()=>{
      shown=finished=true;
      canvas.hidden=title.hidden=enter.hidden=again.hidden=notice.hidden=true;
      $('story').hidden=false;map();
      if(ferryPreview)visit('ferry');
      if(preview==='map'&&new URLSearchParams(location.search).get('training')==='practice')training.open('practice');
      if(preview==='skills')openBattle();
      if(preview==='battle')openBattle('duel');
    });
  }
  function transition(age){
    if(age<1.32)return false;
    if(age>=2.55&&mapReady&&!shown){
      shown=true;revealStart=age;
      $('story').hidden=false;$('story').inert=true;map();
      canvas.hidden=title.hidden=enter.hidden=again.hidden=true;
    }
    const elapsed=shown?age-revealStart:null;
    fog(age,elapsed);
    if(elapsed===null||elapsed<1.55)return false;
    finished=true;veil.hidden=true;$('story').inert=false;
    if(progress===0&&line===0){
      dialogue.show({speaker:'我 · 心声',actor:'hero',expression:'thoughtful',text:'青云宗……我又回来了。这个时辰，师兄该在外院包药了。',action:'去青云宗外院'},()=>visit('home'));
    }else $('task').focus({preventScroll:true});
    return true;
  }
  return {prepare,transition,previewMap,clearProgress,get shown(){return shown;},get finished(){return finished;}};
})();
