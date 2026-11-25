const BATTLE_PITCH=38*Math.PI/180;
// The build reads FogOfWar.shader and its mobile material directly.
async function makeBattleClouds(gl,art){
  const noise=await texture(gl,art.noise,false,false);
  gl.bindTexture(gl.TEXTURE_2D,noise);
  for(const axis of [gl.TEXTURE_WRAP_S,gl.TEXTURE_WRAP_T])gl.texParameteri(gl.TEXTURE_2D,axis,gl.REPEAT);
  gl.generateMipmap(gl.TEXTURE_2D);
  gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MIN_FILTER,gl.LINEAR_MIPMAP_LINEAR);
  const cap=await texture(gl,art.cap,true,false);
  const common='uniform sampler2D clouds;uniform float time,aspect; varying vec3 world; \n'+art.fog.common+`
    // MobileFogMask.Stamp: one half-visible region, radius four as in FogPreview.
    vec3 fogBands(vec2 pos){
      float d=distance(pos,vec2(0.,20.));
      float mask=(1.-smoothstep(3.,23.,d))+(1.-smoothstep(60.,120.,d));
      mask=floor(mask*85.+.5)/85.;
      return smoothstep(vec3(0.),vec3(1.),vec3(mask)-vec3(0.,1.,2.));
    }
  `;
  const vertex=common+`
    attribute vec2 point;
    uniform vec2 tilt;
    void main(){
      vec3 eye=vec3(0.,14.,0.),forward=vec3(0.,-tilt.x,tilt.y),up=vec3(0.,tilt.y,tilt.x);
      vec3 ray=forward+.39*(point.y*up+vec3(point.x*aspect,0.,0.));
      world=eye+ray*(-eye.y/ray.y);
      vec3 bands=fogBands(world.xz);
      float offset=mix(mix(_OffsetOutside,_OffsetEdge,bands.x),_OffsetHalf,bands.y);
      world.y=mix(-_DisplacementHeight,_DisplacementHeight,VERTEX_DENSITY)-offset;
      vec3 rel=world-eye;float d=dot(rel,forward);
      gl_Position=vec4(rel.x/(.39*aspect),dot(rel,up)/.39,0.,d);
    }`;
  const fragment=common+`
    uniform sampler2D matcap;uniform vec4 flash;uniform vec2 tilt;
    void main(){
      float cloud=CloudValue(world.xz,false);
      vec3 cameraRay=normalize(world-vec3(0.,14.,0.)),view=-cameraRay;
      vec3 bands=fogBands(world.xz);
      `+art.fog.light+`
      float reveal=mix(mix(0.,_AlphaOuter-cloud*_CloudsOpacityOuter,bands.x),
                       _AlphaHalf-cloud*_CloudsOpacityHalf,bands.y);
      float alpha=clamp(pow(abs(1.-reveal),_AlphaPower)*_AlphaMultiply,0.,1.);
      rgb=mix(darkLit*(1.+_EmissionStrength),rgb,alpha);
      vec2 delta=(world.xz-flash.xy)*vec2(.16,.19);
      float halo=max(0.,(exp2(-1.65*dot(delta,delta))-.045)/.955);
      if(halo*flash.z>0.){
        vec2 uv=world.xz*.01*_BaseScale*_BaseCloudsScale+_BaseCloudsSpeed.xy*time*_BaseSpeed;
        vec2 dx=vec2(2./1024.,0.);
        vec2 slope=vec2(texture2D(clouds,uv+dx).r-texture2D(clouds,uv-dx).r,
                        texture2D(clouds,uv+dx.yx).r-texture2D(clouds,uv-dx.yx).r)*4.;
        vec3 normal=normalize(vec3(-slope.x,1.,-slope.y));
        float cap=texture2D(matcap,vec2(normal.x,dot(normal,vec3(0.,tilt.y,tilt.x)))*.475+.5).r;
        rgb+=mix(vec3(.12,.29,.38),vec3(.28,.16,.37),flash.w)*flash.z*halo*(.35+.83*cap);
      }
      vec3 srgb=mix(12.92*rgb,1.055*pow(max(rgb,vec3(0.)),vec3(1./2.4))-.055,step(vec3(.0031308),rgb));
      gl_FragColor=vec4(srgb,1.);
    }`;
  const vertexDensity=gl.getParameter(gl.MAX_VERTEX_TEXTURE_IMAGE_UNITS)>0?'CloudValue(world.xz,true)':'0.5';
  const program=makeProgram(gl,vertex.replace('VERTEX_DENSITY',vertexDensity),fragment,['point']);
  const loc=name=>gl.getUniformLocation(program,name), t=loc('time'), ar=loc('aspect'), f=loc('flash');
  gl.useProgram(program);gl.uniform1i(loc('clouds'),0);gl.uniform1i(loc('matcap'),1);
  gl.uniform2f(loc('tilt'),Math.sin(BATTLE_PITCH),Math.cos(BATTLE_PITCH));
  const points=[],indices=[],cols=32,rows=48;
  for(let y=0;y<=rows;y++)for(let x=0;x<=cols;x++){
    points.push((2*x/cols-1)*1.23,(2*y/rows-1)*1.35);
    if(x<cols&&y<rows){const i=y*(cols+1)+x;indices.push(i,i+1,i+cols+1,i+1,i+cols+2,i+cols+1);}
  }
  const vb=buffer(gl,gl.ARRAY_BUFFER,new Float32Array(points)),ib=buffer(gl,gl.ELEMENT_ARRAY_BUFFER,new Uint16Array(indices));
  let cycle=-1,side=1,px=0,py=0,hue=0;
  return function draw(time,aspect){
    const interval=Math.floor(time/3.6);
    if(cycle!==interval){cycle=interval;side=-side;
      px=side*aspect*(3.8+Math.random()*2.2);py=14+Math.random()*9;hue=Math.random();}
    const phase=time%3.6-.52,pulse=smooth(0,.34,phase)*(1-smooth(.36,1.55,phase));
    gl.disable(gl.DEPTH_TEST);gl.disable(gl.BLEND);gl.useProgram(program);
    gl.uniform1f(t,time);gl.uniform1f(ar,aspect);gl.uniform4f(f,px,py,pulse,hue);
    bindTexture(gl,0,noise);bindTexture(gl,1,cap);
    gl.bindBuffer(gl.ARRAY_BUFFER,vb);attribute(gl,0,2);
    for(let i=1;i<=3;i++)gl.disableVertexAttribArray(i);
    gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER,ib);gl.drawElements(gl.TRIANGLES,indices.length,gl.UNSIGNED_SHORT,0);
  };
}

// Two cloud edges cropped from the supplied ink painting, drawn behind the actors.
async function makePaintedCloudEdges(gl,art){
  const plate=await texture(gl,art.edgeClouds,true,false);
  const program=makeProgram(gl,`
    attribute vec3 corner; uniform float time,aspect,gust;varying vec2 uv;varying float opacity;
    void main(){
      float farSide=corner.z;
      vec2 center=mix(vec2(-.86,-.33),vec2(.91,.38),farSide);
      float width=mix(.57,.47,farSide)+.48*gust*farSide;
      center+=vec2(-.28,-.68)*gust*farSide;
      center+=vec2(.027*sin(time*.24+2.*farSide),.014*sin(time*.19+farSide))*(1.-.5*farSide);
      gl_Position=vec4(center+corner.xy*vec2(width,width*aspect*.69),0.,1.);
      vec2 local=corner.xy*.5+.5;
      uv=vec2(.5*(farSide+mix(.002,.998,local.x)),local.y);
      opacity=mix(.58,.44,farSide);
    }`,`uniform sampler2D plate;varying vec2 uv;varying float opacity;
       void main(){vec4 c=texture2D(plate,uv);gl_FragColor=vec4(c.rgb,c.a*opacity);}`,['corner']);
  const verts=[];
  for(const side of [0,1])for(const [x,y] of [[-1,-1],[1,-1],[-1,1],[-1,1],[1,-1],[1,1]])verts.push(x,y,side);
  const vb=buffer(gl,gl.ARRAY_BUFFER,new Float32Array(verts)),t=gl.getUniformLocation(program,'time'),a=gl.getUniformLocation(program,'aspect');
  const gustLocation=gl.getUniformLocation(program,'gust');
  return function draw(time,aspect,gust){
    gl.useProgram(program);gl.uniform1f(gustLocation,gust);gl.bindBuffer(gl.ARRAY_BUFFER,vb);attribute(gl,0,3);bindTexture(gl,0,plate);
    gl.uniform1f(t,time);gl.uniform1f(a,aspect);gl.enable(gl.BLEND);
    gl.blendFuncSeparate(gl.SRC_ALPHA,gl.ONE_MINUS_SRC_ALPHA,gl.ONE,gl.ONE_MINUS_SRC_ALPHA);
    gl.drawArrays(gl.TRIANGLES,0,12);gl.disable(gl.BLEND);
  };
}

async function makeEnemyImpostor(gl,enemy){
  if(!enemy)return null;
  const image=await texture(gl,enemy.image,true,false);
  const program=makeProgram(gl,`
    attribute vec2 point; uniform float aspect,ratio;uniform vec2 anchor;uniform vec4 tile;
    varying vec2 uv;
    void main(){
      vec2 p=point*2.-1.;
      gl_Position=vec4(anchor+vec2(.50*p.x,.50*aspect*ratio*p.y),0.,1.);
      uv=tile.xy+tile.zw*point;
    }`,`uniform sampler2D sprite; varying vec2 uv;
    void main(){gl_FragColor=texture2D(sprite,uv);}`,['point']);
  const ar=gl.getUniformLocation(program,'aspect');
  gl.useProgram(program);
  const atlas=enemy.atlas,frame=impostorFrame(atlas,0);
  gl.uniform4f(gl.getUniformLocation(program,'tile'),(frame.x+.5)/atlas.width,1-(frame.y+frame.h-.5)/atlas.height,(frame.w-1)/atlas.width,(frame.h-1)/atlas.height);
  gl.uniform1f(gl.getUniformLocation(program,'ratio'),frame.h/frame.w);
  const vb=buffer(gl,gl.ARRAY_BUFFER,new Float32Array([0,0,1,0,0,1,1,1]));
  const anchor=gl.getUniformLocation(program,'anchor');
  return function draw(aspect,time,at){
    gl.useProgram(program);gl.uniform2fv(anchor,at);gl.bindBuffer(gl.ARRAY_BUFFER,vb);attribute(gl,0,2);bindTexture(gl,0,image);
    gl.uniform1f(ar,aspect);gl.enable(gl.BLEND);gl.blendFunc(gl.SRC_ALPHA,gl.ONE_MINUS_SRC_ALPHA);
    gl.drawArrays(gl.TRIANGLE_STRIP,0,4);gl.disable(gl.BLEND);
  };
}
