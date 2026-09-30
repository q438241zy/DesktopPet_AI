(function (root, factory) {
  const api = factory();
  if (typeof module === 'object' && module.exports) module.exports = api;
  root.TaskbarLift = api;
})(globalThis, function () {
  const clamp = (value, low, high) => Math.min(high, Math.max(low, value));
  class LiftController {
    constructor() { this.reset(); }
    reset() {
      this.mode = 'grounded'; this.lift = 0; this.x = 0; this.speed = 0;
      this.held = null; this.originTaskbar = false; this.pinRequested = false;
      this.lastDecision = '在任务栏待机'; this.landingUntil = 0; this.lastTick = 0;
    }
    down(x, y, now) {
      const caught = this.mode === 'dropping';
      this.originTaskbar = !caught && this.lift <= 24;
      this.held = { x, y, lift: this.lift, offset: this.x, pressedAt: now,
        motionX: x, motionY: y, lastMotion: now };
      this.pinRequested = false; this.speed = 0; this.mode = 'dragging';
      this.lastDecision = caught ? '半空接住：这次松手会按手动放置' :
        this.originTaskbar ? '从任务栏抓起：快速松手会落下' : '从半空抓起：松手会留在新位置';
      return this.lastDecision;
    }
    move(x, y, now) {
      if (this.mode !== 'dragging' || !this.held) return;
      const h = this.held;
      this.lift = clamp(h.lift + h.y - y, 0, 230);
      this.x = clamp(h.offset + x - h.x, -165, 165);
      if (Math.hypot(x - h.motionX, y - h.motionY) >= 10) {
        h.motionX = x; h.motionY = y; h.lastMotion = now;
      }
    }
    canPlace(now) {
      if (this.mode !== 'dragging' || !this.held || this.lift < 24) return false;
      return !this.originTaskbar || this.pinRequested ||
        now - this.held.pressedAt >= 900 || now - this.held.lastMotion >= 450;
    }
    release(now, pin = false) {
      if (this.mode !== 'dragging' || !this.held) return this.lastDecision;
      this.pinRequested ||= pin;
      const place = this.canPlace(now);
      this.held = null;
      if (this.lift < 24) {
        this.lift = 0; this.mode = 'grounded'; this.lastDecision = '仍在任务栏：保持待机';
      } else if (this.originTaskbar && !place) {
        this.mode = 'dropping'; this.speed = 30; this.lastTick = now;
        this.lastDecision = '快速提起后松手：自然落回任务栏';
      } else {
        this.mode = 'placed'; this.speed = 0;
        this.lastDecision = this.pinRequested ? '按 Shift：固定在这里' :
          this.originTaskbar ? '停留或缓慢移动：固定在这里' : '主动调整位置：留在这里';
      }
      return this.lastDecision;
    }
    tick(now, reducedMotion = false) {
      if (this.mode === 'dropping') {
        if (reducedMotion) { this.lift = 0; this.mode = 'landing'; this.landingUntil = now + 150; }
        else {
          let remaining = Math.min(.12, Math.max(0, (now - this.lastTick) / 1000));
          while (remaining > 0 && this.lift > 0) {
            const dt = Math.min(.016, remaining);
            this.speed = Math.min(850, this.speed + 1050 * dt);
            this.lift = Math.max(0, this.lift - this.speed * dt);
            remaining -= dt;
          }
          if (this.lift === 0) { this.mode = 'landing'; this.landingUntil = now + 410; this.speed = 0; }
        }
      } else if (this.mode === 'landing' && now >= this.landingUntil) {
        this.mode = 'grounded'; this.lastDecision = '落地完成：继续在任务栏待机';
      }
      this.lastTick = now;
      return this.mode;
    }
  }
  return { LiftController };
});
