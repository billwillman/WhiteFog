// The embedded player runs the exported Unity graphs and reports their actual settlements.
(() => {
  const byId = id => document.getElementById('unity-' + id);
  let files, current, nativeFrame, generation = 0, pending = Promise.resolve();
  function loadFiles() {
    if (files) return files;
    files = (async () => {
      if (!window.qingyunPlayerFiles) await new Promise((resolve, reject) => {
        const script = document.createElement('script');
        script.src = 'QingyunSect/LoginDemo/battle-art/unity-player.js';
        script.onload = resolve;
        script.onerror = () => { script.remove(); reject(Error('未能读取 Unity 播放器文件')); };
        document.head.append(script);
      });
      const config = {};
      for (const [key, file] of Object.entries(window.qingyunPlayerFiles)) {
        let blob = new Blob([Uint8Array.from(atob(file.bytes), c => c.charCodeAt(0))]);
        if (file.gzip) blob = await new Response(blob.stream().pipeThrough(new DecompressionStream('gzip'))).blob();
        config[key] = URL.createObjectURL(new Blob([blob], {type:file.type}));
      }
      return config;
    })().catch(error => { files = null; throw error; });
    return files;
  }
  const number = value => Number(value.toFixed(4)).toString();
  function report(data) {
    const status = byId('play-status');
    if (data.kind === 'cast') {
      status.textContent = '正在试放 · ' + data.text;
      byId('interrupt').disabled = false;
    } else if (data.kind === 'hit') {
      const row = document.createElement('li');
      row.textContent = '逻辑段 ' + data.segment + ' · ' + data.text + ' · 扣血 ' + number(data.loss) + ' / 耗盾 ' + number(data.shield);
      byId('hits').append(row);
    } else if (['interrupt','blocked','error'].includes(data.kind)) {
      status.textContent = data.kind === 'interrupt' ? '已打断施法' : data.text;
      byId('interrupt').disabled = true;
    }
    byId('chain').disabled = !data.canChain;
    byId('readout').textContent = '施放者 HP ' + number(data.casterHp) + ' · 状态 ' + (data.casterStates || '无') +
      '\n目标 HP ' + number(data.targetHp) + ' · 状态 ' + (data.targetStates || '无') + (byId('chain').hidden ? '' :
        '\n施法输入 · '+(data.canMove?'移动已放行':'移动受限')+' / '+(data.canChain?'接招已放行':'接招受限'));
  }
  window.addEventListener('message', ({source,data}) => {
    if (!nativeFrame || source !== nativeFrame.contentWindow || data?.type !== 'qingyun-local-skill') return;
    const event = data.data;
    if (event.kind === 'error') byId('play-status').textContent = '试放失败 · ' + event.text;
    else if (Number.isFinite(event.targetHp)) report(event);
  });
  function nativeInput(action) {
    nativeFrame.contentWindow.postMessage({type:'qingyun-local-input',action}, '*');
  }
  function reset() {
    if (nativeFrame) { nativeFrame.remove(); nativeFrame = null; }

    const token = ++generation;
    byId('interrupt').disabled = true;
    byId('chain').disabled = true;
    for (const id of ['play-status','readout','hits']) byId(id).replaceChildren();
    if (current) current.canvas.hidden = true;
    pending = pending.catch(() => {}).then(async () => {
      if (current) {
        const previous = current; current = null;
        try { await previous.instance.Quit(); } finally { previous.canvas.remove(); }
      }
    });
    return token;
  }
  function requestFor(record) {
    const distance = byId('distance'), damage = byId('damage');
    if (!distance.reportValidity() || (record.asset.unwiredHit && !damage.reportValidity())) return;
    const freeLevel = byId('free-level');
    if (!freeLevel.parentNode.hidden && !freeLevel.reportValidity()) return;
    return {guid:record.asset.guid, distance:Number(distance.value), level:Number((freeLevel.parentNode.hidden ? byId('level') : freeLevel).value) || 1,
      fallbackDamage:Number(damage.value), opponent:byId('direction').value === 'enemy'};
  }
  function play(record) {
    const request = requestFor(record);
    if (!request) return;
    const token = reset(), status = byId('play-status');
    const local = window.localBattleSkillIds?.includes(record.graph.sourceId);
    status.textContent = local ? '正在读取本地战斗包…' : '正在载入 Unity 战斗场景…';

    pending = pending.then(async () => {
      if (token !== generation) return;
      if (local) {
        nativeFrame = document.createElement('iframe');
        nativeFrame.title = record.asset.name + ' · 本地战斗';
        nativeFrame.src = 'QingyunSect/LoginDemo/local-battle.html?' + new URLSearchParams({
          skill:record.graph.sourceId, distance:request.distance, level:request.level
        });
        byId('screen').replaceChildren(nativeFrame);
        return;
      }
      const resources = await loadFiles();
      if (token !== generation) return;
      const canvas = document.createElement('canvas');
      canvas.id = 'unity-battle-canvas';
      canvas.width = 540; canvas.height = 960; canvas.setAttribute('aria-label', record.asset.name + ' · Unity 战斗画面');
      canvas.addEventListener('skill-readout', event => { if (token === generation) report(event.detail); });
      byId('screen').replaceChildren(canvas);
      try {
        const instance = await createUnityInstance(canvas, {...resources, matchWebGLToCanvasSize:false, devicePixelRatio:1,
          companyName:'青云宗', productName:'云端御剑', productVersion:'1.3.0', streamingAssetsUrl:'QingyunSect/LoginDemo/StreamingAssets'},
          progress => { if (token === generation) status.textContent = '载入战斗场景 ' + Math.round(progress * 100) + '%'; });
        current = {instance,canvas,record};
        if (token === generation) instance.SendMessage('QingyunBattle','PlayEditorSkill',JSON.stringify(request));
      } catch (error) { canvas.remove(); throw error; }
    }).catch(error => { if (token === generation) status.textContent = '试放失败 · ' + (error.message || error); });
  }
  function chain() {
    if (!current) return;
    const request = requestFor(current.record);
    if (request) {
      byId('hits').replaceChildren();
      current.instance.SendMessage('QingyunBattle','ChainEditorSkill',JSON.stringify(request));
    }
  }
  window.unitySkillPreview = {play, stop:reset, chain, interrupt() { if (nativeFrame) nativeInput('interrupt'); else current?.instance.SendMessage('QingyunBattle','InterruptEditorSkill',''); }};
})();
