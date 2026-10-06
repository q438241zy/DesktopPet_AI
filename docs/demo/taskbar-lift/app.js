(() => {
  'use strict';
  const $ = id => document.getElementById(id);
  const data = window.TASKBAR_FRAMES;
  const controller = new window.TaskbarLift.LiftController();
  const stage = $('stage'), pet = $('pet'), grip = $('grip'), sprite = $('sprite');
  const styles = [['chibi', 'Q版'], ['realistic', '3D真人']];
  const outfits = [['original', '原装'], ['swim', '泳装'], ['wedding', '婚纱'], ['sports', '短袖运动服']].filter(([key])=>styles.every(([style])=>data.appearances[style+'-'+key]));
  let style = 'realistic', outfit = 'original', demo = null, lastImage = '', latestStatus = '', shiftHeld = false;
  const appearance = () => data.appearances[style + '-' + outfit];
  function button(label, active, onClick) {
    const element = document.createElement('button'); element.textContent = label;
    element.setAttribute('aria-pressed', active); element.onclick = onClick; return element;
  }
  function selectors() {
    $('styles').replaceChildren(...styles.map(([key, label]) => button(label, key === style, () => {
      style = key; demo = null; controller.reset(); selectors(); preload(); render(performance.now());
    })));
    $('outfits').replaceChildren(...outfits.map(([key, label]) => button(label, key === outfit, () => {
      outfit = key; demo = null; controller.reset(); selectors(); preload(); render(performance.now());
    })));
  }
  function preload() {
    const clips = appearance();
    if (!clips) return;
    controller.walkSpeed = clips.walkSpeed;
    for (const source of new Set([clips.idle[0], ...clips.pickup, ...clips.land, ...clips.walk])) {
      const image = new Image(); image.src = source;
    }
  }
  function currentFrame(now) {
    const clips = appearance(); if (!clips) return '';
    if (controller.mode === 'dragging') {
      const elapsed = now - controller.pickupStartedAt;
      return clips.pickup[Math.min(clips.pickup.length - 1, Math.floor(elapsed / 40))];
    }
    if (controller.mode === 'dropping') return clips.pickup.at(-1);
    if (controller.mode === 'walking') return clips.walk[Math.floor(controller.walkMilliseconds % clips.walkCycleMs / 40)];
    if (controller.mode === 'landing') {
      const elapsed = 410 - Math.max(0, controller.landingUntil - now);
      return clips.land[Math.min(clips.land.length - 1, Math.max(0, Math.floor(elapsed / 40)))];
    }
    return clips.idle[0];
  }
  function setStatus(value) { if (value !== latestStatus) { $('statusText').textContent = value; latestStatus = value; } }
  function render(now) {
    const mode = controller.mode, lift = controller.lift;
    const stageWidth = stage.clientWidth, stageHeight = stage.clientHeight;
    pet.style.left = (stageWidth / 2 - 252 + controller.x) + 'px';
    pet.style.top = (stageHeight - 72 - 421.2 - lift) + 'px';
    pet.style.transform = mode === 'landing' && controller.landingUntil - now > 275 ? 'scaleY(.96)' : '';
    sprite.style.transform = mode === 'walking' ? `scaleX(${controller.direction})` : '';
    $('shadow').style.left = (stageWidth / 2 + controller.x) + 'px';
    $('shadow').style.width = (90 + lift * .17) + 'px';
    $('shadow').style.opacity = String(Math.max(.16, 1 - lift / 340));
    stage.dataset.mode = mode;
    $('heightmark').textContent = `离任务栏 ${Math.round(lift)} px`;
    $('rise').style.width = Math.min(100, lift / 230 * 100) + '%';
    $('metrics').textContent = `高度 ${Math.round(lift)} px · Shift ${shiftHeld ? '按住' : '未按'}`;
    const frame = currentFrame(now); if (frame && frame !== lastImage) { sprite.src = frame; lastImage = frame; }
    if (mode === 'dragging') {
      $('stateLabel').textContent = '抱在半空';
      $('intentLabel').textContent = controller.originTaskbar ? '从任务栏抓起' : '重新抓取';
      $('outcome').textContent = lift <= 24 ? '落地后自动散步' : shiftHeld ? '松手会下落' : '松手会停住';
      $('why').textContent = lift <= 24 ? '距任务栏 24 px 内，松手会吸附到地面。' :
        shiftHeld ? '正在按住 Shift；松手后启用重力。' : '普通松手停在当前位置，快慢与停留时间都不影响结果。';
      setStatus(lift <= 24 ? '继续向上提；按住 Shift 松手可试下落。' :
        shiftHeld ? '按住 Shift 松手：自然落回任务栏。' : '普通松手：停在这里。');
    } else {
      $('stateLabel').textContent = { grounded:'清醒待机',walking:'任务栏散步',dropping:'自然下落',landing:'轻轻落地',placed:'手动摆放' }[mode];
      $('intentLabel').textContent = controller.lastDecision;
      $('outcome').textContent = { grounded:'已回到任务栏',walking:'沿任务栏走动',dropping:'正在落下',landing:'已经着地',placed:'停在新位置' }[mode];
      $('why').textContent = mode === 'placed' ? '当前位置由你选定；之后不会自己跳回任务栏。' :
        mode === 'dropping' ? '松手后才启用重力。掉落途中仍能再抓住。' :
        mode === 'landing' ? '落地缓冲后自动散步。' :
        mode === 'walking' ? '碰到任务栏后自动走动，不需要先打卡。普通放在半空仍会停住。' : '清醒待机；可以继续抓住她，稍后也会再走走。';
      setStatus(controller.lastDecision);
    }
  }
  function reset() { demo = null; shiftHeld = false; controller.reset(); render(performance.now()); }
  $('reset').onclick = reset;
  grip.onpointerdown = event => {
    if (event.button !== 0) return;
    event.preventDefault(); demo = null; shiftHeld = !!event.shiftKey;
    grip.setPointerCapture(event.pointerId);
    controller.down(event.clientX, event.clientY, performance.now());
    render(performance.now());
  };
  grip.onpointermove = event => {
    if (controller.mode !== 'dragging') return;
    shiftHeld = !!event.shiftKey;
    controller.move(event.clientX, event.clientY); render(performance.now());
  };
  function release(dropOnRelease, resumeOnFloor = true) {
    if (controller.mode !== 'dragging') return;
    controller.release(performance.now(), dropOnRelease, resumeOnFloor); render(performance.now());
  }
  grip.onpointerup = event => release(!!event.shiftKey);
  grip.onpointercancel = () => release(false, false);
  grip.onlostpointercapture = () => release(false, false);
  window.addEventListener('keydown', event => { shiftHeld = event.shiftKey; render(performance.now()); });
  window.addEventListener('keyup', event => { shiftHeld = event.shiftKey; render(performance.now()); });
  window.addEventListener('blur', () => { shiftHeld = false; release(false, false); });
  grip.onkeydown = event => {
    if (event.key !== 'Enter' && event.key !== ' ') return;
    event.preventDefault(); demo = null;
    const now = performance.now();
    shiftHeld = !!event.shiftKey;
    if (controller.mode === 'dragging') controller.release(now, shiftHeld);
    else { controller.down(0, 0, now); controller.move(0, -130); controller.release(now, shiftHeld); }
    render(now);
  };
  function showcase(kind) {
    reset(); const start = performance.now();
    const down = at => controller.down(0, 0, start + at);
    const move = (at, y) => controller.move(0, y, start + at);
    const up = at => controller.release(start + at, false);
    const drop = at => controller.release(start + at, true);
    shiftHeld = kind !== 'place';
    const events = kind === 'drop' ? [[50, down], [225, t => move(t, -150)], [350, drop]] :
      kind === 'place' ? [[50, down], [225, t => move(t, -150)], [350, up]] :
        [[50, down], [225, t => move(t, -155)], [350, drop],
          [620, t => { shiftHeld = false; controller.down(0, -75, start + t); }],
          [840, t => controller.move(0, -120, start + t)], [1480, up]];
    demo = { start, next: 0, events };
  }
  $('showDrop').onclick = () => showcase('drop');
  $('showPlace').onclick = () => showcase('place');
  $('showCatch').onclick = () => showcase('catch');
  function frame(now) {
    if (demo) {
      while (demo.next < demo.events.length && demo.start + demo.events[demo.next][0] <= now) {
        const [at, action] = demo.events[demo.next++]; controller.tick(demo.start + at);
        action(at);
      }
      if (demo.next === demo.events.length && now - demo.start > 2500) demo = null;
    }
    controller.tick(now, window.matchMedia('(prefers-reduced-motion: reduce)').matches);
    render(now);
  }
  const query = new URLSearchParams(location.search);
  if (styles.some(([key]) => key === query.get('style'))) style = query.get('style');
  if (outfits.some(([key]) => key === query.get('outfit'))) outfit = query.get('outfit');
  selectors(); preload();
  const preset = query.get('scenario');
  if (['drop', 'place', 'catch'].includes(preset)) showcase(preset);
  setInterval(() => frame(performance.now()), 16);
})();
