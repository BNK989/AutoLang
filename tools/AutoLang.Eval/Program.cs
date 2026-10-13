using AutoLang.Engine;
using AutoLang.Eval;

// Usage:
//   AutoLang.Eval <dataDir> [--max N] [--sweep] [--thigh X --tlow Y --dict Z --short S] [--samples N]
//   AutoLang.Eval <dataDir> --explain "<text>" [--layout he|en]
// dataDir holds layouts/*.json, words/{he,en}_50k.txt, corpus/{heb,eng}_sentences.tsv (Tatoeba per-language exports).
Console.OutputEncoding = System.Text.Encoding.UTF8;

var dataDir = args.Length > 0 ? args[0] : "data";
int max = Arg("--max", 5000);
bool sweep = args.Contains("--sweep");
var baseOpt = new EngineOptions { THigh = ArgD("--thigh", 10), TLow = ArgD("--tlow", 4), DictBonus = ArgD("--dict", 2), ShortRunPerLetter = ArgD("--short", 0) };
int samples = Arg("--samples", 15);

var layouts = Directory.GetFiles(Path.Combine(dataDir, "layouts"), "*.json").Select(KeyboardLayoutTable.Load).ToArray();
var he = layouts.First(l => (l.LangId & 0xFF) == 0x0D);
var en = layouts.First(l => (l.LangId & 0xFF) == 0x09);
Console.WriteLine($"layouts: {string.Join(", ", layouts.Select(l => l.ToString()))}");
Evaluator.OtherLayouts[he.LangId] = en;
Evaluator.OtherLayouts[en.LangId] = he;

var sw = System.Diagnostics.Stopwatch.StartNew();
var profiles = new[]
{
    LanguageProfile.FromFrequencyFile(he.LangId, "he", Path.Combine(dataDir, "words", "he_50k.txt"), LanguageProfile.DefaultRulesFor(he.LangId), LanguageProfile.DefaultNormalizeFor(he.LangId)),
    LanguageProfile.FromFrequencyFile(en.LangId, "en", Path.Combine(dataDir, "words", "en_50k.txt"), LanguageProfile.DefaultRulesFor(en.LangId), LanguageProfile.DefaultNormalizeFor(en.LangId)),
};
Console.WriteLine($"profiles trained in {sw.ElapsedMilliseconds} ms ({profiles[0].Dictionary.Count} he words, {profiles[1].Dictionary.Count} en words)");

int explainIdx = Array.IndexOf(args, "--explain");
if (explainIdx >= 0)
{
    // --explain "<text>" [--layout he|en]: per-token score breakdown for both readings.
    var text = args[explainIdx + 1];
    var typedLayout = ArgS("--layout", "he") == "he" ? he : en;
    var otherLayout = typedLayout == he ? en : he;
    var typedProfile = profiles.First(p => p.LangId == typedLayout.LangId);
    var otherProfile = profiles.First(p => p.LangId == otherLayout.LangId);
    var decider = new PhraseDecider(profiles, baseOpt);
    Console.WriteLine($"typed under {typedProfile.Name}: '{text}'  →  as {otherProfile.Name}: '{typedLayout.TransliterateTo(otherLayout, text)}'");
    foreach (var t in Tokenizer.Split(text, typedLayout, otherLayout))
    {
        var remapped = typedLayout.TransliterateTo(otherLayout, t.Text);
        Console.WriteLine($"  token '{t.Text}'  {Breakdown(t.Text, typedProfile, decider, typedLayout)}");
        Console.WriteLine($"     as '{remapped}'  {Breakdown(remapped, otherProfile, decider, otherLayout)}");
    }
    var d = decider.Decide(text, typedLayout, otherLayout);
    Console.WriteLine($"  decision: {d.Kind}  margin {d.Margin:F1}  ({d.Reason})" + (d.Replacement is null ? "" : $"  → '{d.Replacement}'"));
    return;
}

var heSentences = Corpus.LoadTatoeba(Path.Combine(dataDir, "corpus", "heb_sentences.tsv"), he, max).ToList();
var enSentences = Corpus.LoadTatoeba(Path.Combine(dataDir, "corpus", "eng_sentences.tsv"), en, max).ToList();
Console.WriteLine($"corpus: {heSentences.Count} he, {enSentences.Count} en typeable sentences (max {max} each)");

// Each case: the user *intends* `intended` in `intendedLayout`; they typed with `typedLayout` active.
// If typedLayout == intendedLayout the right answer is Keep; otherwise Correct with Replacement == intended.
var cases = new List<EvalCase>();
foreach (var s in heSentences) { cases.Add(new EvalCase(s, he, he, "he")); cases.Add(new EvalCase(s, he, en, "he")); }
foreach (var s in enSentences) { cases.Add(new EvalCase(s, en, en, "en")); cases.Add(new EvalCase(s, en, he, "en")); }

if (sweep)
{
    Console.WriteLine();
    Console.WriteLine("sweep: thigh tlow dict short | precision recall w1-prec w1-defer | fp(good) wrong-text missed | resolved<=2");
    var results = new List<(EngineOptions opt, Report r)>();
    foreach (var dict in new[] { 0.0, 2, 4, 8 })
    foreach (var thigh in new[] { 2.0, 4, 6, 8, 10, 14 })
    foreach (var tlow in new[] { 1.0, 2, 4 })
    foreach (var shortRun in new[] { 0.0, 2, 4 })
    {
        var opt = baseOpt with { THigh = thigh, TLow = tlow, DictBonus = dict, ShortRunPerLetter = shortRun };
        var r = Evaluator.Run(new PhraseDecider(profiles, opt), cases, 0);
        results.Add((opt, r));
        Console.WriteLine($"  {thigh,5} {tlow,4} {dict,4} {shortRun,5} | {r.Precision,8:P2} {r.Recall,7:P1} {r.FirstWordPrecision,8:P2} {r.DeferRateFirstWord,8:P1} | {r.CorrectionsOnGoodText,8} {r.CorrectionsWrongText,10} {r.FullMissed,6} | {r.DeferResolvedWithin2,8:P1}");
    }
    // Under the gates, a wrong correction costs more than a miss (PLAN §1), so: fewest mistakes first, then recall,
    // then the least first-word deferral, then the most conservative THigh among ties.
    var best = results.Where(x => x.r.Precision >= 0.995 && x.r.DeferResolvedWithin2 >= 0.95)
        .OrderBy(x => x.r.CorrectionsOnGoodText + x.r.CorrectionsWrongText).ThenByDescending(x => x.r.Recall)
        .ThenBy(x => x.r.DeferRateFirstWord).ThenByDescending(x => x.opt.THigh).FirstOrDefault();
    Console.WriteLine();
    Console.WriteLine(best.r is null
        ? "no setting reaches precision >= 99.5%"
        : $"best under the gates (precision >= 99.5%, defers resolved >= 95%): thigh={best.opt.THigh} tlow={best.opt.TLow} dict={best.opt.DictBonus} short={best.opt.ShortRunPerLetter} → precision {best.r.Precision:P2} recall {best.r.Recall:P1} w1-precision {best.r.FirstWordPrecision:P2}");
}
else
{
    var report = Evaluator.Run(new PhraseDecider(profiles, baseOpt), cases);
    report.Print(Console.Out, samples);
}

int Arg(string name, int def) { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? int.Parse(args[i + 1]) : def; }
string ArgS(string name, string def) { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : def; }
static string Breakdown(string word, LanguageProfile p, PhraseDecider decider, KeyboardLayoutTable layout)
{
    var w = p.Normalize(word);
    int end = w.Length;
    while (end > 0 && !char.IsLetterOrDigit(w[end - 1]) && w[end - 1] != '\'') end--;
    w = w[..end];
    var bare = w.Replace("'", ""); // mirrors PhraseDecider.ScoreWord
    if (bare.Length == 0) return $"[{p.Name}] score 0.0  (no letters)";
    bool impossible = p.Rules?.IsImpossible(bare) == true;
    var dictKey = p.Dictionary.Contains(w) ? w : p.Dictionary.Contains(bare) ? bare : null;
    return $"[{p.Name}] score {decider.ScoreWord(word, p, layout):F1}  lm {p.Model.LogProb(bare):F1}" +
           (impossible ? "  IMPOSSIBLE" : "") + (dictKey is null ? "  not-in-dict" : $"  dict '{dictKey}' lnfreq {p.Dictionary.LogFreq(dictKey):F1}");
}
double ArgD(string name, double def) { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? double.Parse(args[i + 1], System.Globalization.CultureInfo.InvariantCulture) : def; }
