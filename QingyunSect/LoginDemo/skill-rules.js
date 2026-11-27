// Source columns are named in each exported table. An advance and a level together identify a growth row.
function createSkillRules(tables) {
  function rows(name) {
    const {columns,rows}=tables[name];
    return rows.map(values=>Object.fromEntries(columns.map((key,i)=>[key,values[i]])));
  }
  const byId=list=>new Map(list.map(row=>[row.id,row]));
  const books=byId(rows('kungfu_info')), combat=byId(rows('skill_info'));
  const growth=new Map(), levels=rows('skill_lvlup_info');
  for(const row of levels){
    const key=[row.id,row.advance,row.level].join('/');
    if(growth.has(key))throw Error('重复的功法等级 '+key);
    growth.set(key,row);
  }
  const combinations=rows('kungfu_link_info'), recipes=rows('kungfu_compose_info');
  const specialRates=rows('kungfu_compose_special_rate_info'),dropPools=rows('kungfu_compose_special_drop_info');
  function resolve(bookId,advance,level){
    const book=books.get(bookId);
    return book&&growth.get([book.levelup_id,advance,level].join('/'));
  }
  function equippedLinks(ids,world){
    const counts=new Map(),tiers=new Map();
    for(const id of new Set(ids)){
      for(const link of new Set(books.get(id)?.link_id||[]))counts.set(link,(counts.get(link)||0)+1);
    }
    for(const row of combinations){
      if(row.worldid!==world||row.appear_num>(counts.get(row.id)||0))continue;
      if(!tiers.has(row.id)||tiers.get(row.id).rank<row.rank)tiers.set(row.id,row);
    }
    return [...tiers.values()];
  }
  function fixedRecipes(ids){
    const key=ids.join('/');
    return recipes.filter(r=>r.type===5&&[r.value,r.value2,r.value3].filter(Boolean).join('/')===key);
  }
  return {books,combat,levels,combinations,recipes,specialRates,dropPools,resolve,equippedLinks,fixedRecipes};
}
const skillRules=createSkillRules(window.qingyunSkillTables);
