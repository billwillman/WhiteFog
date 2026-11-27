const trainingRules=(()=>{
  const companions={adept:'守关阵修',blade:'巡山剑修',ward:'护阵弟子'};
  const arts={guard:'护脉',seal:'镇行符'};
  const storeKey='qingyun.third-bell.v1',minute=60000,breakthroughCost=30;
  function initial(){
    return {claimed:[],cultivation:0,pages:0,tickets:0,sand:0,marrow:0,realm:1,breath:0,
      recruit:'',ally:'',allyRank:0,bead:false,beadRank:0,swordRank:0,art:'',meditatingSince:0};
  }
  function award(state,tasks,progress,clears,now){
    tasks.forEach((task,i)=>{
      const reward=task.trainingReward;
      if(!reward||state.claimed.includes(reward.key))return;
      // The earliest ferry saves only recorded the task after victory.
      const receipt=task.encounter?(clears[task.stageId]??(reward.key==='ferry'&&i<progress?task.firstClear:null)):null;
      if(task.encounter?!receipt:i>=progress)return;
      state.claimed.push(reward.key);
      for(const key of ['cultivation','tickets','sand','marrow'])state[key]+=reward[key]??0;
      state.pages+=task.encounter?(receipt.universalPages??0):(reward.pages??0);
      if(reward.key==='outerCourt')state.meditatingSince=now;
    });
  }
  const meditation=(s,now)=>s.meditatingSince?Math.max(0,Math.min(60,Math.floor((now-s.meditatingSince)/minute))):0;
  function requireState(value,message){if(!value)throw Error(message);}
  function pay(s,key,n){requireState(s[key]>=n,'材料不足');s[key]-=n;}
  function change(s,action,pick,now){
    const has=k=>s.claimed.includes(k);
    switch(action){
      case 'breath':
        requireState(has('ferry')&&!s.breath,'行气诀第一重已参悟或尚未开放');
        pay(s,'pages',1);s.breath=1;break;
      case 'meditate':{
        const n=meditation(s,now);requireState(n>0,'静修每满一分钟积累一点修为');
        s.cultivation+=n;s.meditatingSince=now-(now-s.meditatingSince)%minute;break;
      }
      case 'breakthrough':
        requireState(has('lampsDone')&&s.realm===1,'巡灯交差后可突破炼气二层');
        pay(s,'cultivation',breakthroughCost);s.realm=2;break;
      case 'recruit':
        requireState(!s.recruit,'已经用帖招募过同门');pay(s,'tickets',1);
        s.recruit=crypto.getRandomValues(new Uint8Array(1))[0]%2?'blade':'ward';
        s.ally=s.recruit;break;
      case 'ally':
        requireState(pick===''||(pick==='adept'&&has('ferry'))||(pick&&pick===s.recruit),'尚未结识这位伙伴');
        s.ally=pick;break;
      case 'teach':
        requireState(s.recruit&&!s.allyRank,'伙伴已经受过指点或尚未招募');
        pay(s,'pages',1);s.allyRank=1;break;
      case 'bead':
        requireState(has('stoneRoad'),'船工尚未交付定风珠');s.bead=pick===true;break;
      case 'polish':
        requireState(has('stoneRoad')&&!s.beadRank,'定风珠当前无法温养');pay(s,'sand',1);s.beadRank=1;break;
      case 'temper':
        requireState(has('delivery')&&!s.swordRank,'飞剑当前无法温养');pay(s,'marrow',1);s.swordRank=1;break;
      default:throw Error('未知的修行动作');
    }
  }
  function snapshot(s){
    return {attack:10+(s.realm-1)+2*s.swordRank,hp:100+10*(s.realm-1),breath:s.breath,previousArt:s.art,
      learnedArts:Object.keys(arts).filter(key=>s.claimed.includes(key==='guard'?'stoneRoad':'rearRoad')),
      bead:s.bead?1+s.beadRank:0,ally:s.ally,allyRank:s.ally===s.recruit?s.allyRank:0};
  }
  return {storeKey,initial,award,change,snapshot,companions,arts,meditation,breakthroughCost};
})();
