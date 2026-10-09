// Checks the browser build on phones and tablets: headless WebKit with iPhone and iPad profiles and headless Chromium
// with an Android phone profile open the page with touch (hasTouch, isMobile, a coarse pointer), measure how much memory
// it takes, and play a short session with touch events only, through the on-screen controls. Also checks that the
// controls stay hidden in desktop Chromium and Firefox.
//
//   node Tools/check-mobile.mjs --serve Builds/pages                         all three devices, then desktop
//   node Tools/check-mobile.mjs --serve Builds/pages --device iphone --play  one device, with the touch session
//
// Options: --device iphone|iphone-landscape|ipad|pixel|pixel-landscape|desktop|all, or several joined by commas (default
// all: the phones upright (the "turn it sideways" prompt), the phones and the iPad on their sides, then desktop Chromium
// and Firefox at 1600x900), --play (the touch session; without it: the title, memory, and the controls' visibility), --out <dir>
// (default Logs/mobile-check), --timeout <seconds to the title> (default 300), --label <name> (a subfolder of --out,
// for before/after runs).
//
// What it measures, every 250 ms from the first request to the end:
//   - the wasm heap (Unity's whole C# and native memory: Module.HEAPU8's size), its peak;
//   - what the page has handed to WebGL: textures, renderbuffers and buffers, counted from the calls' sizes (an
//     estimate: a driver may pad or compress), plus the canvas's own drawing buffer;
//   - the browser's processes (the ones this script started): resident memory (PSS) of the busiest one (the page's
//     web content or renderer process) and of all of them together, their peaks.
// Headless WebKit doesn't enforce iOS's per-tab memory limit, so these numbers are what to compare against it
// (roughly 1 GB or less on a current iPhone, less on older ones).
//
// Touch: Playwright's touchscreen can only tap, so drags and two-finger gestures are dispatched as TouchEvents
// (Chromium: real ones through the DevTools protocol; WebKit: synthetic ones in the page, which Unity and the
// controls handle the same way). Taps are always real (Playwright's), so audio unlocks as it would on a phone.
//
// Needs playwright-core 1.63+ (PLAYWRIGHT_CORE, or it looks in ~/Sites/*/node_modules), WebKit at
// WEBKIT_PATH or ~/.cache/webkit-libs/webkit-2359/pw_run.sh, Chromium cached in ~/.cache/ms-playwright (or
// CHROMIUM_PATH), Firefox at FIREFOX_PATH or /usr/bin/firefox. Every browser runs headless; profiles go in Logs/tmp.
import { createRequire } from "node:module";
import { createServer } from "node:http";
import { createReadStream, existsSync, mkdirSync, readdirSync, readFileSync, statSync, writeFileSync, appendFileSync } from "node:fs";
import os from "node:os";
import path from "node:path";

const require = createRequire(import.meta.url);
const root = path.resolve(path.dirname(new URL(import.meta.url).pathname), "..");

// ------------------------------------------------------------------ arguments
const argv = process.argv.slice(2);
const opts = { device: "all", play: false, out: null, timeout: 300, serve: null, url: null, label: null };
for (let i = 0; i < argv.length; i++) {
  const a = argv[i];
  if (a === "--device") opts.device = argv[++i];
  else if (a === "--play") opts.play = true;
  else if (a === "--out") opts.out = argv[++i];
  else if (a === "--timeout") opts.timeout = Number(argv[++i]);
  else if (a === "--serve") opts.serve = argv[++i];
  else if (a === "--label") opts.label = argv[++i];
  else if (a === "-h" || a === "--help") { console.log("usage: node Tools/check-mobile.mjs <url> | --serve <dir> [--device iphone|iphone-landscape|ipad|pixel|pixel-landscape|desktop|all] [--play] [--out dir] [--label name] [--timeout s]"); process.exit(0); }
  else if (!a.startsWith("--") && !opts.url) opts.url = a;
  else { console.error("unknown argument: " + a); process.exit(2); }
}
if (!opts.url && !opts.serve) { console.error("give a URL or --serve <dir>"); process.exit(2); }
const outRoot = path.resolve(opts.out || path.join(root, "Logs", "mobile-check"), opts.label || "");

// ------------------------------------------------------------------ playwright and browsers
function findPlaywright() {
  if (process.env.PLAYWRIGHT_CORE) return process.env.PLAYWRIGHT_CORE;
  try { return require.resolve("playwright-core"); } catch (e) { }
  const sites = path.join(os.homedir(), "Sites");
  const found = [];
  for (const d of existsSync(sites) ? readdirSync(sites) : []) {
    const p = path.join(sites, d, "node_modules", "playwright-core");
    try {
      const v = JSON.parse(readFileSync(path.join(p, "package.json"), "utf8")).version.split(".").map(Number);
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

// device name -> how to launch it and the context to open
function profiles(pw) {
  const d = pw.devices;
  const webkitPath = process.env.WEBKIT_PATH || path.join(os.homedir(), ".cache/webkit-libs/webkit-2359/pw_run.sh");
  const chromiumArgs = ["--use-angle=vulkan", "--enable-features=Vulkan", "--ignore-gpu-blocklist"];
  const landscape = (dev) => ({ ...dev, viewport: { width: dev.viewport.height, height: dev.viewport.width },
                                screen: dev.screen && { width: dev.screen.height, height: dev.screen.width } });
  return {
    iphone: { engine: "webkit", launch: { executablePath: webkitPath }, context: d["iPhone 15"], name: "iPhone 15 (WebKit)", touch: true },
    "iphone-landscape": { engine: "webkit", launch: { executablePath: webkitPath }, context: landscape(d["iPhone 15"]), name: "iPhone 15 landscape (WebKit)", touch: true },
    ipad: { engine: "webkit", launch: { executablePath: webkitPath }, context: landscape(d["iPad Pro 11"]), name: "iPad Pro 11 landscape (WebKit)", touch: true },
    pixel: { engine: "chromium", launch: { executablePath: cachedChromium(), args: chromiumArgs }, context: d["Pixel 7"], name: "Pixel 7 (Chromium)", touch: true },
    "pixel-landscape": { engine: "chromium", launch: { executablePath: cachedChromium(), args: chromiumArgs }, context: landscape(d["Pixel 7"]), name: "Pixel 7 landscape (Chromium)", touch: true },
    "desktop-chromium": { engine: "chromium", launch: { executablePath: cachedChromium(), args: chromiumArgs }, context: { viewport: { width: 1600, height: 900 } }, name: "desktop Chromium", touch: false },
    "desktop-firefox": { engine: "firefox", launch: { channel: "moz-firefox", executablePath: process.env.FIREFOX_PATH || "/usr/bin/firefox" }, context: { viewport: { width: 1600, height: 900 } }, name: "desktop Firefox", touch: false },
  };
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

// ------------------------------------------------------------------ process memory (only the processes this script started)
function childrenOf(pid) {
  const kids = new Map();
  for (const p of readdirSync("/proc")) {
    if (!/^\d+$/.test(p)) continue;
    try {
      const stat = readFileSync(`/proc/${p}/stat`, "utf8");
      const rest = stat.slice(stat.lastIndexOf(")") + 2).split(" ");
      const ppid = Number(rest[1]);
      if (!kids.has(ppid)) kids.set(ppid, []);
      kids.get(ppid).push(Number(p));
    } catch (e) { }
  }
  const out = [];
  const walk = (p) => { for (const k of kids.get(p) || []) { out.push(k); walk(k); } };
  walk(pid);
  return out;
}
function processMemory() {
  const procs = [];
  for (const pid of childrenOf(process.pid)) {
    try {
      let name = readFileSync(`/proc/${pid}/comm`, "utf8").trim();
      // Chromium's processes are all "chrome": name them by their --type (renderer = the page, gpu-process = WebGL)
      const type = /--type=([\w-]+)/.exec(readFileSync(`/proc/${pid}/cmdline`, "utf8").replace(/\0/g, " "));
      if (type) name += ":" + type[1];
      else if (name === "chrome" || name === "chrome-headless") name += ":browser";
      let kb = 0;
      try { kb = Number(/^Pss:\s+(\d+)/m.exec(readFileSync(`/proc/${pid}/smaps_rollup`, "utf8"))[1]); }
      catch (e) { kb = Number(/^VmRSS:\s+(\d+)/m.exec(readFileSync(`/proc/${pid}/status`, "utf8"))?.[1] || 0); }
      procs.push({ pid, name, mb: kb / 1024 });
    } catch (e) { }
  }
  return procs;
}

// ------------------------------------------------------------------ in the page
// Counts what the page hands to WebGL (textures, renderbuffers, buffers), by object, so deletes and re-uploads count right.
const glProbe = () => {
  const fmtBytes = { 0x8058: 4, 0x8C43: 4, 0x881A: 8, 0x8814: 16, 0x8C3A: 4, 0x88F0: 4, 0x81A6: 4, 0x8CAC: 4, 0x8CAD: 8, 0x81A5: 2,
                     0x8229: 1, 0x822B: 2, 0x822F: 4, 0x822D: 2, 0x822E: 4, 0x8051: 4, 0x8059: 4, 0x1908: 4, 0x1907: 4, 0x1909: 1,
                     0x190A: 2, 0x1906: 1, 0x8D62: 2, 0x8056: 2, 0x8057: 2, 0x8D48: 1, 0x8C41: 4, 0x8230: 8, 0x822C: 4, 0x8236: 4,
                     0x8D7C: 4, 0x8D70: 16, 0x8D7D: 4, 0x8D8E: 4 };
  const stats = { textures: 0, renderbuffers: 0, buffers: 0, texPeak: 0, rbPeak: 0, bufPeak: 0, totalPeak: 0, canvas: 0, uploads: 0 };
  const sizes = new WeakMap();   // object -> { key -> bytes }
  const live = new Map();       // object -> { kind, what, bytes } (for the biggest-first list)
  const set = (kind, obj, key, bytes, what) => {
    if (!obj) return;
    let m = sizes.get(obj);
    if (!m) sizes.set(obj, m = new Map());
    const l = live.get(obj) || { kind, what: what || "", bytes: 0 };
    l.bytes += bytes - (m.get(key) || 0);
    if (what && key !== "0" && !String(key).endsWith(":0") || !l.what) l.what = what || l.what;
    live.set(obj, l);
    stats[kind] += bytes - (m.get(key) || 0);
    m.set(key, bytes);
    stats.uploads++;
    stats.texPeak = Math.max(stats.texPeak, stats.textures);
    stats.rbPeak = Math.max(stats.rbPeak, stats.renderbuffers);
    stats.bufPeak = Math.max(stats.bufPeak, stats.buffers);
    stats.totalPeak = Math.max(stats.totalPeak, stats.textures + stats.renderbuffers + stats.buffers);
  };
  const drop = (kind, obj) => {
    const m = obj && sizes.get(obj);
    if (!m) return;
    for (const v of m.values()) stats[kind] -= v;
    sizes.delete(obj);
    live.delete(obj);
  };
  const P = window.WebGL2RenderingContext && WebGL2RenderingContext.prototype;
  if (!P) return;
  const st = new WeakMap();
  const s = (gl) => { let x = st.get(gl); if (!x) st.set(gl, x = { unit: 0, tex: {}, buf: {}, rb: null }); return x; };
  const texTarget = (t) => (t >= 0x8515 && t <= 0x851A ? 0x8513 : t);   // cube faces -> TEXTURE_CUBE_MAP
  const wrap = (name, fn) => { const orig = P[name]; if (!orig) return; P[name] = function (...a) { try { fn(this, a); } catch (e) { } return orig.apply(this, a); }; };
  wrap("activeTexture", (gl, a) => { s(gl).unit = a[0]; });
  wrap("bindTexture", (gl, a) => { s(gl).tex[s(gl).unit + ":" + a[0]] = a[1]; });
  wrap("bindBuffer", (gl, a) => { s(gl).buf[a[0]] = a[1]; });
  wrap("bindRenderbuffer", (gl, a) => { s(gl).rb = a[1]; });
  const bound = (gl, target) => s(gl).tex[s(gl).unit + ":" + texTarget(target)];
  wrap("texStorage2D", (gl, [t, levels, fmt, w, h]) => {
    let b = 0; for (let l = 0; l < levels; l++) b += Math.max(1, w >> l) * Math.max(1, h >> l) * (fmtBytes[fmt] || 4);
    set("textures", bound(gl, t), "storage", b * (t === 0x8513 ? 6 : 1), `${t === 0x8513 ? "cube " : ""}${w}x${h} fmt 0x${fmt.toString(16)} ${levels} mips`);
  });
  wrap("texStorage3D", (gl, [t, levels, fmt, w, h, d]) => {
    let b = 0; for (let l = 0; l < levels; l++) b += Math.max(1, w >> l) * Math.max(1, h >> l) * (t === 0x806F ? Math.max(1, d >> l) : d) * (fmtBytes[fmt] || 4);
    set("textures", bound(gl, t), "storage", b, `${w}x${h}x${d} fmt 0x${fmt.toString(16)} ${levels} mips`);
  });
  wrap("texImage2D", (gl, a) => {
    const [t, level, fmt] = a;
    let w, h;
    if (a.length >= 8) { w = a[3]; h = a[4]; } else { const src = a[5]; w = src?.width || src?.videoWidth || 0; h = src?.height || src?.videoHeight || 0; }
    set("textures", bound(gl, t), t + ":" + level, w * h * (fmtBytes[fmt] || 4), level === 0 ? `${w}x${h} fmt 0x${fmt.toString(16)} (texImage2D)` : "");
  });
  wrap("texImage3D", (gl, [t, level, fmt, w, h, d]) => { set("textures", bound(gl, t), t + ":" + level, w * h * d * (fmtBytes[fmt] || 4)); });
  wrap("compressedTexImage2D", (gl, a) => {
    const [t, level] = a;
    const data = a[6];
    const bytes = typeof data === "number" ? data : (a.length >= 9 && typeof a[8] === "number" ? a[8] : data?.byteLength || 0);
    set("textures", bound(gl, t), t + ":" + level, bytes, level === 0 ? `${a[3]}x${a[4]} compressed 0x${a[2].toString(16)}` : "");
  });
  wrap("deleteTexture", (gl, [tex]) => drop("textures", tex));
  // bufferData(target, size | data, usage[, srcOffset[, length]]): WebGL 2 callers often pass the whole wasm heap and a range
  wrap("bufferData", (gl, [t, data, , off, len]) => {
    const el = data?.BYTES_PER_ELEMENT || 1;
    const bytes = typeof data === "number" ? data : len ? len * el : off ? data.byteLength - off * el : data?.byteLength || 0;
    set("buffers", s(gl).buf[t], "data", bytes);
  });
  wrap("deleteBuffer", (gl, [b]) => drop("buffers", b));
  wrap("renderbufferStorage", (gl, [, fmt, w, h]) => { set("renderbuffers", s(gl).rb, "s", w * h * (fmtBytes[fmt] || 4), `${w}x${h} fmt 0x${fmt.toString(16)}`); });
  wrap("renderbufferStorageMultisample", (gl, [, samples, fmt, w, h]) => { set("renderbuffers", s(gl).rb, "s", w * h * Math.max(1, samples) * (fmtBytes[fmt] || 4), `${w}x${h} x${samples} fmt 0x${fmt.toString(16)}`); });
  wrap("deleteRenderbuffer", (gl, [rb]) => drop("renderbuffers", rb));
  window.__gl = () => {
    const c = document.querySelector("#unity-canvas");
    let canvas = 0;
    try {
      const gl = c && c.getContext("webgl2");
      const attrs = gl && gl.getContextAttributes();
      if (attrs) canvas = gl.drawingBufferWidth * gl.drawingBufferHeight * (4 + (attrs.depth || attrs.stencil ? 4 : 0)) * (attrs.antialias ? 4 : 1) + gl.drawingBufferWidth * gl.drawingBufferHeight * 4;
    } catch (e) { }
    stats.canvas = canvas;
    return stats;
  };
  // Unity's own numbers: the wasm heap's size and how much of it is in use
  window.__glTop = (n) => [...live.values()].sort((a, b) => b.bytes - a.bytes).slice(0, n)
    .map((l) => `${(l.bytes / 1048576).toFixed(1)} MB ${l.kind} ${l.what}`);
  window.__heap = () => {
    const u = window.unityInstance;
    if (!u) return { total: 0, used: 0 };
    try { const m = u.GetMetricsInfo(); return { total: m.totalWASMHeapSize, used: m.usedWASMHeapSize }; } catch (e) { }
    const m = u.Module;
    return { total: (m && m.HEAPU32 && m.HEAPU32.byteLength) || 0, used: 0 };
  };
};

// Emulates the browsers' autoplay rule (as Tools/check-pages.mjs does): AudioContexts stay suspended until a real tap or key.
const audioProbe = () => {
  const analysers = [];
  let activated = false;
  for (const type of ["keydown", "mousedown", "pointerdown", "touchend"])
    window.addEventListener(type, (e) => { if (e.isTrusted) activated = true; }, true);
  for (const name of ["AudioContext", "webkitAudioContext"]) {
    const Native = window[name];
    if (!Native) continue;
    window[name] = class extends Native {
      constructor(...args) { super(...args); if (!activated) super.suspend(); }
      resume() { return activated ? super.resume() : Promise.resolve(); }
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
    } catch (e) { }
    return r;
  };
  window.__audio = () => {
    let peak = 0;
    const buf = new Float32Array(2048);
    for (const a of analysers) { a.getFloatTimeDomainData(buf); for (let i = 0; i < buf.length; i++) peak = Math.max(peak, Math.abs(buf[i])); }
    return { peak, contexts: analysers.length, states: analysers.map((a) => a.context.state) };
  };
};

// Headless WebKit on the test machine has no GStreamer audio sink (autoaudiosink, from gst-plugins-good, isn't installed),
// and its web process crashes as soon as a page makes an AudioContext. In WebKit runs the page gets AudioContexts with
// no output instead (OfflineAudioContexts that never render): the game's audio code runs, nothing is heard, and the
// check reads whether the game resumed its context after the first tap. A real iPhone has an audio sink; this is the
// test machine's limit, not the game's. (The same machine also lacks the AAC decoder, so WebKit can't decode the
// game's compressed sounds here: their decoded size is measured in Chromium.)
const silentAudio = () => {
  let activated = false;
  for (const type of ["keydown", "mousedown", "pointerdown", "touchend"])
    window.addEventListener(type, (e) => { if (e.isTrusted) activated = true; }, true);
  const contexts = [];
  class SilentAudioContext extends OfflineAudioContext {
    constructor(o) {
      super({ numberOfChannels: 2, length: 128, sampleRate: (o && o.sampleRate) || 44100 });
      this.__state = "suspended";
      this.__resumedAfterInput = false;
      contexts.push(this);
    }
    get state() { return this.__state; }
    get baseLatency() { return 0.01; }
    get outputLatency() { return 0.02; }
    resume() {
      if (activated) { this.__state = "running"; this.__resumedAfterInput = true; this.onstatechange && this.onstatechange(new Event("statechange")); }
      return Promise.resolve();
    }
    suspend() { this.__state = "suspended"; return Promise.resolve(); }
    close() { this.__state = "closed"; return Promise.resolve(); }
    createMediaElementSource(el) { const g = this.createGain(); g.mediaElement = el; return g; }
    createMediaStreamDestination() { return { stream: new MediaStream() }; }
  }
  window.AudioContext = window.webkitAudioContext = SilentAudioContext;
  // and no GStreamer decoding either (the test machine's WebKit can't decode AAC, and its decoder pipeline crashes the page)
  const decoded = { calls: 0, bytes: 0 };
  BaseAudioContext.prototype.decodeAudioData = function (data, ok) {
    decoded.calls++; decoded.bytes += data ? data.byteLength : 0;
    const b = this.createBuffer(2, 4410, 44100);
    if (ok) setTimeout(() => ok(b), 0);
    return Promise.resolve(b);
  };
  window.__decoded = decoded;
  // media elements (Unity plays streamed clips through <audio>) would reach the same missing sink
  const media = HTMLMediaElement.prototype;
  media.play = function () { console.log("[check-mobile] <audio>.play() skipped: " + (this.currentSrc || this.src || "").slice(0, 80)); return Promise.resolve(); };
  media.load = function () { };
  const srcDesc = Object.getOwnPropertyDescriptor(media, "src");
  Object.defineProperty(media, "src", { get() { return this.__src || ""; }, set(v) { this.__src = v; console.log("[check-mobile] <audio>.src skipped"); }, configurable: true });
  void srcDesc;
  window.__audio = () => ({ peak: 0, silent: true, contexts: contexts.length, states: contexts.map((c) => c.state),
                            resumed: contexts.some((c) => c.__resumedAfterInput) });
};

// Synthetic touches in the page (WebKit: Playwright can only tap). Each call is one TouchEvent on the element under the
// first point; ids stay the same across a gesture.
const touchHelper = () => {
  const active = new Map();
  window.__touch = (type, points) => {
    const target0 = active.get(points[0]?.id)?.target || document.elementFromPoint(points[0].x, points[0].y) || document.body;
    // WebKit has no Touch constructor, only the older document.createTouch (whose TouchEvent wants TouchLists)
    const legacy = typeof document.createTouch === "function";
    const mk = (p) => legacy
      ? document.createTouch(window, active.get(p.id)?.target || target0, p.id, p.x + scrollX, p.y + scrollY, p.x, p.y)
      : new Touch({ identifier: p.id, target: active.get(p.id)?.target || target0, clientX: p.x, clientY: p.y, pageX: p.x, pageY: p.y,
                    screenX: p.x, screenY: p.y, radiusX: 8, radiusY: 8, force: 1 });
    const list = (a) => (legacy ? document.createTouchList(...a) : a);
    const changed = points.map(mk);
    if (type === "touchstart") for (const t of changed) active.set(t.identifier, { target: t.target, touch: t });
    for (const t of changed) if (active.has(t.identifier)) active.get(t.identifier).touch = t;
    const all = [...active.values()].map((v) => v.touch);
    if (type === "touchend" || type === "touchcancel") for (const t of changed) active.delete(t.identifier);
    const remaining = [...active.values()].map((v) => v.touch);
    const ending = type === "touchend" || type === "touchcancel";
    const ev = new TouchEvent(type, { bubbles: true, cancelable: true, composed: true, touches: list(ending ? remaining : all),
                                      targetTouches: list(ending ? remaining : all), changedTouches: list(changed) });
    changed[0].target.dispatchEvent(ev);
    // a browser follows a touch with pointer events too; the page's controls listen for those
    for (const t of changed) {
      const ptype = { touchstart: "pointerdown", touchmove: "pointermove", touchend: "pointerup", touchcancel: "pointercancel" }[type];
      t.target.dispatchEvent(new PointerEvent(ptype, { bubbles: true, cancelable: true, composed: true, pointerId: 100 + t.identifier, pointerType: "touch",
                                                         isPrimary: t.identifier === points[0].id && active.size <= 1, clientX: t.clientX, clientY: t.clientY,
                                                         button: 0, buttons: type === "touchend" || type === "touchcancel" ? 0 : 1, width: 16, height: 16, pressure: 0.5 }));
    }
    return { prevented: ev.defaultPrevented };
  };
};

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

// ------------------------------------------------------------------ one device
async function runDevice(pw, key, prof, url) {
  const out = path.join(outRoot, key);
  mkdirSync(out, { recursive: true });
  const logFile = path.join(out, "check.log"), consoleFile = path.join(out, "console.log");
  writeFileSync(logFile, ""); writeFileSync(consoleFile, "");
  const results = [];
  const log = (line) => { console.log(`[${key}] ${line}`); appendFileSync(logFile, line + "\n"); };
  const check = (ok, what) => { results.push({ ok, what }); log(`${ok ? "PASS" : "FAIL"} ${what}`); return ok; };
  const note = (what) => log(`NOTE ${what}`);
  const errors = [];
  let consoleLines = 0;

  log(`${prof.name}; page ${url}; load average ${os.loadavg().map((x) => x.toFixed(1)).join(" ")}`);
  const browser = await pw[prof.engine].launch({ headless: true, ...prof.launch });
  log(`browser version ${browser.version()}`);
  const context = await browser.newContext({ ...prof.context });
  if (!process.env.NO_GL_PROBE) await context.addInitScript(glProbe);
  await context.addInitScript(prof.engine === "webkit" ? silentAudio : audioProbe);
  await context.addInitScript(touchHelper);
  const page = await context.newPage();
  const cdp = prof.engine === "chromium" && prof.touch ? await context.newCDPSession(page) : null;
  let bytes = 0;
  const files = {};
  page.on("console", (m) => {
    if (consoleLines++ < 4000) appendFileSync(consoleFile, `[${m.type()}] ${m.text().slice(0, 2000)}\n`);
    if (m.type() === "error") errors.push("console: " + m.text().slice(0, 400));
  });
  page.on("pageerror", (e) => errors.push("page: " + e.message));
  page.on("crash", () => errors.push("the page crashed"));
  page.on("requestfailed", (r) => errors.push(`download failed: ${r.url()} (${r.failure()?.errorText})`));
  page.on("response", (r) => {
    if (r.status() >= 400) errors.push(`HTTP ${r.status()}: ${r.url()}`);
    const len = Number(r.headers()["content-length"] || 0);
    bytes += len;
    files[r.url().split("/").pop() || "index.html"] = len;
  });

  // ---- memory sampling
  const mem = { byName: {}, jsPeak: 0, heap: 0, heapPeak: 0, used: 0, usedPeak: 0, gl: null, glPeak: 0, procPeak: 0, procPeakName: "", totalPeak: 0, samples: 0 };
  let sampling = true;
  // the processes on a timer of their own (the page can be too busy to answer for seconds at a time)
  const procTimer = setInterval(() => {
    const procs = processMemory();
    const total = procs.reduce((a, p) => a + p.mb, 0);
    const top = procs.reduce((a, p) => (p.mb > a.mb ? p : a), { mb: 0, name: "" });
    mem.totalPeak = Math.max(mem.totalPeak, total);
    if (top.mb > mem.procPeak) { mem.procPeak = top.mb; mem.procPeakName = top.name; }
    for (const p of procs) mem.byName[p.name] = Math.max(mem.byName[p.name] || 0, p.mb);
  }, 250);
  const sampler = (async () => {
    while (sampling) {
      try {
        const [heap, gl, js] = await page.evaluate(() => [window.__heap ? window.__heap() : { total: 0, used: 0 }, window.__gl ? window.__gl() : null,
          performance.memory ? performance.memory.usedJSHeapSize : 0]);
        mem.jsPeak = Math.max(mem.jsPeak, js);
        mem.heap = heap.total; mem.heapPeak = Math.max(mem.heapPeak, heap.total);
        mem.used = heap.used; mem.usedPeak = Math.max(mem.usedPeak, heap.used);
        if (gl) { mem.gl = gl; mem.glPeak = Math.max(mem.glPeak, gl.totalPeak + gl.canvas); }
        mem.samples++;
      } catch (e) { }
      await sleep(250);
    }
  })();
  const memLine = () => `wasm heap ${(mem.heap / 1048576).toFixed(0)} MB, ${(mem.used / 1048576).toFixed(0)} MB used (peaks ${(mem.heapPeak / 1048576).toFixed(0)}, ${(mem.usedPeak / 1048576).toFixed(0)}); WebGL now ` +
    `${mem.gl ? `textures ${(mem.gl.textures / 1048576).toFixed(0)} + renderbuffers ${(mem.gl.renderbuffers / 1048576).toFixed(0)} + buffers ${(mem.gl.buffers / 1048576).toFixed(0)} + canvas ${(mem.gl.canvas / 1048576).toFixed(0)} MB` : "?"}` +
    ` (peak ${(mem.glPeak / 1048576).toFixed(0)} MB);${mem.jsPeak ? ` JS heap peak ${(mem.jsPeak / 1048576).toFixed(0)} MB;` : ""} browser processes: busiest ${mem.procPeakName} peak ${mem.procPeak.toFixed(0)} MB, all ${mem.totalPeak.toFixed(0)} MB peak` +
    ` (${Object.entries(mem.byName).sort((a, b) => b[1] - a[1]).slice(0, 4).map(([n, mb]) => `${n} ${mb.toFixed(0)}`).join(", ")})`;

  const state = () => page.evaluate(() => window.pttState || null);
  const waitFor = async (what, test, seconds) => {
    const end = Date.now() + seconds * 1000;
    let s = null;
    while (Date.now() < end) {
      s = await state().catch(() => null);
      if (s && test(s)) return s;
      const load = await page.evaluate(() => window.pttLoad).catch(() => null);
      if (load && load.startsWith("error")) { errors.push("page says: " + load); return null; }
      await sleep(250);
    }
    log(`timed out waiting for ${what}; last state ${JSON.stringify(s)}`);
    return null;
  };
  const shot = (name) => page.screenshot({ path: path.join(out, name + ".png") }).catch(() => { });
  const toPage = async ([x, y]) => page.evaluate(([x, y]) => {
    const c = document.querySelector("#unity-canvas");
    const r = c.getBoundingClientRect();
    return [r.left + x * r.width / c.width, r.top + y * r.height / c.height];
  }, [x, y]);
  const probe = async (skip = 0) => {
    await page.evaluate((skip) => window.unityInstance.SendMessage("WebBridge", "Probe", String(skip)), skip);
    await sleep(150);
    return state();
  };

  // ---- touch: real taps; drags as TouchEvents (CDP in Chromium, synthetic in WebKit)
  // (the DevTools protocol takes every finger still down with each event: it works out which one changed)
  const cdpFingers = new Map();
  const touch = async (type, points) => {
    if (cdp) {
      const cdpType = { touchstart: "touchStart", touchmove: "touchMove", touchend: "touchEnd", touchcancel: "touchCancel" }[type];
      for (const p of points) {
        if (type === "touchend" || type === "touchcancel") cdpFingers.delete(p.id);
        else cdpFingers.set(p.id, { x: p.x, y: p.y, id: p.id, radiusX: 8, radiusY: 8, force: 1 });
      }
      await cdp.send("Input.dispatchTouchEvent", { type: cdpType, touchPoints: [...cdpFingers.values()] });
    } else await page.evaluate(([type, points]) => window.__touch(type, points), [type, points]);
  };
  // A press held for a few frames (Unity reads input once a frame).
  const tap = async (x, y, hold = 120) => {
    if (hold <= 0) return page.touchscreen.tap(x, y);
    await touch("touchstart", [{ id: 1, x, y }]); await sleep(hold); await touch("touchend", [{ id: 1, x, y }]);
  };
  const drag = async (from, to, { steps = 14, hold = 150, ms = 500, release = true } = {}) => {
    await touch("touchstart", [{ id: 1, x: from[0], y: from[1] }]);
    await sleep(hold);
    for (let i = 1; i <= steps; i++) {
      const t = i / steps;
      await touch("touchmove", [{ id: 1, x: from[0] + (to[0] - from[0]) * t, y: from[1] + (to[1] - from[1]) * t }]);
      await sleep(ms / steps);
    }
    await sleep(150);
    if (release) await touch("touchend", [{ id: 1, x: to[0], y: to[1] }]);
  };
  const twoFingerDrag = async (center, dx, dy, steps = 12) => {
    const a = { id: 1, x: center[0] - 40, y: center[1] }, b = { id: 2, x: center[0] + 40, y: center[1] };
    await touch("touchstart", [a]); await sleep(30);
    await touch("touchstart", [a, b]); await sleep(80);
    for (let i = 1; i <= steps; i++) {
      const p = [{ ...a, x: a.x + dx * i / steps, y: a.y + dy * i / steps }, { ...b, x: b.x + dx * i / steps, y: b.y + dy * i / steps }];
      await touch("touchmove", p); await sleep(40);
    }
    await touch("touchend", [{ ...b, x: b.x + dx, y: b.y + dy }]); await sleep(30);
    await touch("touchend", [{ ...a, x: a.x + dx, y: a.y + dy }]);
  };
  // The page's on-screen controls: name -> bounding box centre, if shown.
  const controls = () => page.evaluate(() => {
    const out = {};
    for (const el of document.querySelectorAll("[data-touch]")) {
      const r = el.getBoundingClientRect();
      const cs = getComputedStyle(el);
      if (r.width > 0 && r.height > 0 && cs.visibility !== "hidden" && cs.display !== "none" && Number(cs.opacity) > 0.05)
        out[el.dataset.touch] = { x: r.left + r.width / 2, y: r.top + r.height / 2, w: r.width, h: r.height };
    }
    return out;
  }).catch(() => ({}));
  const pressControl = async (name, hold = 140) => {
    const c = (await controls())[name];
    if (!c) { log(`no on-screen control "${name}" to press`); return false; }
    await tap(c.x, c.y, hold);
    return true;
  };

  let failedEarly = false;
  try {
    const env = await page.evaluate(() => ({ coarse: matchMedia("(pointer: coarse)").matches, fine: matchMedia("(any-pointer: fine)").matches,
      hover: matchMedia("(hover: hover)").matches, webgl2: !!document.createElement("canvas").getContext("webgl2"), webgpu: !!navigator.gpu,
      dpr: devicePixelRatio, w: innerWidth, h: innerHeight, ua: navigator.userAgent, maxTouch: navigator.maxTouchPoints })).catch(() => null);
    log(`page: ${JSON.stringify(env)}`);

    const t0 = Date.now();
    await page.goto(url, { waitUntil: "load" });
    const title = await waitFor("the title screen", (s) => s.mode === "Title", opts.timeout);
    const secs = (Date.now() - t0) / 1000;
    await sleep(2500);
    await shot("01-title");
    check(!!title, `${title ? "reached the title screen" : "never reached the title screen"} in ${secs.toFixed(1)} s; downloaded ${(bytes / 1048576).toFixed(1)} MB ` +
      `(${Object.entries(files).filter(([, n]) => n > 1048576).map(([f, n]) => `${f} ${(n / 1048576).toFixed(1)} MB`).join(", ")})`);
    if (!title) failedEarly = true;
    log(`state at the title: ${JSON.stringify(title)}`);
    log(`memory at the title: ${memLine()}`);
    log(`biggest WebGL objects at the title:\n    ${(await page.evaluate(() => window.__glTop ? window.__glTop(14) : []).catch(() => [])).join("\n    ")}`);

    if (prof.touch) {
      // the game is laid out for landscape: a phone held upright is asked to turn
      const vp = page.viewportSize();
      const rotate = await page.evaluate(() => { const r = document.querySelector("#rotate"); return !!r && getComputedStyle(r).display !== "none"; }).catch(() => null);
      const portrait = vp.height > vp.width;
      if (rotate !== null) check(rotate === portrait, `the "turn it sideways" prompt ${rotate ? "shows" : "is hidden"} in ${portrait ? "portrait" : "landscape"}`);
      if (rotate) await shot("01-rotate");
    }
    const shown = await controls();
    const page0 = await page.evaluate(() => ({ touchUi: document.documentElement.dataset.touch || null, note: getComputedStyle(document.querySelector("#note") || document.body).display }));
    log(`on-screen controls at the title: ${Object.keys(shown).join(", ") || "none"}; html data-touch=${page0.touchUi}`);
    if (!prof.touch) {
      check(Object.keys(shown).length === 0 && page0.touchUi !== "on", "no on-screen touch controls on desktop");
      // and none after the mouse and keyboard are used
      await page.mouse.move(800, 450); await page.mouse.down(); await sleep(100); await page.mouse.up(); await sleep(600);
      await page.keyboard.press("Enter"); await sleep(1500);
      const after = await controls();
      check(Object.keys(after).length === 0, `still none after a click and a key: ${Object.keys(after).join(", ") || "none"}`);
    }

    const upright = page.viewportSize().height > page.viewportSize().width;
    if (opts.play && title && prof.touch && upright) log("upright: the touch session runs on the landscape profiles (here the game asks to be turned)");
    if (opts.play && title && prof.touch && !upright) await playByTouch();
    else if (title && prof.touch) {
      // without --play: just what a first tap and a drag on the game do
      const vp = page.viewportSize();
      await page.touchscreen.tap(vp.width / 2, vp.height / 2);
      await sleep(2500);
      const s1 = await state();
      log(`after one tap at the title: waiting=${s1.waiting} (false = the menu opened), pointer ${JSON.stringify(s1.pointer)}`);
      await shot("02-after-tap");
      await drag([vp.width * 0.3, vp.height * 0.6], [vp.width * 0.7, vp.height * 0.4], { release: false });
      const s2 = await state();
      log(`during a one-finger drag to ${Math.round(vp.width * 0.7)},${Math.round(vp.height * 0.4)} css px: pointer ${JSON.stringify(s2.pointer)} (game pixels, top-left origin)`);
      await touch("touchend", [{ id: 1, x: vp.width * 0.7, y: vp.height * 0.4 }]);
      const audio = await page.evaluate(() => window.__audio());
      log(`audio after the tap: ${JSON.stringify(audio)}`);
      log(`on-screen controls in the menu: ${Object.keys(await controls()).join(", ") || "none"}`);
      await sleep(4000);
    }
    // let it settle and run a while for the frame rate
    const s = await state().catch(() => null);
    log(`frame rate: ${s?.fps} fps at ${s?.w}x${s?.h} (fidelity ${s?.fidelity}; headless, load ${os.loadavg()[0].toFixed(1)})`);
  } catch (e) {
    errors.push("check script: " + (e.stack || e.message));
  }

  async function playByTouch() {
    const vp = page.viewportSize();
    // ---- audio: a tap at the title unlocks it and opens the menu
    const before = await page.evaluate(() => window.__audio());
    await page.touchscreen.tap(vp.width / 2, vp.height / 2);
    await sleep(2500);
    let after = await page.evaluate(() => window.__audio());
    let peak = after.peak;
    for (let i = 0; i < 10 && peak < 0.001; i++) { await sleep(300); peak = Math.max(peak, (await page.evaluate(() => window.__audio())).peak); }
    if (after.silent) check(after.resumed && after.states.every((s) => s === "running"),
      `the game resumes its audio after the first tap (WebKit here has no audio output: ${after.contexts} context(s), ${after.states.join(",")}; before ${before.states.join(",") || "no context"})`);
    else check(peak > 0.001 && after.states.length > 0 && after.states.every((s) => s === "running"),
      `audio plays after the first tap: ${peak.toFixed(4)} peak (${after.states.join(",")}); before ${before.peak.toFixed(4)} (${before.states.join(",") || "no context"})`);
    let s = await waitFor("the title menu", (s) => s.mode === "Title" && !s.waiting, 8);
    check(!!s, "a tap at the title opens the main menu");
    await shot("02-menu");
    const shownMenu = await controls();
    log(`on-screen controls in the menu: ${Object.keys(shownMenu).join(", ") || "none"}`);

    // ---- the menu by tap: the game publishes where its buttons are (pttState.buttons, page pixels)
    s = await state();
    // the game's own buttons (its uGUI), found by name or label: WebBridge.Probe lists the ones a tap would reach
    const button = async (label) => {
      const st = await probe();
      const want = label.toLowerCase();
      const b = (st.buttons || []).find((b) => b.name.toLowerCase() === want || (b.label || "").toLowerCase().replace(/<[^>]*>/g, "").trim() === want)
        || (st.buttons || []).find((b) => b.name.toLowerCase().includes(want) || (b.label || "").toLowerCase().includes(want));
      return b ? await toPage(b.at) : null;
    };
    const tapButton = async (label, wait = 1500) => {
      const at = await button(label);
      if (!at) { log(`no button "${label}" on screen; buttons: ${JSON.stringify((await state()).buttons?.map((b) => b.name))}`); return false; }
      await tap(at[0], at[1], 150);
      await sleep(wait);
      return true;
    };
    const play = (await tapButton("Continue", 2500)) || (await tapButton("New", 2500)) || (await tapButton("Play", 2500));
    s = await waitFor("the story card or packing", (s) => s.mode === "Story" || s.mode === "Playing", 20);
    check(!!s && play, `tapping the menu's play button starts a trip (${s?.mode})`);
    await shot("03-story");
    for (let i = 0; i < 12 && s && s.mode !== "Playing"; i++) {
      // LET'S PACK! comes up once the texts are read
      if (!(await tapButton("Start", 1500)) && !(await tapButton("let's pack!", 1500))) await sleep(1500);
      s = await state();
    }
    s = await waitFor("packing", (s) => s.mode === "Playing" && s.items > 0, 30);
    check(!!s, `taps through the story into packing (${s?.level}, ${s?.items} things to pack)`);
    if (!s) return;
    await sleep(3000);
    await shot("04-packing");
    const shownPlay = await controls();
    log(`on-screen controls while packing: ${Object.keys(shownPlay).join(", ") || "none"}`);
    log(`memory while packing: ${memLine()}`);

    // ---- drag one thing from the blanket into the trunk; the ghost shows above the finger
    const packOne = async (skip, verbs) => {
      let s = await probe(skip);
      if (!s.pile) return false;
      const from = await toPage(s.pile);
      // touch the item, then (still holding) find where it would fit
      await touch("touchstart", [{ id: 1, x: from[0], y: from[1] }]);
      await sleep(250);
      s = await probe();
      if (!s.held) { await touch("touchend", [{ id: 1, x: from[0], y: from[1] }]); log(`touching ${s.pile} didn't pick anything up: ${JSON.stringify(s)}`); return false; }
      // the aim is where the ghost should be: the finger goes below it by the game's offset
      const finger = async (aim) => { const [ax, ay] = await toPage(aim); const off = (await state()).touchOffset || 0; return [ax, ay + off * (await page.evaluate(() => { const c = document.querySelector("#unity-canvas"); return c.getBoundingClientRect().height / c.height; }))]; };
      if (!s.aim) {
        await sleep(600);
        s = await probe();
      }
      if (!s.aim) {
        // (lifting a finger that didn't move keeps the thing in hand: BACK puts it down again)
        log(`no spot in the trunk for the held thing: ${JSON.stringify({ held: s.held, pile: s.pile, aim: s.aim, packed: s.packed })}; putting it back`);
        await touch("touchend", [{ id: 1, x: from[0], y: from[1] }]);
        await sleep(400);
        await pressControl("back");
        await sleep(600);
        return false;
      }
      const to = await finger(s.aim);
      for (let i = 1; i <= 12; i++) { await touch("touchmove", [{ id: 1, x: from[0] + (to[0] - from[0]) * i / 12, y: from[1] + (to[1] - from[1]) * i / 12 }]); await sleep(40); }
      await sleep(300);
      const mid = await state();
      if (verbs) {
        // while it's held: the turn button (a second finger), then let go
        await shot("05-dragging");
        log(`while dragging: held=${mid.held}, controls ${Object.keys(await controls()).join(", ")}`);
      }
      const before = mid.packed;
      await touch("touchend", [{ id: 1, x: to[0], y: to[1] }]);
      await sleep(1200);
      return (await state()).packed > before;
    };
    let packed = 0;
    for (let n = 0; n < 6 && packed < 2; n++) { await packOne(n > 2 ? n - 2 : 0, n === 0); packed = (await state()).packed; }
    await shot("06-packed");
    check(packed >= 2, `dragging with a finger packs things into the trunk: ${packed} packed`);

    // ---- tap to pick up, then the verbs by button: turn, tip, roll, height, then place with a tap
    s = await probe(0);
    if (s.pile) {
      const at = await toPage(s.pile);
      await tap(at[0], at[1]);
      await sleep(700);
      s = await state();
      check(s.held, "tapping a thing on the blanket picks it up");
      const verbs = {};
      for (const v of ["turn", "tip", "roll", "up", "down"]) verbs[v] = await pressControl(v, 140);
      await sleep(400);
      await shot("07-held-verbs");
      check(Object.values(verbs).every(Boolean), `turn, tip, roll and the two height buttons are on screen while holding: ${JSON.stringify(verbs)}`);
      const rot = (await state()).rot;
      log(`held rotation after turn/tip/roll: ${rot}`);
      s = await probe();
      if (s.aim) {
        const [ax, ay] = await toPage(s.aim);
        const before = s.packed;
        // a tap places it where the ghost is (the tap point is where the finger lands, the ghost above it)
        await tap(ax, ay + (s.touchOffset || 0) * (await page.evaluate(() => { const c = document.querySelector("#unity-canvas"); return c.getBoundingClientRect().height / c.height; })));
        await sleep(1200);
        s = await state();
        check(s.packed > before || !s.held, `a tap drops it: packed ${before} → ${s.packed}, held ${s.held}`);
      }
      if ((await state()).held) await pressControl("back");
    }

    // ---- undo, redo, hint, x-ray, orbit, pause
    s = await state();
    const p0 = s.packed;
    await pressControl("undo"); await sleep(900);
    const p1 = (await state()).packed;
    await pressControl("redo"); await sleep(900);
    const p2 = (await state()).packed;
    check(p1 === p0 - 1 && p2 === p0, `undo and redo buttons: ${p0} → ${p1} → ${p2} packed`);
    await pressControl("hint"); await sleep(1500);
    s = await state();
    check(s.hint, `the hint button asks Grandpa (hint showing: ${s.hint})`);
    await shot("08-hint");
    const xr = (await controls()).xray;
    if (xr) {
      await touch("touchstart", [{ id: 3, x: xr.x, y: xr.y }]); await sleep(700);
      s = await state();
      await shot("09-xray");
      await touch("touchend", [{ id: 3, x: xr.x, y: xr.y }]); await sleep(500);
      const s2 = await state();
      check(s.xray && !s2.xray, `holding the X-ray button turns X-ray on, letting go turns it off (${s.xray} → ${s2.xray})`);
    } else check(false, "an X-ray button");
    const yaw0 = (await state()).yaw;
    await twoFingerDrag([vp.width / 2, vp.height / 2], vp.width * 0.3, 0);
    await sleep(800);
    const yaw1 = (await state()).yaw;
    check(Math.abs((yaw1 ?? 0) - (yaw0 ?? 0)) > 5, `a two-finger drag orbits the trunk (camera yaw ${yaw0} → ${yaw1})`);
    await shot("10-orbit");
    await pressControl("pause"); await sleep(1200);
    s = await state();
    check(s.paused, "the pause button pauses");
    await shot("11-paused");
    const resumed = await tapButton("Resume", 1200);
    s = await state();
    check(resumed && !s.paused, "tapping RESUME carries on");
    log(`memory after the session: ${memLine()}`);
  }

  sampling = false;
  clearInterval(procTimer);
  await sampler;
  log(`memory peaks: ${memLine()} (${mem.samples} samples)`);
  writeFileSync(path.join(out, "memory.json"), JSON.stringify(mem, null, 1));
  check(errors.length === 0, errors.length ? `errors (${errors.length}):\n  ` + errors.slice(0, 20).join("\n  ") : "no console errors, page errors, crashes or failed downloads");
  await context.close().catch(() => { });
  await browser.close().catch(() => { });
  const failed = results.filter((r) => !r.ok).length;
  log(`${failed === 0 ? "OK" : "FAILED"}: ${results.length - failed}/${results.length} checks passed; log ${logFile}`);
  return { key, ok: failed === 0, failedEarly, mem };
}

async function main() {
  process.env.TMPDIR = path.join(root, "Logs", "tmp");
  mkdirSync(process.env.TMPDIR, { recursive: true });
  const pw = require(findPlaywright());
  const all = profiles(pw);
  const keys = opts.device === "all" ? ["iphone", "iphone-landscape", "ipad", "pixel", "pixel-landscape", "desktop-chromium", "desktop-firefox"]
    : opts.device === "desktop" ? ["desktop-chromium", "desktop-firefox"] : opts.device.split(",");
  let server = null, url = opts.url;
  if (opts.serve) { const s = await serve(path.resolve(opts.serve)); server = s.server; url = s.url; }
  let ok = true;
  try {
    for (const k of keys) {
      if (!all[k]) { console.error("unknown device " + k); ok = false; continue; }
      const r = await runDevice(pw, k, all[k], url);
      ok = r.ok && ok;
    }
  } finally {
    if (server) server.close();
  }
  process.exit(ok ? 0 : 1);
}

main().catch((e) => { console.error(e); process.exit(1); });
