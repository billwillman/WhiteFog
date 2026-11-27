// Operates in Unity world metres and seconds. Rendering observes events and never settles hits.
(function (scope) {
'use strict';
const axes = [[1,0,0],[0,1,0],[0,0,1]];
const add = (a,b) => a.map((v,i)=>v+b[i]);
const sub = (a,b) => a.map((v,i)=>v-b[i]);
const scale = (a,k) => a.map(v=>v*k);
const dot = (a,b) => a.reduce((s,v,i)=>s+v*b[i],0);
const length = a => Math.hypot(...a);
const unit = a => scale(a,1/(length(a)||1));
const cross = (a,b) => [a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]];
const xyz = v => [v.x,v.y,v.z];
function basis(forward) {
  const right = unit(cross(Math.abs(forward[1])>.999 ? axes[2] : axes[1],forward));
  return [right,unit(cross(forward,right)),forward];
}
const rotate = (v,b) => add(add(scale(b[0],v[0]),scale(b[1],v[1])),scale(b[2],v[2]));
const localPoint = (actor,v) => add(actor.position,rotate(v,basis(actor.forward)));
function anchor(actor,bone,time) {
  if (!bone) return actor.position;
  return actor.anchor?.(bone,time) || localPoint(actor,[0,bone==='vfx_p_hit'?1.1:0,0]);
}
const live = actor => actor && actor.stats.chp>0 && actor.active!==false;

// Swept separating-axis test for a translating oriented hit box vs a live hurt box.
function contactFraction(origin, delta, a, aHalf, center, b, bHalf) {
  let lo=0, hi=1;
  const separation=sub(origin,center);
  for (const axis of [...a,...b,...a.flatMap(i=>b.map(j=>cross(i,j)))]) {
    if (dot(axis,axis)<1e-10) continue;
    const extent=a.reduce((s,dir,i)=>s+Math.abs(dot(dir,axis))*aHalf[i]+Math.abs(dot(b[i],axis))*bHalf[i],0);
    const start=dot(separation,axis), speed=dot(delta,axis);
    if (Math.abs(speed)<1e-9) { if (Math.abs(start)>extent) return null; continue; }
    const t0=(-extent-start)/speed, t1=(extent-start)/speed;
    lo=Math.max(lo,Math.min(t0,t1)); hi=Math.min(hi,Math.max(t0,t1));
    if(lo>hi) return null;
  }
  return lo;
}
const math = {
  Add:(a,b)=>a+b, Substract:(a,b)=>a-b, Multiply:(a,b)=>a*b, Divide:(a,b)=>b?a/b:0,
  Min:Math.min,Max:Math.max,Power:Math.pow,Sqrt:Math.sqrt,OneMinus:a=>1-a,DivRem:(a,b)=>a%b,
  Floor:Math.floor,Ceil:Math.ceil,Round:a=>Math.abs(a%1)===.5?2*Math.round(a/2):Math.round(a),
  Abs:Math.abs,Clamp:(a,b,c)=>Math.max(b,Math.min(c,a)),Lerp:(a,b,c)=>a+(b-a)*Math.max(0,Math.min(1,c))
};
function create(pkg, actors, notify=()=>{}) {
  if(pkg.version!==1) throw Error('不支持的技能包版本 '+pkg.version);
  const graphs=new Map(pkg.graphs.map(g=>[g.key,{...g,nodes:new Map(g.nodes.map(n=>[n.id,n]))}]));
  const main=graphs.get(pkg.entry), queue=[], shots=[], statuses=new Map();
  let now=0, castNo=0, order=0;
  const emit=(kind,c,n,detail={})=>notify({kind,cast:c.id,node:n?.id,elapsed:now-c.start,time:now,...detail});
  const find=(g,id)=>{const n=g.nodes.get(id);if(!n)throw Error('缺少节点 '+id);return n;};
  const links=(n,i)=>n.inputs[i]?.links||[];
  function nextTarget(contact) {
    const candidate=contact?.next;
    return live(candidate)&&!contact.flight.visited.has(candidate)?candidate:null;
  }
  const entity=(g,id,c)=>[actors[0],c.caster,c.target,null,c.instance,nextTarget(c.contact)][find(g,id).type];
  function value(g,id,c) {
    const n=find(g,id);
    if(n.op==='Math'){
      const fn=math[n.key];if(!fn)throw Error('不支持数学节点 '+n.key);
      return Math.fround(fn(...n.inputs.filter(p=>p.links.length).map(p=>value(g,p.links[0],c))));
    }
    if(n.type===0)return n.value;
    if(n.type===2)return c.vars[n.key]??n.value;
    const owner=entity(g,links(n,0)[0],c);
    const result=(owner===c.caster?c.snap:owner.stats)[n.key];
    if(!Number.isFinite(result)) throw Error('缺少属性 '+n.key);
    return result;
  }
  function condition(g,id,c) {
    const n=find(g,id);
    if(n.op==='Trigger')return links(n,0).every(k=>condition(g,k,c));
    if(n.op==='EntityExist'){
      const actor=entity(g,links(n,0)[0],c);
      return n.ints[0]===2?!!actor&&actor.active!==false:n.ints[0]===1?!actor:!!actor;
    }
    if(n.op!=='AngleDifference')throw Error('未支持条件 '+n.op);
    const a=entity(g,links(n,0)[0],c), b=entity(g,links(n,1)[0],c);
    if(!a||!b)return false;
    const lhs=scale(a.forward,n.ints[0]===3?-1:1);
    const rhs=n.ints[0]<2?scale(b.forward,n.ints[0]===0?-1:1):sub(b.position,a.position);
    if(n.flags[0])rhs[1]=lhs[1];
    const deg=length(rhs)?Math.acos(Math.min(1,Math.max(-1,dot(unit(lhs),unit(rhs)))))*180/Math.PI:0;
    const limit=value(g,links(n,2)[0],c);
    return [deg>limit,Math.abs(deg-limit)<1,deg<limit][n.ints[1]];
  }
  function later(time,run) {queue.push({time,run,order:order++});queue.sort((a,b)=>a.time-b.time||a.order-b.order);}
  function schedule(g,ids,c,detached=false) {
    for(const id of ids) {
      const n=find(g,id), step=Math.max(.01,n.interval);
      const count=n.type===0?1:n.type===1?n.count:1+Math.floor(n.duration/step);
      const decision=n.op==='RedirectProjectile'?c.contact:null;
      if(decision)decision.pending+=count;
      for(let i=0;i<count;i++)later(now+n.at+i*step,()=>{
        try {
          if((!detached&&c.token.interrupted)||!n.inputs.filter(p=>p.type==='ConditionIn').every(p=>p.links.every(k=>condition(g,k,c))))return;
          execute(g,n,c);
        } finally {
          if(decision && --decision.pending===0 && decision.flight.contact===decision)finishShot(decision.flight);
        }
      });
    }
  }
  function formula(key,c){const g=graphs.get(key);return value(g,g.output,c);}
  function settle(g,n,c){
    const target=entity(g,links(n,2)[0],c); if(!live(target))return;
    const level=value(g,links(n,3)[0],c), row=n.levels.find(r=>r.level===level);
    if(!row)throw Error('缺少技能等级 '+level);
    const vars={...c.vars,'hit.index':n.segment,'hit.share':1};
    for(const f of row.fields)vars['level.'+f.key]=f.value;
    let damage=0,shield=0;
    for(const s of n.stages){
      vars['hit.value']=vars['hit.raw']=damage;
      switch(s.kind){
        case 'BaseDamage': damage=vars['hit.total']=Math.max(0,formula(s.formula,{...c,target,vars}));break;
        case 'SegmentShare': vars['hit.share']=Math.fround(n.share/100); damage=Math.fround(damage*vars['hit.share']);break;
        case 'ModifyDamage': damage=formula(s.formula,{...c,target,vars});break;
        case 'Vulnerability': if(target.weakUntil>now)damage=Math.floor(damage*(1+target.weakPercent/100)+.5);break;
        case 'Shield': shield=target.shieldUntil>now?Math.min(target.shield,Math.max(0,damage)):0;damage-=shield;break;
        case 'ApplyHealth': {
          const before=target.stats.chp,amount=Math.max(0,damage);
          const loss=target.guardDamage?target.guardDamage(amount):amount;
          target.stats.chp=Math.fround(Math.max(0,before-loss));
          if(shield)target.shield-=shield;
          emit('hit',c,n,{sourceNode:c.sourceNode,projectileNode:c.projectileNode,target:target.id,segment:n.segment,loss:before-target.stats.chp,shield,hp:target.stats.chp});break;
        }
        default:throw Error('不支持的结算环节 '+s.kind);
      }
    }
  }
  function addStatus(g,n,c) {
    const target=entity(g,links(n,1)[0],c), def=pkg.statuses.find(s=>s.id===Math.floor(value(g,links(n,2)[0],c)));
    if(!def||!live(target))return;
    const key=target.id+'/'+def.key, expires=now+def.duration;
    const old=statuses.get(key);
    if(old){old.expires=expires;return;}
    const status={expires};statuses.set(key,status);
    emit('statusStart',c,n,{target:target.id,status:def.key});
    const child=graphs.get(def.graph), vars=Object.fromEntries(def.variables.map(v=>[v.uid,v.value]));vars.Layer=1;
    const context={...c,target,vars,status:def.key};
    function tick(){
      if(now>status.expires+1e-7||!live(target))return;
      schedule(child,find(child,child.root).outputs[0].links,context,true);
      later(now+def.interval,tick);
    }
    function expire(){
      if(status.expires>now){later(status.expires,expire);return;}
      statuses.delete(key);emit('statusEnd',c,n,{target:target.id,status:def.key});
    }
    later(now+def.interval,tick);later(expires,expire);
  }
  function launch(g,n,c){
    const caster=entity(g,links(n,1)[0],c), aimed=entity(g,links(n,2)[0],c);
    if(!live(caster)||!live(aimed))return;
    const start=add(anchor(caster,n.bone,now),rotate(xyz(n.from),basis(caster.forward)));
    const flight={g,n,c,caster,born:now,visited:new Set(),cursor:c.targets.indexOf(aimed),contact:null};
    startLeg(flight,start,aimed);
    shots.push(flight);emit('launch',c,n,{target:aimed.id,shot:flight});
  }
  function startLeg(f,start,aimed){
    const n=f.n,end=add(anchor(aimed,n.targetBone,now),rotate(xyz(n.to),basis(aimed.forward)));
    const line=sub(end,start),dir=length(line)?unit(line):f.caster.forward;
    const distance=length(line),speed=n.speed,accel=n.acceleration;
    if(speed<=0)throw Error('本地直线投射要求正初速');
    const reach=accel<0?Math.min(distance,-speed*speed/(2*accel)):distance;
    const timeAt=d=>d===0?0:2*d/(speed+Math.sqrt(Math.max(0,speed*speed+2*accel*d)));
    const at=t=>Math.min(reach,Math.max(0,(speed+.5*accel*t)*t));
    Object.assign(f,{aimed,start,dir,basis:basis(dir),legStart:now,reach,timeAt,at,endTime:now+timeAt(reach),contact:null});
    f.position=t=>f.contact?f.contact.point:add(start,scale(dir,at(Math.max(0,Math.min(t,f.endTime)-f.legStart))));
  }
  function finishShot(f){
    const index=shots.indexOf(f);
    if(index<0)return;
    const point=f.position(now);
    shots.splice(index,1);
    f.contact=null;
    emit('projectileEnd',f.c,f.n,{shot:f,point});
  }
  function redirect(g,n,c){
    const contact=c.contact;
    if(!contact||contact.flight.contact!==contact)return;
    const next=entity(g,links(n,1)[0],c);
    if(!live(next)||contact.flight.visited.has(next)||!c.targets.includes(next))return;
    const f=contact.flight;
    f.cursor=c.targets.indexOf(next);
    startLeg(f,contact.point,next);
    emit('redirect',f.c,f.n,{shot:f,point:contact.point,target:next.id});
  }
  function nextContact(f,until){
    const n=f.n;
    if(f.contact)return null;
    if(n.recipients===0&&!live(f.aimed))return {time:now};
    const lo=Math.max(now,f.legStart+n.open), hi=Math.min(until,f.endTime,n.window>0?f.legStart+n.open+n.window:Infinity);
    let best=null;
    if(hi>=lo && xyz(n.size).every(v=>v>0)){
      const p=add(f.position(lo),rotate(xyz(n.offset),f.basis)), delta=sub(f.position(hi),f.position(lo));
      for(const actor of actors){
        if(f.visited.has(actor)||!live(actor)||!(n.mask&(1<<actor.layer))|| (n.recipients===0?actor!==f.aimed:actor===f.caster||!n.tags.some(t=>actor.tags.includes(t))))continue;
        const fraction=contactFraction(p,delta,f.basis,scale(xyz(n.size),.5),localPoint(actor,actor.hurtCenter),basis(actor.forward),scale(actor.hurtSize,.5));
        if(fraction!==null){const time=Math.max(lo,f.legStart+f.timeAt(f.at(lo-f.legStart)+length(delta)*fraction));if(!best||time<best.time)best={time,actor};}
      }
    }
    return best||(until>=f.endTime?{time:f.endTime}:null);
  }
  function dispatchContact(g,n,c,recipient,contact=null) {
    const actions=n.outputs.filter(port=>port.type==='BranchOutAction').flatMap(port=>port.links);
    schedule(g,actions,{...c,target:recipient,sourceNode:n.id,contact,projectileNode:contact?n.id:null},true);
  }
  function playEffect(g,n,c) {
    const caster=entity(g,links(n,1)[0],c),target=entity(g,links(n,2)[0],c);
    const actor=n.onTarget?target:caster;
    if(!live(actor))return;
    const origin=anchor(actor,n.bone,now),facing=basis(actor.forward);
    emit('effect',c,n,{effect:n.fx,bone:n.bone,follow:n.follow,duration:n.value,actor,point:origin});
    if(!n.triggerHit)return;
    function test() {
      if(!live(target))return;
      const center=n.follow?anchor(actor,n.bone,now):origin;
      const overlap=contactFraction(center,[0,0,0],facing,scale(xyz(n.size),.5),
        localPoint(target,target.hurtCenter),basis(target.forward),scale(target.hurtSize,.5));
      emit(overlap===null?'miss':'contact',c,n,{point:center,target:target.id});
      if(overlap!==null)dispatchContact(g,n,c,target);
    }
    if(n.hitDelay>0)later(now+n.hitDelay,test);else test();
  }
  function execute(g,n,c){
    switch(n.op){
      case 'Reference': return;
      case 'Animation':emit('animation',c,n,{motion:n.motion,actor:entity(g,links(n,1)[0],c)});return;
      case 'Effect':playEffect(g,n,c);return;
      case 'RedirectProjectile':redirect(g,n,c);return;
      case 'Projectile':launch(g,n,c);return;
      case 'FormulaImpact':settle(g,n,c);return;
      case 'OverTimeEffect':addStatus(g,n,c);return;
      case 'Event':emit('signal',c,n,{name:n.strings[0]});return;
      case 'AttributeChange':{
        const target=entity(g,links(n,1)[0],c),amount=value(g,links(n,2)[0],c),key=n.strings[0],before=target.stats[key];
        const after=[amount,before+amount,before*amount,before-amount][n.ints[0]];
        target.stats[key]=Math.fround(key==='chp'?Math.max(0,after):after);
        emit('statusTick',c,n,{status:c.status,target:target.id,attribute:key,loss:before-target.stats[key],hp:target.stats.chp});return;
      }
      default:throw Error('未支持动作 '+n.op);
    }
  }
  function advance(end){
    if(!Number.isFinite(end)||end<now)throw Error('无效的战斗时钟');
    do{
      const horizon=Math.min(end,queue[0]?.time??Infinity);
      let first=null;
      for(const f of shots){const hit=nextContact(f,horizon);if(hit&&(!first||hit.time<first.time))first={...hit,f};}
      if(first){
        const {f,actor}=first;now=Math.min(horizon,Math.max(now,first.time));
        const point=f.position(now);
        emit(actor?'contact':'miss',f.c,f.n,{point,target:actor?.id,shot:f});
        if(!actor){finishShot(f);continue;}
        f.visited.add(actor);
        f.cursor=f.cursor<0?-1:f.cursor+1;
        const contact={flight:f,point,next:f.cursor<0?null:f.c.targets[f.cursor],pending:0};
        f.contact=contact;
        dispatchContact(f.g,f.n,f.c,actor,contact);
        if(!contact.pending||f.visited.size>=f.c.targets.length)finishShot(f);
      }else if(queue[0]&&queue[0].time<=end){const e=queue.shift();now=e.time;e.run();}
      else{now=end;break;}
    }while(true);
  }
  function cast(caster,target,level=1,additionalTargets=[]){
    if(!live(caster)||!live(target))return null;
    const targets=[target];
    const limit=Math.max(1,pkg.targetLimit||1);
    for(const candidate of additionalTargets){
      if(targets.length>=limit)break;
      if(candidate&&actors.includes(candidate)&&!targets.includes(candidate))targets.push(candidate);
    }
    const c={targets,id:++castNo,start:now,caster,target,instance:caster,token:{interrupted:false},
      snap:Object.freeze({...caster.stats}),vars:{'skill.level':level}};
    const root=find(main,main.root);
    if(!links(root,0).every(k=>condition(main,k,c)))return null;
    emit('cast',c,root,{target:target.id});schedule(main,root.outputs.flatMap(p=>p.links),c);advance(now);return c;
  }
  return {advance,cast,shots,get time(){return now;},interrupt(c){
    if(c){c.token.interrupted=true;emit('interrupt',c,null);}
  }};
}
scope.LocalSkillRuntime={create};
})(globalThis);
