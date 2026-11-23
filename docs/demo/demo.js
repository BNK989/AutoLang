/*
 * The try-it box on the home page. It behaves like a PC running AutoLang: words typed on the wrong keyboard are
 * fixed at the next space and the box's own keyboard switches, so the keys you press after that come out in the
 * new language. Tap Shift twice to undo a fix or to convert a word. Everything runs here; nothing is sent anywhere.
 */
(function () {
  'use strict';
  var E = window.AutoLangEngine, box = document.querySelector('.try-box');
  if (!E || !box) return;

  var L = E.layouts, PAIRS = ['he', 'ar', 'ru', 'uk', 'el'];
  var SAMPLES = {
    he: ['שלום מה שלומך', 'see you tomorrow'],
    ar: ['مرحبا كيف حالك', 'thank you very much'],
    ru: ['привет как дела', 'see you tomorrow'],
    uk: ['привіт як справи', 'see you soon'],
    el: ['καλημέρα τι κάνεις', 'good morning']
  };
  var CONFIRM = E.options.tHigh, MAX_RUN = 200, TRIM_TO = 120;

  var input = document.getElementById('try-input'), backdrop = box.querySelector('.try-backdrop');
  var statusEl = document.getElementById('try-status'), pill = document.getElementById('try-pill');
  var chipsEl = document.getElementById('try-chips'), endEl = document.getElementById('try-end');
  var pairButtons = [].slice.call(box.querySelectorAll('[data-pair]'));
  var base = document.currentScript ? document.currentScript.src : location.href;

  var profiles = {}, loading = {};
  var pair = pickPair(), sim = 'en', os = null;
  var runStart = 0, prevLang = null, last = null, rejected = null, ignored = new Set();
  var fixes = 0, revealTimer = 0, dead = null, shiftAlone = false, lastShiftTap = 0, auto = null, markTimer = 0;

  function say(key, kbd) { return (box.getAttribute('data-' + key) || '').replace('{kbd}', kbd || ''); }
  function other(id) { return id === 'en' ? pair : 'en'; }
  function inPair(id) { return id === 'en' || id === pair; }
  function ready() { return !!(profiles.en && profiles[pair]); }

  function pickPair() {
    var lang = box.getAttribute('data-page-lang');
    if (PAIRS.indexOf(lang) >= 0) return lang;
    var list = navigator.languages || [navigator.language || ''];
    for (var i = 0; i < list.length; i++) {
      var p = String(list[i]).toLowerCase().split('-')[0];
      if (p === 'iw') p = 'he';
      if (PAIRS.indexOf(p) >= 0) return p;
    }
    return 'he';
  }

  // ---- word lists: fetched the first time the box comes near the screen ----

  function load(id) {
    if (profiles[id]) return Promise.resolve(profiles[id]);
    if (!loading[id]) loading[id] = fetch(new URL('words/' + id + '.txt', base).href)
      .then(function (r) { if (!r.ok) throw new Error(r.status); return r.text(); })
      .then(function (text) {
        return new Promise(function (done) { setTimeout(function () { done(profiles[id] = new E.Profile(id, text)); }, 0); });
      })
      .catch(function (err) { delete loading[id]; throw err; });
    return loading[id];
  }

  function loadPair() {
    var want = pair;
    if (!ready()) status(say('loading'));
    return Promise.all([load('en'), load(want)]).then(function () {
      if (want === pair && !statusEl.getAttribute('data-busy')) status(say('ready'));
    }, function () { status(say('failed')); });
  }

  // ---- what's on screen ----

  function status(text, was, now) {
    statusEl.textContent = '';
    if (was !== undefined) {
      var a = document.createElement('bdi'), b = document.createElement('bdi'), arrow = document.createElement('span');
      a.className = 'was'; a.textContent = was.trim();
      b.className = 'now'; b.textContent = now.trim();
      arrow.className = 'arrow'; arrow.textContent = '→';
      statusEl.appendChild(a); statusEl.appendChild(arrow); statusEl.appendChild(b);
      statusEl.appendChild(document.createTextNode(' ' + text));
    } else statusEl.textContent = text;
  }

  function setSim(id, flash) {
    sim = id;
    pill.textContent = L[id].pill;
    pill.classList.remove('flip');
    if (flash) { void pill.offsetWidth; pill.classList.add('flip'); }
  }

  // An empty box has no text to take its direction from, so it follows the placeholder's language; with text in it,
  // each line runs in the direction of its first letter.
  function setDir() {
    var dir = input.value ? 'auto' : L[pair].rtl ? 'rtl' : 'ltr';
    if (input.dir !== dir) { input.dir = dir; backdrop.dir = dir; }
  }

  // A copy of the text behind the textarea, so a fixed word can light up in place.
  function paint(from, to) {
    var v = input.value;
    setDir();
    backdrop.textContent = '';
    if (from === undefined) { backdrop.textContent = v + '\n'; return; }
    var mark = document.createElement('mark');
    mark.textContent = v.slice(from, to);
    backdrop.appendChild(document.createTextNode(v.slice(0, from)));
    backdrop.appendChild(mark);
    backdrop.appendChild(document.createTextNode(v.slice(to) + '\n'));
    clearTimeout(markTimer);
    requestAnimationFrame(function () { mark.classList.add('in'); });
    markTimer = setTimeout(function () { mark.classList.remove('in'); }, 1400);
    syncScroll();
  }
  function syncScroll() { backdrop.scrollTop = input.scrollTop; }

  function replace(from, to, text) {
    var caret = input.selectionStart, delta = text.length - (to - from);
    input.setRangeText(text, from, to, 'preserve');
    var at = caret >= to ? caret + delta : caret;
    input.setSelectionRange(at, at);
  }

  // ---- typing ----

  function insert(text) {
    input.setRangeText(text, input.selectionStart, input.selectionEnd, 'end');
    afterInsert(text);
  }

  /** A key pressed on the box's own keyboard. */
  function typeKey(code, shift) {
    var t = L[sim].keyText(code, shift);
    if (t === undefined) return false;
    if (dead !== null) {
      var composed = (L[sim].dead[dead] || {})[t];
      var d = dead; dead = null;
      insert(composed !== undefined ? composed : d + t);
      return true;
    }
    if (L[sim].dead[t]) { dead = t; return true; }
    insert(t);
    return true;
  }

  function isWordChar(ch) {
    var o = L[other(sim)], part = L[sim].parse(ch)[0];
    if (part.keys) return part.keys.some(function (k) { return L[sim].isWordKey(k) || o.isWordKey(k); });
    return /[\p{L}\p{M}']/u.test(ch);
  }

  function afterInsert(text) {
    var caret = input.selectionStart;
    if (caret - text.length < runStart) runStart = Math.max(0, caret - text.length);
    var ch = text[text.length - 1];
    if (/\s/.test(ch) || !isWordChar(ch)) {
      if (/\S/.test(input.value.slice(runStart, caret))) decideAt(caret);
      if (ch === '\n') { runStart = input.selectionStart; last = null; }
    }
    trim();
    paint();
  }

  function afterDelete() {
    var caret = input.selectionStart, v = input.value;
    if (caret < runStart) {
      var s = caret;
      while (s > 0 && !/\s/.test(v[s - 1])) s--;
      runStart = s;
    }
    // Deleting a whole fix puts the keyboard back; typing the same word again teaches the box to leave it alone.
    if (last && caret <= last.start) {
      rejected = { lang: last.from, words: words(last.original, last.from) };
      setSim(last.from, true);
      prevLang = null;
      last = null;
    }
    paint();
  }

  function words(text, lang) {
    return E.tokens(text, L[lang], L[other(lang)]).map(function (t) { return t.text.toLowerCase(); });
  }

  function trim() {
    var caret = input.selectionStart;
    if (caret - runStart <= MAX_RUN) return;
    var v = input.value, cut = v.indexOf(' ', caret - TRIM_TO);
    runStart = cut < 0 || cut >= caret ? caret : cut + 1;
  }

  function resetRun() { runStart = input.selectionStart; dead = null; }

  // ---- the decision at a word boundary (SessionController.DecideAt) ----

  function decideAt(caret) {
    if (!ready()) { loadPair(); return; }
    var typed = input.value.slice(runStart, caret), o = other(sim), w = words(typed, sim);

    if (rejected && rejected.lang === sim) {
      var same = w.length <= rejected.words.length && w.every(function (x, i) { return x === rejected.words[i]; });
      if (same && w.length === rejected.words.length) {
        rejected.words.forEach(function (x) { ignored.add(x); });
        rejected = null; prevLang = sim; runStart = caret;
        status(say('learnt'));
        return;
      }
      if (same) return;
      rejected = null;
    }
    if (w.length && w.every(function (x) { return ignored.has(x); })) { prevLang = sim; runStart = caret; return; }

    var d = E.decide(typed, L[sim], L[o], profiles[sim], profiles[o], prevLang);
    if (d.kind === 'correct') {
      var start = runStart;
      replace(start, caret, d.replacement);
      last = { start: start, original: typed, replacement: d.replacement, from: sim, to: o };
      runStart = start + d.replacement.length;
      prevLang = o;
      setSim(o, true);
      status(say('fixed', L[o].pill), typed, d.replacement);
      paint(start, start + d.replacement.trimEnd().length);
      fixed();
      return;
    }
    if (d.kind === 'keep' && d.margin <= -CONFIRM) { prevLang = sim; runStart = caret; }
  }

  // ---- tap Shift twice: undo the last fix, convert a selection, or convert the word it missed ----

  function doubleShift() {
    stopAuto();
    var v = input.value, from = input.selectionStart, to = input.selectionEnd;
    if (from === to && last && v.substr(last.start, last.replacement.length) === last.replacement &&
        !/[\p{L}\p{M}]/u.test(v.slice(last.start + last.replacement.length, from)) && from >= last.start + last.replacement.length) {
      replace(last.start, last.start + last.replacement.length, last.original);
      words(last.original, last.from).forEach(function (x) { ignored.add(x); });
      setSim(last.from, true);
      prevLang = last.from;
      runStart = input.selectionStart;
      status(say('undone'), last.replacement, last.original);
      paint(last.start, last.start + last.original.trimEnd().length);
      last = null;
      return;
    }
    if (from === to) {
      var end = from;
      while (end > 0 && /\s/.test(v[end - 1])) end--;
      from = end;
      while (from > 0 && !/\s/.test(v[from - 1])) from--;
      to = end;
    }
    var text = v.slice(from, to), src = majority(text);
    if (!src) return;
    var dst = other(src), out = L[src].transliterateTo(L[dst], text);
    if (out === text) return;
    replace(from, to, out);
    last = { start: from, original: text, replacement: out, from: src, to: dst };
    setSim(dst, true);
    prevLang = dst;
    runStart = input.selectionStart;
    status(say('converted', L[dst].pill), text, out);
    paint(from, from + out.length);
    fixed();
  }

  function majority(text) {
    var n = { en: 0 }; n[pair] = 0;
    for (var i = 0; i < text.length; i++) {
      var id = E.layoutOfChar(text[i], ['en', pair]);
      if (id) n[id]++;
    }
    return n.en === 0 && n[pair] === 0 ? null : n[pair] > n.en ? pair : 'en';
  }

  // ---- the last word: after a couple of fixes, show what this means on a real PC ----

  function fixed() {
    fixes++;
    clearTimeout(revealTimer);
    if (fixes >= 2) reveal();
    else revealTimer = setTimeout(reveal, 6000);
  }

  function reveal() {
    if (!endEl.hidden) return;
    endEl.hidden = false;
    requestAnimationFrame(function () { endEl.classList.add('in'); });
  }

  // ---- the real keyboard ----

  /** The real keyboard typed a letter of this layout. A change means the visitor switched it by hand. */
  function noteOs(id) {
    if (id === os) return;
    os = id;
    if (inPair(id)) setSim(id, false);
  }

  input.addEventListener('keydown', function (e) {
    if (e.key === 'Shift') { if (!e.repeat) shiftAlone = true; return; }
    shiftAlone = false; lastShiftTap = 0;
    stopAuto();
    if (e.isComposing || e.keyCode === 229 || e.ctrlKey || e.metaKey || e.altKey) return;
    if (/^(Arrow|Home|End|Page)/.test(e.key)) { setTimeout(resetRun, 0); return; }
    if (e.key.length !== 1) return;
    var seen = E.layoutOfChar(e.key);
    if (seen) noteOs(inPair(seen) ? seen : 'other');
    if (sim === os && dead === null) return; // the real keyboard already types the box's language
    var shift = e.shiftKey;
    if (e.getModifierState && e.getModifierState('CapsLock') && /^Key/.test(e.code) && !L[sim].rtl) shift = !shift;
    if (typeKey(e.code, shift)) e.preventDefault();
  });

  input.addEventListener('keyup', function (e) {
    if (e.key !== 'Shift' || !shiftAlone) return;
    shiftAlone = false;
    var now = Date.now();
    if (now - lastShiftTap < 450) { lastShiftTap = 0; doubleShift(); }
    else lastShiftTap = now;
  });

  // Phones and on-screen keyboards report no physical key: re-read the character instead.
  input.addEventListener('beforeinput', function (e) {
    if (e.inputType !== 'insertText' || !e.data) return;
    stopAuto();
    var seen = null;
    for (var i = 0; i < e.data.length && !seen; i++) seen = E.layoutOfChar(e.data[i]);
    if (seen) noteOs(inPair(seen) ? seen : 'other');
    if (!os || !inPair(os) || sim === os) return;
    var mapped = L[os].transliterateTo(L[sim], e.data);
    if (mapped === e.data) return;
    e.preventDefault();
    insert(mapped);
  });

  input.addEventListener('input', function (e) {
    var t = e.inputType || '';
    if (t === 'insertText' && e.data) afterInsert(e.data);
    else if (t === 'insertLineBreak' || t === 'insertParagraph') afterInsert('\n');
    else if (/^delete/.test(t)) afterDelete();
    else { resetRun(); paint(); }
  });

  input.addEventListener('pointerup', function () { resetRun(); lastShiftTap = 0; });
  input.addEventListener('scroll', syncScroll);
  input.addEventListener('focus', loadPair);

  // ---- examples that type themselves ----

  function renderChips() {
    chipsEl.textContent = '';
    var s = SAMPLES[pair];
    [[s[0], pair, 'en'], [s[1], 'en', pair]].forEach(function (x) {
      var keys = L[x[1]].parse(x[0]), shown = L[x[2]].render(keys);
      var b = document.createElement('button');
      b.type = 'button'; b.className = 'chip'; b.dir = 'auto';
      b.innerHTML = '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><polygon points="6 3 20 12 6 21 6 3"/></svg>';
      var span = document.createElement('span');
      span.textContent = shown;
      b.appendChild(span);
      b.addEventListener('click', function () { play(keys, x[2]); });
      chipsEl.appendChild(b);
    });
  }

  function play(parts, wrong) {
    stopAuto();
    loadPair().then(function () {
      if (!ready()) return;
      var v = input.value;
      input.setSelectionRange(v.length, v.length);
      if (v.length && !/\n$/.test(v)) insert('\n');
      resetRun();
      setSim(wrong, false);
      status(say('typing'));
      statusEl.setAttribute('data-busy', '1');
      var queue = [];
      parts.forEach(function (p) { (p.keys || []).forEach(function (k) { queue.push(k); }); if (p.ch) queue.push(p.ch); });
      queue.push(' ');
      var token = auto = {};
      (function step() {
        if (auto !== token) return;
        if (!queue.length) { auto = null; statusEl.removeAttribute('data-busy'); return; }
        var k = queue.shift();
        if (k === ' ') insert(' ');
        else if (/^(Key|Digit|Backquote|Minus|Equal|Bracket|Backslash|Semicolon|Quote|Comma|Period|Slash)/.test(k)) typeKey(k.replace('+', ''), /\+$/.test(k));
        else insert(k);
        setTimeout(step, k === ' ' ? 260 : 70 + Math.random() * 70);
      })();
    });
  }

  function stopAuto() { if (auto) { auto = null; statusEl.removeAttribute('data-busy'); } }

  // ---- the controls ----

  function setPair(id, lazy) {
    pair = id;
    pairButtons.forEach(function (b) {
      var on = b.getAttribute('data-pair') === id;
      b.setAttribute('aria-checked', String(on));
      if (on) input.placeholder = b.getAttribute('data-placeholder');
    });
    setDir();
    stopAuto();
    if (!inPair(sim)) setSim(os && inPair(os) ? os : 'en', false);
    last = null; rejected = null; prevLang = null; resetRun();
    renderChips();
    if (!lazy) loadPair();
  }

  pairButtons.forEach(function (b) {
    b.addEventListener('click', function () { setPair(b.getAttribute('data-pair')); });
  });

  document.querySelector('.try-clear').addEventListener('click', function () {
    stopAuto();
    input.value = '';
    runStart = 0; last = null; rejected = null; prevLang = null; dead = null;
    if (os && inPair(os)) setSim(os, false);
    status(say(ready() ? 'ready' : 'loading'));
    paint();
    input.focus();
  });

  setPair(pair, true);
  status(say('ready'));
  if ('IntersectionObserver' in window) {
    var seen = new IntersectionObserver(function (entries) {
      if (entries.some(function (x) { return x.isIntersecting; })) { seen.disconnect(); loadPair(); }
    }, { rootMargin: '300px' });
    seen.observe(box);
  } else loadPair();
})();
