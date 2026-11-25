// The battle uses the same skinned mesh, UVs and sampled clip data as the downloadable GLB.
async function makeFlyingEnemy(gl,url) {
  const data=await (await fetch(url)).arrayBuffer();
  const jsonLength=new DataView(data).getUint32(12,true);
  const model=JSON.parse(new TextDecoder().decode(new Uint8Array(data,20,jsonLength)));
  const start=28+jsonLength;
  function access(id) {
    const a=model.accessors[id], v=model.bufferViews[a.bufferView];
    const type={5126:Float32Array,5123:Uint16Array}[a.componentType];
    const size={SCALAR:1,VEC2:2,VEC3:3,VEC4:4}[a.type];
    return new type(data,start+(v.byteOffset||0)+(a.byteOffset||0),a.count*size);
  }
  const prim=model.meshes[0].primitives[0],attrs=prim.attributes;
  const pos=access(attrs.POSITION),uv=access(attrs.TEXCOORD_0),joint=access(attrs.JOINTS_0),weight=access(attrs.WEIGHTS_0);
  const vertices=new Float32Array(pos.length/3*9);
  for(let i=0;i<pos.length/3;i++){
    vertices.set([pos[i*3],pos[i*3+1],pos[i*3+2],uv[i*2],uv[i*2+1],joint[i*4],joint[i*4+1],weight[i*4],weight[i*4+1]],i*9);
  }
  const indices=access(prim.indices),vb=buffer(gl,gl.ARRAY_BUFFER,vertices),ib=buffer(gl,gl.ELEMENT_ARRAY_BUFFER,indices);
  const base=model.materials[prim.material].pbrMetallicRoughness.baseColorTexture.index;
  const image=model.images[model.textures[base].source],view=model.bufferViews[image.bufferView];
  const blob=URL.createObjectURL(new Blob([new Uint8Array(data,start+view.byteOffset,view.byteLength)],{type:image.mimeType}));
  let paint;
  try{paint=await texture(gl,blob,false,false);}finally{URL.revokeObjectURL(blob);}
  const rows=model.skins[0].joints.length*3;
  const program=makeProgram(gl,`
    attribute vec3 position; attribute vec2 texcoord,joints,weights;
    uniform vec4 palette[`+rows+`];uniform float aspect;uniform vec2 anchor;varying vec2 uv;
    vec3 skin(float joint,vec4 p){int k=int(joint)*3;return vec3(dot(palette[k],p),dot(palette[k+1],p),dot(palette[k+2],p));}
    void main(){
      vec4 p=vec4(position,1.);vec3 q=weights.x*skin(joints.x,p)+weights.y*skin(joints.y,p)-vec3(0.,2.10,0.);
      float angle=`+BATTLE_PITCH.toFixed(9)+`,height=q.y*cos(angle)-q.z*sin(angle);
      float depth=6.4-q.y*sin(angle)-q.z*cos(angle);
      gl_Position=vec4(depth*anchor+1.16*vec2(q.x/aspect,height),depth-.35,depth);uv=texcoord;
    }`,`uniform sampler2D paint;varying vec2 uv;void main(){vec4 c=texture2D(paint,uv);if(c.a<.45)discard;gl_FragColor=vec4(c.rgb,1.);}`,
    ['position','texcoord','joints','weights']);
  const paletteLocation=gl.getUniformLocation(program,'palette[0]'),aspectLocation=gl.getUniformLocation(program,'aspect'),anchorLocation=gl.getUniformLocation(program,'anchor');
  const clips=model.animations.filter(a=>a.extras?.skinRows!==undefined).map(a=>({...a.extras,data:access(a.extras.skinRows)}));
  const fly=clips[0],hover=clips[1],size=rows*4,pose=new Float32Array(size);
  function sample(clip,t) {
    const cursor=(t*clip.rate)%(clip.frames-1),first=Math.floor(cursor);
    return {data:clip.data,a:first*size,b:(first+1)*size,u:cursor-first};
  }
  return function draw(aspect,time,at){
    const a=sample(fly,time),b=sample(hover,time),blend=smooth(1.25,1.7,time%4)*(1-smooth(3.05,3.55,time%4));
    for(let i=0;i<size;i++){
      const x=a.data[a.a+i]+(a.data[a.b+i]-a.data[a.a+i])*a.u;
      const y=b.data[b.a+i]+(b.data[b.b+i]-b.data[b.a+i])*b.u;
      pose[i]=x+(y-x)*blend;
    }
    gl.enable(gl.DEPTH_TEST);gl.useProgram(program);
    gl.uniform4fv(paletteLocation,pose);gl.uniform1f(aspectLocation,aspect);gl.uniform2f(anchorLocation,at[0],at[1]);
    gl.bindBuffer(gl.ARRAY_BUFFER,vb);attribute(gl,0,3,36,0);attribute(gl,1,2,36,12);attribute(gl,2,2,36,20);attribute(gl,3,2,36,28);
    gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER,ib);bindTexture(gl,0,paint);
    gl.drawElements(gl.TRIANGLES,indices.length,gl.UNSIGNED_SHORT,0);
  };
}

async function makeSwordEffects(gl,art,boat) {
  const definitions=Object.fromEntries(await Promise.all(Object.entries(art).map(async ([key,fx])=>[key,{...fx,texture:await texture(gl,fx.image,true,false)}])));
  const hull=await texture(gl,boat.src,true,false);
  const quad=buffer(gl,gl.ARRAY_BUFFER,new Float32Array([-1,-1,1,-1,-1,1,1,1]));
  const program=makeProgram(gl,`attribute vec2 point;uniform vec4 rect,tile;varying vec2 uv;
    void main(){gl_Position=vec4(rect.xy+point*rect.zw,0.,1.);uv=tile.xy+(point*.5+.5)*tile.zw;}`,
    `uniform sampler2D image;varying vec2 uv;void main(){gl_FragColor=texture2D(image,uv);}`,['point']);
  const rect=gl.getUniformLocation(program,'rect'),tile=gl.getUniformLocation(program,'tile');
  function stamp(fx,age,center,scale,aspect){
    const index=Math.floor(age*fx.rate);
    if(index<0||index>=fx.count)return;
    const col=index%fx.cols,row=Math.floor(index/fx.cols), inset=.5/(fx.cell*fx.cols),edge=.5/(fx.cell*fx.rows);
    gl.uniform4f(tile,col/fx.cols+inset,1-(row+1)/fx.rows+edge,1/fx.cols-2*inset,1/fx.rows-2*edge);
    // Capture camera was 384 square. Preserve the original emitter pivot after atlas trimming.
    const [l,top,r,bottom]=fx.captureBounds, unit=scale/384;
    gl.uniform4f(rect,center[0]+((l+r)/2-192)*unit/aspect,center[1]+(192-(top+bottom)/2)*unit,
      (r-l)*.5*unit/aspect,(bottom-top)*.5*unit);
    bindTexture(gl,0,fx.texture);gl.drawArrays(gl.TRIANGLE_STRIP,0,4);
  }
  return (aspect,t,s)=>{
    gl.useProgram(program);gl.bindBuffer(gl.ARRAY_BUFFER,quad);attribute(gl,0,2);
    for(let i=1;i<4;i++)gl.disableVertexAttribArray(i);
    gl.disable(gl.DEPTH_TEST);gl.enable(gl.BLEND);gl.blendFuncSeparate(gl.SRC_ALPHA,gl.ONE_MINUS_SRC_ALPHA,gl.ONE,gl.ONE_MINUS_SRC_ALPHA);
    if(s.plan===swordEncounters.ferry){
      const half=.22;
      gl.uniform4f(rect,s.boat[0],s.boat[1],half,half*aspect*boat.box[3]/boat.box[2]);
      gl.uniform4f(tile,0,0,1,1);bindTexture(gl,0,hull);gl.drawArrays(gl.TRIANGLE_STRIP,0,4);
      stamp(definitions.ward,s.wardAge,s.boat,2.7,aspect);
    }
    if(s.clip&&s.phase<s.arrival+1.35){
      const prefix=s.move.fx,age=s.phase-s.release,cast=definitions[prefix+'cast'];
      const castAge=age+Math.min(s.release,cast.count/cast.rate*.70);
      const hand=swordHand(s,aspect,t,false);
      stamp(cast,castAge,hand,prefix==='rain'?1.28:1.70,aspect);
      if(age>=0&&age<s.move.travel){
        const from=swordHand(s,aspect,t-age,true),u=age/s.move.travel;
        const at=prefix==='rain'?s.enemy:[from[0]*(1-u)+s.enemy[0]*u,from[1]*(1-u)+s.enemy[1]*u];
        stamp(definitions[prefix+'trail'],age,at,prefix==='rain'?1.33:1.68,aspect);
      }
      stamp(definitions[prefix+'hit'],s.phase-s.arrival,s.enemy,2.65,aspect);
    }
    gl.disable(gl.BLEND);gl.enable(gl.DEPTH_TEST);
  };
}
