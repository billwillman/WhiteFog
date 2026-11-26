// The embedded player runs the exported Unity graphs and reports their actual settlements.
(() => {
  const byId = id => document.getElementById('unity-' + id);
  let files, current, generation = 0, pending = Promise.resolve();
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
    } else if (data.kind === 'interrupt' || data.kind === 'error') {
      status.textContent = data.kind === 'error' ? data.text : '已打断施法';
      byId('interrupt').disabled = true;
    }
    byId('readout').textContent = '施放者 HP ' + number(data.casterHp) + ' · 状态 ' + (data.casterStates || '无') +
      '\n目标 HP ' + number(data.targetHp) + ' · 状态 ' + (data.targetStates || '无');
  }
  function reset() {
    const token = ++generation;
    byId('interrupt').disabled = true;
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
  function play(record) {
    const distance = byId('distance'), damage = byId('damage');
    if (!distance.reportValidity() || (record.asset.unwiredHit && !damage.reportValidity())) return;
    const request = {guid:record.asset.guid, distance:Number(distance.value), level:Number(byId('level').value) || 1,
      fallbackDamage:Number(damage.value), opponent:byId('direction').value === 'enemy'};
    const token = reset(), status = byId('play-status');
    status.textContent = '正在载入 Unity 战斗场景…';
    pending = pending.then(async () => {
      if (token !== generation) return;
      const resources = await loadFiles();
      if (token !== generation) return;
      const canvas = document.createElement('canvas');
      canvas.width = 540; canvas.height = 960; canvas.setAttribute('aria-label', record.asset.name + ' · Unity 战斗画面');
      canvas.addEventListener('skill-readout', event => { if (token === generation) report(event.detail); });
      byId('screen').replaceChildren(canvas);
      try {
        const instance = await createUnityInstance(canvas, {...resources, matchWebGLToCanvasSize:false, devicePixelRatio:1,
          companyName:'青云宗', productName:'云端御剑', productVersion:'1.3.0', streamingAssetsUrl:'QingyunSect/LoginDemo/StreamingAssets'},
          progress => { if (token === generation) status.textContent = '载入战斗场景 ' + Math.round(progress * 100) + '%'; });
        current = {instance,canvas};
        if (token === generation) instance.SendMessage('QingyunBattle','PlayEditorSkill',JSON.stringify(request));
      } catch (error) { canvas.remove(); throw error; }
    }).catch(error => { if (token === generation) status.textContent = '试放失败 · ' + (error.message || error); });
  }
  window.unitySkillPreview = {play, stop:reset, interrupt() { current?.instance.SendMessage('QingyunBattle','InterruptEditorSkill',''); }};
})();
