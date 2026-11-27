const battleParties=window.qingyunBattleParties;
function resolveBattleParty(name,rules,available,loadout) {
  const party=battleParties[name];
  if(!Object.hasOwn(battleParties,name))throw Error('未配置这场战斗 '+name);
  function move(id){
    const skill=rules.combat.get(id);
    if(!skill||!available.includes(id))throw Error('战斗技能 '+id+' 尚未导出本地技能包');
    return {id,cooldown:skill.cooldown/1000};
  }
  const slots=party.practice?party.player:(loadout??party.player);
  if(slots.length>6||new Set(slots.map(s=>s.book)).size!==slots.length)
    throw Error('上排最多放六门不同的我方武学');
  const player=slots.map(slot=>{
    const growth=rules.resolve(slot.book,slot.advance,slot.level);
    if(!growth)throw Error('功法缺少指定阶级配置 '+slot.book);
    return {...move(growth.skill),...slot};
  });
  const waves=(party.waves??[party]).map(row=>({
    model:row.enemyModel,enemy:row.enemySkills.map(move),flock:row.flock,health:row.health,attack:row.attack,slam:row.slam
  }));
  const enemy=[...new Map(waves.flatMap(wave=>wave.enemy).map(skill=>[skill.id,skill])).values()];
  return {heading:party.heading,title:party.title,practice:party.practice===true,player,enemy,waves,
    links:rules.equippedLinks(slots.map(slot=>slot.book),party.world)};
}
