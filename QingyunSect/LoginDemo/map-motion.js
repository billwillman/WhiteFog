function makeMapMotion(art) {
  const canvas = document.getElementById('river-motion');
  const ctx = canvas.getContext('2d');
  const reduced = matchMedia('(prefers-reduced-motion: reduce)');
  const fieldWidth = 1080, fieldHeight = 1647, cropTop = 150;

  function loadImage(src) {
    const image = new Image();
    image.src = src;
    return image;
  }
  const water = loadImage(art.water);
  const cells = art.current;
  const current = loadImage(cells.src);
  const flag = document.getElementById('bridge-flag');
  const flagCtx = flag.getContext('2d');
  const flagArt = loadImage(art.parts['bridge-flag'].src);
  const flagMargin = 12;
  for (const [name, part] of Object.entries(art.parts)) {
    const element = document.getElementById(name);
    let [x,y,w,h] = part.box;
    if (element === flag) {
      x -= flagMargin;
      w += 2*flagMargin;
      h += 4;
      flag.width = 2*w;
      flag.height = 2*h;
    } else {
      element.src = part.src;
    }
    Object.assign(element.style, {
      left: 100*x/fieldWidth+'%', top: 100*y/fieldHeight+'%',
      width: 100*w/fieldWidth+'%', height: 100*h/fieldHeight+'%',
      transformOrigin: part.pivot.map(v => 100*v+'%').join(' ')
    });
  }
  const mooredBoat = document.getElementById('boat');
  const sailboat = document.getElementById('sailboat');
  const river = new Path2D('M 915 666 Q 977 687 918 709 T 917 745 Q 1078 777 1013 817 Q 949 860 821 887 C 718 916 581 940 474 988 C 399 1017 350 1047 382 1074 S 525 1106 608 1133 C 759 1164 942 1210 979 1262 S 1000 1363 846 1393 Q 690 1410 655 1462 T 477 1546 Q 390 1567 348 1606');
  // Separate inner and outer brush strokes follow the curvature of the bank.
  const bend = new Path2D('M 552 962 C 458 985 369 1021 353 1052 C 340 1081 413 1098 483 1114 S 585 1140 631 1148');
  const bank = new Path2D('M 970 839 Q 887 882 796 896 M 565 973 Q 453 1008 412 1037 T 435 1080 Q 479 1094 522 1098 M 787 1190 Q 905 1220 944 1260');
  const falls = new Path2D('M 346 817 Q 366 829 368 850 M 360 834 Q 384 858 391 879 M 299 1610 Q 319 1640 313 1685 M 320 1625 Q 351 1682 347 1720 M 345 1682 Q 366 1710 378 1748');
  const mist = new Path2D('M 42 467 Q 98 447 155 460 T 275 450 M 435 603 Q 478 590 535 598 T 633 580');

  function ink(path, color, width, dash, phase) {
    ctx.strokeStyle = color;
    ctx.lineWidth = width;
    ctx.setLineDash(dash);
    ctx.lineDashOffset = phase;
    ctx.stroke(path);
  }
  function drawWater(t) {
    if (current.complete && current.naturalWidth) {
      const i = Math.floor(t*16)%cells.frames;
      ctx.drawImage(current, (i%cells.columns)*cells.width, Math.floor(i/cells.columns)*cells.height,
        cells.width,cells.height,...cells.box);
    }
    if (!water.complete || !water.naturalWidth) return;
    ctx.lineCap = 'round';
    for (let i=0;i<4;i++) {
      ctx.save();
      ctx.translate(i*3-5, i*4.4-7);
      const speed = 20+i*3;
      ink(river, i%2?'rgba(235,249,216,.70)':'rgba(61,137,140,.60)',
        2.5+i%2, [12+i*2,57+9*i,4,24], -t*speed+19*i);
      ink(bend, 'rgba(247,248,218,.75)', 2.8, [21,47+i*6,6,30], -t*speed+23*i);
      ctx.restore();
    }
    ink(bank, 'rgba(239,249,221,.80)', 2.9, [16,34,5,57], -t*20);
    ctx.save();
    ctx.globalCompositeOperation = 'destination-in';
    ctx.drawImage(water,0,cropTop,fieldWidth,fieldHeight);
    ctx.restore();
    ink(falls, 'rgba(255,252,224,.78)', 2.8, [7,14], -t*33);
    ctx.save();
    ctx.translate(7*Math.sin(t*.25),0);
    ink(mist,'rgba(247,243,222,.15)',6,[],0);
    ctx.restore();
  }
  function placeBoat(element,key,dx,dy,angle) {
    const [, ,w,h] = art.parts[key].box;
    element.style.transform = 'translate('+100*dx/w+'%,'+100*dy/h+'%) rotate('+angle+'deg)';
  }
  function reflection(image,key,dx,dy,waterline,t,opacity) {
    if (!image.complete || !image.naturalWidth) return;
    const [x,,w,h] = art.parts[key].box;
    ctx.save();
    for (let row=0;row<6;row++) {
      ctx.globalAlpha = opacity*(1-row/6);
      const srcY = Math.max(0,h-4-row*4);
      const shift = 2.2*Math.sin(t*3+row*1.35);
      ctx.drawImage(image,0,srcY,w,3,x+dx+shift,waterline+dy+row*2.2,w,2.4);
    }
    ctx.restore();
  }
  function drawBoats(t) {
    const bob = Math.sin(t*2.0);
    const dx = 7.8*Math.sin(t*1.27), dy = 3.8*bob;
    const angle = 2.4*Math.sin(t*1.75+.35);
    placeBoat(mooredBoat,'boat',dx,dy,angle);
    reflection(mooredBoat,'boat',dx,dy,1144,t,.37);
    // Tie the bow back to the fixed mooring, including the bow's roll about the stern.
    ctx.strokeStyle = 'rgba(71,76,54,.72)';
    ctx.lineWidth = 1.9;
    ctx.setLineDash([]);
    ctx.beginPath();
    ctx.moveTo(647,1136);
    ctx.quadraticCurveTo(657+dx*.35,1132+dy*.75,669+dx,1119+dy-132*Math.sin(angle*Math.PI/180));
    ctx.stroke();

    // Emerge from behind the shoreline pine and leave beyond the right map edge.
    const travel = (t/26+.42)%1;
    const sailX = 845+286*travel;
    const sailY = 903-87*travel-3*Math.sin(travel*Math.PI)+1.6*Math.sin(t*2.1);
    const ox = sailX-1000, oy = sailY-866;
    const show = Math.min(1,travel*9);
    sailboat.style.opacity = show;
    placeBoat(sailboat,'sailboat',ox,oy,1.6*Math.sin(t*1.48));
    reflection(sailboat,'sailboat',ox,oy,873,t,show*.38);
    ctx.save();
    ctx.globalAlpha = show;
    for (let i=0;i<3;i++) {
      const age = (t*.54+i/3)%1;
      const tail = sailX-31-age*35, y = sailY+8+age*9;
      ctx.strokeStyle = 'rgba(235,248,219,'+(.64*(1-age))+')';
      ctx.lineWidth = 1.9;
      ctx.beginPath();ctx.moveTo(tail-8,y-1);ctx.quadraticCurveTo(tail,y+2,tail+5,y+4);ctx.stroke();
    }
    ctx.restore();
  }
  function drawFlag(t) {
    if (!flagArt.complete || !flagArt.naturalWidth) return;
    const [,,w,h] = art.parts['bridge-flag'].box;
    flagCtx.setTransform(2,0,0,2,0,0);
    flagCtx.clearRect(0,0,flag.width/2,flag.height/2);
    // The crossbar fixes the top hem. Gusts reach the free hem a little later.
    for (let y=0;y<h;y+=2) {
      const depth = Math.max(0,(y-4)/(h-5)), loose = depth*depth;
      const flutter = Math.sin(t*3.6-depth*3)+.33*Math.sin(t*6.1-depth*4.7);
      const shift = loose*(flutter*5.5-3.1);
      const extra = depth*(1.6+1.4*Math.sin(t*3.6-depth*2));
      const rows = Math.min(2,h-y);
      flagCtx.drawImage(flagArt,0,y,w,rows,flagMargin+shift-extra,y,w+extra,rows+.28);
    }
  }

  let visible = false, frame = 0, last = null, elapsed = 0;
  function paint(stamp) {
    frame = 0;
    if (!visible || document.hidden) return;
    if (last===null || stamp-last>=48) {
      if (last!==null && !reduced.matches) elapsed += Math.max(0,stamp-last)/1000;
      last = stamp;
      ctx.setTransform(1,0,0,1,0,0);
      ctx.clearRect(0,0,canvas.width,canvas.height);
      ctx.setTransform(canvas.width/fieldWidth,0,0,canvas.height/fieldHeight,0,-cropTop*canvas.height/fieldHeight);
      drawWater(elapsed);
      drawBoats(elapsed);
      drawFlag(elapsed);
    }
    if (!reduced.matches) frame = requestAnimationFrame(paint);
  }
  function setVisible(value) {
    visible = value;
    cancelAnimationFrame(frame);
    frame = 0;
    last = null;
    if (visible && !document.hidden) frame = requestAnimationFrame(paint);
  }
  for (const image of [flagArt,current,water,mooredBoat,sailboat]) {
    image.addEventListener('load',()=>{ if (visible && reduced.matches) setVisible(true); });
  }
  document.addEventListener('visibilitychange',()=>setVisible(visible));
  reduced.addEventListener?.('change',()=>setVisible(visible));
  return {setVisible};
}

