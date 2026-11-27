// One transaction covers the books, their occupied slots and the recipe output.
const skillInventory=(()=>{
  let database;
  function open(){
    if(!database)database=new Promise((resolve,reject)=>{
      const req=indexedDB.open('qingyun-cultivation',1);
      req.onupgradeneeded=()=>req.result.createObjectStore('player');
      req.onsuccess=()=>{const db=req.result;db.onversionchange=()=>{db.close();database=null;};resolve(db);};
      req.onerror=()=>{database=null;reject(req.error);};
    });
    return database;
  }
  function initial(){
    const start=window.qingyunBookStart,slots=start.startingBooks;
    const books={};
    for(const id of slots)books[id]={count:1,advance:1,level:0};
    for(const item of start.extraBooks){
      const book=books[item.book]??={count:0,advance:1,level:0};book.count+=item.count;
    }
    return {books,slots:Array.from({length:6},(_,i)=>slots[i]||0)};
  }
  async function transact(change,key='books-v1',create=initial){
    const db=await open();
    return new Promise((resolve,reject)=>{
      const tx=db.transaction('player','readwrite'),store=tx.objectStore('player'),req=store.get(key);
      let state,reason;
      req.onsuccess=()=>{
        try{
          state=req.result??create();
          if(change)change(state);
          if(change||!req.result)store.put(state,key);
        }catch(err){reason=err;tx.abort();}
      };
      tx.oncomplete=()=>resolve(state);
      tx.onabort=()=>reject(reason||tx.error||Error('背包未保存'));
    });
  }
  function loadout(state){
    return state.slots.flatMap((book,slot)=>{
      if(!Number.isInteger(book)||!book)return [];
      const item=state.books[book];
      if(!item?.count)throw Error('出战功法缺少藏本 '+book);
      return [{book,slot,advance:item.advance,level:item.level}];
    });
  }
  function eligible(row){
    return window.qingyunBookStart.openRecipeLevels.includes(row.level_id)&&row.success===1000&&
      !row.limited_time&&!row.requirement&&!row.add&&!row.special_prop&&!row.safety&&!row.item&&!row.number&&
      row.success_item?.length&&row.success_item.every(([type,id,count])=>type===4&&window.qingyunBookStart.ordinaryResults.includes(id)&&skillRules.books.has(id)&&Number.isSafeInteger(count)&&count>0);
  }
  function recipe(ids){
    const books=ids.map(id=>skillRules.books.get(id));
    if(books.some(book=>!book))throw Error('剧情传授的招式不能作合成材料');
    if(books.some(book=>book.quality!==books[0].quality))throw Error('只有同品质的武学才能合成');
    const matches=skillRules.fixedRecipes(ids).filter(eligible);
    if(matches.length!==1)throw Error('当前历练尚未开放这组配方');
    return matches[0];
  }
  function ingredients(ids){
    const counts=new Map();for(const id of ids)counts.set(id,(counts.get(id)||0)+1);return counts;
  }
  function check(state,ids){
    const row=recipe(ids);
    for(const [id,count] of ingredients(ids))if(spare(state,id)<count)throw Error('缺少'+skillRules.books.get(id).name+' × '+count);
    return row;
  }
  function craft(ids){
    return transact(state=>{
      const row=check(state,ids);
      for(const [id,count] of ingredients(ids))state.books[id].count-=count;
      for(const [,id,count] of row.success_item){
        const book=state.books[id]??={count:0,advance:1,level:0};book.count+=count;
      }
      for(const id of ids)if(state.books[id]?.count===0)delete state.books[id];
    });
  }
  function spare(state,id){
    return (state.books[id]?.count||0)-(state.slots.includes(id)?1:0);
  }
  function arrange(id,slot,arts=[],from){
    return transact(state=>{
      if(!Number.isInteger(slot)||slot<0||slot>=state.slots.length)throw Error('请选择有效的上排位置');
      if(from?.slot!==undefined&&state.slots[from.slot]!==from.id)throw Error('武学位置已变动');
      const old=state.slots.indexOf(id);
      if(typeof id==='string'){
        if(!arts.includes(id))throw Error('尚未学会这门武学');
      }else if(id){
        const item=state.books[id];
        if(!item?.count||!window.localBattleSkillIds.includes(skillRules.resolve(id,item.advance,item.level)?.skill))
          throw Error('这本书目前仅可用作合成材料');
      }
      if(id&&old>=0)state.slots[old]=state.slots[slot];
      state.slots[slot]=id;
    });
  }
  function readForBattle(arts,previousArt){
    if(!arts.includes(previousArt))return transact();
    return transact(state=>{
      if(state.boardVersion)return;
      const free=state.slots.indexOf(0);
      if(free>=0&&!state.slots.includes(previousArt))state.slots[free]=previousArt;
      state.boardVersion=1;
    });
  }
  async function reset(){
    const db=await open();
    return new Promise((resolve,reject)=>{
      const tx=db.transaction('player','readwrite'),store=tx.objectStore('player');
      store.delete('books-v1');store.delete('training-v1');
      tx.oncomplete=resolve;tx.onabort=()=>reject(tx.error||Error('清理失败'));
    });
  }
  const training=change=>transact(change,'training-v1',trainingRules.initial);
  function cultivate(action,pick){
    return training(state=>{
      let saved=null;
      try{saved=JSON.parse(localStorage.getItem(trainingRules.storeKey));}catch(_){}
      const now=Date.now();
      trainingRules.award(state,window.qingyunChapterTasks,saved?.progress??0,saved?.firstClears??{},now);
      if(action)trainingRules.change(state,action,pick,now);
    });
  }
  return {read:()=>transact(),craft,arrange,spare,readForBattle,check,loadout,training,cultivate,reset};
})();