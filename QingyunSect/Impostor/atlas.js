// Yaw is measured from +Z toward +X. Pixel rectangles start at the top left.
function impostorFrame(atlas,yawDegrees) {
  const count=atlas.frames.length;
  const turn=((yawDegrees%360)+360)%360;
  return atlas.frames[Math.round(turn*count/360)%count];
}
