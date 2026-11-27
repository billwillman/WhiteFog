(()=>{
  const get=id=>document.getElementById(id);
  const panel=get('gm-panel'),actions=get('gm-actions'),confirm=get('gm-confirm'),status=get('gm-status');
  const close=get('gm-close'),cancel=get('gm-cancel'),reset=get('gm-reset');
  let clearing=false;
  function confirmation(visible){
    actions.hidden=visible;confirm.hidden=!visible;status.hidden=true;
  }
  get('gm-open').addEventListener('click',()=>{
    confirmation(false);panel.showModal();
  });
  close.addEventListener('click',()=>panel.close());
  get('gm-clear').addEventListener('click',()=>{confirmation(true);cancel.focus();});
  cancel.addEventListener('click',()=>{confirmation(false);get('gm-clear').focus();});
  panel.addEventListener('cancel',event=>{if(clearing)event.preventDefault();});
  reset.addEventListener('click',async()=>{
    if(clearing)return;
    clearing=close.disabled=cancel.disabled=reset.disabled=true;
    status.textContent='正在清理进度…';status.hidden=false;
    try{await firstChapter.clearProgress();}
    catch(error){
      status.textContent='清理未完成，请重试。'+(error.message||'');
      clearing=close.disabled=cancel.disabled=reset.disabled=false;
    }
  });
})();
