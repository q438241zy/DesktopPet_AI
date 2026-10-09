globalThis.CompanionAgendaUI={create(bridge){
  'use strict';
  const G=PetAgenda,$=s=>document.querySelector(s),key='cloud-companions.demo.agenda.v1';
  let db=G.create(),pending=null,activeId=null,preview=false,calendarView='week',calendarDate=bridge.now(),selectedDate=G.day(bridge.now()),filter='active',busy=false,previewTicket=0,renderStamp='';
  const name=id=>PetPersonas.all[id].name;
  const time=t=>new Date(t).toLocaleTimeString('zh-CN',{hour:'2-digit',minute:'2-digit',hour12:false});
  const date=t=>new Date(t).toLocaleDateString('zh-CN',{month:'long',day:'numeric',weekday:'short'});
  const read=()=>{const raw=localStorage.getItem(key);return G.create(raw?JSON.parse(raw):null);};
  try{db=read();}catch{bridge.toast('日程存储暂时不可用，请检查浏览器权限。');}
  const section=document.createElement('section');section.id='page-agenda';section.className='page';section.hidden=true;
  section.innerHTML=`<div class="agenda-toolbar"><span id="agenda-api-badge" class="agenda-api-badge"></span><div class="button-row"><button id="agenda-settings" class="quiet">接入 AI</button><button id="agenda-talk" class="primary"><span data-icon="chat"></span>告诉宠物</button></div></div>
    <section class="agenda-hero"><div><span class="eyebrow" id="agenda-next-label">下一件小事</span><h2 id="agenda-next-title">让她帮你记着</h2><p id="agenda-next-time">告诉宠物要记得的事情和时间。</p><div class="agenda-metrics"><span>待提醒 <b id="agenda-count">0</b></span><span>今天 <b id="agenda-today-count">0</b></span></div></div><div class="agenda-companion"><canvas id="agenda-pet"></canvas><span id="agenda-owner"></span></div></section>
    <section class="agenda-calendar"><div class="agenda-calendar-heading"><div><h2 id="agenda-period"></h2><small id="agenda-zone"></small></div><div class="agenda-calendar-controls"><div class="segmented" aria-label="行事历视图"><button data-agenda-view="week" class="selected">周</button><button data-agenda-view="month">月</button></div><button id="agenda-prev" class="icon-button" aria-label="上一周期"><span data-icon="back"></span></button><button id="agenda-today" class="quiet">今天</button><button id="agenda-next" class="icon-button" aria-label="下一周期"><span data-icon="back"></span></button></div></div><div class="agenda-weekdays" aria-hidden="true"><span>一</span><span>二</span><span>三</span><span>四</span><span>五</span><span>六</span><span>日</span></div><div id="agenda-days" class="agenda-days"></div><div class="agenda-calendar-foot"><span><i></i>当天有日程</span><button id="agenda-all" class="text-button">查看全部日程</button></div></section>
    <div class="agenda-lower"><section class="agenda-list-panel"><div class="section-row"><h2 id="agenda-list-title">待提醒</h2><div class="segmented"><button id="agenda-active" class="selected">待提醒</button><button id="agenda-history">已完成</button></div></div><div id="agenda-list"></div></section><aside class="agenda-examples"><span class="eyebrow">先试一次</span><h3>说给她听，替你记住</h3><button data-agenda-sample="meeting">明天下午的会议 <span>单次 · 提前 10 分钟</span></button><button data-agenda-sample="workdays">工作日的下班收尾 <span>重复 · 每天 18:00</span></button><button data-agenda-sample="unclear">「明天提醒我买牛奶」<span>时间不清楚，她会追问</span></button><small>演示话术，不调用 API。</small></aside></div>
    <p class="agenda-scope">HTML 预览：页面可见时提醒，返回或重开页面会补提醒；不唤醒电脑。重复日程显示下一次。</p>`;
  $('main').append(section);
  const nav=document.createElement('button');nav.dataset.page='agenda';nav.innerHTML='<span data-icon="clock"></span>宠物行事历';$('[data-page="daily"]').before(nav);
  const tools=document.createElement('div');tools.className='agenda-chat-tools';tools.innerHTML='<span id="agenda-chat-status"></span><button id="agenda-chat-open" class="text-button">行事历</button><button id="agenda-chat-sample" class="text-button">体验记日程</button>';
  $('.companion-room').append(tools);
  const card=document.createElement('section');card.id='agenda-draft';card.className='agenda-draft';card.hidden=true;
  card.innerHTML=`<div class="section-row"><div><span id="agenda-draft-source" class="eyebrow"></span><h2>这件事，记在这里</h2></div><span class="agenda-draft-icon" data-icon="calendar"></span></div><p id="agenda-user-line" class="agenda-user-line"></p><p id="agenda-question" hidden></p><button id="agenda-answer-sample" class="quiet" hidden>明天下午 3 点</button>
    <form id="agenda-draft-form"><label>事情<input id="agenda-title" maxlength="80" required></label><div class="agenda-form-grid"><label>日期与时间<input id="agenda-start" type="datetime-local" required></label><label>提前提醒<div class="unit-field"><input id="agenda-lead" type="number" min="0" max="10080" step="1" value="0" required><span>分钟</span></div></label><label>重复<select id="agenda-repeat"><option value="none">不重复</option><option value="daily">每天</option><option value="weekdays">工作日</option><option value="weekly">每周</option></select></label></div><p id="agenda-confirm-line" class="muted"></p><div class="button-row"><button id="agenda-confirm" class="primary">收进行事历</button><button id="agenda-discard" class="quiet" type="button">先不记</button></div></form><p id="agenda-draft-result" role="status"></p><button id="agenda-saved-open" class="text-button" hidden>查看行事历 →</button>`;
  $('#page-interaction').append(card);
  const popup=document.createElement('section');popup.id='agenda-alert';popup.className='agenda-alert';popup.hidden=true;popup.setAttribute('role','region');popup.setAttribute('aria-label','宠物日程提醒');popup.setAttribute('aria-live','polite');
  popup.innerHTML='<div class="agenda-alert-art"><canvas id="agenda-alert-pet"></canvas></div><div class="agenda-alert-body"><span id="agenda-alert-label" class="eyebrow"></span><h2 id="agenda-alert-title"></h2><p id="agenda-alert-time"></p><p id="agenda-alert-text"></p><div class="button-row"><button id="agenda-alert-done" class="primary">已完成</button><button id="agenda-alert-snooze" class="quiet">10 分钟后</button></div><small id="agenda-alert-queue"></small></div>';
  document.body.append(popup);
  const removeDialog=document.createElement('dialog');removeDialog.id='agenda-delete-dialog';removeDialog.innerHTML='<div class="dialog-heading"><h2>删除这条日程？</h2><button id="agenda-delete-close" class="icon-button" aria-label="关闭删除日程"><span data-icon="close"></span></button></div><p id="agenda-delete-title"></p><p class="muted">重复日程将停止后续提醒。</p><div class="button-row"><button id="agenda-delete-confirm" class="primary">删除</button><button id="agenda-delete-cancel" class="quiet">保留</button></div>';document.body.append(removeDialog);
  const heroButton=document.createElement('button');heroButton.id='hero-chat';heroButton.className='quiet';heroButton.textContent='聊聊天 / 记日程';$('.hero-tools').append(heroButton);
  function freshStatus(){
    const configured=bridge.api().provider!=='local'&&!!bridge.api().endpoint;
    let needsKey=configured&&!bridge.key();try{if(['localhost','127.0.0.1','[::1]'].includes(new URL(bridge.api().endpoint).hostname))needsKey=false;}catch{}
    $('#agenda-api-badge').textContent=needsKey?'API 已设定 · 待填写 Key':configured?'AI 已设定 · 随聊天记事':'AI 未接入';$('#agenda-api-badge').classList.toggle('ready',configured&&!needsKey);
    $('#agenda-chat-status').textContent=needsKey?'请在设定中补充本次会话的 API Key。':configured?'说出事情和时间，我会整理成日程卡。':'记日程需要接入 AI，也可以先体验流程。';
  }
  function draw(){
    bridge.draw($('#agenda-pet'),bridge.family(),'smile');
    const e=db.events.find(x=>x.id===activeId);if(e&&!popup.hidden)bridge.draw($('#agenda-alert-pet'),e.family,'smile');
  }
  function renderCalendar(){
    const d=new Date(calendarDate);d.setHours(12,0,0,0);const currentMonth=d.getMonth();
    if(calendarView==='week')d.setDate(d.getDate()-(d.getDay()+6)%7);else{d.setDate(1);d.setDate(d.getDate()-(d.getDay()+6)%7);}
    const end=new Date(d);end.setDate(end.getDate()+6);
    $('#agenda-period').textContent=calendarView==='month'?new Date(calendarDate).toLocaleDateString('zh-CN',{year:'numeric',month:'long'}):`${d.getFullYear()}年 ${date(d)} — ${end.getFullYear()!==d.getFullYear()?end.getFullYear()+'年 ':''}${date(end)}`;
    $('#agenda-zone').textContent=G.zone();$('#agenda-days').classList.toggle('month',calendarView==='month');$('#agenda-days').replaceChildren();
    for(let i=0;i<(calendarView==='week'?7:42);i++){
      const dateKey=G.day(d),items=db.events.filter(e=>e.status!=='cancelled'&&G.day(e.startAt)===dateKey),button=document.createElement('button');button.className='agenda-day';button.dataset.agendaDate=dateKey;button.setAttribute('aria-label',`${dateKey}，${items.length} 件日程`);button.setAttribute('aria-pressed',String(selectedDate===dateKey));if(dateKey===G.day(bridge.now()))button.setAttribute('aria-current','date');if(d.getMonth()!==currentMonth&&calendarView==='month')button.classList.add('other-month');
      const num=document.createElement('strong');num.textContent=d.getDate();button.append(num);
      if(items.length){const label=document.createElement('span');label.textContent=items[0].title;const count=document.createElement('small');count.textContent=items.length>1?`+${items.length-1} 件`:'●';button.append(label,count);}
      button.onclick=()=>{selectedDate=dateKey;render();};$('#agenda-days').append(button);d.setDate(d.getDate()+1);
    }
    document.querySelectorAll('[data-agenda-view]').forEach(b=>{b.classList.toggle('selected',b.dataset.agendaView===calendarView);b.setAttribute('aria-pressed',String(b.dataset.agendaView===calendarView));});
  }
  function actionButton(label,fn,kind='text-button'){const b=document.createElement('button');b.type='button';b.className=kind;b.textContent=label;b.onclick=fn;return b;}
  function render(){
    renderStamp=JSON.stringify(db)+G.day(bridge.now())+bridge.family();
    freshStatus();const active=db.events.filter(G.active).sort((a,b)=>G.remindAt(a)-G.remindAt(b)),next=active[0];
    $('#agenda-next-title').textContent=next?.title||'让她帮你记着';$('#agenda-next-time').textContent=next?`${date(next.startAt)} ${time(next.startAt)} · ${name(next.family)} 来提醒`:'告诉宠物要记得的事情和时间。';$('#agenda-next-label').textContent=next?.status==='notified'?'有一件事在等你':'下一件小事';
    $('#agenda-count').textContent=active.length;$('#agenda-today-count').textContent=active.filter(e=>G.day(e.startAt)===G.day(bridge.now())).length;$('#agenda-owner').textContent=name(bridge.family())+' 的小日历';
    renderCalendar();$('#agenda-active').classList.toggle('selected',filter==='active');$('#agenda-history').classList.toggle('selected',filter==='done');$('#agenda-list-title').textContent=selectedDate?date(G.parseLocal(selectedDate+'T12:00')):filter==='active'?'待提醒':'已完成';
    const visible=db.events.filter(e=>(filter==='active'?G.active(e):e.status==='done')&&(!selectedDate||G.day(e.startAt)===selectedDate)).sort((a,b)=>a.startAt-b.startAt);$('#agenda-list').replaceChildren();
    if(!visible.length){const empty=document.createElement('div');empty.className='agenda-empty';empty.innerHTML='<span>☁</span><p></p><small></small>';empty.querySelector('p').textContent=selectedDate?'这一天，还空着':'还没有日程';empty.querySelector('small').textContent='对宠物说一句，她会先把时间整理给你看。';$('#agenda-list').append(empty);}
    for(const e of visible){
      const item=document.createElement('article');item.className='agenda-event';item.dataset.eventId=e.id;
      const stamp=document.createElement('div');stamp.className='agenda-event-time';stamp.textContent=time(e.startAt);const info=document.createElement('div');info.className='agenda-event-info';const title=document.createElement('strong');title.textContent=e.title;const detail=document.createElement('p');detail.textContent=`${date(e.startAt)} · ${name(e.family)} · ${G.repeats[e.repeat]}`;const meta=document.createElement('small');meta.textContent=(e.source==='demo'?'演示日程 · ':'')+(e.snoozeUntil?'稍后 '+time(e.snoozeUntil):e.status==='notified'?'正在提醒':e.status==='done'?'已完成':e.leadMinutes?`提前 ${e.leadMinutes} 分钟`:'准时提醒');info.append(title,detail,meta);
      const actions=document.createElement('div');actions.className='agenda-event-actions';if(G.active(e)){actions.append(actionButton('预览提醒',()=>showAlert(e,true)),actionButton('修改',()=>edit(e)));}actions.append(actionButton('删除',()=>removePrompt(e)));item.append(stamp,info,actions);$('#agenda-list').append(item);
    }
    draw();
  }
  async function transaction(fn){
    const apply=()=>{const next=read(),result=fn(next);localStorage.setItem(key,JSON.stringify(next));db=next;return result;};
    const result=navigator.locks?await navigator.locks.request(key,apply):apply();render();return result;
  }
  function draftFields(value){$('#agenda-title').value=value.title;$('#agenda-start').value=value.startLocal;$('#agenda-lead').value=value.leadMinutes;$('#agenda-repeat').value=value.repeat;}
  function openDraft(value,{family=bridge.family(),source='api',line='',id=null}={}){
    pending={...value,family,source,id};card.hidden=false;$('#agenda-draft-form').hidden=false;$('#agenda-question').hidden=true;$('#agenda-answer-sample').hidden=true;$('#agenda-saved-open').hidden=true;$('#agenda-draft-source').textContent=source==='demo'?'流程演示 · 尚未保存':'AI 日程卡 · 尚未保存';$('#agenda-user-line').textContent=line;$('#agenda-confirm-line').textContent=`${name(family)} 来提醒 · ${G.zone()} · 确认后才开始计时`;$('#agenda-draft-result').textContent='';$('#agenda-confirm').textContent=id?'保存修改':'收进行事历';draftFields(value);card.scrollIntoView({block:'nearest',behavior:'smooth'});
  }
  function invalidate(){previewTicket++;pending=null;card.hidden=true;$('#agenda-draft-result').textContent='';}
  async function saveDraft(event){
    event.preventDefault();if(!pending)return;const ticket=previewTicket,p={...pending},form={...p,title:$('#agenda-title').value,startLocal:$('#agenda-start').value,leadMinutes:Number($('#agenda-lead').value),repeat:$('#agenda-repeat').value};$('#agenda-confirm').disabled=true;
    try{
      const saved=await transaction(s=>G.put(s,form,{family:p.family,source:p.source,id:p.id,now:bridge.now()}));if(ticket!==previewTicket)return;pending=null;$('#agenda-draft-form').hidden=true;$('#agenda-draft-source').textContent=p.source==='demo'?'演示日程 · 已保存':'已收进行事历';$('#agenda-draft-result').textContent=`${date(saved.startAt)} ${time(saved.startAt)} · ${saved.leadMinutes?'提前 '+saved.leadMinutes+' 分钟':'准时提醒'}`;$('#agenda-saved-open').hidden=false;selectedDate=G.day(saved.startAt);calendarDate=saved.startAt;bridge.speak('记好了，这件事交给我。到时间会笑着来提醒你。','smile',5000);render();
    }catch(error){$('#agenda-draft-result').textContent=error.message||'没有保存成功，请检查浏览器存储。';}finally{$('#agenda-confirm').disabled=false;}
  }
  function edit(e){bridge.select(e.family);bridge.go('interaction');openDraft({...e,startLocal:G.local(e.startAt)},{family:e.family,source:e.source,id:e.id,line:'修改日程；重复日程会一同更新。'});}
  function removePrompt(e){$('#agenda-delete-title').textContent=e.title;$('#agenda-delete-confirm').onclick=async()=>{try{await transaction(s=>G.remove(s,e.id));if(activeId===e.id){activeId=null;popup.hidden=true;}removeDialog.close();bridge.toast('日程已删除');poll();}catch{bridge.toast('没有删除成功，请检查浏览器存储。');}};removeDialog.showModal();}
  function showAlert(e,isPreview=false){
    if(activeId&&!preview&&isPreview)return bridge.toast('先处理当前提醒，再预览其他日程。');activeId=e.id;preview=isPreview;popup.hidden=false;
    $('#agenda-alert-label').textContent=isPreview?'提醒预览':bridge.now()>e.startAt+60000?'补提醒 · '+name(e.family):name(e.family)+' 来提醒你啦';$('#agenda-alert-title').textContent=e.title;$('#agenda-alert-time').textContent=`${date(e.startAt)} ${time(e.startAt)}`;$('#agenda-alert-text').textContent=e.reminder||'笑着提醒你一下，这件事到时间啦。';$('#agenda-alert-done').textContent=isPreview?'结束预览':'已完成';$('#agenda-alert-snooze').hidden=isPreview;$('#agenda-alert-queue').textContent=isPreview?'预览不会改变日程时间或完成状态。':(G.due(db,bridge.now()).length>1?`还有 ${G.due(db,bridge.now()).length-1} 条提醒，逐条查看`:'');draw();
  }
  async function handleAlert(snooze){
    if(preview){popup.hidden=true;activeId=null;preview=false;return;}
    const id=activeId;try{await transaction(s=>snooze?G.snooze(s,id,bridge.now()):G.finish(s,id,bridge.now()));activeId=null;popup.hidden=true;bridge.toast(snooze?'10 分钟后再提醒':'已经记好啦');poll();}catch(error){bridge.toast(error.message||'没有保存成功，请重试。');}
  }
  async function poll(){
    if(busy||document.hidden)return;busy=true;
    try{
      db=read();if(preview){if(!G.due(db,bridge.now()).length)return;preview=false;activeId=null;popup.hidden=true;}const current=db.events.find(e=>e.id===activeId);if(activeId&&(!current||current.status!=='notified')){activeId=null;popup.hidden=true;}
      if(!activeId){const next=G.due(db,bridge.now())[0];if(next){const ok=await transaction(s=>G.deliver(s,next.id,bridge.now()));if(ok)showAlert(db.events.find(e=>e.id===next.id));}}
      if(!section.hidden&&renderStamp!==JSON.stringify(db)+G.day(bridge.now())+bridge.family())render();else freshStatus();
    }catch{/* No success claims when browser storage is unavailable. */}finally{busy=false;}
  }
  async function demo(kind='meeting'){
    bridge.cancelChat();invalidate();bridge.go('interaction');if(bridge.paused())return bridge.toast('先继续陪伴吧。');const ticket=++previewTicket,family=bridge.family();
    card.hidden=false;$('#agenda-draft-form').hidden=true;$('#agenda-saved-open').hidden=true;$('#agenda-question').hidden=true;$('#agenda-answer-sample').hidden=true;$('#agenda-draft-source').textContent='流程演示 · 不调用 API';$('#agenda-user-line').textContent=kind==='unclear'?'你：明天提醒我买牛奶。':'你：'+G.sample(kind,bridge.now()).line;bridge.think();
    await new Promise(r=>setTimeout(r,1000));if(ticket!==previewTicket||family!==bridge.family()||bridge.paused())return;
    if(kind==='unclear'){
      const question='明天几点提醒你比较合适？';$('#agenda-question').textContent=question;$('#agenda-question').hidden=false;$('#agenda-answer-sample').hidden=false;bridge.speak(question,'talk',5000);$('#agenda-answer-sample').onclick=()=>{const s=G.sample('meeting',bridge.now());s.event.title='买牛奶';s.event.leadMinutes=0;openDraft(s.event,{source:'demo',line:'你：明天下午 3 点，提醒我买牛奶。'});bridge.speak('好呀，看看这张日程卡。','talk',4000);};
    }else{const s=G.sample(kind,bridge.now());openDraft(s.event,{source:'demo',line:'你：'+s.line});bridge.speak('我整理成卡片啦，看看时间对不对。','talk',4500);}
    bridge.endThinking();card.scrollIntoView({block:'nearest',behavior:'smooth'});
  }
  async function ask(messages,persona,{signal}={}){
    const response=await CompanionProviders.send(bridge.api(),bridge.key(),messages,persona+G.prompt(bridge.now()),{signal});return G.envelope(response,bridge.now());
  }
  function receive(result,line){invalidate();if(result.kind==='event')openDraft(result.event,{source:'api',line:'你：'+line});}
  $('#agenda-draft-form').onsubmit=saveDraft;$('#agenda-discard').onclick=()=>{invalidate();bridge.speak('好，先不记。','talk',2500);};
  for(const id of ['#agenda-chat-open','#agenda-saved-open'])$(id).onclick=()=>bridge.go('agenda');
  for(const id of ['#hero-chat','#agenda-talk'])$(id).onclick=()=>{bridge.go('interaction');$('#chat-input').focus();};
  $('#agenda-settings').onclick=()=>{bridge.go('settings');$('#api-provider').focus();};$('#agenda-chat-sample').onclick=()=>demo();
  document.querySelectorAll('[data-agenda-sample]').forEach(b=>b.onclick=()=>demo(b.dataset.agendaSample));
  $('#agenda-all').onclick=()=>{selectedDate='';render();};$('#agenda-active').onclick=()=>{filter='active';render();};$('#agenda-history').onclick=()=>{filter='done';render();};
  document.querySelectorAll('[data-agenda-view]').forEach(b=>b.onclick=()=>{calendarView=b.dataset.agendaView;render();});
  const move=direction=>{const d=new Date(calendarDate);if(calendarView==='week')d.setDate(d.getDate()+direction*7);else{d.setDate(1);d.setMonth(d.getMonth()+direction);}calendarDate=d.getTime();render();};
  $('#agenda-prev').onclick=()=>move(-1);$('#agenda-next').onclick=()=>move(1);$('#agenda-today').onclick=()=>{calendarDate=bridge.now();selectedDate=G.day(calendarDate);render();};
  $('#agenda-alert-done').onclick=()=>handleAlert(false);$('#agenda-alert-snooze').onclick=()=>handleAlert(true);
  for(const id of ['#agenda-delete-close','#agenda-delete-cancel'])$(id).onclick=()=>removeDialog.close();
  window.addEventListener('storage',event=>{if(event.key===key){try{db=read();render();poll();}catch{bridge.toast('日程读取失败，请刷新页面。');}}});document.addEventListener('visibilitychange',poll);setInterval(poll,1000);render();
  return {render,draw,ask,receive,invalidate,poll,snapshot:()=>({events:structuredClone(db.events),pending:pending?{...pending}:null,alertId:activeId,preview,view:calendarView,selectedDate}),blocksHide:()=>!card.hidden&&($('#agenda-draft-form').hidden===false||!$('#agenda-question').hidden)};
}};
