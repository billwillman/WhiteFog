// The lower row holds spare copies. The upper row belongs to the ongoing battle.
function makeBattleBoard(bar,art,onChange){
  const shelf=document.getElementById('battle-books'),list=shelf.querySelector('div'),notice=shelf.querySelector('output');
  const name=id=>trainingRules.arts[id]||skillRules.books.get(id)?.name||'';
  let bag,arts=[],selected=null,pointer=null,ghost=null,busy=false,session=0;
  function readCell(target){
    const slot=target?.closest('#battle-bar li');
    if(slot && bar.contains(slot)){
      const index=Array.from(bar.children).indexOf(slot);
      return {slot:index,id:bag.slots[index]||0};
    }
    const book=target?.closest('[data-shelf-book]');
    if(book && shelf.contains(book))return {id:JSON.parse(book.dataset.shelfBook)};
    return target&&shelf.contains(target)?{}:null;
  }
  const hint='下排武学拖到上排出招，拖到另一本同品质武学上合成。';
  function select(cell){
    selected=cell;
    for(const el of [...bar.children,...list.children]){
      const item=readCell(el);
      el.classList.toggle('picked',!!cell&&cell.slot===item.slot&&cell.id===item.id);
    }
    notice.textContent=cell?name(cell.id)+' · 选择上排位置或下排合成材料':hint;
  }
  function draw(){
    const items=Object.keys(bag.books).map(Number).flatMap(id=>{
      const count=skillInventory.spare(bag,id);
      return count>1?[[id,1],[id,count-1]]:[[id,count]];
    })
      .concat(arts.filter(id=>!bag.slots.includes(id)).map(id=>[id,1]));
    list.replaceChildren();
    for(const [id,count] of items){
      if(count<=0)continue;
      const button=document.createElement('button');button.type='button';button.dataset.shelfBook=JSON.stringify(id);
      button.setAttribute('aria-label',name(id)+' × '+count);button.title=name(id);
      const url=art.ui[id];
      if(url){const img=new Image();img.src=url;img.alt='';img.draggable=false;button.append(img);}
      else button.append(name(id));
      if(count>1){const quantity=document.createElement('b');quantity.textContent=count;button.append(quantity);}
      list.append(button);
    }
    select(null);
  }
  async function move(from,to){
    if(busy||!bag||!from?.id||!to)return;
    const token=session;busy=true;
    try{
      let state,text;
      if(to.slot!==undefined){
        state=await skillInventory.arrange(from.id,to.slot,arts,from);text=name(from.id)+'已在上排生效';
      }else if(from.slot!==undefined){
        state=await skillInventory.arrange(0,from.slot,arts,from);text=name(from.id)+'已移回下排';
      }else if(to.id){
        const ids=[from.id,to.id],recipe=skillInventory.check(bag,ids);
        state=await skillInventory.craft(ids);
        text='合成获得'+recipe.success_item.map(([,id])=>name(id)).join('、');
      }else return;
      if(token!==session)return;
      bag=state;onChange(bag);draw();notice.textContent=text;
    }catch(err){if(token===session){select(null);notice.textContent=err.message;}}
    finally{if(token===session)busy=false;}
  }
  function tap(cell){
    if(!cell||busy)return;
    if(selected)move(selected,cell);
    else if(cell.id)select(cell);
  }
  function endDrag(){
    ghost?.remove();ghost=null;pointer=null;
  }
  for(const region of [bar,shelf]){
    region.addEventListener('pointerdown',e=>{
      if(!bag||busy||e.button!==0||pointer)return;
      const from=readCell(e.target);
      if(from?.id){pointer={from,id:e.pointerId,x:e.clientX,y:e.clientY};region.setPointerCapture(e.pointerId);}
    });
    region.addEventListener('pointermove',e=>{
      if(!pointer||pointer.id!==e.pointerId)return;
      if(!ghost&&Math.hypot(e.clientX-pointer.x,e.clientY-pointer.y)>6){
        const id=pointer.from.id;
        ghost=document.createElement('span');ghost.className='battle-drag-book';ghost.setAttribute('aria-hidden','true');
        if(art.ui[id]){const img=new Image();img.src=art.ui[id];ghost.append(img);}
        else ghost.textContent=name(id);
        document.body.append(ghost);
      }
      if(ghost){ghost.style.left=e.clientX+'px';ghost.style.top=e.clientY+'px';}
    });
    region.addEventListener('pointerup',e=>{
      if(!bag||busy)return;
      if(pointer && pointer.id!==e.pointerId)return;
      const to=readCell(document.elementFromPoint(e.clientX,e.clientY));
      const start=pointer;endDrag();
      if(start&&Math.hypot(e.clientX-start.x,e.clientY-start.y)>6)move(start.from,to);
      else tap(to);
    });
    region.addEventListener('pointercancel',endDrag);
    region.addEventListener('click',e=>{if(bag&&e.detail===0)tap(readCell(e.target));});
    region.addEventListener('dragstart',e=>{if(bag)e.preventDefault();});
  }
  function close(){session++;bag=null;endDrag();selected=null;busy=false;shelf.hidden=true;}
  return {open(state,learned){close();bag=state;arts=learned;shelf.hidden=false;draw();},close};
}
