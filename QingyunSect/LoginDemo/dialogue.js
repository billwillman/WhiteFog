// Each speaker has a root, a breathing chest and a head joint. Expression
// attachments share mesh UVs, so changing a line never resets the idle pose.
function makePortraitPlayer(frame, url) {
  const surface=frame.querySelector('canvas');
  const still=matchMedia('(prefers-reduced-motion: reduce)');
  // Column and row of each authored facial expression in the shared atlas.
  const faces={
    hero:{thoughtful:[0,0],warm:[0,1],intent:[2,0],calling:[2,1]},
    senior:{kind:[1,0],earnest:[1,1]},
    adept:{searching:[3,0],relieved:[3,1]}
  };
  let actor=null,expression,cellUV,render=null,request=0,last=-Infinity,building=null;
  function draw(stamp){
    request=0;
    if(!actor||document.hidden||!render)return;
    if(stamp-last>=49){
      render(still.matches?0:stamp*.001,...cellUV);
      last=stamp;
    }
    if(!still.matches)request=requestAnimationFrame(draw);
  }
  function refresh(){
    cancelAnimationFrame(request);last=-Infinity;
    if(actor&&render&&!document.hidden)request=requestAnimationFrame(draw);
  }
  async function build(){
    const gl=surface.getContext('webgl',{alpha:true,premultipliedAlpha:true,antialias:false,depth:false,stencil:false});
    if(!gl)return;
    const prog=makeProgram(gl,`
      attribute vec2 uv; uniform vec2 angles; varying vec2 coord;
      vec2 rotateAt(vec2 p,vec2 pivot,float a){
        vec2 v=p-pivot;float s=sin(a),c=cos(a);
        return pivot+vec2(c*v.x-s*v.y,s*v.x+c*v.y);
      }
      void main(){
        vec2 p=uv*vec2(.75,1.);
        float chest=1.-smoothstep(.48,.96,uv.y);
        float head=1.-smoothstep(.25,.46,uv.y);
        vec2 posed=mix(p,rotateAt(p,vec2(.38,.33),angles.y),head);
        posed=mix(posed,rotateAt(posed,vec2(.38,.88),angles.x),chest);
        posed.y-=.14*angles.x*chest;
        gl_Position=vec4((posed/vec2(.75,1.)-.5)*vec2(1.94,-1.94),0.,1.);coord=uv;
      }`,`varying vec2 coord;uniform sampler2D sheet;uniform vec2 cell;
      void main(){gl_FragColor=texture2D(sheet,(coord+cell)/vec2(4.,2.));}`,['uv']);
    const uv=[],indices=[];
    for(let y=0;y<=12;y++)for(let x=0;x<=8;x++){
      const i=y*9+x;uv.push(x/8,y/12);
      if(x<8&&y<12)indices.push(i,i+1,i+9,i+1,i+10,i+9);
    }
    const vb=buffer(gl,gl.ARRAY_BUFFER,new Float32Array(uv));
    const ib=buffer(gl,gl.ELEMENT_ARRAY_BUFFER,new Uint16Array(indices));
    const tex=await texture(gl,url,false,true);
    gl.useProgram(prog);gl.uniform1i(gl.getUniformLocation(prog,'sheet'),0);
    const pose=gl.getUniformLocation(prog,'angles'),cell=gl.getUniformLocation(prog,'cell');
    render=(t,side,face)=>{
      gl.viewport(0,0,surface.width,surface.height);gl.clearColor(0,0,0,0);gl.clear(gl.COLOR_BUFFER_BIT);
      gl.bindBuffer(gl.ARRAY_BUFFER,vb);attribute(gl,0,2);gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER,ib);
      bindTexture(gl,0,tex);
      gl.uniform2f(pose,.012*Math.sin(t*1.8),.018*Math.sin(t*1.45+.6));
      gl.uniform2f(cell,side,face);gl.drawElements(gl.TRIANGLES,indices.length,gl.UNSIGNED_SHORT,0);
    };
    frame.style.backgroundImage='none';surface.hidden=false;refresh();
  }
  function show(who,face){
    actor=who;expression=face;frame.hidden=!who;
    if(!who){refresh();return;}
    cellUV=faces[who][face];
    frame.classList.toggle('right',who!=='hero');
    if(!render){
      surface.hidden=true;
      frame.style.backgroundImage='url("'+url+'")';
      frame.style.backgroundPosition=(cellUV[0]*100/3)+'% '+(cellUV[1]*100)+'%';
      if(!building)building=build().catch(()=>{ /* The atlas remains visible on a 2D-only device. */ });
    }
    refresh();
  }
  surface.addEventListener('webglcontextlost',e=>{e.preventDefault();render=null;show(actor,expression);});
  surface.addEventListener('webglcontextrestored',()=>{building=null;show(actor,expression);});
  document.addEventListener('visibilitychange',refresh);
  return {show};
}

function createDialogue(host, template, portraitAtlas){
  host.replaceChildren(template.content.cloneNode(true));
  const name=host.querySelector('.speaker'),text=host.querySelector('.words');
  const button=host.querySelector('.advance');
  const portrait=makePortraitPlayer(host.querySelector('.portrait-frame'),portraitAtlas);
  let next=null;
  button.addEventListener('click',()=>{const action=next;next=null;if(action)action();});
  return {
    show(line,action){
      name.textContent=line.speaker;text.textContent=line.text;
      button.textContent=line.action||'继续';next=action;host.hidden=false;
      portrait.show(line.actor,line.expression);button.focus({preventScroll:true});
    },
    hide(){next=null;host.hidden=true;portrait.show(null);}
  };
}
