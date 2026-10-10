/* Approved for native v1.8. Keep the reference comparison in the long-term Demo. */
(() => {
  const look=CLOUD_DATA.families.claude.styles.chibi.looks.sports;
  const desc={file:'art/claude-sports-stand-v2.png',demoFile:'art/claude-sports-stand-v2.png',
    cells:[{x:0,y:0,width:1254,height:1254,footX:636,footY:1186,visibleHeight:1117,bounds:[231,69,1046,1186],scale:1}],
    reference:990,frames:[0],frameMs:[5000],facing:'right'};
  globalThis.ClaudeIdleReview={previous:{...look.walk,frames:[7]},candidate:desc};
  look.stand=desc;
})();
