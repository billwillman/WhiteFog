// The Unity tab reads the exported graph. Browser drafts keep their own schema and storage.
(() => {
 const records = (window.unitySkillCatalog || []).map(({asset, graph}) => ({asset, graph: graph.MonoBehaviour || graph}))
  .sort((a,b) => a.graph.sourceId-b.graph.sourceId || a.asset.path.localeCompare(b.asset.path));
 const seconds = value => Number(value).toFixed(3) + ' s';
 let draftReady = false;

 function describe(record, action) {
  const {graph, asset} = record;
  if (action.ClassName !== 'OriginalSkillVisual') return {name:action.ClassName, fields:[]};
  const [stage, index] = action.IntValues;
  const cue = graph.effects[index];
  const stageNames = action.CustomInterface.Elements.find(item => item.Uid === 'stage')?.EnumOptions;
  const name = stageNames?.[stage] || (stage === 5 ? '位移事件标记' : action.ClassName);
  switch (stage) {
   case 0: {
    const motion = asset.motions?.find(item => item.uid === action.Uid);
    if (!motion) return {name, fields:[['角色动作', asset.motion]]};
    if (motion.mode === 'AnimationClip') return {name, fields:[['Animation Clip',motion.clipName],['资源路径',motion.clip]]};
    return {name, fields:[['Animator State',motion.state], ...(motion.clip ? [['原作绑定片段',motion.clip]] : [])]};
   }
   case 1: return {name, fields:[['施法特效', asset.cast]]};
   case 2: return {name, fields:[['武器特效', asset.weapon], ['挂点', graph.weaponBone], ['显示时长', seconds(graph.weaponSeconds)]]};
   case 3: return {name, fields:[['飞行道具', asset.projectile], ['拖尾', asset.trail], ['命中特效', asset.impact],
    ['发射挂点', graph.projectileBone || '角色根节点'], ['初速', graph.projectileSpeed + ' m/s'],
    ['加速度', graph.projectileAcceleration + ' m/s²'], ['拖尾时长', seconds(graph.tailSeconds)]]};
   case 6:
    if (cue) return {name:cue.onTarget ? '目标特效与命中' : '施法者特效',
     fields:[['特效资源', asset.cuePrefabs[index]], ['作用对象', cue.onTarget ? '目标' : '施法者'],
      ['挂点', cue.bone || '角色根节点'], ['跟随位置', cue.followBone ? '是' : '否'], ['跟随旋转', cue.followRotation ? '是' : '否'],
      ['显示时长', cue.duration > 0 ? seconds(cue.duration) : '按特效自身时长'], ...(cue.onTarget ? [['命中延迟',seconds(cue.hitDelay)]] : [])]};
    break;
  }
  return {name, fields:[]};
 }

 function fields(parent, pairs) {
  const list = el('dl');
  for (const [key, value] of pairs) {
   list.append(el('dt','',key), el('dd','',value === '' || value == null ? '—' : String(value)));
  }
  parent.append(list);
 }

 function showSkill(record, selectedId) {
  const {asset, graph} = record;
  $('unity-title').textContent = asset.name;
  for (const item of $('unity-skills').children) item.setAttribute('aria-current', String(item.dataset.guid === asset.guid));
  const events = [...graph.RootSet.Actions].sort((a,b) => a.WaitTime-b.WaitTime);
  const selected = events.find(action => action.Uid === selectedId) || events[0];
  $('unity-summary').textContent = events.length + ' 个动作';
  const play = $('unity-play');
  play.hidden = false;
  play.href = 'QingyunSect/LoginDemo/index.html?preview=skills&skill=' + encodeURIComponent(graph.sourceId);
  $('unity-events').replaceChildren();
  for (const action of events) {
   const data = describe(record, action);
   const row = button('', () => showSkill(record, action.Uid), 'step');
   row.classList.toggle('selected', action === selected);
   row.setAttribute('aria-pressed', String(action === selected));
   const text = el('span','',data.name);
   const resource = data.fields[0]?.[1];
   if (resource) text.append(el('small','',String(resource).split('/').pop()));
   row.append(el('time','',seconds(action.WaitTime)), text, el('em','',action.Uid));
   $('unity-events').append(row);
  }
  const details = $('unity-fields'); details.replaceChildren();
  if (selected) {
   const data = describe(record, selected);
   const execution = ['单次','执行多次','持续执行'];
   details.append(el('h3','',data.name));
   fields(details, [['节点', selected.Uid], ['开始时间', seconds(selected.WaitTime)], ['执行方式', execution[selected.Type] ?? selected.Type],
    ...(selected.Type !== 0 ? [['执行间隔', seconds(selected.Interval)], [selected.Type === 1 ? '执行次数' : '持续时长',
      selected.Type === 1 ? selected.Count : seconds(selected.KeepTime)]] : []), ...data.fields]);
  }
  details.append(el('h3','','技能来源'));
  fields(details, [['原作 ID', graph.sourceId], ['版本', graph.sourceVersion], ['施放方', graph.previewAsOpponent ? '敌方' : '主角'],
   ...(asset.enemy ? [['预览敌人', asset.enemy]] : []), ['技能图', asset.path]]);
 }

 function selectTab(useDraft) {
  $('unity-pane').hidden = useDraft;
  $('draft-pane').hidden = $('draft-tools').hidden = !useDraft;
  $('unity-tab').setAttribute('aria-pressed',String(!useDraft));
  $('draft-tab').setAttribute('aria-pressed',String(useDraft));
  if (useDraft) {
   if (!draftReady) { draftReady = true; boot(); }
   else { render(); message('已打开本机草稿'); }
   $('storage-status').textContent = '本机草稿';
  } else {
   if (draftReady) playing = false;
   $('storage-status').textContent = 'Unity 资产 · 只读';
   message('在 Unity 技能工作台保存后，刷新网页查看最新配置。战场试放使用已构建的 WebGL 包。');
  }
 }
 for (const record of records) {
  const {asset, graph} = record;
  const row = button('', () => showSkill(record), 'skill-row');
  row.dataset.guid = asset.guid;
  const words = el('span');
  words.append(el('strong','',asset.name),el('small','',graph.sourceId + ' · ' + (graph.previewAsOpponent ? '怪物' : '主角')));
  row.append(el('span','skill-glyph',asset.name[0]),words);
  $('unity-skills').append(row);
 }
 $('unity-count').textContent = records.length + ' 个';
 $('unity-tab').onclick = () => selectTab(false);
 $('draft-tab').onclick = () => selectTab(true);
 if (records.length) showSkill(records[0]);
 else $('unity-events').textContent = window.unitySkillCatalog ? 'Unity 原作技能库为空。' : '未载入技能库。请在 Unity 的 Tools → 技能工作台中运行「同步原作技能至 H5 编辑器」，再刷新本页。';
 selectTab(false);
})();
