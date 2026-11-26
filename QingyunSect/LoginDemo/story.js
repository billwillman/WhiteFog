const firstChapter=(()=>{
  const $=id=>document.getElementById(id);
  const art=assets.story,storeKey='qingyun.third-bell.v1';
  const preview=new URLSearchParams(location.search).get('preview');
  const ferryPreview=preview==='ferry'||preview==='passage';
  const isolated=ferryPreview||preview==='skills';
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
  const tasks=['到外院，找师兄领药','沿水路去云关送药','循着细烟去旧渡','在旧渡接应药舟','随药舟入雾','药舟已入避风水面，查看护舟所得'];
  let progress=ferryPreview?3:0,line=0,ferryLine=preview==='passage'?ferryLines.length-1:0;
  try{
    const saved=!isolated&&JSON.parse(localStorage.getItem(storeKey));
    if(saved&&Number.isInteger(saved.progress)&&saved.progress>=0&&saved.progress<tasks.length){
      progress=saved.progress;
      if(Number.isInteger(saved.line)&&saved.line>=0&&saved.line<medicine.length)line=saved.line;
      if(Number.isInteger(saved.ferryLine)&&saved.ferryLine>=0&&saved.ferryLine<ferryLines.length)ferryLine=saved.ferryLine;
    }
  }catch(_){}
  function save(){if(!isolated)try{localStorage.setItem(storeKey,JSON.stringify({progress,line,ferryLine,firstClears:progress>=5?{'1-3':{cultivation:true,universalPages:1}}:{}}));}catch(_){}}
  const dialogue=createDialogue($('dialogue'),$('dialogue-template'),art.portraits);
  const battle=makeBattleEntry(assets.battle,()=>{
    if(progress===4){progress=5;save();}
  });
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
    battle.stop();dialogue.hide();$('court').hidden=true;$('region').hidden=false;
    $('task').textContent=tasks[progress];
    $('home-seal').hidden=!progress;$('route-seal').hidden=!progress;
    Object.assign($('route-seal').style,progress===1?{left:'74%',top:'22%'}:{left:'79%',top:'54%'});
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
  function backdrop(place){
    const ferry=place==='ferry';
    const title=ferry?(progress===4?'雾里有灯':'两响之后'):'外院的药包';
    const scene=$('court'),picture=$('court-art'),src=ferry?art.ferry:art.court;
    if(picture.getAttribute('src')!==src)picture.src=src;
    picture.alt=ferry?'旧渡岸边有人往火盆里添湿柴，雾中药舟正等着引路':'青云宗外院的石阶与廊庑';
    scene.setAttribute('aria-label',ferry?'旧渡':'青云宗外院');
    scene.querySelector('.court-chapter strong').textContent=title;
    $('region').hidden=true;scene.hidden=false;motion.setVisible(false);
  }
  function atFerry(){
    backdrop('ferry');
    if(progress===5){
      caption('旧渡 · 首通所得','阵修把药舟稳稳护进了避风水面。这一趟所得的修为和通用残页，已记在历练簿上。');return;
    }
    if(progress===4){
      beginPassage();
      return;
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
    battle.prepare().then(()=>{ready=true;},()=>{
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
            dialogue.hide();court.hidden=true;battle.show();openedAt=age;
            progress=4;save();
          }
        }else{
          const opening=age-openedAt;
          fog(age,opening);
          if(opening>=1.55){
            reset();$('battle-map').focus({preventScroll:true});return;
          }
        }
      }
      frame=requestAnimationFrame(advance);
    }
    frame=requestAnimationFrame(advance);
  }
  function visit(place){
    if(!finished)return;
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
    }else if(place==='ferry'&&progress>=2){
      if(progress===2){progress=3;ferryLine=0;save();}
      atFerry();
    }else{
      const notes={pass:['云关','关楼管着这段水路，船来船往都听这里的收渡钟。'],
        ferry:['旧渡','乌篷船系在木桩上，钟亭正对着上游的关楼。'],
        road:['古道','石桥那头的松阴里有间驿亭，远行的同道常在这里歇脚。']};
      caption(...notes[place]);
    }
  }
  document.querySelectorAll('[data-place]').forEach(el=>el.addEventListener('click',()=>visit(el.dataset.place)));
  $('task').addEventListener('click',()=>visit(progress===0?'home':progress===1?'pass':'ferry'));
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
  const hints={修行:'办完这趟差事，外院会给你留一席静修的蒲团。',功法:'护送药舟所得的残页，可以用来参悟行气诀。',伙伴:'下一趟巡灯差事，可以邀一位同道同行。'};
  let openingSkills=false;
  function openSkills(){
    if(openingSkills)return;
    openingSkills=true;
    caption('功法试放','正在载入云端战场……','回到舆图',()=>{openingSkills=false;map();});
    battle.prepare().then(()=>{
      if(!openingSkills)return;
      openingSkills=false;dialogue.hide();$('region').hidden=true;motion.setVisible(false);battle.show('skills');
    },()=>{
      if(!openingSkills)return;
      openingSkills=false;caption('功法试放','云端战场载入失败。','重试',openSkills);
    });
  }
  document.querySelectorAll('[data-note]').forEach(el=>el.addEventListener('click',()=>{
    if(el.dataset.note==='功法')openSkills();else caption(el.dataset.note,hints[el.dataset.note]);
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
      if(preview==='skills')openSkills();
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
  return {prepare,transition,previewMap,get shown(){return shown;},get finished(){return finished;}};
})();
