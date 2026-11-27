// Faceted stone plates share a mesh. Both arms pivot at shoulder height during the wind-up.
function makeStoneGuardian(gl,wooden){
  const vertices=[];
  function plate(x,y,z,width,height,depth,color,arm=0,glow=0){
    const rings=[];
    const outline=[[-.36,-.5],[.36,-.5],[.5,-.32],[.5,.32],[.36,.5],[-.36,.5],[-.5,.32],[-.5,-.32]];
    for(const [level,scale] of [[-.5,.78],[-.34,1],[.33,.98],[.5,.75]])
      rings.push(outline.map(([u,v])=>[x+u*scale*width,y+level*height,z+v*scale*depth]));
    function triangle(a,b,c){
      const u=b.map((n,i)=>n-a[i]),v=c.map((n,i)=>n-a[i]);
      const normal=[u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0]];
      const shade=.71+.29*(-.45*normal[0]+.74*normal[1]-.50*normal[2])/Math.hypot(...normal);
      for(const p of [a,b,c])vertices.push(...p,...color.map(value=>value*shade),arm,glow);
    }
    for(let n=0;n<8;n++){
      const m=(n+1)%8;
      for(let j=0;j<3;j++){
        triangle(rings[j][n],rings[j+1][n],rings[j][m]);
        triangle(rings[j][m],rings[j+1][n],rings[j+1][m]);
      }
      triangle([x,y-height/2,z],rings[0][n],rings[0][m]);
      triangle([x,y+height/2,z],rings[3][m],rings[3][n]);
    }
  }
  if(wooden){
    const oak=[.58,.39,.20],binding=[.32,.23,.15];
    plate(0,1,0,.53,1.8,.51,oak);plate(0,2.06,0,.45,.40,.43,oak);
    plate(0,.16,0,1.15,.23,.83,binding);plate(0,1.48,0,1.43,.19,.22,oak);
    plate(0,1.3,0,.55,.17,.52,binding);
  }else{
    const slate=[.38,.45,.43],granite=[.54,.59,.52],seam=[.24,.32,.31],rune=[.75,.60,.31];
    for(const side of [-1,1]){
      plate(side*.33,.19,-.10,.57,.34,.82,slate);
      plate(side*.33,.55,0,.46,.54,.49,granite);
      plate(side*.84,1.74,.02,.65,.55,.67,granite,1);
      plate(side*1.04,1.32,.00,.46,.59,.52,slate,1);
      plate(side*1.10,.92,-.10,.63,.54,.73,granite,1);
    }
    plate(0,.90,0,.89,.38,.62,seam);
    plate(0,1.39,.04,1.2,.83,.72,slate);
    plate(0,1.62,-.24,1.02,.43,.37,granite);
    plate(0,1.49,-.444,.12,.26,.06,rune,0,1);
    plate(0,2.02,-.03,.65,.51,.53,granite);
    for(const side of [-1,1])plate(side*.14,2.05,-.30,.17,.068,.045,rune,0,1);
  }
  const mesh=buffer(gl,gl.ARRAY_BUFFER,new Float32Array(vertices));
  const program=makeProgram(gl,`
    attribute vec3 pos,paint;attribute vec2 joint;
    uniform vec3 origin;uniform mat4 clipFromWorld;uniform float angle,pulse,bob;
    varying vec3 color;
    void main(){
      vec3 p=pos;
      if(joint.x>.5){
        vec2 v=p.yz-vec2(1.79,.02);
        p.yz=vec2(v.x*cos(angle)-v.y*sin(angle),v.x*sin(angle)+v.y*cos(angle))+vec2(1.79,.02);
      }
      p+=origin;p.y+=bob;
      gl_Position=clipFromWorld*vec4(p,1.);
      color=paint+joint.y*pulse*vec3(.38,.24,.08);
    }`,`varying vec3 color;void main(){gl_FragColor=vec4(color,1.);}`,['pos','paint','joint']);
  const loc=Object.fromEntries(['origin','clipFromWorld','angle','pulse','bob'].map(k=>[k,gl.getUniformLocation(program,k)]));
  return (view,position,time,strikeAt,windup)=>{
    const charge=smooth(0,windup,time-strikeAt+windup);
    gl.useProgram(program);gl.bindBuffer(gl.ARRAY_BUFFER,mesh);
    attribute(gl,0,3,32,0);attribute(gl,1,3,32,12);attribute(gl,2,2,32,24);
    gl.uniformMatrix4fv(loc.clipFromWorld,false,view);gl.uniform3fv(loc.origin,position);
    gl.uniform1f(loc.angle,charge*2.72+.07*Math.sin(time*2.4));
    gl.uniform1f(loc.pulse,charge);gl.uniform1f(loc.bob,.026*Math.sin(time*2.4)-.10*charge);
    gl.drawArrays(gl.TRIANGLES,0,vertices.length/8);
    gl.disableVertexAttribArray(2);
  };
}
