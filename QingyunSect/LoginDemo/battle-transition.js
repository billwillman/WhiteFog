// The cover and opening use the same cloud mask as the original ferry entrance.
function makeMistPainter(canvas){
  const {mist,mistWidth,mistHeight}=qingyunBattleMist;
  canvas.width=mistWidth;canvas.height=mistHeight;
  const ctx=canvas.getContext('2d');
  const pixels=ctx.createImageData(mistWidth,mistHeight);
  const mask=Uint8Array.from(atob(mist),c=>c.charCodeAt(0));
  const smooth=(low,high,value)=>{
    const t=Math.max(0,Math.min(1,(value-low)/(high-low)));
    return t*t*(3-2*t);
  };
  return function paint(age,opening=null){
    const coverTime=(age-1.32)/1.18;
    const openTime=opening===null?0:opening/1.48;
    const warm=smooth(0,1,(age-1.4)/2.3);
    for(let i=0,p=0;i<mask.length;i+=3,p+=4){
      const cover=mask[i]*1.4/255-.2,radius=mask[i+1]*1.2/255-.12;
      const shade=(mask[i+2]-155)*.27;
      pixels.data[p]=239-6*warm+shade;pixels.data[p+1]=223+5*warm+shade;
      pixels.data[p+2]=198+21*warm+shade;
      pixels.data[p+3]=255*(opening===null?smooth(0,.22,1.55*coverTime-.25-cover):
        1-smooth(0,.25,openTime*1.2-radius+.09));
    }
    ctx.putImageData(pixels,0,0);
  };
}

// The caller swaps scenes and draws the first battle frame under the closed clouds.
function makeBattleTransition(layer){
  const mist=layer.querySelector('.battle-transition-mist');
  const paint=makeMistPainter(mist);
  const controls=layer.querySelector('.battle-transition-controls');
  const status=layer.querySelector('[role="status"]');
  const cancel=layer.querySelector('button');
  const reduced=matchMedia('(prefers-reduced-motion: reduce)');
  let cancelAnimation;
  // An independently opened trial starts under closed clouds.
  paint(2.55);
  layer.dataset.ready='';

  function wait(message,onCancel){
    controls.hidden=!message;
    status.textContent=message||'';
    cancel.hidden=!onCancel;
    cancel.onclick=onCancel||null;
  }
  function animate(duration,draw){
    return new Promise((resolve,reject)=>{
      let frame,elapsed=0,last=null;
      function stop(){
        cancelAnimationFrame(frame);cancelAnimation=null;
        reject(new Error('云幕过渡已取消'));
      }
      function tick(now){
        if(document.hidden)last=null;
        else{
          if(last!==null)elapsed=Math.min(duration,elapsed+Math.min(.1,(now-last)/1000));
          last=now;
          draw(elapsed);
          if(elapsed>=duration){cancelAnimation=null;resolve();return;}
        }
        frame=requestAnimationFrame(tick);
      }
      cancelAnimation=stop;
      frame=requestAnimationFrame(tick);
    });
  }
  function close(){
    cancelAnimation?.();
    layer.hidden=true;
    wait();
  }
  return {
    cover(intro){
      wait();
      mist.style.opacity=reduced.matches?'0':'';
      paint(reduced.matches?2.55:1.32);
      layer.hidden=false;
      if(reduced.matches)return animate(.11,age=>{mist.style.opacity=age/.11;});
      const lead=intro?1.32:0;
      return animate(lead+1.23,age=>{
        intro?.(age);
        if(age>=lead)paint(1.32+age-lead);
      });
    },
    wait,
    async reveal(){
      wait();
      if(reduced.matches){
        paint(2.55);
        await animate(.11,age=>{mist.style.opacity=1-age/.11;});
      }else{
        mist.style.opacity='';
        await animate(1.55,age=>paint(2.55+age,age));
      }
      close();
    },
    close
  };
}
