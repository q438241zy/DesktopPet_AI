(function(root,factory){const value=factory();if(typeof module==='object'&&module.exports)module.exports=value;else root.CompanionPhoto=value;})(globalThis,function(){
  'use strict';
  function extent(desc){
    const cell=desc.cells[desc.frames[0]],b=cell.bounds||[0,cell.footY-cell.visibleHeight,cell.width,cell.footY],k=cell.scale/desc.reference;
    return {left:(b[0]-cell.footX)*k,right:(b[2]-cell.footX)*k,top:(b[1]-cell.footY)*k,bottom:(b[3]-cell.footY)*k};
  }
  function layout(descriptors,style){
    const count=descriptors.length;if(count<2||count>8)throw Error('请选择 2–8 位伙伴。');
    const width=count===2?1200:1800,height=count<=2?1400:count<=4?1350:1760;
    const area={x:55,y:55,width:width-110,height:height-300},rows=count>4?2:1,columns=Math.ceil(count/rows),gap=34,padding=36;
    const rowHeight=(area.height-padding*2-gap*(rows-1))/rows,slots=[],bounds=descriptors.map(extent);
    let nominalHeight=rows===1?(style==='chibi'?700:950):650;
    for(let i=0;i<count;i++){
      const row=Math.floor(i/columns),inRow=Math.min(columns,count-row*columns),column=i%columns;
      const cellWidth=(area.width-padding*2-gap*(inRow-1))/inRow;
      const slot={x:area.x+padding+column*(cellWidth+gap),y:area.y+padding+row*(rowHeight+gap),width:cellWidth,height:rowHeight};
      const b=bounds[i];nominalHeight=Math.min(nominalHeight,(cellWidth-10)/(b.right-b.left),(rowHeight-18)/(b.bottom-b.top));slots.push(slot);
    }
    // One shared scale for the group. Wide hair, skirts and tails reserve space.
    const placements=slots.map((slot,i)=>{const b=bounds[i],x=slot.x+slot.width/2-(b.left+b.right)*nominalHeight/2,floor=slot.y+slot.height-9-b.bottom*nominalHeight;return {x,floor,height:nominalHeight,slot,bounds:{left:x+b.left*nominalHeight,right:x+b.right*nominalHeight,top:floor+b.top*nominalHeight,bottom:floor+b.bottom*nominalHeight}};});
    return {width,height,area,rows,placements};
  }
  return {layout,extent};
});
