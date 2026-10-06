/* Vector gifts mirror the native Collectibles / ItemArt shapes. */
(function(root,factory){const value=factory();if(typeof module==='object'&&module.exports)module.exports=value;else root.FiveItems=value;})(globalThis,function(){
  const circle=(x,y,r,color,stroke='#80909e')=>`<circle cx="${x}" cy="${y}" r="${r}" fill="${color}" stroke="${stroke}"/>`;
  const path=(d,fill='none',stroke='#8994a5',width=1.2)=>`<path d="${d}" fill="${fill}" stroke="${stroke}" stroke-width="${width}"/>`;
  const art={
    baseball:circle(18,18,15,'#fffdf7')+path('M8 5C19 12 19 24 8 31M28 5C17 12 17 24 28 31','none','#d96266')+path('M9 8l4 2M12 13l4 2M12 21l4-2M9 28l4-2M26 8l-4 2M23 13l-4 2M23 21l-4-2M26 28l-4-2','none','#d96266',.8),
    basketball:circle(18,18,15,'#ef9853','#ab724f')+path('M3 18h30M18 3v30M7 7C20 10 20 26 7 29M29 7C16 10 16 26 29 29','none','#885a40'),
    football:circle(18,18,15,'#fafbfd')+path('M18 10l7 5-3 8h-8l-3-8ZM8 6l3 4-5 6-3-1M28 6l-3 4 5 6 3-1M9 29l5-3 5 7M29 27l-4-1-3 7','#61718a')+path('M18 10V3M25 15l6-4M22 23l5 6M14 23l-4 7M11 15l-6-4','none','#8493a6',.8),
    volleyball:circle(18,18,15,'#fffdf2')+path('M18 3C9 11 11 17 18 18C27 17 31 11 30 9M18 18C21 27 14 32 11 31M5 12C7 22 12 26 18 25M9 6C8 16 14 21 18 21M21 5C16 12 19 15 22 15M32 18C25 21 24 28 24 31','none','#cda162',1.5),
    tennis:circle(18,18,15,'#b8cf58','#8eab7c')+path('M7 7C22 4 14 32 29 28M4 13C11 17 8 29 17 33','none','#ffffeb',2.1),
    pingpong:circle(18,18,15,'#f7b05d','#ca9767')+path('M8 9Q11 5 16 8','none','#ffe2a5',3)+path('M27 23Q24 29 18 29','none','#d68c4d'),
    badminton:path('M4 5Q17 0 32 6L23 27 15 29Z','#fbfdff','#8fadc4')+path('M7 6l9 19M13 4l5 21M19 4l1 21M25 5l-3 21M8 13Q19 10 29 14M12 22h13','none','#aac7da',1)+path('M15 25h9v4Q21 35 17 32Z','#d8b98b','#8c7966'),
    rugby:path('M3 20C6 2 26 0 33 16C29 34 10 36 3 20Z','#bc754a','#987254')+path('M8 9Q15 18 10 28M27 7Q20 20 28 26','none','#fff3dd',2)+path('M13 21l11-8M15 16l4 4M19 13l4 4','none','#fff4e0',1.7),
    golf:circle(18,18,15,'#f8fafc')+[[10,10],[17,7],[24,10],[8,18],[16,15],[25,17],[12,26],[20,25],[27,24]].map(([x,y])=>circle(x,y,1.2,'#c9d4df','none')).join(''),
    bowling:circle(18,18,15,'#7c82bc','#7c82ac')+path('M7 23Q12 28 19 27','none','#b0b7e2',3)+[[18,9],[24,12],[18,17]].map(([x,y])=>circle(x,y,2.5,'#465571','none')).join(''),
    bread:path('M7 29V15C1 11 5 3 12 5C16 1 23 2 25 5C33 5 34 12 29 15V29Q18 33 7 29Z','#d79856','#bd8d60')+path('M10 27V14C6 11 9 6 13 8C17 5 22 6 24 8C29 8 30 12 26 15V27Q17 29 10 27Z','#ffe1a4','none'),
    rice:circle(18,17,12,'#fffdf8','none')+circle(12,13,6,'#fffdf8','none')+circle(21,11,7,'#fffdf8','none')+path('M4 18h28Q30 30 23 31H13Q5 28 4 18Z','#9cc8e4','#7ca8bd')+path('M10 22Q18 28 26 22','none','#f7fcff',2),
    apple:path('M18 12C1 2 0 24 13 32Q18 29 22 32C35 27 36 4 20 11Z','#e88487','#bc858b')+path('M18 12l2-7','none','#8b6747',2)+path('M20 6Q25 0 30 5Q25 11 20 6Z','#8eb58c','none')+path('M10 13Q6 17 8 23','none','#ffced0',2.5),
    banana:path('M7 8Q7 26 27 20l5-7Q35 34 17 32Q1 28 3 11Z','#f5cf65','#bbaa78')+path('M6 10 5 5 9 4 11 9Z','#927044','none')+path('M8 20Q15 32 27 24','none','#e7b744'),
    cookie:circle(18,18,15,'#eac58a','#bda584')+[[10,10],[22,8],[27,18],[20,26],[8,22],[16,17]].map(([x,y])=>circle(x,y,2,'#906245','none')).join(''),
    milk:path('M9 9l4-6h13l4 7v22H9Z','#f9fdff','#7fa9c2')+path('M9 9h14v23H9Z','#c7e3f4','none')+path('M23 9l3-6 4 7M13 3l10 6','none','#7fa9c2')+path('M15 16l-3 7Q16 29 20 23Z','#fff','none'),
    shell:path('M18 30 4 16C0 3 9 4 11 6C15 0 20 1 23 5C31 0 38 10 31 20L23 30Z','#f1c3b2','#c58f83')+path('M7 11l11 17M13 7l6 20M20 5v23M27 9l-5 19M32 15l-9 13','none','#d59e8d',1),
    leaf:path('M6 28C0 9 20 2 31 4C34 23 22 34 6 28Z','#9dc297','#85a581')+path('M5 32 27 9M11 25 8 15M16 20 15 10M17 19l9 1M22 14l7 1','none','#e5eecb',1.5),
    pencil:path('M5 31 8 20 25 3 33 11 16 28Z','#f0c574','#bca176')+path('M25 3 28 0 36 8 33 11Z','#e9a8b3','#c49ca9')+path('M8 20 16 28 5 31Z','#e9d0ac','none')+path('M5 31 7 25 11 29Z','#647281','none')+path('M13 23 29 7','none','#fff1b4',2),
    key:circle(11,11,8,'#e5c47a','#b4a47e')+circle(11,11,3,'#fff7e0','none')+path('M15 16 28 31 33 26 30 22 26 24 23 19 25 17 22 14 19 17Z','#e5c47a','#b4a47e')
  };
  const svg=id=>`<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 36 36" aria-hidden="true" stroke-width="1.1" stroke-linejoin="round" stroke-linecap="round">${art[id]||''}</svg>`;
  return {svg,ids:Object.keys(art)};
});
