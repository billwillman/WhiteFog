// The caller swaps scenes and draws the first battle frame while the clouds cover the viewport.
function makeBattleTransition(layer){
  const mist=layer.querySelector('.battle-transition-mist');
  const controls=layer.querySelector('.battle-transition-controls');
  const status=layer.querySelector('[role="status"]');
  const cancel=layer.querySelector('button');
  const reduced=matchMedia('(prefers-reduced-motion: reduce)');
  let animation;

  function wait(message,onCancel){
    controls.hidden=!message;
    status.textContent=message||'';
    cancel.hidden=!onCancel;
    cancel.onclick=onCancel||null;
  }
  function fade(from,to,duration,easing){
    animation?.cancel();
    const frames=[{opacity:from},{opacity:to}];
    if(!reduced.matches){
      frames[0].transform=from?'scale(1.04)':'scale(1.16) translateY(4%)';
      frames[1].transform=to?'scale(1.04)':'scale(1.16) translateY(-4%)';
    }
    animation=mist.animate(frames,{duration:reduced.matches?110:duration,fill:'both',easing});
    return animation.finished;
  }
  function close(){
    layer.hidden=true;
    animation?.cancel();animation=null;
    wait();
  }
  return {
    cover(ferry=false){
      wait();layer.hidden=false;
      return fade(0,1,ferry?1100:480,'ease-in-out');
    },
    wait,
    async reveal(){
      wait();
      await fade(1,0,550,'ease-in-out');
      close();
    },
    close
  };
}
