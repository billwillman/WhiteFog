const add=(a,b)=>a.map((v,i)=>v+b[i]), sub=(a,b)=>a.map((v,i)=>v-b[i])
const mul=(a,s)=>a.map(v=>v*s)
const unit=a=>mul(a,1/Math.hypot(...a))
const cross=(a,b)=>[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]]
function route(z){
  const a=z*Math.PI/96
  return [11*Math.sin(a+.25)+5*Math.sin(2*a-.6),1.6+3.4*Math.sin(a-.6)+1.4*Math.sin(2*a+.9),z]
}
function random(seed){
  let x=(Math.imul(seed,747796405)+2891336453)>>>0
  x=Math.imul((x>>>((x>>>28)+4))^x,277803737)>>>0
  return ((x^(x>>>22))&65535)/65535
}
function ramp(a,b,t){const x=Math.max(0,Math.min(1,(t-a)/(b-a)));return x*x*(3-2*x)}
function pose(t,wide,sites,flashSeed){
  const z=(t%24)*8, p=route(z), f=unit(sub(route(z+1),route(z-1)))
  const right=unit(cross([0,1,0],f)),u=cross(f,right),bank=13*Math.sin(z*.056-.5)*Math.PI/180
  const wing=add(mul(right,Math.cos(bank)),mul(u,Math.sin(bank))),top=cross(f,wing)
  const model=new Float32Array([...mul(wing,.52),0,...mul(top,.52),0,...mul(f,.52),0,...p,1])
  const eye=add(route(z-(wide?22:16)),[0,wide?5.5:3.3,0])
  const bay=Math.pow(.5+.5*Math.cos(z*Math.PI/96-.26),6)
  eye[0]+=4.8*bay;eye[1]+=3.4*bay
  const forward=unit(sub(add(route(z+12),[0,1.6,0]),eye)),viewRight=unit(cross([0,1,0],forward))
  const event=Math.floor(t/3.2),start=event*3.2,seed=flashSeed+7*event,age=t-start-.48-.29*random(seed+1)
  const slice=Math.floor((start*8+11+7*random(seed+2)-192*Math.floor(t/24))/6+.5)
  const wrapped=((slice%32)+32)%32
  const spot=sites[8*wrapped+Math.min(7,Math.floor(8*random(seed+3)))]
  const pulse=[spot.x,spot.y,spot.z+6*(slice-wrapped),ramp(0,.32,age)*(1-ramp(.32,1.26,age))]
  return {model,eye,forward,viewRight,viewUp:cross(forward,viewRight),pulse}
}
module.exports={pose}
