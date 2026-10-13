using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

// Writes, relative to the repo root (found by walking up to AutoLang.sln):
//   docs/index.html, docs/privacy.html            English
//   docs/<lang>/index.html, docs/<lang>/privacy.html  every other language
// from the templates site/index.html and site/privacy.html and the strings in site/strings/<lang>.txt.
//
// Template placeholders: {{key}} is a string, {{?key}} drops its whole line when the string is empty, and
// {{@name}} is filled in here (@lang, @dir, @root, @head, @langmenu, @menuscript). Strings are HTML.
// English decides which keys exist; every translation must have exactly the same ones.
const string BaseUrl = "https://bnk989.github.io/AutoLang/";
const string StorageKey = "autolang-lang";
string[] langs = ["en", "he", "ar", "ru", "uk", "el"];
var rtl = new HashSet<string> { "he", "ar" };
string[] pages = ["index.html", "privacy.html"];

var root = AppContext.BaseDirectory;
while (root is not null && !File.Exists(Path.Combine(root, "AutoLang.sln"))) root = Path.GetDirectoryName(root);
if (root is null) { Console.Error.WriteLine("AutoLang.sln not found above " + AppContext.BaseDirectory); return 1; }

var site = Path.Combine(root, "site");
var docs = Path.Combine(root, "docs");
var errors = new List<string>();
var placeholder = new Regex(@"\{\{([?@]?)([a-z0-9_]+)\}\}");

var present = Directory.GetFiles(Path.Combine(site, "strings"), "*.txt").Select(f => Path.GetFileNameWithoutExtension(f)).ToHashSet();
foreach (var extra in present.Except(langs)) errors.Add($"site/strings/{extra}.txt: not a site language (add it to the list in Program.cs)");
foreach (var missing in langs.Except(present)) errors.Add($"site/strings/{missing}.txt: missing");
if (errors.Count > 0) return Fail();

var strings = langs.ToDictionary(l => l, l => ParseStrings($"site/strings/{l}.txt"));
var templates = pages.ToDictionary(p => p, p => Read(Path.Combine(site, p)));

// Every key a template uses exists in English, and every English key is used.
var used = templates.Values.SelectMany(t => placeholder.Matches(t)).Where(m => m.Groups[1].Value != "@").Select(m => m.Groups[2].Value).ToHashSet();
used.Add("lang_name");
used.Add("lang_label");
foreach (var k in used.Except(strings["en"].Keys)) errors.Add($"site/strings/en.txt: no @{k}, which a template uses");
foreach (var k in strings["en"].Keys.Except(used)) errors.Add($"site/strings/en.txt: @{k} is not used by any template");
foreach (var l in langs.Skip(1))
{
    foreach (var k in strings["en"].Keys.Except(strings[l].Keys)) errors.Add($"site/strings/{l}.txt: missing @{k}");
    foreach (var k in strings[l].Keys.Except(strings["en"].Keys)) errors.Add($"site/strings/{l}.txt: unknown @{k}");
}
foreach (var l in langs) CheckStrings(l, strings[l]);
if (errors.Count > 0) return Fail();

foreach (var lang in langs)
foreach (var page in pages)
{
    var html = Render(templates[page], page, lang);
    if (errors.Count > 0) return Fail();
    var path = Path.Combine(docs, lang == "en" ? "" : lang, page);
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    var bytes = new UTF8Encoding(false).GetBytes(html);
    if (File.Exists(path) && File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes)) continue;
    File.WriteAllBytes(path, bytes);
    Console.WriteLine(Path.GetRelativePath(root, path).Replace('\\', '/'));
}
return 0;

int Fail()
{
    foreach (var e in errors) Console.Error.WriteLine(e);
    return 1;
}

static string Read(string path) => File.ReadAllText(path, Encoding.UTF8).Replace("\r\n", "\n");

// "@key" on its own line starts an entry; the value runs to the next "@key", trimmed. "#" at column 0 is a comment.
Dictionary<string, string> ParseStrings(string rel)
{
    var result = new Dictionary<string, string>();
    string? key = null;
    var value = new StringBuilder();
    var n = 0;
    foreach (var line in Read(Path.Combine(root, rel)).Split('\n'))
    {
        n++;
        if (line.StartsWith('#')) continue;
        var start = Regex.Match(line, @"^@([a-z0-9_]+)\s*$");
        if (start.Success)
        {
            Flush();
            key = start.Groups[1].Value;
            if (result.ContainsKey(key)) errors.Add($"{rel}:{n}: @{key} appears twice");
            continue;
        }
        if (key is null) { if (line.Trim().Length > 0) errors.Add($"{rel}:{n}: text before the first @key"); continue; }
        value.Append(line).Append('\n');
    }
    Flush();
    return result;

    void Flush()
    {
        if (key is not null) result[key] = value.ToString().Trim();
        value.Clear();
    }
}

void CheckStrings(string lang, Dictionary<string, string> s)
{
    var rel = $"site/strings/{lang}.txt";
    // Strings that land inside a tag (attributes) must not be able to end it.
    var inTag = templates.Values.SelectMany(t => placeholder.Matches(t)
        .Where(m => m.Groups[1].Value != "@" && t.LastIndexOf('<', m.Index) > t.LastIndexOf('>', m.Index))
        .Select(m => m.Groups[2].Value)).Append("lang_label").Append("lang_name").ToHashSet();
    foreach (var k in inTag)
        if (s.TryGetValue(k, out var v) && v.IndexOfAny(['"', '<', '>']) >= 0)
            errors.Add($"{rel}: @{k} goes into an attribute, so it can't contain \", < or >");
    foreach (var k in new[] { "lang_name", "lang_label" })
        if (s.TryGetValue(k, out var v) && v.Contains('&'))
            errors.Add($"{rel}: @{k} can't contain &");

    if (s.TryGetValue("demo_switched", out var sw) && !sw.Contains("{to}")) errors.Add($"{rel}: @demo_switched needs {{to}}");
    if (s.TryGetValue("demo_reduced", out var rd) && !rd.Contains("{typed}")) errors.Add($"{rel}: @demo_reduced needs {{typed}}");

    // The demo scenes go straight into a script, so they must be plain JSON.
    if (!s.TryGetValue("scenes", out var scenes)) return;
    if (scenes.Contains('<')) { errors.Add($"{rel}: @scenes can't contain <"); return; }
    try
    {
        using var doc = JsonDocument.Parse(scenes);
        if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
            errors.Add($"{rel}: @scenes must be a non-empty JSON array");
        else
            foreach (var scene in doc.RootElement.EnumerateArray())
                foreach (var f in new[] { "from", "to", "typed", "fixed" })
                    if (scene.ValueKind != JsonValueKind.Object || !scene.TryGetProperty(f, out var p) || p.ValueKind != JsonValueKind.String)
                        errors.Add($"{rel}: every scene needs a \"{f}\" string");
    }
    catch (JsonException e) { errors.Add($"{rel}: @scenes is not valid JSON: {e.Message}"); }
}

string Render(string template, string page, string lang)
{
    var s = strings[lang];
    var lines = template.Split('\n').Where(line => placeholder.Matches(line)
        .All(m => m.Groups[1].Value != "?" || s.GetValueOrDefault(m.Groups[2].Value, "").Length > 0));
    var html = placeholder.Replace(string.Join('\n', lines), m => m.Groups[1].Value switch
    {
        "@" => Special(m.Groups[2].Value, page, lang),
        _ => s.TryGetValue(m.Groups[2].Value, out var v) ? v : "",
    });
    var cut = html.IndexOf('\n') + 1;
    var header = $"<!-- Generated from site/{page} and site/strings/{lang}.txt by tools/AutoLang.Site. Edit those, not this file. -->\n";
    return html[..cut] + header + html[cut..];
}

string Special(string name, string page, string lang)
{
    var file = page == "index.html" ? "" : page;
    switch (name)
    {
        case "lang": return lang;
        case "dir": return rtl.Contains(lang) ? "rtl" : "ltr";
        case "root": return lang == "en" ? "" : "../";
        case "head":
        {
            var head = new StringBuilder();
            head.Append($"<link rel=\"canonical\" href=\"{Url(lang, file)}\">\n");
            foreach (var l in langs) head.Append($"<link rel=\"alternate\" hreflang=\"{l}\" href=\"{Url(l, file)}\">\n");
            head.Append($"<link rel=\"alternate\" hreflang=\"x-default\" href=\"{Url("en", file)}\">");
            if (lang == "en") head.Append('\n').Append(Redirect(file));
            return head.ToString();
        }
        case "langmenu":
        {
            var s = strings[lang];
            var menu = new StringBuilder();
            menu.Append("      <details class=\"lang\">\n");
            menu.Append($"        <summary aria-label=\"{s["lang_label"]}: {s["lang_name"]}\">{Globe}<span>{s["lang_name"]}</span></summary>\n");
            menu.Append("        <ul>\n");
            foreach (var l in langs)
            {
                var href = l == lang ? (file == "" ? "./" : file) : (lang == "en" ? "" : "../") + (l == "en" ? "" : l + "/") + file;
                if (href == "") href = "./";
                var current = l == lang ? " aria-current=\"true\"" : "";
                menu.Append($"          <li><a href=\"{href}\" lang=\"{l}\" hreflang=\"{l}\" data-lang=\"{l}\"{current}>{strings[l]["lang_name"]}</a></li>\n");
            }
            menu.Append("        </ul>\n");
            menu.Append("      </details>");
            return menu.ToString();
        }
        case "menuscript": return MenuScript;
        default:
            errors.Add($"site/{page}: unknown placeholder {{{{@{name}}}}}");
            return "";
    }
}

static string Url(string lang, string file) => BaseUrl + (lang == "en" ? "" : lang + "/") + file;

// English pages only: on a first visit, open the page in the browser's language. A choice made in the menu wins.
string Redirect(string file)
{
    var have = string.Join(", ", langs.Skip(1).Select(l => l + ": 1"));
    return $$"""
        <script>
        (function () {
          // First visit: open the page in the browser's language. A choice made in the language menu wins.
          var have = { {{have}} }, pick = null;
          try { pick = localStorage.getItem('{{StorageKey}}'); } catch (e) {}
          if (!pick) {
            var list = navigator.languages || [navigator.language || ''];
            for (var i = 0; i < list.length; i++) {
              var p = String(list[i]).toLowerCase().split('-')[0];
              if (p === 'iw') p = 'he';
              if (p === 'en' || have[p]) { pick = p; break; }
            }
          }
          if (pick && have[pick]) location.replace(pick + '/{{file}}' + location.hash);
        })();
        </script>
        """.Replace("\r\n", "\n");
}

partial class Program
{
    const string Globe = "<svg width=\"16\" height=\"16\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" " +
        "stroke-linecap=\"round\" aria-hidden=\"true\"><circle cx=\"12\" cy=\"12\" r=\"10\"/>" +
        "<path d=\"M2 12h20M12 2a15 15 0 0 1 0 20M12 2a15 15 0 0 0 0 20\"/></svg>";

    const string MenuScript = """
        <script>
        (function () {
          // The language menu: remember the pick for the next visit, and close on a click outside or Escape.
          var menu = document.querySelector('details.lang');
          menu.addEventListener('click', function (e) {
            var a = e.target.closest('a[data-lang]');
            if (a) try { localStorage.setItem('autolang-lang', a.getAttribute('data-lang')); } catch (x) {}
          });
          document.addEventListener('click', function (e) { if (!menu.contains(e.target)) menu.open = false; });
          document.addEventListener('keydown', function (e) { if (e.key === 'Escape') menu.open = false; });
        })();
        </script>
        """;
}
