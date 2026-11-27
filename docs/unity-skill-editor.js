(() => {
  const records = (window.unitySkillCatalog || []).map(({asset,graph}) => {
    graph = graph.MonoBehaviour || graph;
    const root = graph.RootSet, nodes = new Map([[root.Uid,root]]);
    for (const group of ['Actions','Triggers','Handlers','ConditionGroups','Maths','Variables','Entities','Branchs'])
      for (const node of root[group]) nodes.set(node.Uid,node);
    const executionSources = new Map();
    for (const owner of [root,...root.Branchs,...root.Actions]) {
      for (const port of owner.outputPort) {
        if (port.Type !== 'RootOut' && port.Type !== 'BranchOutAction') continue;
        for (const input of port.uid) {
          if (!executionSources.has(input)) executionSources.set(input,[]);
          executionSources.get(input).push(owner.Uid);
        }
      }
    }
    return {asset,graph,root,nodes,executionSources,actions:new Map(asset.actions.map(a=>[a.uid,a]))};
  }).sort((a,b)=> Number(b.asset.original)-Number(a.asset.original) || (a.graph.sourceId||0)-(b.graph.sourceId||0) ||
    a.asset.name.localeCompare(b.asset.name,'zh') || a.asset.path.localeCompare(b.asset.path));
  const number = value => Number(Number(value).toPrecision(7));
  const seconds = value => number(value) + ' 秒';
  const links = port => (port?.uid || []).map(ref=>ref.split('#')[0]);
  let selected, draftReady = false, camp = '全部', role = '';
  function pairs(parent, values) {
    if (!values.length) return;
    const dl = el('dl');
    for (const [label,value] of values) dl.append(el('dt','',label),el('dd','',String(value ?? '—')));
    parent.append(dl);
  }
  function section(parent, title) {
    const details = el('details');
    details.append(el('summary','',title)); parent.append(details); return details;
  }
  function label(id) {
    const node = selected.nodes.get(id);
    if (!node) return id;
    if (node === selected.root) return '技能入口';
    if (selected.actions.has(id)) return selected.actions.get(id).title;
    if (node.Name === 'Variable') return ['常数 '+number(node.Constant),'属性 '+node.AttributeKey,'变量 '+node.VariableUid,'随机数'][node.Type];
    if (selected.root.Entities.includes(node)) return ['玩家','施加者','目标','指定实体 '+node.EntityUid,'技能实例'][node.Type];
    return node.ClassName || node.Name;
  }
  function condition(parent,id,ancestors = []) {
    const node = selected.nodes.get(id);
    const d = section(parent,label(id)+' · '+id);
    if (!node || ancestors.includes(id)) return;
    const fields = [], arrays = ['BoolValues','StringValues','FloatValues','IntValues'];
    for (const f of node.CustomInterface?.Elements || []) {
      if (f.Type < 4) fields.push([f.Label,node[arrays[f.Type]]?.[f.Index]]);
      if (f.Type === 7) fields.push([f.Label,f.EnumOptions[node.IntValues[f.Index]]]);
    }
    if (selected.root.ConditionGroups.includes(node)) fields.push(['组合逻辑',['AND','OR','XOR','NOR'][node.Type]]);
    pairs(d,fields);
    for (const p of node.inputPort || []) for (const other of links(p)) condition(d,other,[...ancestors,id]);
  }
  function describeTiming(node) {
    const parents = selected.executionSources.get(node.Uid+'#0') || [];
    return parents.map(parent=>parent === selected.root.Uid ? '直接' :
      selected.actions.get(parent)?.onContact ? parent+' 命中后' : '分支 · '+parent).join('、') || '未连接';
  }
  function drawAction(details, node) {
    const a = selected.actions.get(node.Uid);
    details.append(el('h3','',a.title));
    for (const parent of selected.executionSources.get(node.Uid+'#0') || []) {
      if (selected.actions.get(parent)?.onContact)
        details.append(button('命中来源 · '+label(parent)+' '+parent,()=>chooseNode(parent)));
    }
    if (a.sourceTime) {
      details.append(el('p','',a.sourceTime)); return;
    }
    pairs(details,[['节点',node.Uid],['执行入口',describeTiming(node)],['开始延迟',seconds(node.WaitTime)],
      ['执行方式',['单次','执行多次','持续执行'][node.Type]],...(node.Type ? [['执行间隔',seconds(node.Interval)],
        node.Type===1 ? ['执行次数',node.Count] : ['持续时长',seconds(node.KeepTime)]]:[]),...a.fields.map(f=>[f.label,f.value])]);
    const guards = node.inputPort.filter(p=>p.Type==='ConditionIn').flatMap(links);
    const pre = section(details,'动作前置条件 · '+guards.length+' 项');
    for (const id of guards) condition(pre,id);
    for (const p of node.outputPort) for (const id of links(p)) {
      if (selected.actions.has(id)) details.append(button((a.onContact?'命中后执行':'后续节点')+' · '+label(id)+' · '+id,()=>chooseNode(id)));
      else condition(details,id);
    }
  }
  function drawDetails(node) {
    const {asset,graph,root} = selected, details = $('unity-fields');
    details.replaceChildren();
    if (node) drawAction(details,node);
    const guards = links(root.inputPort[0]).flatMap(id=>links(selected.nodes.get(id)?.inputPort?.[0]));
    const entry = section(details,'技能入口条件 · '+guards.length+' 项');
    for (const id of guards) condition(entry,id);
    for (const table of asset.levels) {
      const d = section(details,'技能等级表 · '+table.path.split('/').pop());
      for (const row of table.rows) { d.append(el('h4','','等级 '+row.level)); pairs(d,row.fields.map(f=>[f.key,number(f.value)])); }
    }
    if (asset.statuses.length) pairs(section(details,'绑定状态'),asset.statuses.map(f=>[f.label,f.value]));
    const ref = window.unityDamageReference?.skills.find(r=>r.skillId===graph.sourceId);
    if (ref) pairs(section(details,'原作数值核对'),[...(ref.hasLevelRow ? [['整招倍率',number(ref.rawRatio/10)+'%'],['固定值',ref.rawFlat],
      ['目标数',ref.targetCount],['冷却',seconds(ref.cooldownMs/1000)]]:[]),['时序',ref.timing],['核对范围',ref.limit]]);
    const source = section(details,'技能来源');
    pairs(source,[...(asset.original?[['原作 ID',graph.sourceId],['版本',graph.sourceVersion]]:[]),
      ['默认施放方',graph.previewAsOpponent?'怪物':'主角'],...(asset.enemy?[['预览敌人',asset.enemy]]:[]),['技能图',asset.path]]);
  }
  function chooseNode(id) {
    for (const row of $('unity-events').querySelectorAll('[data-uid]')) {
      row.classList.toggle('selected',row.dataset.uid===id);
      row.setAttribute('aria-pressed',String(row.dataset.uid===id));
    }
    drawDetails(selected.nodes.get(id));
  }
  function showTimeline() {
    const list = $('unity-events'); list.replaceChildren();
    const actions = [...selected.root.Actions].sort((a,b)=>a.WaitTime-b.WaitTime);
    const runtime = actions.filter(n=>!selected.actions.get(n.Uid).sourceTime);
    const reference = actions.filter(n=>selected.actions.get(n.Uid).sourceTime);
    for (const node of runtime) {
      const a = selected.actions.get(node.Uid), row = button('',()=>chooseNode(node.Uid),'step');
      row.dataset.uid=node.Uid;
      const name=el('span','',a.title); name.append(el('small','',describeTiming(node)+(a.timing?' · '+a.timing:'')));
      if (a.duration>0 && a.bands.length) {
        const track=el('span','motion-track');
        a.bands.forEach((band,i)=>{
          const end=a.bands[i+1]?.start ?? a.duration;
          if (end<=band.start) return;
          const part=el('i',band.blend?'blend':''); part.title=band.name+' · '+seconds(band.start);
          part.style.left=(100*band.start/a.duration)+'%'; part.style.width=(100*(end-band.start)/a.duration)+'%'; track.append(part);
        }); name.append(track);
      }
      row.append(el('time','',seconds(node.WaitTime)),name,el('em','',node.Uid)); list.append(row);
    }
    if (!runtime.length) list.textContent='此技能图没有动作。';
    if (reference.length) {
      const fold = section(list,'原作时间参考 · '+reference.length+' 项');
      for(const node of reference) {
        const a = selected.actions.get(node.Uid), row=button('',()=>chooseNode(node.Uid),'step');
        row.dataset.uid=node.Uid; row.append(el('time','',seconds(node.WaitTime)),el('span','',a.title),el('em','',node.Uid)); fold.append(row);
      }
    }
    chooseNode(runtime[0]?.Uid);
  }
  function setView(show) {
    $('unity-preview').hidden=!show; $('unity-events').hidden=show;
    $('unity-summary').previousElementSibling.textContent=show?'战斗预演':'动作时间轴';
    $('unity-timeline-tab').setAttribute('aria-pressed',String(!show));
    $('unity-preview-tab').setAttribute('aria-pressed',String(show));
    if (!show) window.unitySkillPreview.stop();
  }
  function open(record) {
    window.unitySkillPreview.stop(); selected=record;
    const {asset,graph}=record;
    $('unity-views').hidden=false; $('unity-title').textContent=asset.name; $('unity-summary').textContent=asset.actions.filter(a=>!a.sourceTime).length+' 个动作';
    const table=asset.levels[0], level=$('unity-level'); level.replaceChildren();
    const levels = table ? [...new Set(table.rows.map(r=>r.level))].filter(n=>asset.levels.every(t=>t.rows.some(r=>r.level===n))).sort((a,b)=>a-b) : [1];
    for(const n of levels) level.add(new Option(n,n));
    $('unity-level-label').hidden=!table;
    const freeLevel=$('unity-free-level');
    freeLevel.parentNode.hidden=Boolean(table) || !record.root.Variables.some(v=>v.Type===2 && v.VariableUid==='skill.level');
    freeLevel.value='1';
    $('unity-play').disabled=levels.length===0;
    if(!levels.length) $('unity-play-status').textContent='所绑定的等级表没有共同等级。';
    $('unity-damage-label').hidden=!asset.unwiredHit;
    $('unity-direction-label').hidden=asset.original; $('unity-direction').value=graph.previewAsOpponent?'enemy':'hero';
    for(const row of $('unity-skills').querySelectorAll('[data-guid]')) row.setAttribute('aria-current',String(row.dataset.guid===asset.guid));
    showTimeline();
  }
  function library() {
    const query=$('unity-search').value.trim().toLowerCase(), list=$('unity-skills'); list.replaceChildren();
    const matches=records.filter(({asset,graph})=>(camp==='全部'||asset.camp===camp) && (!role||asset.character===role) &&
      [asset.camp,asset.character,asset.path,graph.sourceId].join(' ').toLowerCase().includes(query));
    const groups=new Map();
    for(const record of matches) {
      const {camp,character}=record.asset;
      const heading=character?camp+' · '+character:'未归属角色';
      if(!groups.has(heading)) groups.set(heading,[]);
      groups.get(heading).push(record);
    }
    for(const [heading,group] of groups) {
      if(camp==='全部') list.append(el('h3','',heading));
      for(const record of group) {
        const {asset,graph}=record,row=button('',()=>open(record),'skill-row'),name=el('span');
        row.dataset.guid=asset.guid;row.title=asset.path;row.setAttribute('aria-current',String(record===selected));
        name.append(el('strong','',asset.name),el('small','',asset.original?String(graph.sourceId):asset.path.split('/').slice(-2,-1)[0]));
        row.append(el('span','skill-glyph',asset.name[0]),name);list.append(row);
      }
    }
    $('unity-count').textContent=matches.length+' / '+records.length;
  }
  function filters() {
    const camps=$('unity-camps'), roles=$('unity-roles');
    camps.replaceChildren(); roles.replaceChildren();
    for (const name of ['全部','友方','敌方','未归属角色']) {
      const tab=button(name==='未归属角色'?'未归属':name,()=>{ camp=name; role=''; filters(); $('unity-tab').click(); });
      tab.setAttribute('aria-pressed',String(name===camp)); camps.append(tab);
    }
    const names=[...new Set(records.filter(r=>r.asset.camp===camp).map(r=>r.asset.character).filter(Boolean))];
    roles.hidden=names.length===0;
    if (names.length) {
      role=names.includes(role)?role:names[0];
      roles.append(el('span','','角色'));
      for (const name of names) {
        const item=el('label'), radio=el('input');
        radio.type='radio'; radio.name='unity-role'; radio.checked=role===name;
        radio.onchange=()=>{role=name; $('unity-tab').click();}; item.append(radio,document.createTextNode(name)); roles.append(item);
      }
    }
  }
  function selectTab(draft) {
    $('unity-pane').hidden=draft; $('draft-pane').hidden=$('draft-tools').hidden=!draft;
    $('unity-tab').setAttribute('aria-pressed',String(!draft)); $('draft-tab').setAttribute('aria-pressed',String(draft));
    if (draft) {
      window.unitySkillPreview.stop();
      if (!draftReady) {draftReady=true;boot();} else {render();message('已打开本机草稿');}
      $('storage-status').textContent='本机草稿';
    } else {
      if (draftReady) playing=false;
      $('storage-status').textContent='Unity 资产 · 只读';
      message('Unity 保存后刷新可查看配置。已导出的本地战斗包可直接在 H5 试放。');
    }
  }
  $('unity-tab').onclick=()=>{ selectTab(false); window.unitySkillPreview.stop(); selected=null; setView(false); $('unity-views').hidden=true;
    $('unity-title').textContent='Unity 技能工作台'; $('unity-events').textContent='请选择技能'; $('unity-summary').replaceChildren(); $('unity-fields').replaceChildren(); library(); };
  $('draft-tab').onclick=()=>selectTab(true);
  $('unity-search').oninput=library;
  $('unity-timeline-tab').onclick=()=>setView(false); $('unity-preview-tab').onclick=()=>setView(true);
  $('unity-play').onclick=()=>{if(selected)window.unitySkillPreview.play(selected);};
  $('unity-interrupt').onclick=()=>window.unitySkillPreview.interrupt();
  filters(); $('unity-tab').click();
  if(!records.length) $('unity-events').textContent='尚未载入 Unity 技能清单。';
})();
