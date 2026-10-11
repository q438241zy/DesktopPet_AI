/* Real-time, isolated-browser audit of gallery cards. Never advances/fixes the clock. */
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const { pathToFileURL } = require('node:url');
const root = path.resolve(__dirname, '..');
let chromium;
try { ({ chromium } = require('playwright')); }
catch { ({ chromium } = require(path.join(process.env.USERPROFILE, '.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright'))); }
const target = path.resolve(process.env.CARD_IDLE_TARGET || path.join(root, 'Release/win-x64/Demo/CompanionV01/index.html'));
const output = path.resolve(process.env.CARD_IDLE_OUTPUT || path.join(root, '.artifacts/companion-v19-cards/baseline'));
const duration = Number(process.env.CARD_IDLE_DURATION || 12000);
const independent = process.argv.includes('--expect-independent');
const families = ['whale', 'gpt', 'claude', 'gemini', 'grok', 'qwen', 'zhipu', 'kimi'];
const styles = ['chibi', 'realistic'];
const outfits = ['original', 'sports', 'swim', 'wedding'];
const storageKey = 'cloud-companions.demo.v01.state';
const checksum = file => crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
fs.mkdirSync(output, { recursive: true });

async function sample(page, milliseconds, warmup = true) {
  return page.evaluate(async ({ milliseconds, warmup }) => {
    const rows = new Map();
    function hash(canvas) {
      const rgba = canvas.getContext('2d').getImageData(0, 0, canvas.width, canvas.height).data;
      let h = 2166136261;
      for (let i = 0; i < rgba.length; i += 64) {
        h = Math.imul(h ^ rgba[i], 16777619);
        h = Math.imul(h ^ rgba[i + 1], 16777619);
        h = Math.imul(h ^ rgba[i + 2], 16777619);
        h = Math.imul(h ^ rgba[i + 3], 16777619);
      }
      return (h >>> 0).toString(16);
    }
    // Chrome may switch a repeatedly-read canvas from GPU to CPU rasterization.
    // Warm readbacks before starting the observation window so its one-time
    // subpixel rounding difference is not mistaken for a character action.
    if (warmup) {
      for (const canvas of document.querySelectorAll('.partner-card canvas')) { hash(canvas); hash(canvas); }
      await new Promise(resolve => setTimeout(resolve, 160));
    }
    const started = performance.now();
    const wallStart = Date.now();
    while (performance.now() - started < milliseconds) {
      for (const card of document.querySelectorAll('.partner-card')) {
        const canvas = card.querySelector('canvas');
        const id = card.dataset.family;
        if (!rows.has(id)) rows.set(id, []);
        const samples = rows.get(id);
        const next = { ms: Math.round(performance.now() - started), pose: canvas.dataset.pose, frame: canvas.dataset.frame, file: canvas.dataset.file, pixels: hash(canvas) };
        const previous = samples.at(-1);
        if (!previous || previous.pose !== next.pose || previous.frame !== next.frame || previous.pixels !== next.pixels) samples.push(next);
      }
      await new Promise(resolve => setTimeout(resolve, 90));
    }
    return { elapsed: performance.now() - started, wallElapsed: Date.now() - wallStart, cards: Object.fromEntries(rows) };
  }, { milliseconds, warmup });
}

(async () => {
  const exe = path.join(root, 'Release/win-x64/DesktopPet.exe');
  const nativeBefore = checksum(exe);
  const browser = await chromium.launch({ executablePath: path.join(process.env.ProgramFiles, 'Google/Chrome/Application/chrome.exe'), headless: true, args: ['--allow-file-access-from-files'] });
  const report = { target, realClock: true, acceleratedClock: false, trials: [], errors: [], nativeBefore };
  async function trial(style, outfit, mode, milliseconds = duration) {
    const context = await browser.newContext({ viewport: { width: 1440, height: 1100 } });
    const page = await context.newPage();
    page.on('pageerror', error => report.errors.push(error.message));
    await page.route(/^https?:/, route => route.abort());
    const saved = { schema: 1, selected: 'whale', style, outfits: Object.fromEntries(families.flatMap(id => styles.map(style => [id + '/' + style, outfit]))), postures: Object.fromEntries(families.map((id, i) => [id, mode === 'fixed' ? (i % 2 ? 'sit' : 'stand') : 'auto'])), settings: { autoHide: false, reducedMotion: mode === 'reduced', calendarView: 'month' } };
    await page.addInitScript(({ storageKey, saved }) => localStorage.setItem(storageKey, JSON.stringify(saved)), { storageKey, saved });
    await page.goto(pathToFileURL(target).href);
    await page.waitForFunction(() => globalThis.companionDemo && [...document.querySelectorAll('.partner-card canvas')].length === 8 && [...document.querySelectorAll('.partner-card canvas')].every(canvas => canvas.dataset.drawKey));
    // A draft from the conversation remains in the DOM when returning to the
    // gallery. Seed that existing state without clicking a hidden input.
    if (mode === 'draft') await page.evaluate(() => { document.querySelector('#chat-input').value = '尚未发送的草稿'; });
    const before = await page.evaluate(() => companionDemo.snapshot());
    await page.locator('#partner-grid').screenshot({ path: path.join(output, `${style}-${outfit}-${mode}-before.png`) });
    const samples = await sample(page, milliseconds);
    const after = await page.evaluate(() => companionDemo.snapshot());
    await page.locator('#partner-grid').screenshot({ path: path.join(output, `${style}-${outfit}-${mode}-after.png`) });
    const summary = Object.fromEntries(Object.entries(samples.cards).map(([id, values]) => [id, { poses: [...new Set(values.map(value => value.pose))], distinctPixels: new Set(values.map(value => value.pixels)).size, renderedFrames: values.length }]));
    const heroPose = await page.locator('#hero-canvas').getAttribute('data-pose');
    const item = { style, outfit, mode, ...samples, summary, desktopPostureMode: before.postureMode, heroPose, idleSinceUnchanged: before.runtime.idleSince === after.runtime.idleSince };
    report.trials.push(item);
    if (independent && style === 'chibi' && mode === 'fixed') {
      const click = await page.evaluate(() => {
        const before = companionDemo.snapshot();
        document.querySelector('.partner-card[data-family="gpt"]').click();
        const after = companionDemo.snapshot();
        return { before: before.gallery, after: after.gallery };
      });
      await page.waitForFunction(() => document.querySelector('#hero-canvas').dataset.pose === 'sit');
      await page.locator('#cards-motion').click();
      await page.waitForTimeout(250);
      const paused = await sample(page, 2400);
      const pauseState = await page.evaluate(() => companionDemo.snapshot().gallery);
      await page.locator('#cards-motion').click();
      const resumed = await sample(page, 2800, false);
      report.boundaries = { click, pauseState, paused, resumed };
    }
    fs.writeFileSync(path.join(output, 'report.json'), JSON.stringify(report, null, 2));
    console.log(JSON.stringify({ style, outfit, mode, elapsed: samples.elapsed, cards: summary }));
    await context.close();
  }
  try {
    for (const style of styles) for (const outfit of outfits) await trial(style, outfit, 'auto');
    for (const style of styles) await trial(style, 'sports', 'fixed', 7500);
    await trial('chibi', 'sports', 'draft', 7500);
    await trial('realistic', 'sports', 'reduced', 3000);
    report.nativeAfter = checksum(exe);
    report.nativeUnchanged = report.nativeBefore === report.nativeAfter;
    report.failures = [];
    for (const trial of report.trials) {
      for (const [id, item] of Object.entries(trial.summary)) {
        const expectedAnimated = trial.mode === 'auto' || independent && ['fixed', 'draft'].includes(trial.mode);
        if (expectedAnimated && (item.poses.length < 3 || item.distinctPixels < 3)) report.failures.push(`${trial.style}/${trial.outfit}/${trial.mode}/${id}: only ${item.poses.length} poses and ${item.distinctPixels} pixel states`);
        if (trial.mode === 'reduced' && item.renderedFrames > 1) report.failures.push(`${id}: reduced motion did not stop the gallery`);
      }
      if (independent && trial.mode === 'fixed' && trial.heroPose !== 'stand') report.failures.push(`${trial.style}: fixed hero posture was overridden`);
      if (!trial.idleSinceUnchanged) report.failures.push(`${trial.style}/${trial.outfit}/${trial.mode}: gallery changed the user inactivity clock`);
    }
    if (independent) {
      const boundaries = report.boundaries;
      if (!boundaries?.click?.before?.clocks || !boundaries?.click?.after?.clocks) report.failures.push('Gallery clock diagnostics missing');
      else for (const id of families) {
        const before = boundaries.click.before.clocks[id], after = boundaries.click.after.clocks[id];
        if (before.started !== after.started || before.next !== after.next || before.pose !== after.pose) report.failures.push(`${id}: selecting a partner reset its gallery clock`);
      }
      if (!boundaries?.pauseState?.paused) report.failures.push('Gallery pause state was not reported');
      for (const [id, values] of Object.entries(boundaries?.paused.cards || {})) if (values.length !== 1) report.failures.push(`${id}: gallery kept animating while paused`);
      for (const [id, values] of Object.entries(boundaries?.resumed.cards || {})) {
        const firstChange = values.find(value => value.pose !== values[0].pose);
        if (!firstChange || firstChange.ms < 850 || firstChange.ms > 2500) report.failures.push(`${id}: resume did not wait a fresh 1–2-second interval`);
      }
    }
    if (!report.nativeUnchanged) report.failures.push('Native executable changed during audit');
    if (report.errors.length) report.failures.push(...report.errors);
    fs.writeFileSync(path.join(output, 'report.json'), JSON.stringify(report, null, 2));
    if (report.failures.length) throw Error(report.failures.join('\n'));
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
