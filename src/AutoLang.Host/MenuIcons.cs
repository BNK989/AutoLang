using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Xml.Linq;

namespace AutoLang.Host;

/// <summary>
/// The tray menu icons: Fluent UI System Icons (16 px, regular; MIT, see Icons/LICENSE.txt), embedded as the
/// original SVG files and drawn as vector paths in the menu's text color, so they stay sharp at any DPI and follow
/// the light / dark theme. Only the path subset those files use is parsed (M L H V C S Q T Z, absolute or relative).
/// </summary>
internal static class MenuIcons
{
    private static readonly Dictionary<string, (GraphicsPath Path, float Size)> Cache = new();

    /// <summary>Fills icon <paramref name="name"/> (an Icons/*.svg file name without extension) centered in <paramref name="box"/>.</summary>
    public static void Draw(Graphics g, string name, Rectangle box, Color color)
    {
        var (path, size) = Get(name);
        if (path.PointCount == 0) return;
        using var p = (GraphicsPath)path.Clone();
        float side = Math.Min(box.Width, box.Height);
        using (var m = new Matrix())
        {
            m.Translate(box.X + (box.Width - side) / 2f, box.Y + (box.Height - side) / 2f);
            m.Scale(side / size, side / size);
            p.Transform(m);
        }
        var smoothing = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var b = new SolidBrush(color)) g.FillPath(b, p);
        g.SmoothingMode = smoothing;
    }

    private static (GraphicsPath Path, float Size) Get(string name)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(name, out var hit)) return hit;
            var path = new GraphicsPath(FillMode.Winding);
            float size = 16;
            try
            {
                using var s = typeof(MenuIcons).Assembly.GetManifestResourceStream($"icons/{name}.svg")
                    ?? throw new FileNotFoundException("not embedded");
                var svg = XDocument.Load(s).Root!;
                var box = ((string?)svg.Attribute("viewBox"))?.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (box is { Length: 4 }) size = float.Parse(box[2], CultureInfo.InvariantCulture);
                foreach (var el in svg.Descendants(svg.Name.Namespace + "path"))
                {
                    if ((string?)el.Attribute("fill-rule") == "evenodd") path.FillMode = FillMode.Alternate;
                    AddPathData(path, (string?)el.Attribute("d") ?? "");
                }
            }
            catch (Exception ex) { Log.Warn($"menu icon {name}: {ex.Message}"); path.Reset(); }
            return Cache[name] = (path, size);
        }
    }

    /// <summary>Appends SVG path data ("d") to <paramref name="path"/>.</summary>
    private static void AddPathData(GraphicsPath path, string d)
    {
        var t = new Tokens(d);
        PointF cur = default, start = default, ctrl = default;
        char cmd = ' ', prev = ' ';
        while (t.More())
        {
            if (t.PeekCommand() is char c) cmd = c;
            else if (cmd == ' ') throw new FormatException("path data must start with a command");
            bool rel = char.IsLower(cmd);
            PointF P(PointF from) { float x = t.Num(), y = t.Num(); return rel ? new(from.X + x, from.Y + y) : new(x, y); }
            switch (char.ToUpperInvariant(cmd))
            {
                case 'M':
                    path.StartFigure();
                    cur = start = P(cur);
                    cmd = rel ? 'l' : 'L'; // further pairs after a moveto are linetos
                    break;
                case 'L': { var p = P(cur); path.AddLine(cur, p); cur = p; break; }
                case 'H': { float x = t.Num(); var p = new PointF(rel ? cur.X + x : x, cur.Y); path.AddLine(cur, p); cur = p; break; }
                case 'V': { float y = t.Num(); var p = new PointF(cur.X, rel ? cur.Y + y : y); path.AddLine(cur, p); cur = p; break; }
                case 'C':
                {
                    var c1 = P(cur); var c2 = P(cur); var p = P(cur);
                    path.AddBezier(cur, c1, c2, p); ctrl = c2; cur = p; break;
                }
                case 'S':
                {
                    var c1 = "CcSs".Contains(prev) ? Reflect(ctrl, cur) : cur;
                    var c2 = P(cur); var p = P(cur);
                    path.AddBezier(cur, c1, c2, p); ctrl = c2; cur = p; break;
                }
                case 'Q':
                {
                    var q = P(cur); var p = P(cur);
                    AddQuad(path, cur, q, p); ctrl = q; cur = p; break;
                }
                case 'T':
                {
                    var q = "QqTt".Contains(prev) ? Reflect(ctrl, cur) : cur;
                    var p = P(cur);
                    AddQuad(path, cur, q, p); ctrl = q; cur = p; break;
                }
                case 'Z':
                    path.CloseFigure();
                    cur = start;
                    prev = cmd;
                    cmd = ' '; // a closepath takes no numbers
                    continue;
                default: throw new NotSupportedException($"path command '{cmd}'");
            }
            prev = cmd;
        }
    }

    private static PointF Reflect(PointF c, PointF about) => new(2 * about.X - c.X, 2 * about.Y - c.Y);

    private static void AddQuad(GraphicsPath path, PointF p0, PointF q, PointF p1)
        => path.AddBezier(p0, new PointF(p0.X + 2f / 3 * (q.X - p0.X), p0.Y + 2f / 3 * (q.Y - p0.Y)),
            new PointF(p1.X + 2f / 3 * (q.X - p1.X), p1.Y + 2f / 3 * (q.Y - p1.Y)), p1);

    /// <summary>Reads path data: single-letter commands and numbers separated by spaces, commas or signs ("1-2", "1.5.5").</summary>
    private sealed class Tokens(string s)
    {
        private int _i;

        private void Skip() { while (_i < s.Length && (char.IsWhiteSpace(s[_i]) || s[_i] == ',')) _i++; }

        public bool More() { Skip(); return _i < s.Length; }

        public char? PeekCommand()
        {
            Skip();
            if (_i < s.Length && char.IsLetter(s[_i]) && s[_i] is not ('e' or 'E')) return s[_i++];
            return null;
        }

        public float Num()
        {
            Skip();
            int b = _i;
            if (_i < s.Length && s[_i] is '+' or '-') _i++;
            bool dot = false;
            while (_i < s.Length && (char.IsDigit(s[_i]) || (s[_i] == '.' && !dot)))
                dot |= s[_i++] == '.';
            if (_i < s.Length && s[_i] is 'e' or 'E')
            {
                _i++;
                if (_i < s.Length && s[_i] is '+' or '-') _i++;
                while (_i < s.Length && char.IsDigit(s[_i])) _i++;
            }
            if (_i == b) throw new FormatException($"number expected at {b}");
            return float.Parse(s.AsSpan(b, _i - b), NumberStyles.Float, CultureInfo.InvariantCulture);
        }
    }
}
