const smooth = (a,b,x) => {
  const t = Math.min(1, Math.max(0,(x-a)/(b-a)));
  return t*t*(3-2*t);
};

function makeProgram(gl, vs, fs, attributes, fragmentPreamble='') {
  const program = gl.createProgram();
  const shaders = [[gl.VERTEX_SHADER,vs],[gl.FRAGMENT_SHADER,fs]].map(([type,source]) => {
    const shader = gl.createShader(type);
    gl.shaderSource(shader,(type===gl.FRAGMENT_SHADER?fragmentPreamble:'')+'precision highp float;\n'+source);
    gl.compileShader(shader);
    if (!gl.getShaderParameter(shader,gl.COMPILE_STATUS)) throw Error(gl.getShaderInfoLog(shader));
    gl.attachShader(program,shader);
    return shader;
  });
  attributes.forEach((name,i)=>gl.bindAttribLocation(program,i,name));
  gl.linkProgram(program);
  shaders.forEach(s=>gl.deleteShader(s));
  if (!gl.getProgramParameter(program,gl.LINK_STATUS)) throw Error(gl.getProgramInfoLog(program));
  return program;
}
function buffer(gl,target,data,usage=gl.STATIC_DRAW) {
  const result=gl.createBuffer();
  gl.bindBuffer(target,result); gl.bufferData(target,data,usage);
  return result;
}
function attribute(gl,index,size,stride=0,offset=0) {
  gl.enableVertexAttribArray(index);
  gl.vertexAttribPointer(index,size,gl.FLOAT,false,stride,offset);
}
async function texture(gl,url,flip,premultiply) {
  const img=new Image();
  await new Promise((resolve,reject)=>{img.onload=resolve;img.onerror=()=>reject(Error('纹理载入失败'));img.src=url;});
  const result=gl.createTexture();
  gl.bindTexture(gl.TEXTURE_2D,result);
  gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL,flip);
  gl.pixelStorei(gl.UNPACK_PREMULTIPLY_ALPHA_WEBGL,premultiply);
  gl.texImage2D(gl.TEXTURE_2D,0,gl.RGBA,gl.RGBA,gl.UNSIGNED_BYTE,img);
  for (const param of [gl.TEXTURE_WRAP_S,gl.TEXTURE_WRAP_T]) gl.texParameteri(gl.TEXTURE_2D,param,gl.CLAMP_TO_EDGE);
  for (const param of [gl.TEXTURE_MIN_FILTER,gl.TEXTURE_MAG_FILTER]) gl.texParameteri(gl.TEXTURE_2D,param,gl.LINEAR);
  return result;
}
function bindTexture(gl,unit,tex) {
  gl.activeTexture(gl.TEXTURE0+unit); gl.bindTexture(gl.TEXTURE_2D,tex);
}
