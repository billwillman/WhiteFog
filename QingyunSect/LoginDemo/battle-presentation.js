// Column-major orthographic transform, shared by opaque meshes, effects and their HUD anchors.
function makeBattleCamera(degrees,offset){
  const pitch=degrees*Math.PI/180,c=Math.cos(pitch),s=Math.sin(pitch);
  const matrix=new Float32Array(16),depthScale=.04;
  matrix[6]=-s*depthScale;matrix[10]=c*depthScale;matrix[14]=-1.05*matrix[6];matrix[15]=1;
  function resize(aspect,span){
    matrix[0]=1/(aspect*span);matrix[5]=c/span;matrix[9]=s/span;matrix[13]=offset-1.05*matrix[5];
  }
  function project(p){
    return [p[0]*matrix[0],p[1]*matrix[5]+p[2]*matrix[9]+matrix[13]];
  }
  function depth(p){return p[1]*matrix[6]+p[2]*matrix[10]+matrix[14];}
  return {matrix,resize,project,depth,depthScale,towardEye:[0,s,-c]};
}

// The workbench applies OriginalSkillPreviewLight after actors and particles, before the HUD.
function makeBattlePresentation(gl){
  const vertex='attribute vec2 point;varying vec2 uv;void main(){uv=point;gl_Position=vec4(point*2.-1.,0.,1.);}';
  function pass(fragment,names){
    const program=makeProgram(gl,vertex,'varying vec2 uv;uniform sampler2D src,halo;uniform vec2 texel;'+fragment,['point']);
    gl.useProgram(program);
    gl.uniform1i(gl.getUniformLocation(program,'halo'),1);
    return {program,...Object.fromEntries(names.map(n=>[n,gl.getUniformLocation(program,n)]))};
  }
  const box='vec3 box(){vec2 d=texel*.5;return .25*(texture2D(src,uv+d).rgb+texture2D(src,uv-d).rgb+texture2D(src,uv+vec2(d.x,-d.y)).rgb+texture2D(src,uv+vec2(-d.x,d.y)).rgb);}';
  const linear=pass('void main(){vec3 c=texture2D(src,uv).rgb;gl_FragColor=vec4(mix(c/12.92,pow((c+.055)/1.055,vec3(2.4)),step(.04045,c)),1.);}',[]);
  const extract=pass(box+'void main(){vec3 c=box();float p=max(c.r,max(c.g,c.b)),t=.7874123,k=t*.5,s=clamp(p-t+k,0.,2.*k);gl_FragColor=vec4(c*max(s*s/(4.*k+.00001),p-t)/max(p,.00001),1.);}', ['texel']);
  const reduce=pass(box+'void main(){gl_FragColor=vec4(box(),1.);}', ['texel']);
  const expand=pass('void main(){gl_FragColor=vec4(mix(texture2D(src,uv).rgb,texture2D(halo,uv).rgb,.7),1.);}',[]);
  const finish=pass(`void main(){vec3 h=texture2D(halo,uv).rgb;h=mix(vec3(dot(h,vec3(.2126,.7152,.0722))),h,1.05);
    vec3 x=max(vec3(0.),(texture2D(src,uv).rgb+.6*h)*exp2(.1));
    vec3 c=clamp(x*(2.51*x+.03)/(x*(2.43*x+.59)+.14),0.,1.);
    gl_FragColor=vec4(mix(12.92*c,1.055*pow(c,vec3(1./2.4))-.055,step(.0031308,c)),1.);}`,[]);
  const quad=buffer(gl,gl.ARRAY_BUFFER,new Float32Array([0,0,1,0,0,1,1,1]));
  const half=gl.getExtension('OES_texture_half_float'),filter=gl.getExtension('OES_texture_half_float_linear');
  gl.getExtension('EXT_color_buffer_half_float');
  const canReadDepth=!gl.getExtension('EXT_frag_depth')&&gl.getExtension('WEBGL_depth_texture');
  let hdr=!!(half&&filter),scene,working,down=[],up=[],targets=[],depth,depthTexture,particleFramebuffer;
  function target(w,h,linearColor,withDepth){
    const tex=gl.createTexture(),fb=gl.createFramebuffer();
    gl.bindTexture(gl.TEXTURE_2D,tex);
    gl.texImage2D(gl.TEXTURE_2D,0,gl.RGBA,w,h,0,gl.RGBA,linearColor&&hdr?half.HALF_FLOAT_OES:gl.UNSIGNED_BYTE,null);
    for(const name of [gl.TEXTURE_MIN_FILTER,gl.TEXTURE_MAG_FILTER])gl.texParameteri(gl.TEXTURE_2D,name,gl.LINEAR);
    for(const name of [gl.TEXTURE_WRAP_S,gl.TEXTURE_WRAP_T])gl.texParameteri(gl.TEXTURE_2D,name,gl.CLAMP_TO_EDGE);
    gl.bindFramebuffer(gl.FRAMEBUFFER,fb);gl.framebufferTexture2D(gl.FRAMEBUFFER,gl.COLOR_ATTACHMENT0,gl.TEXTURE_2D,tex,0);
    if(withDepth){
      if(depthTexture)gl.framebufferTexture2D(gl.FRAMEBUFFER,gl.DEPTH_ATTACHMENT,gl.TEXTURE_2D,depthTexture,0);
      else gl.framebufferRenderbuffer(gl.FRAMEBUFFER,gl.DEPTH_ATTACHMENT,gl.RENDERBUFFER,depth);
    }
    const t={tex,fb,w,h};targets.push(t);
    if(gl.checkFramebufferStatus(gl.FRAMEBUFFER)!==gl.FRAMEBUFFER_COMPLETE)throw Error('战斗画面缓冲区不可用');
    return t;
  }
  function dispose(){
    for(const t of targets){gl.deleteTexture(t.tex);gl.deleteFramebuffer(t.fb);}
    targets=[];
    gl.deleteRenderbuffer(depth||null);gl.deleteTexture(depthTexture||null);gl.deleteFramebuffer(particleFramebuffer||null);
    depth=depthTexture=particleFramebuffer=null;
  }
  function resize(w,h){
    dispose();
    if(canReadDepth){
      depthTexture=gl.createTexture();gl.bindTexture(gl.TEXTURE_2D,depthTexture);
      gl.texImage2D(gl.TEXTURE_2D,0,gl.DEPTH_COMPONENT,w,h,0,gl.DEPTH_COMPONENT,gl.UNSIGNED_SHORT,null);
      for(const param of [gl.TEXTURE_MIN_FILTER,gl.TEXTURE_MAG_FILTER])gl.texParameteri(gl.TEXTURE_2D,param,gl.NEAREST);
      for(const param of [gl.TEXTURE_WRAP_S,gl.TEXTURE_WRAP_T])gl.texParameteri(gl.TEXTURE_2D,param,gl.CLAMP_TO_EDGE);
      // Keep a colour-only framebuffer so sampling opaque depth never reads an active attachment.
      working=target(w,h,false,false);
      particleFramebuffer=gl.createFramebuffer();
      gl.bindFramebuffer(gl.FRAMEBUFFER,particleFramebuffer);
      gl.framebufferTexture2D(gl.FRAMEBUFFER,gl.COLOR_ATTACHMENT0,gl.TEXTURE_2D,working.tex,0);
      gl.bindFramebuffer(gl.FRAMEBUFFER,working.fb);
      gl.framebufferTexture2D(gl.FRAMEBUFFER,gl.DEPTH_ATTACHMENT,gl.TEXTURE_2D,depthTexture,0);
      if(gl.checkFramebufferStatus(gl.FRAMEBUFFER)!==gl.FRAMEBUFFER_COMPLETE)dispose();
    }
    if(!depthTexture){
      depth=gl.createRenderbuffer();gl.bindRenderbuffer(gl.RENDERBUFFER,depth);
      gl.renderbufferStorage(gl.RENDERBUFFER,gl.DEPTH_COMPONENT16,w,h);
      working=target(w,h,false,true);
    }
    // The Unity workbench clamps scene colour in its 8-bit target before bloom.
    scene=target(w,h,false,true);
    down=[];up=[];
    for(let i=0;i<5;i++){w=Math.max(1,w>>1);h=Math.max(1,h>>1);down.push(target(w,h,true,false));if(i<4)up.push(target(w,h,true,false));}
  }
  function begin(w,h){
    if(!scene||scene.w!==w||scene.h!==h){
      try{resize(w,h);}catch(err){if(!hdr)throw err;hdr=false;resize(w,h);}
    }
    gl.bindFramebuffer(gl.FRAMEBUFFER,scene.fb);gl.viewport(0,0,w,h);
  }
  function blit(p,source,dest,halo){
    gl.bindFramebuffer(gl.FRAMEBUFFER,dest?.fb||null);gl.viewport(0,0,dest?.w||scene.w,dest?.h||scene.h);
    gl.useProgram(p.program);gl.bindBuffer(gl.ARRAY_BUFFER,quad);attribute(gl,0,2);gl.disableVertexAttribArray(1);
    bindTexture(gl,0,source.tex);if(halo)bindTexture(gl,1,halo.tex);
    if(p.texel)gl.uniform2f(p.texel,1/source.w,1/source.h);
    gl.drawArrays(gl.TRIANGLE_STRIP,0,4);
  }
  function particles(){
    gl.disable(gl.BLEND);gl.disable(gl.DEPTH_TEST);gl.depthMask(false);blit(linear,scene,working);
  }
  function present(){
    gl.disable(gl.BLEND);gl.disable(gl.DEPTH_TEST);
    down.forEach((t,i)=>blit(i?reduce:extract,i?down[i-1]:working,t));
    let glow=down[4];for(let i=3;i>=0;i--){blit(expand,down[i],up[i],glow);glow=up[i];}
    blit(finish,working,null,glow);gl.depthMask(true);gl.activeTexture(gl.TEXTURE0);
  }
  function transparentTarget(ward){
    if(depthTexture)gl.bindFramebuffer(gl.FRAMEBUFFER,ward?working.fb:particleFramebuffer);
  }
  return {begin,particles,present,transparentTarget,get depthTexture(){return depthTexture;}};
}

function makeParticleRenderer(gl,presentation){
  const fragmentDepth=gl.getExtension('EXT_frag_depth');
  const canReadDepth=!fragmentDepth&&presentation&&gl.getExtension('WEBGL_depth_texture');
  const program=makeProgram(gl,`attribute vec2 point;uniform vec4 rect;uniform vec3 depth;varying vec2 uv;
    void main(){gl_Position=vec4(rect.xy+(point-.5)*rect.zw,2.*depth.x-1.,1.);uv=vec2(point.x,1.-point.y);}`,
    `uniform sampler2D coverage,radiance;uniform vec3 depth;uniform vec4 tile;uniform float inner,coveragePass;varying vec2 uv;
    ${canReadDepth?'uniform sampler2D opaqueDepth;uniform vec2 pixelSize;':''}
    void main(){
      vec2 coord=uv*2.-1.,a=abs(coord);
      vec2 packedUV=min(a/inner,1.)*.75+max(a-inner,0.)*(.25/(1.-inner));
      vec2 sampleUV=tile.xy+(.5+.5*sign(coord)*packedUV)*tile.zw;
      vec3 c=texture2D(radiance,sampleUV).rgb;c*=c;
      vec4 mask=texture2D(coverage,sampleUV);
      ${fragmentDepth||canReadDepth?'float z=mask.b>.5?clamp(depth.x+(mask.g/mask.b-.5)*depth.y,0.,1.):0.;':''}
      ${fragmentDepth?'gl_FragDepthEXT=z;':''}
      ${canReadDepth?'if(coveragePass==2. && z>texture2D(opaqueDepth,gl_FragCoord.xy*pixelSize).r)discard;':''}
      ${fragmentDepth?'':'if((coveragePass==1. && mask.b<=.5)||(coveragePass==-1. && mask.b>.5))discard;'}
      gl_FragColor=vec4(c/max(vec3(.0001),1.-c),mix(mask.a,mask.r,depth.z));}`,['point'],
    fragmentDepth?'#extension GL_EXT_frag_depth : require\n':'');
  const loc=Object.fromEntries(['rect','tile','depth','inner','coveragePass','opaqueDepth','pixelSize'].map(n=>[n,gl.getUniformLocation(program,n)]));
  gl.useProgram(program);gl.uniform1i(gl.getUniformLocation(program,'radiance'),1);
  if(canReadDepth)gl.uniform1i(loc.opaqueDepth,2);
  const quad=buffer(gl,gl.ARRAY_BUFFER,new Float32Array([0,0,1,0,0,1,1,1]));
  return (fx,i,x,y,w,h,clipDepth,clipRange)=>{
    gl.useProgram(program);gl.bindBuffer(gl.ARRAY_BUFFER,quad);attribute(gl,0,2);
    bindTexture(gl,0,fx.coverage);bindTexture(gl,1,fx.radiance);
    const rows=Math.ceil(fx.count/fx.cols),inset=.5/fx.cell;
    gl.uniform4f(loc.tile,(i%fx.cols+inset)/fx.cols,(Math.floor(i/fx.cols)+inset)/rows,(1-2*inset)/fx.cols,(1-2*inset)/rows);
    gl.uniform4f(loc.rect,x,y,w,h);
    gl.uniform1f(loc.inner,fx.innerFraction||.75);
    // Same orthographic depth transform as the actor vertices. The scene and particle FBOs share the actor depth buffer.
    gl.uniform3f(loc.depth,.5+.5*clipDepth,.5*clipRange,fx.depthCoverage?1:0);
    gl.enable(gl.BLEND);gl.blendFuncSeparate(gl.ONE,gl.ONE_MINUS_SRC_ALPHA,gl.ZERO,gl.ONE);
    const readDepth=canReadDepth&&presentation.depthTexture;
    if(canReadDepth){
      bindTexture(gl,2,readDepth||fx.coverage);
      gl.uniform2f(loc.pixelSize,1/gl.drawingBufferWidth,1/gl.drawingBufferHeight);
    }
    if(fx.depthCoverage&&!readDepth){gl.enable(gl.DEPTH_TEST);gl.depthFunc(gl.LEQUAL);}
    else gl.disable(gl.DEPTH_TEST);
    // Without per-pixel depth, depth-tested materials use the effect's anchor plane.
    // A mixed atlas also contains authored Always pixels, which keep their own pass.
    if(!fragmentDepth)gl.uniform1f(loc.coveragePass,fx.depthCoverage?(readDepth?2:1):0);
    gl.drawArrays(gl.TRIANGLE_STRIP,0,4);
    if(!fragmentDepth&&!readDepth&&fx.depthCoverage){
      gl.disable(gl.DEPTH_TEST);gl.uniform1f(loc.coveragePass,-1);gl.drawArrays(gl.TRIANGLE_STRIP,0,4);
    }
  };
}

function makeWardRenderer(gl){
  const program=makeProgram(gl,`attribute vec3 unit;uniform mat4 clipFromWorld;uniform vec3 center;
    varying vec3 normal;void main(){
      vec3 radii=vec3(.67,1.06,.67);normal=unit/radii;
      gl_Position=clipFromWorld*vec4(center+unit*radii,1.);
    }`,`uniform vec3 towardEye;uniform float opacity;varying vec3 normal;
    void main(){float facing=dot(normalize(normal),towardEye);if(facing<0.)discard;
      float rim=1.-smoothstep(0.,.32,facing);
      gl_FragColor=vec4(.51,.86,.84,opacity*mix(.11,.82,rim));
    }`,['unit']);
  const vertices=[],indices=[],bands=16,sides=32;
  for(let j=0;j<=bands;j++){
    const lat=Math.PI*j/bands;
    for(let i=0;i<=sides;i++){
      const az=2*Math.PI*i/sides;
      vertices.push(Math.sin(lat)*Math.cos(az),Math.cos(lat),Math.sin(lat)*Math.sin(az));
      if(j<bands&&i<sides){const k=j*(sides+1)+i;indices.push(k,k+1,k+sides+1,k+1,k+sides+2,k+sides+1);}
    }
  }
  const mesh=buffer(gl,gl.ARRAY_BUFFER,new Float32Array(vertices)),triangles=buffer(gl,gl.ELEMENT_ARRAY_BUFFER,new Uint16Array(indices));
  const loc=Object.fromEntries(['clipFromWorld','center','towardEye','opacity'].map(k=>[k,gl.getUniformLocation(program,k)]));
  return (camera,center,opacity)=>{
    gl.useProgram(program);gl.bindBuffer(gl.ARRAY_BUFFER,mesh);attribute(gl,0,3);
    gl.uniformMatrix4fv(loc.clipFromWorld,false,camera.matrix);gl.uniform3fv(loc.center,center);
    gl.uniform3fv(loc.towardEye,camera.towardEye);gl.uniform1f(loc.opacity,opacity);
    gl.enable(gl.DEPTH_TEST);gl.depthFunc(gl.LEQUAL);gl.enable(gl.BLEND);
    gl.blendFuncSeparate(gl.SRC_ALPHA,gl.ONE_MINUS_SRC_ALPHA,gl.ZERO,gl.ONE);
    gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER,triangles);gl.drawElements(gl.TRIANGLES,indices.length,gl.UNSIGNED_SHORT,0);
  };
}
