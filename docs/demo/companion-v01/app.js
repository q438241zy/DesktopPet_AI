(() => {
  'use strict';
  const $=selector=>document.querySelector(selector), $$=selector=>[...document.querySelectorAll(selector)];
  const M=CompanionModel,P=PetPersonas,D=CLOUD_DATA,A=CompanionProviders;
  const storageKey='cloud-companions.demo.v01.state',accountKey='cloud-companions.demo.v01.accounts';
  const read=key=>{try{return JSON.parse(localStorage.getItem(key)||'null');}catch{return null;}};
  const state=M.create(read(storageKey));
  let offset=0;const now=()=>Date.now()+offset;
  const runtime=M.createRuntime(now()),images=new Map(),photoImages=new Map(),pendingPhotos=new Map(),errors=[];
  let page='partners',detailId=state.selected,detailTab='bond',collectionTab='balls',apiKey='',session=null,authMode='login',chatTicket=0,chatAbort=null,chatStarted=0,chatFinished=0,photoStamp='',photoBusy=false,photoTicket=0,story=null,storyLine=0,storyFinished=false,lastTick=performance.now(),lastSave=now(),touches=[],saveFailed=false;
  const chats=Object.fromEntries(M.families.map(id=>[id,[]]));
  let viewStamp='',lastPose=null,toastTimer,reminderTimer,reminderTicket=0,reminderAbort=null,reminderIndex=0,apiTestTicket=0,apiTestAbort=null;
  const outfit=(family=state.selected,style=state.style)=>state.outfits[family+'/'+style]||'original';
  const look=(family=state.selected,style=state.style,clothes=outfit(family,style))=>D.families[family].styles[style].looks[clothes];
  const icon=name=>`<svg viewBox="0 0 24 24" aria-hidden="true"><path d="${CompanionIcons[name]||CompanionIcons.cloud}"></path></svg>`;
  function icons(root=document){root.querySelectorAll('[data-icon]').forEach(el=>el.innerHTML=icon(el.dataset.icon));}
  function save(){try{localStorage.setItem(storageKey,JSON.stringify(state));saveFailed=false;}catch{if(!saveFailed)toast('浏览器存储不可用，本次记录只保留到关页。');saveFailed=true;}}
  function toast(message){$('#toast').textContent=message;$('#toast').hidden=false;clearTimeout(toastTimer);toastTimer=setTimeout(()=>$('#toast').hidden=true,3400);}
  function errorMessage(error){errors.push(String(error.message||error));toast(error.message||'操作没有完成，请重试。');}
  function loadedImage(src){
    if(images.has(src))return images.get(src);
    const img=new Image();images.set(src,img);if(images.size>48)images.delete(images.keys().next().value);img.onload=()=>{drawStatic();drawPet();};img.onerror=()=>errorMessage(new Error('图片读取失败：'+src.split('/').at(-1)));img.src=src;
    return img;
  }
  function source(family,style,desc){return new URL(D.assetRoot+D.families[family].styles[style].id+'/'+desc.file,location.href).href;}
  function frameAt(desc,elapsed){const duration=desc.frameMs.reduce((a,b)=>a+b,0);let time=Math.max(0,elapsed)%duration;for(let i=0;i<desc.frames.length;i++){if(time<desc.frameMs[i])return desc.frames[i];time-=desc.frameMs[i];}return desc.frames.at(-1);}
  function draw(out,img,desc,frame,x,floor,height,flip=false){
    if(!img.complete||!img.naturalWidth)return false;
    const c=desc.cells[frame],k=height/desc.reference*c.scale;
    out.save();out.translate(x,floor);if(flip)out.scale(-1,1);out.imageSmoothingEnabled=true;out.imageSmoothingQuality='high';out.drawImage(img,c.x,c.y,c.width,c.height,-c.footX*k,-c.footY*k,c.width*k,c.height*k);out.restore();return true;
  }
  function drawCanvas(canvas,family,style,clothes,pose='idle',elapsed=0){
    if(!canvas||!canvas.isConnected||!canvas.getBoundingClientRect().width)return;
    const box=canvas.getBoundingClientRect(),dpr=Math.min(3,devicePixelRatio||1),w=Math.round(box.width*dpr),h=Math.round(box.height*dpr);
    if(canvas.width!==w||canvas.height!==h){canvas.width=w;canvas.height=h;}
    const ctx=canvas.getContext('2d');ctx.clearRect(0,0,w,h);
    const desc=look(family,style,clothes)[pose]||look(family,style,clothes).idle;
    const frame=frameAt(desc,state.settings.reducedMotion?0:elapsed),img=loadedImage(source(family,style,desc));
    draw(ctx,img,desc,frame,w*.5,h*.95,h*.86);
    return {file:desc.file,frame,reference:desc.reference};
  }
  function displayTime(seconds){if(seconds<60)return `${Math.floor(seconds)} 秒`;if(seconds<3600)return `${Math.floor(seconds/60)} 分钟`;return `${Math.floor(seconds/3600)} 小时 ${Math.floor(seconds%3600/60)} 分钟`;}
  function affect(delta,label,key,cooldown){const result=M.affect(state,state.selected,delta,label,now(),key,cooldown);save();if(page==='detail')renderBond();return result;}
  function speak(text,pose='talk',duration=4600){$('#bubble-text').textContent=text;if(!['hidden','hiding'].includes(runtime.action)){runtime.action=pose;runtime.started=now();runtime.until=now()+duration;}syncStage(true);}
  function cancelChat(){chatTicket++;chatAbort?.abort();chatAbort=null;if(runtime.action==='thinking'){runtime.action='idle';runtime.started=now();runtime.idleSince=now();}$('#chat-send').disabled=false;$('#chat-input').disabled=runtime.paused;$('#pet-stage').classList.remove('thinking');$('.thinking-dots').hidden=true;}
  function touch(){runtime.idleSince=now();}
  function navigate(next){
    page=next;$$('.page').forEach(el=>el.hidden=el.id!=='page-'+next);$$('[data-page]').forEach(el=>el.classList.toggle('active',el.dataset.page===(next==='detail'?'partners':next)));
    $('#page-title').textContent={partners:'我的伙伴',daily:'陪伴日常',member:'会员中心',settings:'设定',detail:'伙伴档案'}[next];touch();$('#pet-menu').hidden=true;
    if(next==='daily')renderDaily();if(next==='detail')renderDetail();if(next==='settings')renderSettings();drawStatic();syncStage(true);window.scrollTo({top:0,behavior:'instant'});
  }
  function select(family){
    if(!M.families.includes(family))return;cancelChat();dismissReminder();state.selected=family;detailId=family;runtime.action='idle';runtime.started=now();touch();touches=[];viewStamp='';
    renderCards();$('#active-name').textContent=P.all[family].name;$('#chat-input').placeholder=`和 ${P.all[family].name} 说句话…`;$('#chat-input').value='';$('#pet-canvas').setAttribute('aria-label',P.all[family].name);$('#bubble-text').textContent=P.all[family].hello;save();syncStage(true);
  }
  function style(value){if(!['chibi','realistic'].includes(value))return;cancelChat();dismissReminder();state.style=value;runtime.action='idle';runtime.started=now();touch();renderCards();save();syncStage(true);if(page==='detail')renderDetail();}
  function renderCards(){
    const grid=$('#partner-grid');grid.replaceChildren();
    for(const id of M.families){const card=document.createElement('button');card.className='partner-card'+(state.selected===id?' active':'');card.dataset.family=id;card.setAttribute('aria-pressed',String(state.selected===id));card.setAttribute('aria-label',`${P.all[id].name}，右键查看档案`);card.innerHTML='<canvas width="400" height="320"></canvas><strong></strong>';card.querySelector('strong').textContent=P.all[id].name;card.onclick=()=>select(id);card.oncontextmenu=event=>{event.preventDefault();openDetail(id);};card.onkeydown=event=>{if(event.key==='ContextMenu'||event.shiftKey&&event.key==='F10'){event.preventDefault();openDetail(id);}};grid.append(card);}
    $$('[data-style]').forEach(el=>el.classList.toggle('selected',el.dataset.style===state.style));drawStatic();
  }
  function drawStatic(){
    if(page==='partners')$$('.partner-card').forEach(el=>drawCanvas(el.querySelector('canvas'),el.dataset.family,state.style,outfit(el.dataset.family)));
    if(page==='detail'){
      drawCanvas($('#profile-canvas'),detailId,state.style,outfit(detailId));
      $$('.appearance-card').forEach(el=>drawCanvas(el.querySelector('canvas'),detailId,el.dataset.lookStyle,el.dataset.outfit));
    }
    if(!$('#reminder').hidden)drawCanvas($('#reminder-canvas'),$('#reminder').dataset.family,$('#reminder').dataset.style,$('#reminder').dataset.outfit,'smile');
  }
  function syncStage(force=false){
    const hidden=['hidden','hiding'].includes(runtime.action),stamp=[runtime.action,runtime.paused,state.style,state.selected,hidden].join('|');
    if(stamp!==viewStamp||force){
      viewStamp=stamp;$('#pet-stage').classList.toggle('realistic',state.style==='realistic');$('#pet-stage').classList.toggle('thinking',runtime.action==='thinking');$('.thinking-dots').hidden=runtime.action!=='thinking';$('#inline-chat').hidden=hidden||runtime.paused;$('#pet-bubble').hidden=hidden;
      $('#find-left').hidden=!(runtime.action==='hidden'&&runtime.hideSide==='left');$('#find-right').hidden=!(runtime.action==='hidden'&&runtime.hideSide==='right');
      $('#pet-body').setAttribute('aria-label',hidden?'找到躲藏的伙伴':'摸摸头');$('#chat-input').disabled=runtime.paused;$('#presence').setAttribute('aria-pressed',String(!runtime.paused));$('#presence span').textContent=runtime.paused?'暂停陪伴':'正在陪伴';
    }
    drawPet();
  }
  function drawPet(){
    if(page!=='partners')return;
    const elapsed=now()-runtime.started,canvas=$('#pet-canvas'),stage=$('#pet-stage'),body=$('#pet-body'),w=stage.clientWidth,bw=body.clientWidth,baseX=(w-bw)/2;
    let x=baseX,pose='idle',flip=false;
    if(runtime.action==='hiding'||runtime.action==='hidden'){
      const dest=runtime.hideSide==='left'?-bw*.78:w-bw*.22;
      const progress=runtime.action==='hidden'?1:Math.min(1,elapsed/2200);
      x=baseX+(dest-baseX)*(progress*progress*(3-2*progress));pose=runtime.action==='hiding'?'walk':'peek';flip=runtime.hideSide==='left';
    }else if(runtime.action==='thinking')pose=state.style==='chibi'?'think':'idle';
    else if(['found','smile'].includes(runtime.action))pose='smile';else if(runtime.action==='pat')pose='pat';else if(runtime.action==='talk')pose='talk';
    body.style.left=x+'px';body.style.opacity=runtime.paused?'.65':'1';
    const b=canvas.getBoundingClientRect(),dpr=Math.min(3,devicePixelRatio||1),cw=Math.round(b.width*dpr),ch=Math.round(b.height*dpr);
    if(!cw||!ch)return;if(canvas.width!==cw||canvas.height!==ch){canvas.width=cw;canvas.height=ch;}
    const ctx=canvas.getContext('2d');ctx.clearRect(0,0,cw,ch);const desc=look()[pose]||look().idle;const frame=frameAt(desc,state.settings.reducedMotion?0:elapsed);
    draw(ctx,loadedImage(source(state.selected,state.style,desc)),desc,frame,cw*.5,ch*.96,ch*.88,flip&&(desc.facing!=='left'));
    lastPose={family:state.selected,style:state.style,outfit:outfit(),pose,file:desc.file,frame};
    const bubbleHeight=$('#pet-bubble').offsetHeight;$('#inline-chat').style.top=Math.min(115,27+bubbleHeight)+'px';
  }
  function pat(){
    if(runtime.paused)return toast('先继续陪伴吧。');if(find())return;cancelChat();touch();
    const t=now();touches=touches.filter(x=>t-x<10000);touches.push(t);
    if(touches.length>=6){affect(-2,'连续逗弄','rough',10000);speak(P.all[state.selected].distant,'idle',2500);toast('让她休息一下吧');return;}
    const gain=affect(1,'摸摸头','pat',30000);speak(gain?'嗯，收到你的温柔啦。':'我在这里。','pat',2200);$('#touch-heart').hidden=false;setTimeout(()=>$('#touch-heart').hidden=true,1050);
  }
  function hide(){if(runtime.paused)return toast('先继续陪伴吧。');if(['hiding','hidden'].includes(runtime.action))return;cancelChat();M.startHide(runtime,now(),Math.random()<.5?'left':'right');touch();syncStage(true);}
  function find(){if(!M.find(runtime,now()))return false;affect(1,'找到伙伴','find',60000);$('#bubble-text').textContent='被你找到啦。';touch();syncStage(true);return true;}
  async function reply(){
    const text=$('#chat-input').value.trim();if(!text||runtime.paused||runtime.action==='thinking')return;
    if(['hidden','hiding'].includes(runtime.action))return;
    touch();const id=state.selected,ticket=++chatTicket;chatAbort?.abort();chatAbort=new AbortController();const controller=chatAbort;
    chats[id].push({role:'user',content:text});chats[id]=chats[id].slice(-24);$('#chat-input').value='';$('#chat-send').disabled=true;
    runtime.action='thinking';runtime.started=now();chatStarted=performance.now();$('#bubble-text').textContent='让我想一想';syncStage(true);
    try{
      const fetchReply=async()=>{
        if(state.api.provider==='local'||!state.api.endpoint.trim())return P.reply(id,chats[id],state.relations[id].score);
        return A.send({...state.api},apiKey,chats[id],P.prompt(id,state.relations[id].score),{signal:controller.signal});
      };
      const result=await Promise.allSettled([fetchReply(),new Promise(resolve=>setTimeout(resolve,1000))]);
      if(ticket!==chatTicket||state.selected!==id)return;
      if(result[0].status==='rejected')throw result[0].reason;
      const answer=result[0].value;chats[id].push({role:'assistant',content:answer});chatFinished=performance.now();affect(1,'聊了几句','chat',60000);speak(answer,'talk',Math.max(3600,Math.min(9000,answer.length*75)));
    }catch(error){if(ticket===chatTicket){runtime.action='idle';runtime.started=now();runtime.idleSince=now();$('#bubble-text').textContent=error.name==='AbortError'?'这次回复超时了，请再试一次。':error instanceof TypeError?'连接没有成功，浏览器可能限制此接口。请检查地址或使用本机对话。':error.message;syncStage(true);}}
    finally{if(ticket===chatTicket){$('#chat-send').disabled=false;chatAbort=null;syncStage(true);}}
  }
  function openDetail(id){detailId=id;detailTab='bond';navigate('detail');}
  function renderDetail(){
    const p=P.all[detailId];$('#profile-name').textContent=p.name;$('#profile-description').textContent=p.description;$('#profile-traits').replaceChildren(...p.traits.map(text=>{const el=document.createElement('span');el.textContent=text;return el;}));
    $$('[data-detail]').forEach(el=>el.classList.toggle('selected',el.dataset.detail===detailTab));for(const key of ['bond','style','voice'])$('#detail-'+key).hidden=key!==detailTab;
    if(detailTab==='bond')renderBond();if(detailTab==='style')renderStyles();if(detailTab==='voice')renderVoice();drawStatic();
  }
  function renderBond(){
    const r=state.relations[detailId];$('#bond-label').textContent=M.label(r.score);$('#bond-score').textContent=r.score>0?'+'+r.score:String(r.score);$('#bond-fill').style.left=r.score<0?(50+r.score/2)+'%':'50%';$('#bond-fill').style.width=Math.abs(r.score)/2+'%';$('#bond-fill').style.background=r.score<0?'#a59baa':'#c58aa4';$('#companion-time').textContent=displayTime(r.seconds);$('#interaction-count').textContent=r.interactions+' 次';
    $('#bond-history').replaceChildren(...r.history.slice(-5).reverse().map(e=>{const li=document.createElement('li'),label=document.createElement('span'),time=document.createElement('time'),delta=document.createElement('span');label.textContent=e.label;time.textContent=new Date(e.at).toLocaleTimeString('zh-CN',{hour:'2-digit',minute:'2-digit'});delta.textContent=e.delta>0?'+'+e.delta:String(e.delta);li.append(label,time,delta);return li;}));
    if(!r.history.length){const li=document.createElement('li');li.textContent='还没有相处记录';$('#bond-history').append(li);}
  }
  function appearanceCard(style,clothes,title,gate,selected,click){
    const card=document.createElement('button');card.className='appearance-card'+(selected?' selected':'');card.dataset.lookStyle=style;card.dataset.outfit=clothes;card.innerHTML='<canvas></canvas><div class="look-name"></div><div class="look-gate"></div>';card.querySelector('.look-name').textContent=title;
    const g=card.querySelector('.look-gate');if(gate===undefined)g.textContent=selected?'已选择':'选择风格';else if(state.relations[detailId].score<gate){g.innerHTML=icon('lock');g.append(document.createTextNode(`好感 ${gate} · 测试开放`));}else g.textContent='已达成';card.onclick=click;return card;
  }
  function renderStyles(){
    $('#style-pair').replaceChildren(...['chibi','realistic'].map(s=>appearanceCard(s,outfit(detailId,s),s==='chibi'?'Q版':'3D真人',undefined,state.style===s,()=>{select(detailId);state.style=s;save();detailTab='style';renderCards();renderDetail();})));
    $('#wardrobe-grid').replaceChildren(...Object.entries(M.outfits).map(([key,value])=>appearanceCard(state.style,key,value.name,value.score,outfit(detailId)===key,()=>{state.outfits[detailId+'/'+state.style]=key;save();if(state.selected===detailId){cancelChat();runtime.action='idle';runtime.started=now();}renderStyles();drawStatic();toast('已换上'+value.name);})));drawStatic();
  }
  function renderVoice(){const p=P.all[detailId];$('#voice-title').textContent=p.traits.join(' · ');$('#voice-description').textContent=p.tone;$('#voice-samples').replaceChildren(...['我今天工作很多','我好累','今天终于完成了'].map(text=>{const el=document.createElement('div');el.className='voice-sample';const user=document.createElement('b'),answer=document.createElement('span');user.textContent='你：'+text;answer.textContent=p.name+'：'+P.reply(detailId,[{role:'user',content:text}],state.relations[detailId].score);el.append(user,answer);return el;}));}
  function renderDaily(){
    const t=now(),date=new Date(t);$('#today-date').textContent=date.toLocaleDateString('zh-CN',{month:'long',day:'numeric',weekday:'long'});const checked=state.checkins.includes(M.day(t));$('#check-in').disabled=checked;$('#check-in').innerHTML=icon('check')+(checked?'已打卡':'打卡');$('#streak-count').textContent=M.streak(state,t);$('#total-count').textContent=state.checkins.length;
    const monday=new Date(t);monday.setHours(12,0,0,0);monday.setDate(monday.getDate()-((monday.getDay()+6)%7));$('#checkin-week').replaceChildren(...Array.from({length:7},(_,i)=>{const d=new Date(monday);d.setDate(d.getDate()+i);const key=M.day(d),el=document.createElement('div');el.className='day'+(state.checkins.includes(key)?' done':'')+(key===M.day(t)?' today':'');el.innerHTML='<span></span><b></b>';el.querySelector('span').textContent=['一','二','三','四','五','六','日'][i];el.querySelector('b').textContent=state.checkins.includes(key)?'✓':d.getDate();return el;}));
    for(const kind of ['balls','food']){const entries=D.items.filter(x=>x.kind===kind);$('#'+kind+'-count').textContent=entries.filter(x=>state.collection[x.id]).length+'/'+entries.length;}
    $('#stories-count').textContent=Object.keys(state.stories).length+'/'+D.stories.length;
    $$('[data-collection]').forEach(el=>el.classList.toggle('selected',el.dataset.collection===collectionTab));
    if(collectionTab==='stories')$('#collection-grid').replaceChildren(...D.stories.map(s=>{const el=document.createElement('button'),owned=state.stories[s.id];el.className='collection-card story'+(owned?' earned':' unearned');el.innerHTML=icon('book')+'<span></span><small></small>';el.querySelector('span').textContent=s.title;el.querySelector('small').textContent=owned?`读过 ${owned} 次 · 再读一遍`:'读完收藏';el.onclick=()=>openStory(s.id);return el;}));
    else $('#collection-grid').replaceChildren(...D.items.filter(x=>x.kind===collectionTab).map(itemCard));
    $('#keepsake-grid').replaceChildren(...D.items.filter(x=>x.kind==='keepsakes').map(itemCard));$('#keepsakes').hidden=collectionTab==='stories';
  }
  function itemCard(item){const el=document.createElement('div');el.className='collection-card'+(state.collection[item.id]?' earned':' unearned');el.innerHTML=FiveItems.svg(item.id)+'<span></span><small></small>';el.querySelector('span').textContent=item.name;el.querySelector('small').textContent=state.collection[item.id]?`已收藏 ×${state.collection[item.id]}`:'还没遇见';return el;}
  function openStory(id){story=D.stories.find(x=>x.id===id);storyLine=0;storyFinished=false;renderStory();$('#story-dialog').showModal();}
  function renderStory(){$('#story-title').textContent=story.title;$('#story-sentence').textContent=story.sentences[storyLine];$('#story-page').textContent=`${storyLine+1} / ${story.sentences.length}`;$('#story-progress-fill').style.width=((storyLine+1)/story.sentences.length*100)+'%';$('#story-next').textContent=storyLine===story.sentences.length-1?'读完收藏':'下一句';}
  function nextStory(){if(storyFinished)return;if(storyLine<story.sentences.length-1){storyLine++;renderStory();return;}storyFinished=true;state.stories[story.id]=(state.stories[story.id]||0)+1;affect(2,'共读《'+story.title+'》','story-'+story.id,86400000);save();$('#story-dialog').close();renderDaily();toast('《'+story.title+'》已收进故事区');}
  function renderSettings(){
    $('#auto-hide').checked=state.settings.autoHide;$('#reduced-motion').checked=state.settings.reducedMotion;$('#work-minutes').value=state.work.minutes;$('#api-provider').value=state.api.provider;$('#api-endpoint').value=state.api.endpoint;$('#api-model').value=state.api.model;$('#api-key').value=apiKey;$('#api-workspace').value=state.api.workspace||'';apiFields();syncWork();
  }
  function apiFields(){
    const spec=A.catalog[$('#api-provider').value];$('#api-fields').hidden=!spec;$('#api-test').hidden=!spec;$('#api-workspace-row').hidden=spec?.protocol!=='messages';
    if(spec){$('#api-endpoint').placeholder=spec.base;$('#api-model').placeholder='例如 '+spec.hint;$('#api-provider-note').textContent=spec.note||({messages:'Claude 原生接口',gemini:'Gemini 原生接口',chat:'Chat Completions 接口'}[spec.protocol]);$('#api-doc').href=spec.doc;}
  }
  function apiDraft(){
    const api=A.cleanConfig({provider:$('#api-provider').value,endpoint:$('#api-endpoint').value.trim(),model:$('#api-model').value.trim(),workspace:$('#api-workspace').value.trim()});
    if(api.provider!=='claude')api.workspace='';
    if(api.provider!=='local'&&api.endpoint){if(!api.model)throw new Error('请填写模型名称。');M.endpoint(api.endpoint,api.provider,api.model);}
    return api;
  }
  function cancelApiTest(){apiTestTicket++;apiTestAbort?.abort();apiTestAbort=null;$('#api-test').disabled=false;$('#api-test').textContent='测试连接';}
  function apiSave(event){
    event.preventDefault();try{const api=apiDraft();cancelChat();dismissReminder();cancelApiTest();state.api=api;apiKey=api.provider==='local'?'':$('#api-key').value.trim();save();$('#api-status').textContent=api.provider==='local'||!api.endpoint?'已保存 · 本机对话':'已保存 · 尚未测试连接';chatSource();syncWork();toast('设定已保存');}catch(error){$('#api-status').textContent=error.message;}
  }
  async function apiTest(){
    cancelApiTest();const ticket=apiTestTicket,controller=new AbortController();apiTestAbort=controller;
    try{
      const api=apiDraft();$('#api-test').disabled=true;$('#api-test').textContent='连接中…';$('#api-status').textContent='正在测试当前填写的接口…';
      await A.send(api,$('#api-key').value.trim(),[{role:'user',content:'请只回复：连接成功。'}],'这是一个文本连接测试。请简短回复。',{signal:controller.signal});
      if(ticket===apiTestTicket)$('#api-status').textContent='连接成功 · '+A.catalog[api.provider].name;
    }catch(error){if(ticket===apiTestTicket)$('#api-status').textContent=error.name==='AbortError'?'连接超时，请稍后重试。':error instanceof TypeError?'连接失败，请检查网络及接口是否允许浏览器访问。':error.message;}
    finally{if(ticket===apiTestTicket){apiTestAbort=null;$('#api-test').disabled=false;$('#api-test').textContent='测试连接';}}
  }
  function chatSource(){$('#chat-source').textContent=state.api.provider==='local'||!state.api.endpoint?'本机对话':A.catalog[state.api.provider].name;}
  function workStart(event){
    event?.preventDefault();if(!runtime.workActive)return;
    const minutes=Number($('#work-minutes').value);if(!Number.isInteger(minutes)||minutes<1||minutes>180){toast('提醒间隔为 1 至 180 分钟。');return;}
    state.work.minutes=minutes;M.workStart(runtime,state,now());save();syncWork();toast(`每 ${minutes} 分钟提醒一次`);
  }
  function workToggle(){
    if($('#work-enabled').checked){M.workStart(runtime,state,now());$('#work-minutes').value=state.work.minutes;}
    else{M.workStop(runtime,state);dismissReminder();}
    save();syncWork();
  }
  function dismissReminder(){reminderTicket++;reminderAbort?.abort();reminderAbort=null;clearTimeout(reminderTimer);$('#reminder').hidden=true;}
  async function remind(){
    if(!runtime.workActive||runtime.paused)return;
    dismissReminder();const ticket=reminderTicket,id=state.selected,p=P.all[id],text=P.reminder(id,reminderIndex++),controller=new AbortController();reminderAbort=controller;
    $('#reminder').dataset.family=id;$('#reminder').dataset.style=state.style;$('#reminder').dataset.outfit=outfit();$('#reminder-name').textContent=p.name;$('#reminder-text').textContent=text;$('#reminder-source').textContent='预设提醒';$('#reminder').hidden=false;drawStatic();reminderTimer=setTimeout(dismissReminder,15000);
    if(!['hiding','hidden','thinking'].includes(runtime.action))speak(text,'smile',6000);
    const smileStarted=runtime.started;
    if(state.api.provider==='local'||!state.api.endpoint.trim()||!apiKey){reminderAbort=null;return;}
    $('#reminder-source').textContent='预设提醒 · 正在准备新话语';
    try{
      const reply=await A.send({...state.api},apiKey,[{role:'user',content:`已专注 ${state.work.minutes} 分钟。请围绕“${text}”写一句轻松、带笑意的中文休息提醒，不超过45字，不加引号或说明。`}],P.prompt(id,state.relations[id].score),{signal:controller.signal,timeout:10000});
      if(ticket!==reminderTicket||id!==state.selected||$('#reminder').hidden)return;
      const answer=[...reply].slice(0,90).join('');$('#reminder-text').textContent=answer;$('#reminder-source').textContent='模型提醒';
      if(runtime.action==='smile'&&runtime.started===smileStarted)speak(answer,'smile',6000);
    }catch(error){if(ticket===reminderTicket&&!$('#reminder').hidden)$('#reminder-source').textContent='预设提醒 · 模型暂未响应';}
    finally{if(ticket===reminderTicket)reminderAbort=null;}
  }
  function syncWork(){
    $('#work-enabled').checked=runtime.workActive;$('#work-enabled').setAttribute('aria-expanded',String(runtime.workActive));$('#work-form').hidden=!runtime.workActive;$('#work-form').querySelectorAll('input,button').forEach(el=>el.disabled=!runtime.workActive);$('#work-try').disabled=!runtime.workActive||runtime.paused;$('#work-pill').hidden=!runtime.workActive;
    $('#work-preview-name').textContent=P.all[state.selected].name+' · 自动提醒';$('#work-preview').textContent=P.reminder(state.selected,reminderIndex);
    if(runtime.workActive){const remain=runtime.paused?runtime.workRemaining:Math.max(0,runtime.nextReminder-now()),seconds=Math.ceil(remain/1000),time=`${String(Math.floor(seconds/60)).padStart(2,'0')}:${String(seconds%60).padStart(2,'0')}`;$('#work-countdown').textContent=time;$('#work-status').textContent=runtime.paused?`已暂停 · 剩余 ${time}`:`每 ${state.work.minutes} 分钟 · 下次 ${time}`;}else $('#work-status').textContent='未开启';
  }
  async function digest(password,salt){const material=await crypto.subtle.importKey('raw',new TextEncoder().encode(password),'PBKDF2',false,['deriveBits']);const bits=await crypto.subtle.deriveBits({name:'PBKDF2',salt:new Uint8Array(salt),iterations:210000,hash:'SHA-256'},material,256);return Array.from(new Uint8Array(bits),b=>b.toString(16).padStart(2,'0')).join('');}
  function authTabs(mode){authMode=mode;$('#login-tab').classList.toggle('selected',mode==='login');$('#register-tab').classList.toggle('selected',mode==='register');$('#login-tab').setAttribute('aria-selected',String(mode==='login'));$('#register-tab').setAttribute('aria-selected',String(mode==='register'));$('#password-again-row').hidden=mode!=='register';$('#account-password-again').required=mode==='register';$('#account-password').autocomplete=mode==='register'?'new-password':'current-password';$('#auth-submit').textContent=mode==='register'?'注册':'登录';$('#auth-result').textContent='';$('#account-password').value='';$('#account-password-again').value='';}
  async function auth(event){event.preventDefault();$('#auth-submit').disabled=true;const name=$('#account-name').value.trim(),password=$('#account-password').value,mode=authMode;try{
    if(!/^[\p{L}\p{N}_-]{3,32}$/u.test(name))throw new Error('账号需为 3–32 位文字、数字、下划线或短横线。');if(password.length<8)throw new Error('密码至少 8 位。');
    const accounts=read(accountKey)||{},id=name.toLocaleLowerCase();
    if(mode==='register'){if(password!==$('#account-password-again').value)throw new Error('两次密码不一致。');if(Object.hasOwn(accounts,id))throw new Error('账号已存在，请登录。');const salt=[...crypto.getRandomValues(new Uint8Array(16))];const hash=await digest(password,salt);Object.defineProperty(accounts,id,{value:{name,salt,hash,tier:'bronze'},enumerable:true});localStorage.setItem(accountKey,JSON.stringify(accounts));authTabs('login');$('#auth-result').textContent='注册成功，请登录。';}
    else{const account=Object.hasOwn(accounts,id)?accounts[id]:null;if(!account||await digest(password,account.salt)!==account.hash)throw new Error('账号或密码不正确。');session={name:account.name,tier:'bronze'};$('#auth-form').hidden=true;$('#account-session').hidden=false;$('#signed-name').textContent=session.name;$('#account-password').value='';$('.auth-card [role=tablist]').hidden=true;}
  }catch(error){$('#auth-result').textContent=error.message;}finally{$('#auth-submit').disabled=false;}}
  globalThis.receivePhotoSource=(key,url)=>{const pending=pendingPhotos.get(key);if(!pending)return;const img=new Image();img.onload=()=>{photoImages.set(key,img);if(photoImages.size>8)photoImages.delete(photoImages.keys().next().value);pending.resolve(img);};img.onerror=()=>pending.reject(new Error('合照素材读取失败。'));img.src=url;};
  function photoImage(meta){if(photoImages.has(meta.key))return Promise.resolve(photoImages.get(meta.key));if(pendingPhotos.has(meta.key))return pendingPhotos.get(meta.key).promise;
    let resolve,reject;const promise=new Promise((a,b)=>{resolve=a;reject=b;}),script=document.createElement('script');const timer=setTimeout(()=>reject(new Error('合照素材读取超时，请重试。')),20000);
    pendingPhotos.set(meta.key,{promise,resolve,reject});script.src=meta.module;script.onerror=()=>reject(new Error('找不到合照素材，请重新生成 Demo 数据。'));document.head.append(script);
    promise.finally(()=>{clearTimeout(timer);script.remove();pendingPhotos.delete(meta.key);}).catch(()=>{});return promise;
  }
  function photoOptions(){return {first:state.selected,second:$('#photo-partner').value,style:state.style,firstOutfit:outfit(),secondOutfit:outfit($('#photo-partner').value),caption:$('#photo-caption').value.trim(),frame:$('input[name=frame]:checked').value};}
  function invalidatePhoto(){$('#photo-save').disabled=true;photoStamp='';$('#photo-status').textContent='';photoTicket++;}
  async function openPhoto(){
    $('#photo-partner').replaceChildren(...M.families.filter(id=>id!==state.selected).map(id=>{const op=document.createElement('option');op.value=id;op.textContent=P.all[id].name;return op;}));$('#photo-caption').value='一起收藏今天的小美好';$('#photo-dialog').showModal();invalidatePhoto();await generatePhoto();
  }
  function rounded(ctx,x,y,w,h,r,fill,stroke){ctx.beginPath();ctx.roundRect(x,y,w,h,r);if(fill){ctx.fillStyle=fill;ctx.fill();}if(stroke){ctx.strokeStyle=stroke;ctx.stroke();}}
  function cloud(ctx,x,y,scale,color){ctx.save();ctx.translate(x,y);ctx.scale(scale,scale);ctx.beginPath();ctx.ellipse(0,0,46,22,0,0,Math.PI*2);ctx.ellipse(-20,-17,23,23,0,0,Math.PI*2);ctx.ellipse(15,-23,29,29,0,0,Math.PI*2);ctx.fillStyle=color;ctx.fill();ctx.restore();}
  function wrapText(ctx,text,max){const lines=[];let line='';for(const char of [...text]){if(char==='\n'){lines.push(line);line='';continue;}if(ctx.measureText(line+char).width>max&&line){lines.push(line);line=char;}else line+=char;}if(line)lines.push(line);return lines.slice(0,3);}
  async function generatePhoto(event){event?.preventDefault();const ticket=++photoTicket;photoBusy=true;$('#photo-generate').disabled=true;$('#photo-save').disabled=true;$('#photo-status').textContent='正在准备合照…';const options=photoOptions();
    try{const first=look(options.first,options.style,options.firstOutfit),second=look(options.second,options.style,options.secondOutfit);const [a,b]=await Promise.all([photoImage(first.photoData),photoImage(second.photoData)]);if(ticket!==photoTicket)return;
      const canvas=$('#photo-canvas'),ctx=canvas.getContext('2d');canvas.width=1200;canvas.height=1400;
      const theme={cloud:['#f4dae7','#fff5fa','#dcb4c8','#b27090'],jade:['#cfe2d8','#f2faf5','#a9c9ba','#688b7b'],cream:['#eee0c5','#fffaf0','#d5c099','#a2895c']}[options.frame];
      ctx.fillStyle=theme[0];ctx.fillRect(0,0,1200,1400);ctx.lineWidth=3;rounded(ctx,24,24,1152,1352,20,null,theme[2]);rounded(ctx,55,55,1090,1100,12,theme[1],theme[2]);
      ctx.save();ctx.beginPath();ctx.rect(60,60,1080,1090);ctx.clip();cloud(ctx,245,205,2.9,'#ffffffbf');cloud(ctx,1040,328,3.5,'#ffffffa0');cloud(ctx,138,865,2.1,'#ffffff9c');
      const gradient=ctx.createLinearGradient(0,760,0,1150);gradient.addColorStop(0,'#ffffff00');gradient.addColorStop(1,theme[0]+'aa');ctx.fillStyle=gradient;ctx.fillRect(60,720,1080,430);
      ctx.fillStyle=theme[2]+'44';ctx.beginPath();ctx.ellipse(395,1091,190,20,0,0,Math.PI*2);ctx.ellipse(815,1091,190,20,0,0,Math.PI*2);ctx.fill();
      const height=options.style==='chibi'?630:880;draw(ctx,a,first.photo,first.photo.frames[0],380,1090,height);draw(ctx,b,second.photo,second.photo.frames[0],825,1090,height);ctx.restore();
      ctx.textAlign='center';ctx.fillStyle=theme[3];ctx.font='500 29px "Segoe UI", "Microsoft YaHei UI"';ctx.fillText(P.all[options.first].name+'  ＋  '+P.all[options.second].name,600,1204);
      ctx.font='30px "Microsoft YaHei UI", sans-serif';ctx.fillStyle='#66535e';const lines=wrapText(ctx,(options.caption||'今天，和你一起').replace(/\s+/g,' '),970);lines.forEach((line,i)=>ctx.fillText(line,600,1248+i*37));
      ctx.font='20px "Segoe UI", "Microsoft YaHei UI"';ctx.fillStyle=theme[3];ctx.textAlign='left';ctx.fillText('云朵伙伴',68,1360);ctx.textAlign='right';ctx.fillText(new Date(now()).toLocaleDateString('zh-CN'),1130,1360);
      photoStamp=JSON.stringify(options);canvas.toDataURL('image/png');$('#photo-save').disabled=false;$('#photo-status').textContent='';
    }catch(error){if(ticket===photoTicket)$('#photo-status').textContent=error.message;}finally{photoBusy=false;$('#photo-generate').disabled=false;}
  }
  async function savePhoto(){
    if(!photoStamp||photoStamp!==JSON.stringify(photoOptions()))return toast('内容改变了，请先重新生成合照。');
    const options=JSON.parse(photoStamp),filename=`云朵伙伴-${P.all[options.first].name}-${P.all[options.second].name}-${M.day(now())}-${Date.now()}.png`;
    const blobPromise=new Promise(resolve=>$('#photo-canvas').toBlob(resolve,'image/png'));
    try{if('showSaveFilePicker' in window){const handle=await showSaveFilePicker({suggestedName:filename,startIn:'desktop',types:[{description:'PNG 图片',accept:{'image/png':['.png']}}]});const blob=await blobPromise;if(!blob)throw new Error('图片生成失败，请重试。');const stream=await handle.createWritable();await stream.write(blob);await stream.close();$('#photo-status').textContent='已保存合照';}
      else{const blob=await blobPromise;if(!blob)throw new Error('图片生成失败，请重试。');const url=URL.createObjectURL(blob),a=document.createElement('a');a.href=url;a.download=filename;a.click();setTimeout(()=>URL.revokeObjectURL(url),30000);$('#photo-status').textContent='图片已下载；此浏览器不支持指定桌面目录。';}
      affect(1,'留下合照','photo',60000);
    }catch(error){if(error.name!=='AbortError')$('#photo-status').textContent=error.message;}
  }
  function action(key){touch();if(key==='chat'){if(['hidden','hiding'].includes(runtime.action)){toast('先找到她，再聊天吧。');return;}$('#chat-input').focus();}else if(key==='pat')pat();else if(key==='hide')hide();else if(key==='photo')openPhoto();}
  function tick(){
    const p=performance.now(),elapsed=(p-lastTick)/1000;lastTick=p;
    if(!runtime.paused&&!document.hidden&&elapsed<2)M.companion(state,state.selected,elapsed,now());
    const events=M.tick(runtime,state,now(),{canHide:page==='partners'&&!document.hidden&&!document.querySelector('dialog[open]')&&$('#pet-menu').hidden&&!$('#chat-input').value.trim()});
    if(events.includes('reminder'))remind();syncStage();syncWork();if(page==='detail'&&detailTab==='bond'){$('#companion-time').textContent=displayTime(state.relations[detailId].seconds);}
    if(now()-lastSave>=5000){save();lastSave=now();}
  }
  icons();$$('[data-version]').forEach(el=>el.textContent='v'+D.version);document.title='云朵伙伴 · v'+D.version+' Demo';document.body.classList.toggle('reduced-motion',state.settings.reducedMotion);
  $$('[data-page]').forEach(el=>el.onclick=()=>navigate(el.dataset.page));$$('[data-style]').forEach(el=>el.onclick=()=>style(el.dataset.style));$$('[data-act]').forEach(el=>el.onclick=()=>action(el.dataset.act));$$('[data-close]').forEach(el=>el.onclick=()=>$('#'+el.dataset.close).close());
  $('#details-open').onclick=()=>openDetail(state.selected);$('#detail-back').onclick=()=>navigate('partners');$$('[data-detail]').forEach(el=>el.onclick=()=>{detailTab=el.dataset.detail;renderDetail();});$('#voice-chat').onclick=()=>{select(detailId);navigate('partners');$('#chat-input').focus();};
  $('#pet-body').onclick=pat;$('#find-left').onclick=find;$('#find-right').onclick=find;$('#pet-stage').oncontextmenu=event=>{event.preventDefault();$('#pet-menu').hidden=!$('#pet-menu').hidden;};
  $('#inline-chat').onsubmit=event=>{event.preventDefault();reply();};
  $('#presence').onclick=()=>{M.pause(runtime,now());cancelChat();if(runtime.paused)dismissReminder();if(runtime.action==='thinking')runtime.action='idle';save();syncStage(true);syncWork();};
  $('#check-in').onclick=()=>{if(M.checkin(state,now())){save();renderDaily();toast('打卡成功 · 好感 +3');}};$$('[data-collection]').forEach(el=>el.onclick=()=>{collectionTab=el.dataset.collection;renderDaily();});$('#story-next').onclick=nextStory;
  $('#work-open').onclick=()=>{navigate('settings');$(runtime.workActive?'#work-minutes':'#work-enabled').focus();};$('#work-enabled').onchange=workToggle;$('#work-form').onsubmit=workStart;$('#work-try').onclick=()=>remind();$('#reminder-dismiss').onclick=dismissReminder;
  $('#auto-hide').onchange=event=>{state.settings.autoHide=event.target.checked;touch();save();};$('#reduced-motion').onchange=event=>{state.settings.reducedMotion=event.target.checked;document.body.classList.toggle('reduced-motion',event.target.checked);save();};
  $('#api-provider').onchange=()=>{cancelApiTest();const provider=$('#api-provider').value;$('#api-endpoint').value=A.catalog[provider]?.base||'';$('#api-key').value='';$('#api-workspace').value='';$('#api-model').value=provider==='deepseek'?'deepseek-flash':'';$('#api-status').textContent='尚未保存';apiFields();};$('#api-form').onsubmit=apiSave;$('#api-test').onclick=apiTest;
  $('#api-form').oninput=event=>{cancelApiTest();if(event.target.id==='api-endpoint')$('#api-key').value='';$('#api-status').textContent='尚未保存';};
  $('#login-tab').onclick=()=>authTabs('login');$('#register-tab').onclick=()=>authTabs('register');$('#auth-form').onsubmit=auth;$('#sign-out').onclick=()=>{session=null;apiKey='';cancelChat();dismissReminder();cancelApiTest();for(const id of M.families)chats[id]=[];$('#api-key').value='';$('#account-session').hidden=true;$('#auth-form').hidden=false;$('.auth-card [role=tablist]').hidden=false;authTabs('login');toast('已退出登录');};
  $('#photo-form').onsubmit=generatePhoto;$('#photo-save').onclick=savePhoto;$('#photo-partner').onchange=invalidatePhoto;$('#photo-caption').oninput=invalidatePhoto;$$('input[name=frame]').forEach(el=>el.onchange=invalidatePhoto);
  $('#review-open').onclick=()=>{const id=page==='detail'?detailId:state.selected;$('#review-affinity').dataset.family=id;$('#review-affinity').value=state.relations[id].score;$('#review-score').textContent=state.relations[id].score;$('#review-dialog').showModal();};
  $('#review-idle').onclick=()=>{cancelChat();runtime.action='idle';runtime.idleSince=now()-60001;$('#review-dialog').close();navigate('partners');runtime.idleSince=now()-60001;document.activeElement.blur();tick();};
  $('#review-work').onclick=()=>{if(!runtime.workActive){toast('请先开启工作模式');return;}offset+=Math.max(0,runtime.nextReminder-now())+1;$('#review-dialog').close();tick();};
  $('#review-affinity').oninput=event=>{state.relations[event.target.dataset.family].score=Number(event.target.value);$('#review-score').textContent=event.target.value;save();if(page==='detail')renderDetail();};
  document.addEventListener('pointerdown',event=>{if(!event.target.closest('#review-dialog'))touch();});document.addEventListener('keydown',event=>{touch();if(event.key==='Escape')$('#pet-menu').hidden=true;});document.addEventListener('visibilitychange',()=>{lastTick=performance.now();save();});window.addEventListener('pagehide',()=>{save();cancelChat();dismissReminder();cancelApiTest();});window.addEventListener('resize',()=>{drawStatic();drawPet();});
  $('.brand').onclick=event=>{event.preventDefault();navigate('partners');};
  globalThis.companionDemo={snapshot:()=>({page,selected:state.selected,style:state.style,outfit:outfit(),runtime:{...runtime},relations:structuredClone(state.relations),checkins:[...state.checkins],collection:{...state.collection},stories:{...state.stories},pose:lastPose,photoReady:!!photoStamp,photoBusy,chatCount:chats[state.selected].length,chatDuration:chatFinished-chatStarted,errors:[...errors],version:D.version}),advance:ms=>{offset+=ms;tick();},select,style,navigate,openDetail,render:tick};
  if(state.work.enabled)M.workStart(runtime,state,now());renderSettings();renderCards();select(state.selected);chatSource();setInterval(tick,100);tick();
  function animate(){if(!document.hidden&&!runtime.paused&&page==='partners'&&['hiding','thinking','talk','pat'].includes(runtime.action)&&!state.settings.reducedMotion)drawPet();requestAnimationFrame(animate);}requestAnimationFrame(animate);
})();
