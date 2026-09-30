(() => {
  'use strict';
  const $ = id => document.getElementById(id);
  const data = window.TASKBAR_FRAMES;
  const controller = new window.TaskbarLift.LiftController();
  const stage = $('stage'), pet = $('pet'), grip = $('grip'), sprite = $('sprite');
  const styles = [['chibi', 'Q版'], ['realistic', '3D真人']];
  const outfits = [['original', '原装'], ['swim', '泳装'], ['wedding', '婚纱']];
  let style = 'realistic', outfit = 'original', demo = null, lastImage = '', latestStatus = '';
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
    for (const source of new Set([clips.idle[0], ...clips.pickup, ...clips.land])) {
      const image = new Image(); image.src = source;
    }
  }
  function currentFrame(now) {
    const clips = appearance(); if (!clips) return '';
    if (controller.mode === 'dragging') {
      const elapsed = now - controller.held.pressedAt;
      return clips.pickup[Math.min(clips.pickup.length - 1, Math.floor(elapsed / 40))];
    }
    if (controller.mode === 'dropping') return clips.pickup.at(-1);
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
    $('shadow').style.left = (stageWidth / 2 + controller.x) + 'px';
    $('shadow').style.width = (90 + lift * .17) + 'px';
    $('shadow').style.opacity = String(Math.max(.16, 1 - lift / 340));
    stage.dataset.mode = mode;
    $('heightmark').textContent = `离任务栏 ${Math.round(lift)} px`;
    $('rise').style.width = Math.min(100, lift / 230 * 100) + '%';
    const hover = mode === 'dragging' ? Math.max(0, now - controller.held.lastMotion) : 0;
    $('metrics').textContent = `高度 ${Math.round(lift)} px · 停留 ${Math.round(hover)} ms`;
    const frame = currentFrame(now); if (frame && frame !== lastImage) { sprite.src = frame; lastImage = frame; }
    if (mode === 'dragging') {
      const place = controller.canPlace(now);
      $('stateLabel').textContent = '抱在半空';
      $('intentLabel').textContent = controller.originTaskbar ? '从任务栏抓起' : '重新抓取';
      $('outcome').textContent = lift < 24 ? '松手留在任务栏' : place ? '松手会固定' : '松手会掉下';
      $('why').textContent = lift < 24 ? '提起高度还不够，回到原位。' : place ?
        controller.originTaskbar ? '已停留半秒或缓慢移动；Shift 也可以直接固定。' : '这次是半空调整，不再自动下落。' :
        '快速从任务栏往上抓起，尚未确认摆放位置。';
      setStatus(lift < 24 ? '继续向上提；到半空后松手可试下落。' : place ? '这是主动摆放：松手后会停在这里。' : '现在松手：会自然落回任务栏。');
    } else {
      $('stateLabel').textContent = { grounded:'任务栏待机',dropping:'自然下落',landing:'轻轻落地',placed:'手动摆放' }[mode];
      $('intentLabel').textContent = controller.lastDecision;
      $('outcome').textContent = { grounded:'已回到任务栏',dropping:'正在落下',landing:'已经着地',placed:'停在新位置' }[mode];
      $('why').textContent = mode === 'placed' ? '当前位置由你选定；之后不会自己跳回任务栏。' :
        mode === 'dropping' ? '松手后才启用重力。掉落途中仍能再抓住。' :
        mode === 'landing' ? '落地缓冲后回到待机。' : '从任务栏抓住她，再试不同提起方式。';
      setStatus(controller.lastDecision);
    }
  }
  function reset() { demo = null; controller.reset(); render(performance.now()); }
  $('reset').onclick = reset;
  grip.onpointerdown = event => {
    if (event.button !== 0) return;
    event.preventDefault(); demo = null;
    grip.setPointerCapture(event.pointerId);
    controller.down(event.clientX, event.clientY, performance.now());
    render(performance.now());
  };
  grip.onpointermove = event => {
    if (controller.mode !== 'dragging') return;
    controller.move(event.clientX, event.clientY, performance.now()); render(performance.now());
  };
  function release(event, pin) {
    if (controller.mode !== 'dragging') return;
    controller.release(performance.now(), pin || !!event?.shiftKey); render(performance.now());
  }
  grip.onpointerup = event => release(event, false);
  grip.onpointercancel = event => release(event, true);
  grip.onlostpointercapture = event => release(event, true);
  grip.onkeydown = event => {
    if (event.key !== 'Enter' && event.key !== ' ') return;
    event.preventDefault(); demo = null;
    const now = performance.now();
    if (controller.mode === 'dragging') controller.release(now, true);
    else { controller.down(0, 0, now); controller.move(0, -130, now + 100); controller.release(now + 100, true); }
    render(now);
  };
  function showcase(kind) {
    reset(); const start = performance.now();
    const down = at => controller.down(0, 0, start + at);
    const move = (at, y) => controller.move(0, y, start + at);
    const up = at => controller.release(start + at);
    const events = kind === 'drop' ? [[50, down], [225, t => move(t, -150)], [350, up]] :
      kind === 'place' ? [[50, down], [250, t => move(t, -35)], [480, t => move(t, -90)],
        [730, t => move(t, -150)], [1230, up]] :
        [[50, down], [225, t => move(t, -155)], [350, up],
          [620, t => { controller.down(0, -75, start + t); }],
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
