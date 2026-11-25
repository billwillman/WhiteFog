// Idle uses two baked poses. Casts use the retargeted humanoid palette.
function makeBattleEntry(art,onVictory) {
  const scene=document.getElementById('battle-entry');
  const canvas=document.getElementById('battle-role'), still=document.getElementById('battle-still');
  const bar=makeBattleBar(art.ui),result=document.getElementById('battle-result');
  const palette=new Float32Array(60);
  let mode='ferry',victorySent=false,pose;
  const reduced=matchMedia('(prefers-reduced-motion: reduce)');
  let gl,program,parts,locations,drawClouds,drawEdges,drawEnemy,drawSkill,loading,frame=0,last=null,age=0;

  function swordMesh() {
    const vertices=[],indices=[];
    // Longitudinal sections in model space, from the pommel to the sword tip.
    const rings=[[1.15,.059,.052,2],[1.08,.051,.042,2],[1.04,.038,.036,3],
      [.99,.043,.038,2],[.92,.038,.036,3],[.90,.041,.037,2],[.80,.038,.036,3],
      [.78,.21,.047,2],[.70,.17,.046,2],[.67,.11,.034,0],
      [.49,.14,.034,0],[-1.62,.084,.026,0],[-2.28,.003,.003,0]];
    for(let j=1;j<rings.length;j++){
      const ring=([y,w,h])=>[[-w,y,-.012],[0,y,h-.012],[w,y,-.012],[0,y,-h-.012]];
      const front=ring(rings[j-1]),back=ring(rings[j]);
      for(let side=0;side<4;side++){
        const index=vertices.length/9,next=(side+1)%4;
        const swatch=rings[j][3]===0&&side%2===1?1:rings[j][3];
        for(const p of [front[side],front[next],back[side],back[next]])
          vertices.push(...p,...p,(swatch+.5)/4,.5,[1,.93,.8,.76][side]);
        indices.push(index,index+1,index+2,index+2,index+1,index+3);
      }
    }
    const palette=gl.createTexture();gl.bindTexture(gl.TEXTURE_2D,palette);
    gl.texImage2D(gl.TEXTURE_2D,0,gl.RGBA,4,1,0,gl.RGBA,gl.UNSIGNED_BYTE,
      new Uint8Array([234,246,238,255,101,156,156,255,199,159,91,255,76,63,48,255]));
    for(const p of [gl.TEXTURE_MIN_FILTER,gl.TEXTURE_MAG_FILTER])gl.texParameteri(gl.TEXTURE_2D,p,gl.NEAREST);
    return upload(new Float32Array(vertices),new Uint16Array(indices),palette);
  }
  function upload(vertices,indices,image){
    return {vertex:buffer(gl,gl.ARRAY_BUFFER,vertices),index:buffer(gl,gl.ELEMENT_ARRAY_BUFFER,indices),count:indices.length,image};
  }
  async function setup(){
    still.src=art.still || assets.rider;
    await still.decode();
    try{
      gl=canvas.getContext('webgl',{alpha:true,depth:true,antialias:true,premultipliedAlpha:false});
      if(!gl)throw Error('webgl');
      program=makeProgram(gl,`
        attribute vec3 rest,raised; attribute vec2 coords; attribute float shade;
        attribute vec3 bindPos; attribute vec4 weights; attribute float lastWeight;
        uniform vec4 bones[15]; uniform float time,aspect,blend,casting;
        vec3 skin(int k){vec4 p=vec4(bindPos,1.);return vec3(dot(bones[k],p),dot(bones[k+1],p),dot(bones[k+2],p));}
        varying vec2 uv; varying float light;
        void main(){
          float yaw=-3.04, el=${BATTLE_PITCH.toFixed(9)};
          vec3 right=vec3(cos(yaw),sin(yaw),0.);
          vec3 eye=vec3(sin(yaw)*cos(el),-cos(yaw)*cos(el),sin(el));
          vec3 up=cross(eye,right);
          vec3 idle=mix(rest,raised,blend);
          float total=dot(weights,vec4(1.))+lastWeight;
          vec3 posed=weights.x*skin(0)+weights.y*skin(3)+weights.z*skin(6)+weights.w*skin(9)+lastWeight*skin(12);
          vec3 p=idle+casting*(posed-total*idle)-vec3(0.,0.,.78);
          float d=7.5-dot(p,eye);
          vec2 anchor=vec2(.009*sin(time*.6),-.345+.012*sin(time*1.55));
          gl_Position=vec4(d*anchor+1.70*vec2(dot(p,right)/aspect,dot(p,up)),d-.35,d);
          uv=coords;light=shade;
        }`,`
        varying vec2 uv; varying float light; uniform sampler2D paint;
        void main(){vec4 c=texture2D(paint,uv);if(c.a<.45) discard;
          gl_FragColor=vec4(c.rgb*light,1.);}
        `,['rest','raised','coords','shade','bindPos','weights','lastWeight']);
      locations=['time','aspect','blend','casting','bones[0]'].map(name=>gl.getUniformLocation(program,name));
      const bytes=s=>Uint8Array.from(atob(s),c=>c.charCodeAt(0)).buffer;
      parts=await Promise.all(art.parts.map(async part=>upload(
        Float32Array.from(new Int16Array(bytes(part.vertices)),v=>v/4096),
        new Uint16Array(bytes(part.indices)),await texture(gl,part.texture,true,false))));
      pose=decodeSwordPose(art.swordPose);
      parts.forEach((part,i)=>{part.skin=buffer(gl,gl.ARRAY_BUFFER,pose.parts[i]);});
      parts.unshift(swordMesh());
      drawClouds=await makeBattleClouds(gl,art);
      drawEdges=await makePaintedCloudEdges(gl,art);
      drawEnemy=art.enemy ? await makeEnemyImpostor(gl,art.enemy) : await makeFlyingEnemy(gl,art.enemyModel);
      drawSkill=await makeSwordEffects(gl,art.effects,assets.story.parts.boat);
      gl.enable(gl.DEPTH_TEST);
      gl.clearColor(0,0,0,0);
      still.hidden=true;canvas.hidden=false;
    }catch(error){console.warn("战场切换静态画面",error);parts=null;canvas.hidden=true;still.hidden=false;}
  }
  canvas.addEventListener('webglcontextlost',e=>{
    e.preventDefault();parts=null;canvas.hidden=true;still.hidden=false;
  });
  canvas.addEventListener('webglcontextrestored',()=>{loading=null;if(!scene.hidden)prepare();});
  function prepare(){
    if(!loading)loading=setup().catch(error=>{loading=null;throw error;});
    return loading;
  }
  function draw(t){
    const s=battleState(t);
    bar.update(s);result.hidden=!s.complete;
    if(s.complete&&!victorySent){victorySent=true;onVictory();}
    if(parts){
      const ratio=Math.min(devicePixelRatio,1.5,900/Math.max(1,scene.clientWidth));
      const w=Math.max(1,Math.round(scene.clientWidth*ratio)),h=Math.max(1,Math.round(scene.clientHeight*ratio));
      if(canvas.width!==w||canvas.height!==h){canvas.width=w;canvas.height=h;}
      gl.viewport(0,0,w,h);gl.clear(gl.COLOR_BUFFER_BIT|gl.DEPTH_BUFFER_BIT);
      drawClouds(reduced.matches?0:t,w/h);drawEdges(reduced.matches?0:t,w/h,reduced.matches?0:s.gust);
      if(drawEnemy&&s.enemy[1]<1.35)drawEnemy(w/h,t,s.enemy);
      gl.enable(gl.DEPTH_TEST);gl.useProgram(program);
      gl.uniform1f(locations[0],t);gl.uniform1f(locations[1],w/h);gl.uniform1f(locations[2],.5-.5*Math.cos(t*1.35));
      let strength=0;
      if(s.clip&&s.phase<=s.duration){
        const cursor=Math.min(s.clip.frames-1,s.phase*s.clip.rate),a=Math.floor(cursor),b=Math.min(a+1,s.clip.frames-1),u=cursor-a;
        for(let i=0;i<60;i++)palette[i]=s.clip.rows[a*60+i]*(1-u)+s.clip.rows[b*60+i]*u;
        strength=smooth(0,.16,s.phase)*(1-smooth(s.duration-.19,s.duration,s.phase));
      }
      gl.uniform4fv(locations[4],palette);gl.uniform1f(locations[3],strength);
      for(const part of parts){
        gl.bindBuffer(gl.ARRAY_BUFFER,part.vertex);gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER,part.index);
        attribute(gl,0,3,36,0);attribute(gl,1,3,36,12);attribute(gl,2,2,36,24);attribute(gl,3,1,36,32);
        if(part.skin){gl.bindBuffer(gl.ARRAY_BUFFER,part.skin);attribute(gl,4,3,32,0);attribute(gl,5,4,32,12);attribute(gl,6,1,32,28);}
        else{for(let i=4;i<=6;i++){gl.disableVertexAttribArray(i);gl.vertexAttrib4f(i,0,0,0,0);}}
        bindTexture(gl,0,part.image);gl.drawElements(gl.TRIANGLES,part.count,gl.UNSIGNED_SHORT,0);
      }
      for(let i=4;i<=6;i++)gl.disableVertexAttribArray(i);
      drawSkill(w/h,t,s);
    }
  }
  function battleState(t){return battleFrame(mode,t,pose?.clips||art.swordPose.clips);}
  function tick(now){
    if(document.hidden){last=null;}else{
      if(last!==null)age+=Math.min(.1,(now-last)/1000);
      last=now;draw(age);
    }
    frame=requestAnimationFrame(tick);
  }
  function stop(){cancelAnimationFrame(frame);scene.hidden=true;last=null;}
  function show(which='ferry'){
    stop();mode=which;victorySent=false;age=0;
    const tutorial=mode==='ferry';
    scene.querySelector('.court-chapter small').textContent=tutorial?'第一章　第三声渡钟':'功法演示';
    scene.querySelector('.court-chapter strong').textContent=tutorial?'雾里有灯':'破空剑法 · 流星剑雨';
    bar.configure(swordEncounters[mode]);scene.hidden=false;draw(0);frame=requestAnimationFrame(tick);
  }
  return {prepare,show,stop};
}

