/*
 * AutoLang demo engine: the website's try-it box. A JavaScript port of src/AutoLang.Engine (KeyboardLayoutTable,
 * CharNGramModel, LanguageProfile, PhraseDecider) and the run rules of src/AutoLang.Session, with the same options.
 * The real app reads layouts from Windows; here they are fixed tables keyed by KeyboardEvent.code.
 * Runs in the browser (window.AutoLangEngine) and in Node (module.exports) for tests.
 */
(function (root) {
  'use strict';

  // Physical keys in row order. Each layout below lists what they type, space-separated, in this order.
  var CODES = ('Backquote Digit1 Digit2 Digit3 Digit4 Digit5 Digit6 Digit7 Digit8 Digit9 Digit0 Minus Equal ' +
    'KeyQ KeyW KeyE KeyR KeyT KeyY KeyU KeyI KeyO KeyP BracketLeft BracketRight Backslash ' +
    'KeyA KeyS KeyD KeyF KeyG KeyH KeyJ KeyK KeyL Semicolon Quote ' +
    'KeyZ KeyX KeyC KeyV KeyB KeyN KeyM Comma Period Slash').split(' ');

  // Windows layouts: US, Hebrew (standard), Arabic (101), Russian, Ukrainian (enhanced), Greek.
  var DEFS = {
    en: {
      pill: 'ENG', script: 'Latin',
      normal: '` 1 2 3 4 5 6 7 8 9 0 - = q w e r t y u i o p [ ] \\ a s d f g h j k l ; \' z x c v b n m , . /',
      shift: '~ ! @ # $ % ^ & * ( ) _ + Q W E R T Y U I O P { } | A S D F G H J K L : " Z X C V B N M < > ?'
    },
    he: {
      pill: 'HEB', script: 'Hebrew', rtl: true,
      normal: '; 1 2 3 4 5 6 7 8 9 0 - = / \' ק ר א ט ו ן ם פ ] [ \\ ש ד ג כ ע י ח ל ך ף , ז ס ב ה נ מ צ ת ץ .',
      shift: '~ ! @ # $ % ^ & * ) ( _ + Q W E R T Y U I O P } { | A S D F G H J K L : " Z X C V B N M > < ?'
    },
    ar: {
      pill: 'ARA', script: 'Arabic', rtl: true,
      normal: 'ذ 1 2 3 4 5 6 7 8 9 0 - = ض ص ث ق ف غ ع ه خ ح ج د \\ ش س ي ب ل ا ت ن م ك ط ئ ء ؤ ر لا ى ة و ز ظ',
      shift: 'ّ ! @ # $ % ^ & * ) ( _ + َ ً ُ ٌ لإ إ ‘ ÷ × ؛ < > | ِ ٍ ] [ لأ أ ـ ، / : " ~ ْ } { لآ آ ’ , . ؟'
    },
    ru: {
      pill: 'RUS', script: 'Cyrillic',
      normal: 'ё 1 2 3 4 5 6 7 8 9 0 - = й ц у к е н г ш щ з х ъ \\ ф ы в а п р о л д ж э я ч с м и т ь б ю .',
      shift: 'Ё ! " № ; % : ? * ( ) _ + Й Ц У К Е Н Г Ш Щ З Х Ъ / Ф Ы В А П Р О Л Д Ж Э Я Ч С М И Т Ь Б Ю ,'
    },
    uk: {
      pill: 'UKR', script: 'Cyrillic',
      normal: '\' 1 2 3 4 5 6 7 8 9 0 - = й ц у к е н г ш щ з х ї ґ ф і в а п р о л д ж є я ч с м и т ь б ю .',
      shift: '₴ ! " № ; % : ? * ( ) _ + Й Ц У К Е Н Г Ш Щ З Х Ї Ґ Ф І В А П Р О Л Д Ж Є Я Ч С М И Т Ь Б Ю ,'
    },
    el: {
      pill: 'ELL', script: 'Greek',
      normal: '` 1 2 3 4 5 6 7 8 9 0 - = ; ς ε ρ τ υ θ ι ο π [ ] \\ α σ δ φ γ η ξ κ λ ΄ \' ζ χ ψ ω β ν μ , . /',
      shift: '~ ! @ # $ % ^ & * ( ) _ + : ΅ Ε Ρ Τ Υ Θ Ι Ο Π { } | Α Σ Δ Φ Γ Η Ξ Κ Λ ¨ " Ζ Χ Ψ Ω Β Ν Μ < > ?',
      // Dead keys: the accent key, then the vowel.
      dead: {
        '΄': { 'α': 'ά', 'ε': 'έ', 'η': 'ή', 'ι': 'ί', 'ο': 'ό', 'υ': 'ύ', 'ω': 'ώ', 'Α': 'Ά', 'Ε': 'Έ', 'Η': 'Ή', 'Ι': 'Ί', 'Ο': 'Ό', 'Υ': 'Ύ', 'Ω': 'Ώ' },
        '¨': { 'ι': 'ϊ', 'υ': 'ϋ', 'Ι': 'Ϊ', 'Υ': 'Ϋ' },
        '΅': { 'ι': 'ΐ', 'υ': 'ΰ' }
      }
    }
  };

  var LETTER = /[\p{L}\p{M}]/u, LETTER_OR_DIGIT = /[\p{L}\p{M}\p{Nd}]/u, DIGIT = /\p{Nd}/u;
  function isLetter(c) { return LETTER.test(c); }

  function Layout(id, def) {
    var normal = def.normal.split(' '), shift = def.shift.split(' ');
    if (normal.length !== CODES.length || shift.length !== CODES.length) throw new Error('layout ' + id + ': wrong key count');
    this.id = id; this.pill = def.pill; this.script = def.script; this.rtl = !!def.rtl;
    this.dead = def.dead || {};
    this.text = {};      // key id ("KeyA" / "KeyA+") → text
    this.reverse = {};   // text → [key ids]
    this.maxLen = 1;
    var self = this;
    function put(key, text, unshifted) {
      self.text[key] = text;
      if (!self.reverse[text] || (unshifted && self.reverse[text].length === 1 && /\+$/.test(self.reverse[text][0]))) self.reverse[text] = [key];
      self.maxLen = Math.max(self.maxLen, text.length);
    }
    CODES.forEach(function (code, i) { put(code, normal[i], true); put(code + '+', shift[i], false); });
    // Accented letters come from two keys: the dead key, then the vowel.
    Object.keys(this.dead).forEach(function (d) {
      var deadKey = self.reverse[d][0];
      Object.keys(self.dead[d]).forEach(function (base) {
        var composed = self.dead[d][base];
        if (!self.reverse[composed]) self.reverse[composed] = [deadKey, self.reverse[base][0]];
      });
    });
  }

  Layout.prototype.keyText = function (code, shift) { return this.text[code + (shift ? '+' : '')]; };

  /** Text → what produced it: {key} for text this layout types, {ch} for anything else. Each part keeps its position. */
  Layout.prototype.parse = function (s) {
    var out = [];
    for (var i = 0; i < s.length;) {
      var hit = null;
      for (var n = Math.min(this.maxLen, s.length - i); n >= 1 && !hit; n--) {
        var keys = this.reverse[s.substr(i, n)];
        if (keys) hit = { keys: keys, start: i, len: n };
      }
      if (hit) { out.push(hit); i += hit.len; }
      else { out.push({ ch: s[i], start: i, len: 1 }); i++; }
    }
    return out;
  };

  /** Type a key sequence on this layout, composing dead keys. */
  Layout.prototype.render = function (parts) {
    var s = '', pending = null, self = this;
    parts.forEach(function (p) {
      (p.keys || [null]).forEach(function (key) {
        var t = key ? self.text[key] : p.ch;
        if (t === undefined) t = p.ch || '';
        if (pending !== null) {
          var composed = self.dead[pending][t];
          s += composed !== undefined ? composed : pending + t;
          pending = null;
          return;
        }
        if (key && self.dead[t]) { pending = t; return; }
        s += t;
      });
    });
    if (pending !== null) s += pending;
    return s;
  };

  /** Re-read text that appeared under this layout as the same keys pressed on another. */
  Layout.prototype.transliterateTo = function (target, s) { return target.render(this.parse(s)); };

  Layout.prototype.isWordKey = function (key) {
    var t = this.text[key];
    return t !== undefined && (isLetter(t[0]) || t === '\'' || !!this.dead[t]);
  };

  var layouts = {};
  Object.keys(DEFS).forEach(function (id) { layouts[id] = new Layout(id, DEFS[id]); });

  /** Which demo layout types this character's alphabet, or null. */
  function layoutOfChar(c, among) {
    if (!isLetter(c)) return null;
    var cp = c.codePointAt(0), script =
      cp <= 0x024F || (cp >= 0x1E00 && cp <= 0x1EFF) ? 'Latin' :
      cp >= 0x0370 && cp <= 0x03FF || (cp >= 0x1F00 && cp <= 0x1FFF) ? 'Greek' :
      cp >= 0x0400 && cp <= 0x052F ? 'Cyrillic' :
      cp >= 0x0590 && cp <= 0x05FF ? 'Hebrew' :
      cp >= 0x0600 && cp <= 0x06FF || (cp >= 0xFB50 && cp <= 0xFEFF) ? 'Arabic' : null;
    if (!script) return null;
    var hit = null;
    (among || Object.keys(layouts)).forEach(function (id) {
      if (hit || layouts[id].script !== script) return;
      // Russian and Ukrainian share an alphabet; a letter only one of them has decides.
      if (script === 'Cyrillic' && !layouts[id].reverse[c] && !layouts[id].reverse[c.toLowerCase()]) return;
      hit = id;
    });
    return hit;
  }

  // ---- language model (CharNGramModel.cs) ----

  var START = '\u0002', END = '\u0003';

  function NGram(order) { this.order = order; this.counts = new Map(); this.rows = new Map(); this.vocab = new Set(); }

  NGram.prototype.add = function (word, weight) {
    if (weight <= 0 || !word.length) return;
    var o = this.order, padded = START.repeat(o - 1) + word + END;
    for (var i = o - 1; i < padded.length; i++) {
      var next = padded[i];
      this.vocab.add(next);
      for (var len = 0; len <= o - 1; len++) {
        var ctx = padded.substr(i - len, len), k = ctx + '\u0001' + next;
        var had = this.counts.get(k);
        this.counts.set(k, (had || 0) + weight);
        var row = this.rows.get(ctx);
        if (!row) this.rows.set(ctx, row = [0, 0]);
        row[0] += weight;
        if (had === undefined) row[1]++;
      }
    }
  };

  NGram.prototype.logProb = function (word) {
    if (!word.length) return 0;
    var o = this.order, padded = START.repeat(o - 1) + word + END, lp = 0, base = 1 / (this.vocab.size + 1);
    for (var i = o - 1; i < padded.length; i++) {
      var p = base, next = padded[i];
      for (var len = 0; len <= o - 1; len++) {
        var ctx = padded.substr(i - len, len), row = this.rows.get(ctx);
        if (!row) break;
        var seen = this.counts.get(ctx + '\u0001' + next) || 0;
        p = (seen + row[1] * p) / (row[0] + row[1]);
      }
      lp += Math.log(p);
    }
    return lp;
  };

  // ---- language profiles (LanguageProfile.cs, WordSupplements.cs) ----

  var SUPPLEMENT_EN = ('tbh idk idc imo imho sth smth ux ui ngl afaik iirc irl tbf fwiw tldr wfh lmk nvm brb ttyl hmu omw thx ' +
    'pls plz np ofc ikr wtf lmao ppl btw lol omg fyi asap faq api url tba tbd eod wip pov fomo diy hbu wbu bday tmrw imma kinda sorta').split(' ');

  var RULES = {
    // Hebrew final forms only end a word.
    he: function (w) {
      for (var i = 0; i < w.length - 1; i++)
        if ('ךםןףץ'.indexOf(w[i]) >= 0) {
          for (var j = i + 1; j < w.length; j++) if (isLetter(w[j])) return true;
        }
      return false;
    },
    // Greek final sigma only ends a word.
    el: function (w) {
      var end = w.length;
      while (end > 0 && !isLetter(w[end - 1])) end--;
      for (var i = 0; i < end - 1; i++) if (w[i] === 'ς') return true;
      return false;
    }
  };

  /** A profile from "word count" lines: the dictionary is the list, the 5-gram model is trained on it with ln(1+count). */
  function Profile(id, text) {
    this.id = id;
    this.normalize = id === 'he' ? function (s) { return s; } : function (s) { return s.toLowerCase(); };
    this.rules = RULES[id] || null;
    this.dict = new Map();
    var total = 0, self = this, lines = text.split('\n');
    if (id === 'en') lines = SUPPLEMENT_EN.map(function (w) { return w + ' 3000'; }).concat(lines);
    lines.forEach(function (raw) {
      var line = raw.trim(), sp = line.lastIndexOf(' ');
      if (!line || line[0] === '#' || sp <= 0) return;
      var c = parseInt(line.slice(sp + 1), 10);
      if (!(c > 0)) return;
      var w = self.normalize(line.slice(0, sp));
      self.dict.set(w, (self.dict.get(w) || 0) + c);
      total += c;
    });
    this.logTotal = Math.log(Math.max(total, 1));
    this.lm = new NGram(5);
    this.dict.forEach(function (c, w) {
      var stripped = w.replace(/'/g, '');
      if (stripped.length) self.lm.add(stripped, Math.log(1 + c));
    });
  }

  Profile.prototype.logFreq = function (w) { var c = this.dict.get(w); return c ? Math.log(c) - this.logTotal : 0; };

  // ---- the decision (PhraseDecider.cs) ----

  var OPT = {
    tHigh: 10, tLow: 4, minDecisiveLetters: 5, dictBonus: 2, dictFreqWeight: 0.25,
    impossiblePenalty: 30, innerPunctPenalty: 6, contextPrior: 3
  };

  function score(word, p) {
    var w = p.normalize(word), end = w.length;
    while (end > 0 && !LETTER_OR_DIGIT.test(w[end - 1]) && w[end - 1] !== '\'') end--;
    w = w.slice(0, end);
    if (!w.length) return { s: 0, known: false };
    var bare = w.replace(/'/g, '');
    if (!bare.length) return { s: 0, known: false };
    var s = p.lm.logProb(bare);
    if (p.rules && p.rules(bare)) return { s: s - OPT.impossiblePenalty, known: false };
    var inner = 0;
    for (var i = 0; i < bare.length; i++) if (!isLetter(bare[i]) && bare[i] !== '-') inner++;
    s -= inner * OPT.innerPunctPenalty;
    var key = p.dict.has(w) ? w : p.dict.has(bare) ? bare : null;
    if (key) s += OPT.dictBonus + OPT.dictFreqWeight * (p.logFreq(key) + 10);
    return { s: s, known: !!key };
  }

  /** Words of typed text: runs of keys that type a letter on either layout (under Hebrew, "," is ת). */
  function tokens(typed, tl, ol) {
    var out = [], cur = null;
    tl.parse(typed).forEach(function (part) {
      var word = part.keys
        ? part.keys.some(function (k) { return tl.isWordKey(k) || ol.isWordKey(k); })
        : isLetter(part.ch) || part.ch === '\'';
      if (word) {
        if (!cur) out.push(cur = { start: part.start, text: '' });
        cur.text += typed.substr(part.start, part.len);
      } else cur = null;
    });
    return out;
  }

  function isRepeatedLetter(t) {
    if (t.length < 2 || !isLetter(t[0])) return false;
    var first = t[0].toLowerCase();
    for (var i = 1; i < t.length; i++) if (t[i].toLowerCase() !== first) return false;
    return true;
  }

  /**
   * Does typed text (as it appeared under layout tl) read better as the same keys on layout ol?
   * Returns { kind: 'correct' | 'keep' | 'defer', margin, replacement }.
   */
  function decide(typed, tl, ol, tp, op, prevLang, skip) {
    if (!typed.length) return { kind: 'keep', margin: 0 };
    if (typed.indexOf('@') >= 0 || typed.indexOf('://') >= 0) return { kind: 'keep', margin: 0 };
    var typedScore = 0, otherScore = 0, letters = 0;
    tokens(typed, tl, ol).forEach(function (t) {
      var remapped = tl.transliterateTo(ol, t.text);
      if (remapped === t.text) return;
      var before = typed[t.start - 1], after = typed[t.start + t.text.length];
      if (isRepeatedLetter(t.text) || (before && DIGIT.test(before)) || (after && DIGIT.test(after))) return;
      if (skip && skip(t.text)) return;
      typedScore += score(t.text, tp).s;
      otherScore += score(remapped, op).s;
      for (var i = 0; i < t.text.length; i++) if (isLetter(t.text[i])) letters++;
    });
    var margin = otherScore - typedScore;
    if (prevLang === ol.id) margin += OPT.contextPrior;
    else if (prevLang === tl.id) margin -= OPT.contextPrior;
    if (margin > OPT.tHigh) return { kind: 'correct', margin: margin, replacement: tl.transliterateTo(ol, typed) };
    if (letters < OPT.minDecisiveLetters && Math.abs(margin) < OPT.tLow) return { kind: 'defer', margin: margin };
    return { kind: 'keep', margin: margin };
  }

  var api = { CODES: CODES, layouts: layouts, layoutOfChar: layoutOfChar, Profile: Profile, decide: decide, tokens: tokens, options: OPT };
  if (typeof module === 'object' && module.exports) module.exports = api;
  else root.AutoLangEngine = api;
})(this);
