// Checks the browser build the way players get it: headless Chromium or Firefox opens the page and waits for the
// game's title screen. Exits 0 only if the title came up with no errors (console errors, page errors, failed
// downloads, or the page's own "couldn't start" message).
//
//   node Tools/check-pages.mjs https://nearbycoder.github.io/PackTheTrunk/      the live site, Chromium
//   node Tools/check-pages.mjs --serve Builds/pages --browser firefox          the local site, served at /PackTheTrunk/
//   node Tools/check-pages.mjs --serve Builds/pages --play                     and a short play session (below)
//
// Options: --browser chromium|firefox|both (default chromium), --play, --out <dir> (default Logs/pages-check-<browser>),
// --timeout <seconds to the title> (default 240). --serve <dir> serves <dir> at http://127.0.0.1:<free port>/PackTheTrunk/
// with no Content-Encoding headers, as GitHub Pages does, and stops the server at the end.
//
// --play then: reads the page's audio before any input (silent: the autoplay rule is emulated, see audioProbe), presses
// a key at the title (the audio must start), opens
// Settings with the keyboard and steps the Graphics fidelity slider, closes Settings, reloads the page (the step must
// survive: it's in the browser's IndexedDB), starts the first trip with the keyboard and packs three things with the
// mouse, undoes one with Z, packs the rest, closes the trunk with Space (the postcard; the album photo is saved), and
// reloads again: CONTINUE must start the next trip. The game publishes what's on screen in window.pttState (Assets/Scripts/Gameplay/WebBridge.cs).
//
// Needs playwright-core 1.63 or newer (for Firefox: it drives the system Firefox over WebDriver BiDi, channel
// "moz-firefox"). Set PLAYWRIGHT_CORE=/path/to/node_modules/playwright-core, or it looks in ~/Sites/*/node_modules.
// Chromium is the newest one cached in ~/.cache/ms-playwright (or CHROMIUM_PATH); Firefox is FIREFOX_PATH or
// /usr/bin/firefox. Browser profiles go in Logs/tmp, not the shared /tmp. Every browser runs headless.
import { createRequire } from "node:module";
import { createServer } from "node:http";
import { createReadStream, existsSync, mkdirSync, readdirSync, statSync, writeFileSync, appendFileSync } from "node:fs";
import os from "node:os";
import path from "node:path";

const require = createRequire(import.meta.url);
const root = path.resolve(path.dirname(new URL(import.meta.url).pathname), "..");

// ------------------------------------------------------------------ arguments
const argv = process.argv.slice(2);
const opts = { browser: "chromium", play: false, out: null, timeout: 240, serve: null, url: null };
for (let i = 0; i < argv.length; i++) {
  const a = argv[i];
  if (a === "--browser") opts.browser = argv[++i];
  else if (a === "--play") opts.play = true;
  else if (a === "--out") opts.out = argv[++i];
  else if (a === "--timeout") opts.timeout = Number(argv[++i]);
  else if (a === "--serve") opts.serve = argv[++i];
  else if (a === "-h" || a === "--help") { console.log("usage: node Tools/check-pages.mjs <url> | --serve <dir> [--browser chromium|firefox|both] [--play] [--out dir] [--timeout s]"); process.exit(0); }
  else if (!a.startsWith("--") && !opts.url) opts.url = a;
  else { console.error("unknown argument: " + a); process.exit(2); }
}
if (!opts.url && !opts.serve) { console.error("give a URL or --serve <dir>"); process.exit(2); }
const browsers = opts.browser === "both" ? ["chromium", "firefox"] : [opts.browser];

// ------------------------------------------------------------------ playwright
function findPlaywright() {
  if (process.env.PLAYWRIGHT_CORE) return process.env.PLAYWRIGHT_CORE;
  try { return require.resolve("playwright-core"); } catch (e) { }
  const sites = path.join(os.homedir(), "Sites");
  const found = [];
  for (const d of existsSync(sites) ? readdirSync(sites) : []) {
    const p = path.join(sites, d, "node_modules", "playwright-core");
    try {
      const v = JSON.parse(require("node:fs").readFileSync(path.join(p, "package.json"), "utf8")).version.split(".").map(Number);
      if (v[0] > 1 || (v[0] === 1 && v[1] >= 63)) found.push({ p, v });
    } catch (e) { }
  }
  found.sort((a, b) => b.v[1] - a.v[1] || b.v[2] - a.v[2]);
  if (!found.length) throw new Error("playwright-core 1.63+ not found: set PLAYWRIGHT_CORE");
  return found[0].p;
}

function cachedChromium() {
  if (process.env.CHROMIUM_PATH) return process.env.CHROMIUM_PATH;
  const cache = path.join(os.homedir(), ".cache", "ms-playwright");
  const dirs = existsSync(cache) ? readdirSync(cache) : [];
  const pick = (prefix, rel) => dirs.filter((d) => d.startsWith(prefix)).map((d) => ({ d, rev: Number(d.slice(prefix.length)) }))
    .sort((a, b) => b.rev - a.rev).map((x) => path.join(cache, x.d, rel)).find((p) => existsSync(p));
  return pick("chromium-", "chrome-linux64/chrome") || pick("chromium_headless_shell-", "chrome-headless-shell-linux64/chrome-headless-shell");
}

// ------------------------------------------------------------------ a static server like GitHub Pages
const types = { ".html": "text/html; charset=utf-8", ".js": "application/javascript", ".wasm": "application/wasm", ".json": "application/json",
                ".jpg": "image/jpeg", ".png": "image/png", ".ico": "image/x-icon", ".css": "text/css", ".data": "application/octet-stream" };
function serve(dir) {
  const base = "/PackTheTrunk/";
  const server = createServer((req, res) => {
    const url = decodeURIComponent(new URL(req.url, "http://x").pathname);
    if (url === "/PackTheTrunk") { res.writeHead(301, { Location: base }); return res.end(); }
    if (!url.startsWith(base)) { res.writeHead(404); return res.end("not found"); }
    let file = path.join(dir, url.slice(base.length));
    if (!file.startsWith(path.resolve(dir))) { res.writeHead(403); return res.end(); }
    if (existsSync(file) && statSync(file).isDirectory()) file = path.join(file, "index.html");
    if (!existsSync(file)) { res.writeHead(404); return res.end("not found"); }
    res.writeHead(200, { "Content-Type": types[path.extname(file)] || "application/octet-stream", "Content-Length": statSync(file).size,
                         "Cache-Control": "max-age=600" });
    createReadStream(file).pipe(res);
  });
  return new Promise((resolve) => server.listen(0, "127.0.0.1", () => resolve({ server, url: `http://127.0.0.1:${server.address().port}${base}` })));
}

// ------------------------------------------------------------------ page helpers
// An analyser on every AudioContext that reaches the speakers: tells us whether the game makes sound.
// Headless browsers under automation let pages autoplay, so the probe also plays the browsers' autoplay rule: an
// AudioContext starts suspended and resume() does nothing until the page has had a real click or key press
// (navigator.userActivation can't tell: automation marks the page active from the start). That's what a player's
// browser does, and the game has to start its sound itself.
const audioProbe = () => {
  const analysers = [];
  let activated = false;
  for (const type of ["keydown", "mousedown", "pointerdown", "touchend"])
    window.addEventListener(type, (e) => { if (e.isTrusted) activated = true; }, true);
  const active = () => activated;
  for (const name of ["AudioContext", "webkitAudioContext"]) {
    const Native = window[name];
    if (!Native) continue;
    window[name] = class extends Native {
      constructor(...args) {
        super(...args);
        if (!active()) super.suspend();
      }
      resume() { return active() ? super.resume() : Promise.resolve(); }
    };
  }
  const connect = AudioNode.prototype.connect;
  AudioNode.prototype.connect = function (dest, ...rest) {
    const r = connect.call(this, dest, ...rest);
    try {
      if (typeof AudioDestinationNode !== "undefined" && dest instanceof AudioDestinationNode) {
        const ctx = dest.context;
        if (!ctx.__probe) {
          ctx.__probe = ctx.createAnalyser();
          ctx.__probe.fftSize = 2048;
          const mute = ctx.createGain();
          mute.gain.value = 0;
          connect.call(ctx.__probe, mute);
          connect.call(mute, ctx.destination);
          analysers.push(ctx.__probe);
        }
        connect.call(this, ctx.__probe);
      }
    } catch (e) { /* never break the game's audio */ }
    return r;
  };
  window.__audio = () => {
    let peak = 0;
    const buf = new Float32Array(2048);
    for (const a of analysers) {
      a.getFloatTimeDomainData(buf);
      for (let i = 0; i < buf.length; i++) peak = Math.max(peak, Math.abs(buf[i]));
    }
    return { peak, contexts: analysers.length, states: analysers.map((a) => a.context.state) };
  };
};

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

// The files Unity has flushed to IndexedDB (its /idbfs file system) whose path contains `part`.
const idbFiles = (part) => new Promise((resolve) => {
  const open = indexedDB.open("/idbfs");
  open.onerror = () => resolve([]);
  open.onsuccess = () => {
    const db = open.result;
    if (!db.objectStoreNames.contains("FILE_DATA")) return resolve([]);
    const req = db.transaction("FILE_DATA").objectStore("FILE_DATA").getAllKeys();
    req.onsuccess = () => resolve(req.result.map(String).filter((k) => k.includes(part)));
    req.onerror = () => resolve([]);
  };
});

async function runBrowser(pw, engine, url) {
  const out = path.resolve(opts.out ? (browsers.length > 1 ? path.join(opts.out, engine) : opts.out) : path.join(root, "Logs", "pages-check-" + engine));
  mkdirSync(out, { recursive: true });
  const logFile = path.join(out, "check.log");
  const consoleFile = path.join(out, "console.log");
  writeFileSync(logFile, "");
  writeFileSync(consoleFile, "");
  const results = [];
  const log = (line) => { console.log(`[${engine}] ${line}`); appendFileSync(logFile, line + "\n"); };
  const check = (ok, what) => { results.push({ ok, what }); log(`${ok ? "PASS" : "FAIL"} ${what}`); return ok; };
  let consoleLines = 0;
  const errors = [];

  const launch = { headless: true };
  if (engine === "chromium") {
    launch.executablePath = cachedChromium();
    // the real GPU through ANGLE/Vulkan instead of SwiftShader, which is far too slow for a 3D game
    launch.args = ["--use-angle=vulkan", "--enable-features=Vulkan", "--ignore-gpu-blocklist"];
  } else {
    launch.channel = "moz-firefox";
    launch.executablePath = process.env.FIREFOX_PATH || "/usr/bin/firefox";
  }
  log(`browser: ${launch.executablePath || engine}; page: ${url}; load average ${os.loadavg().map((x) => x.toFixed(1)).join(" ")}`);
  const browser = await pw[engine].launch(launch);
  log(`version: ${browser.version()}`);
  const context = await browser.newContext({ viewport: { width: 1600, height: 900 } });
  await context.addInitScript(audioProbe);
  const page = await context.newPage();
  let bytes = 0;
  const files = {};
  page.on("console", (m) => {
    const text = m.text();
    if (consoleLines++ < 4000) appendFileSync(consoleFile, `[${m.type()}] ${text}\n`);
    if (m.type() === "error") errors.push("console: " + text);
  });
  page.on("pageerror", (e) => errors.push("page: " + e.message));
  page.on("requestfailed", (r) => errors.push(`download failed: ${r.url()} (${r.failure()?.errorText})`));
  page.on("response", async (r) => {
    if (r.status() >= 400) errors.push(`HTTP ${r.status()}: ${r.url()}`);
    const len = Number(r.headers()["content-length"] || 0);
    bytes += len;
    files[r.url().split("/").pop() || "index.html"] = len;
  });

  const state = () => page.evaluate(() => window.pttState || null);
  const waitFor = async (what, test, seconds) => {
    const end = Date.now() + seconds * 1000;
    let s = null;
    while (Date.now() < end) {
      s = await state();
      if (s && test(s)) return s;
      const load = await page.evaluate(() => window.pttLoad);
      if (load && load.startsWith("error")) { errors.push("page says: " + load); return null; }
      await sleep(200);
    }
    log(`timed out waiting for ${what}; last state ${JSON.stringify(s)}`);
    return null;
  };
  // Held for a few frames like a finger would: a press and release inside one frame is invisible to the game.
  const press = async (key, wait = 350) => { await page.keyboard.down(key); await sleep(100); await page.keyboard.up(key); await sleep(wait); };
  // Arrow keys walk the keyboard cursor (its control's name is pttState.nav) until it's on `name`.
  const navTo = async (name, ...keys) => {
    const path = [];
    let s = await state();
    for (const key of keys)
      for (let i = 0; i < 10 && s.nav !== name; i++) { await press(key); s = await state(); path.push(s.nav); }
    log(`keyboard cursor to ${name}: ${path.join(" > ") || "(already there)"}`);
    return s.nav === name;
  };
  // Hand pointing back to the real mouse (after keys, the game's pointer is the keyboard cursor until the mouse moves).
  const wiggle = async (x, y) => {
    await page.mouse.move(x + 40, y + 30, { steps: 4 });
    await sleep(150);
    await page.mouse.move(x, y, { steps: 8 });
    await sleep(250);
  };
  const click = async (x, y) => {
    await wiggle(x, y);
    await page.mouse.down();
    await sleep(120);
    await page.mouse.up();
  };
  const shot = (name) => page.screenshot({ path: path.join(out, name + ".png") }).catch(() => { });
  // Unity's screen pixels (top-left origin, as WebBridge sends them) to page coordinates
  const toPage = async ([x, y]) => page.evaluate(([x, y]) => {
    const c = document.querySelector("#unity-canvas");
    const r = c.getBoundingClientRect();
    return [r.left + x * r.width / c.width, r.top + y * r.height / c.height];
  }, [x, y]);
  const probe = async (skip = 0) => {
    await page.evaluate((skip) => window.unityInstance.SendMessage("WebBridge", "Probe", String(skip)), skip);
    await sleep(100);
    return state();
  };

  async function loadToTitle(label) {
    const t0 = Date.now();
    await page.goto(url, { waitUntil: "load" });
    const s = await waitFor("the title screen", (s) => s.mode === "Title", opts.timeout);
    const secs = (Date.now() - t0) / 1000;
    await sleep(1500);
    await shot(label);
    return { s, secs };
  }

  try {
    const first = await loadToTitle("title");
    check(!!first.s, `${first.s ? "reached the title screen" : "never reached the title screen"} in ${first.secs.toFixed(1)} s; ` +
      `downloaded ${(bytes / 1048576).toFixed(1)} MB (${Object.entries(files).filter(([, n]) => n > 1048576).map(([f, n]) => `${f} ${(n / 1048576).toFixed(1)} MB`).join(", ")})`);
    if (first.s) log(`state: ${JSON.stringify(first.s)}`);

    if (opts.play && first.s) {
      // ---- audio: quiet (or suspended) before any input, playing after a key
      const before = await page.evaluate(() => window.__audio());
      await page.mouse.move(800, 450);
      await press("Enter", 300);
      await sleep(2500);
      const after = await page.evaluate(() => window.__audio());
      let peak = after.peak;
      for (let i = 0; i < 10 && peak < 0.001; i++) { await sleep(300); peak = Math.max(peak, (await page.evaluate(() => window.__audio())).peak); }
      const was = `${before.peak.toFixed(4)} peak (${before.states.join(",") || "no audio context"})`;
      check(peak > 0.001 && after.states.length > 0 && after.states.every((s) => s === "running"),
        `audio plays after the first key: ${peak.toFixed(4)} peak (${after.states.join(",")}); before any input ${was}`);
      check(before.peak < 0.001 && before.states.every((s) => s !== "running"), `silent until the first input (the autoplay rule, emulated): ${was}`);
      const menu = await waitFor("the title menu", (s) => s.mode === "Title" && !s.waiting, 10);
      check(!!menu, "a key at the title opens the main menu");
      await shot("menu");

      // ---- a setting that survives a reload: Settings → Graphics → fidelity one step right
      let s = await state();
      const startFidelity = s.fidelity;
      const onSettings = await navTo("Settings", "ArrowDown", "ArrowUp");
      if (onSettings) await press("Enter", 1200);
      s = await state();
      const opened = s.settings;
      await navTo("Tab GRAPHICS", "ArrowUp", "ArrowRight", "ArrowLeft");
      await press("Enter", 800);
      await press("ArrowDown", 500);
      log(`on the Graphics tab, ↓ lands on ${(await state()).nav}`);
      const want = startFidelity < 3 ? startFidelity + 1 : 2;
      await press(want > startFidelity ? "ArrowRight" : "ArrowLeft", 800);
      s = await state();
      await shot("settings-graphics");
      const changed = s.fidelity === want;
      check(opened && changed, `keyboard: Settings opens and the Graphics fidelity slider steps ${startFidelity} → ${s.fidelity} (wanted ${want})`);
      await press("Escape", 1200);
      s = await state();
      check(!s.settings, "Escape closes Settings (which saves)");
      await sleep(1500); // the save reaches IndexedDB asynchronously

      bytes = 0;
      const second = await loadToTitle("title-after-reload");
      check(!!second.s && second.s.fidelity === want && !second.s.custom,
        `after a reload (${second.secs.toFixed(1)} s, ${(bytes / 1048576).toFixed(1)} MB downloaded) the fidelity step is still ${second.s?.fidelity} (wanted ${want})`);

      // ---- a short trip: CONTINUE, the story card, then pack three things with the mouse and undo one
      await page.mouse.move(800, 450);
      await press("Enter", 1500);
      if (await navTo("Continue", "ArrowUp", "ArrowDown")) await press("Enter", 2500);
      s = await waitFor("the story card", (s) => s.mode === "Story" || s.mode === "Playing", 20);
      await shot("story");
      for (let i = 0; i < 12 && s && s.mode !== "Playing"; i++) { await press("Space", 1500); s = await state(); }
      s = await waitFor("packing", (s) => s.mode === "Playing" && s.items > 0, 30);
      check(!!s, `CONTINUE and Space start the first trip (${s?.level}, ${s?.items} things to pack)`);
      if (s) {
        await sleep(2500); // the camera sweeps in
        // Click something on the blanket (the skip-th essential, then extras), then a spot in the trunk where it fits.
        let clicks = 0;
        const packOne = async (skip, turn) => {
          let s = await probe(skip);
          if (!s.pile) return false;
          const [px, py] = await toPage(s.pile);
          await click(px, py);
          await sleep(600);
          s = await probe();
          if (clicks++ === 0) await shot("first-click");
          if (!s.held) { log(`clicking ${s.pile} didn't pick anything up: ${JSON.stringify(s)}`); return false; }
          if (turn) { await press("r", 500); s = await probe(); }   // turn it once, as a player would
          if (!s.aim) { log("no spot in the trunk for the held item; putting it back"); await press("Escape", 600); return false; }
          const [ax, ay] = await toPage(s.aim);
          const before = s.packed;
          await click(ax, ay);
          await sleep(1200);
          return (await state()).packed > before;
        };
        let packed = 0;
        for (let n = 0; n < 6 && packed < 3; n++) {
          await packOne(0, n === 0);
          packed = (await state()).packed;
        }
        await shot("packed");
        check(packed >= 3, `the mouse packs things into the trunk: ${packed} packed (with one turned by R)`);
        await press("z", 1200);
        s = await state();
        check(s.packed === packed - 1, `Z undoes the last one: ${packed} → ${s.packed} packed`);

        // ---- finish the trip: pack what fits (trying other things when one doesn't), close the trunk with Space
        for (let skip = 0, tries = 0; tries < 12 && skip < 6; tries++) {
          if (await packOne(skip, false)) skip = 0;
          else skip++;
        }
        s = await state();
        const trip = s.level;
        log(`packed ${s.packed} of ${s.items}; closing the trunk`);
        await press("Space", 500);
        s = await waitFor("the postcard", (s) => s.mode === "Results", 30);
        await sleep(2500);
        await shot("postcard");
        check(!!s, `Space closes the trunk and the postcard comes up`);
        await sleep(2000);
        const photos = await page.evaluate(idbFiles, "/album/");
        check(photos.some((f) => f.endsWith(`/album/${trip}.jpg`)) && photos.some((f) => f.endsWith(`/album/${trip}.thumb.jpg`)),
          `the trip's album photo is saved in the browser's storage: ${photos.map((f) => f.split("/").pop()).join(", ") || "none"}`);
        log(`frame rate: ${s?.fps} fps at ${s?.w}x${s?.h} (fidelity ${s?.fidelity}; headless, load ${os.loadavg()[0].toFixed(1)})`);

        // ---- progress survives a reload: CONTINUE now starts the next trip
        await sleep(1500);
        await loadToTitle("title-after-trip");
        await page.mouse.move(800, 450);
        await press("Enter", 1500);
        if (await navTo("Continue", "ArrowUp", "ArrowDown")) await press("Enter", 2500);
        s = await waitFor("the next story card", (s) => s.mode === "Story" || s.mode === "Playing", 20);
        for (let i = 0; i < 12 && s && s.mode !== "Playing"; i++) { await press("Space", 1500); s = await state(); }
        s = await waitFor("packing", (s) => s.mode === "Playing" && s.items > 0, 30);
        await shot("next-trip");
        check(!!s && s.level !== trip, `after a reload, CONTINUE starts the next trip (${s?.level}, not ${trip}): progress is saved in the browser`);
      }
    }
  } catch (e) {
    errors.push("check script: " + (e.stack || e.message));
  }

  check(errors.length === 0, errors.length ? `errors (${errors.length}):\n  ` + errors.slice(0, 20).join("\n  ") : "no console errors, page errors or failed downloads");
  await context.close().catch(() => { });
  await browser.close().catch(() => { });
  const failed = results.filter((r) => !r.ok).length;
  log(`${failed === 0 ? "OK" : "FAILED"}: ${results.length - failed}/${results.length} checks passed; log ${logFile}, browser console ${consoleFile}`);
  return failed === 0;
}

async function main() {
  // Browser profiles and temporary files go in the repo's Logs/, not the shared RAM-disk /tmp.
  process.env.TMPDIR = path.join(root, "Logs", "tmp");
  mkdirSync(process.env.TMPDIR, { recursive: true });
  const pw = require(findPlaywright());
  let server = null, url = opts.url;
  if (opts.serve) {
    const s = await serve(path.resolve(opts.serve));
    server = s.server;
    url = s.url;
  }
  let ok = true;
  try {
    for (const engine of browsers) ok = (await runBrowser(pw, engine, url)) && ok;
  } finally {
    if (server) server.close();
  }
  process.exit(ok ? 0 : 1);
}

main().catch((e) => { console.error(e); process.exit(1); });
