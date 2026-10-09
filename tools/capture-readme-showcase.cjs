/* Capture only an isolated HTML page, never the desktop or a personal profile. */
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const {pathToFileURL} = require('node:url');
let playwright;
try { playwright = require('playwright'); }
catch { playwright = require(path.join(process.env.USERPROFILE, '.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright')); }
const root = path.resolve(__dirname, '..');
const target = path.join(root, 'Release/win-x64/Demo/CompanionV01/index.html');
const output = path.join(root, 'docs/images/v1.0');
const evidence = path.join(root, '.artifacts/v1.0-showcase');
const versionXml = fs.readFileSync(path.join(root, 'Version.props'), 'utf8');
const version = ['Major', 'Minor'].map(p => versionXml.match(new RegExp(`<DesktopPet${p}>(\\d+)</DesktopPet${p}>`))[1]).join('.');
const captures = [], errors = [], network = [];
fs.mkdirSync(output, {recursive: true});
fs.mkdirSync(evidence, {recursive: true});
function record(name, kind) {
  const bytes = fs.readFileSync(path.join(output, name));
  // PNG written by Chromium canvas/screenshot must not carry EXIF or text metadata.
  const chunks = [];
  for (let offset = 8; offset < bytes.length;) {
    const length = bytes.readUInt32BE(offset), type = bytes.toString('ascii', offset + 4, offset + 8);
    chunks.push(type); offset += length + 12;
  }
  assert(!chunks.some(x => ['eXIf', 'tEXt', 'zTXt', 'iTXt'].includes(x)), name);
  captures.push({file: name, kind, bytes: bytes.length, sha256: crypto.createHash('sha256').update(bytes).digest('hex'), chunks});
}
(async () => {
  const executablePath = process.env.CHROME_PATH || path.join(process.env.ProgramFiles || 'C:/Program Files', 'Google/Chrome/Application/chrome.exe');
  const browser = await playwright.chromium.launch({executablePath, headless: true});
  const context = await browser.newContext({viewport: {width: 1440, height: 960}, deviceScaleFactor: 1, locale: 'zh-CN', timezoneId: 'Asia/Shanghai'});
  const page = await context.newPage();
  page.on('pageerror', e => errors.push(e.message));
  await page.route(/^https?:/, async route => { network.push(route.request().url()); await route.abort(); });
  const capture = async name => {
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.waitForTimeout(150);
    const visibleText = await page.locator('body').innerText();
    assert(!/(?:file:\/\/|[a-z]:[\\/](?:Users|VibeCoding)\b|\bsk-[a-z0-9]{12,}|\b[^\s@]+@[^\s@]+\.[a-z]{2,})/i.test(visibleText));
    fs.writeFileSync(path.join(evidence, name + '.txt'), visibleText);
    await page.screenshot({path: path.join(output, name), fullPage: true, animations: 'disabled'});
    record(name, 'isolated-demo-page');
  };
  const photo = async name => {
    await page.locator('#hero-photo').click();
    await page.waitForFunction(() => companionDemo.snapshot().photoReady);
    await page.locator('#photo-select-all').click();
    await page.locator('#photo-caption').fill('今天，和伙伴们一起');
    await page.locator('#photo-generate').click();
    await page.waitForFunction(() => companionDemo.snapshot().photoReady);
    assert.equal((await page.evaluate(() => companionDemo.snapshot())).photoMembers.length, 8);
    const png = await page.locator('#photo-canvas').evaluate(c => c.toDataURL('image/png'));
    fs.writeFileSync(path.join(output, name), Buffer.from(png.split(',')[1], 'base64'));
    record(name, 'demo-canvas-export');
    await page.locator('[data-close=photo-dialog]').click();
  };
  try {
    await page.goto(pathToFileURL(target).href);
    await page.waitForFunction(() => globalThis.companionDemo && document.querySelector('#hero-canvas').dataset.file);
    assert.equal((await page.evaluate(() => companionDemo.snapshot())).version, version);
    assert.equal(await page.locator('#api-provider').inputValue(), 'local');
    assert.equal(await page.locator('#api-key').inputValue(), '');
    await page.waitForTimeout(900);
    await capture('companions-q.png');
    await photo('group-photo-q.png');
    await page.locator('[data-style=realistic]').click();
    const families = await page.evaluate(() => CompanionModel.families);
    for (const family of families) {
      await page.locator(`.partner-card[data-family=${family}]`).click();
      await page.locator('[data-hero-outfit=sports]').click();
    }
    await page.locator('.partner-card[data-family=whale]').click();
    await page.waitForTimeout(600);
    await capture('companions-realistic.png');
    await photo('group-photo-realistic.png');
    await page.locator('[data-page=daily]').click();
    await page.locator('#check-in').click();
    await page.locator('#toast').waitFor({state: 'hidden'});
    await capture('calendar.png');
    assert.deepEqual(errors, []);
    assert.deepEqual(network, []);
    fs.writeFileSync(path.join(evidence, 'report.json'), JSON.stringify({version, captures, errors, network, source: 'New headless Chromium context; page pixels and exported canvas only; no desktop capture or personal browser profile.'}, null, 2));
    console.log(JSON.stringify({version, images: captures.length, bytes: captures.reduce((n, x) => n + x.bytes, 0), errors, network}));
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exit(1); });
