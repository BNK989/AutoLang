using Tmds.DBus.Protocol;

namespace AutoLang.Linux;

/// <summary>
/// The IBus side of the spike, straight on the D-Bus wire (no libibus): ibus-daemon runs its own private bus, spawns
/// the component's exec with --ibus, waits for it to own the component name, then calls the factory's CreateEngine and
/// drives the returned engine object with ProcessKeyEvent, FocusIn/Out, SetCapabilities and friends. The engine
/// answers with signals: CommitText, DeleteSurroundingText, ForwardKeyEvent, RegisterProperties.
/// </summary>
internal static class IBus
{
    public const string ComponentName = "org.freedesktop.IBus.AutoLang";
    public const string EngineName = "autolang";
    public const string FactoryPath = "/org/freedesktop/IBus/Factory";
    public const string FactoryInterface = "org.freedesktop.IBus.Factory";
    public const string EngineInterface = "org.freedesktop.IBus.Engine";
    public const string ServiceInterface = "org.freedesktop.IBus.Service";
    public const string PropertiesInterface = "org.freedesktop.DBus.Properties";

    // IBusModifierType
    public const uint ShiftMask = 1 << 0, LockMask = 1 << 1, ControlMask = 1 << 2, Mod1Mask = 1 << 3, Mod4Mask = 1 << 6,
        Mod5Mask = 1 << 7, SuperMask = 1 << 26, HyperMask = 1 << 27, MetaMask = 1 << 28, ReleaseMask = 1u << 30;

    // IBusCapabilite
    public const uint CapSurroundingText = 1 << 5;

    // IBusInputPurpose
    public const uint PurposePassword = 8, PurposePin = 9, PurposeTerminal = 13;

    /// <summary>
    /// The daemon's address: IBUS_ADDRESS when set, else the newest address file in ~/.config/ibus/bus whose daemon is
    /// still running (libibus picks the file by machine id and display; any live one is the same daemon in practice).
    /// </summary>
    public static string? Address()
    {
        var env = Environment.GetEnvironmentVariable("IBUS_ADDRESS");
        if (!string.IsNullOrEmpty(env)) return env;
        var config = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (string.IsNullOrEmpty(config))
            config = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        var dir = new DirectoryInfo(Path.Combine(config, "ibus", "bus"));
        if (!dir.Exists) return null;
        foreach (var file in dir.GetFiles().OrderByDescending(f => f.LastWriteTimeUtc))
        {
            string? address = null, pid = null;
            foreach (var line in File.ReadLines(file.FullName))
            {
                if (line.StartsWith("IBUS_ADDRESS=", StringComparison.Ordinal)) address = line["IBUS_ADDRESS=".Length..];
                else if (line.StartsWith("IBUS_DAEMON_PID=", StringComparison.Ordinal)) pid = line["IBUS_DAEMON_PID=".Length..];
            }
            if (address is not null && pid is not null && Directory.Exists($"/proc/{pid}")) return address;
        }
        return null;
    }

    /// <summary>
    /// Writes an IBusText as a variant: ("IBusText", {}, text, &lt;("IBusAttrList", {}, [attributes])&gt;). With
    /// <paramref name="noUnderline"/> the text carries one attribute, underline = none over all of it, so preedit
    /// looks like ordinary text in clients that honour attributes.
    /// </summary>
    public static void WriteTextVariant(ref MessageWriter w, string text, bool noUnderline = false)
    {
        w.WriteSignature("(sa{sv}sv)");
        WriteText(ref w, text, noUnderline);
    }

    private static void WriteText(ref MessageWriter w, string text, bool noUnderline)
    {
        w.WriteStructureStart();
        w.WriteString("IBusText");
        w.WriteDictionaryEnd(w.WriteDictionaryStart());
        w.WriteString(text);
        w.WriteSignature("(sa{sv}av)");
        w.WriteStructureStart();
        w.WriteString("IBusAttrList");
        w.WriteDictionaryEnd(w.WriteDictionaryStart());
        var attrs = w.WriteArrayStart(DBusType.Variant);
        if (noUnderline && text.Length > 0)
        {
            // IBusAttribute: (name, attachments, type, value, start, end); type 1 = underline, value 0 = none.
            w.WriteSignature("(sa{sv}uuuu)");
            w.WriteStructureStart();
            w.WriteString("IBusAttribute");
            w.WriteDictionaryEnd(w.WriteDictionaryStart());
            w.WriteUInt32(1);
            w.WriteUInt32(0);
            w.WriteUInt32(0);
            w.WriteUInt32((uint)text.EnumerateRunes().Count());
        }
        w.WriteArrayEnd(attrs);
    }

    public sealed record Property(string Key, string Label, string Symbol, string Tooltip);

    private const string PropertySignature = "(sa{sv}suvsvbbuvv)";

    /// <summary>IBusPropList as a variant: ("IBusPropList", {}, [&lt;IBusProperty&gt;...]).</summary>
    public static void WritePropListVariant(ref MessageWriter w, IEnumerable<Property> props)
    {
        w.WriteSignature("(sa{sv}av)");
        WritePropList(ref w, props);
    }

    private static void WritePropList(ref MessageWriter w, IEnumerable<Property> props)
    {
        w.WriteStructureStart();
        w.WriteString("IBusPropList");
        w.WriteDictionaryEnd(w.WriteDictionaryStart());
        var array = w.WriteArrayStart(DBusType.Variant);
        foreach (var p in props) WritePropertyVariant(ref w, p);
        w.WriteArrayEnd(array);
    }

    /// <summary>IBusProperty: (name, attachments, key, type, label, icon, tooltip, sensitive, visible, state, sub_props, symbol).</summary>
    public static void WritePropertyVariant(ref MessageWriter w, Property p)
    {
        w.WriteSignature(PropertySignature);
        w.WriteStructureStart();
        w.WriteString("IBusProperty");
        w.WriteDictionaryEnd(w.WriteDictionaryStart());
        w.WriteString(p.Key);
        w.WriteUInt32(0); // PROP_TYPE_NORMAL
        WriteTextVariant(ref w, p.Label);
        w.WriteString("");
        WriteTextVariant(ref w, p.Tooltip);
        w.WriteBool(true);
        w.WriteBool(true);
        w.WriteUInt32(0);
        WritePropListVariant(ref w, Array.Empty<Property>());
        WriteTextVariant(ref w, p.Symbol);
    }
}

/// <summary>org.freedesktop.IBus.Factory: the daemon asks it for an engine and gets an object path back.</summary>
internal sealed class Factory(DBusConnection connection, Func<DBusConnection, string, EngineObject> create) : IPathMethodHandler
{
    private int _next;

    public string Path => IBus.FactoryPath;
    public bool HandlesChildPaths => false;

    public ValueTask HandleMethodAsync(MethodContext context)
    {
        var request = context.Request;
        if (request.InterfaceAsString == IBus.FactoryInterface && request.MemberAsString == "CreateEngine")
        {
            var name = request.GetBodyReader().ReadString();
            var path = $"/org/freedesktop/IBus/Engine/{Interlocked.Increment(ref _next)}";
            var engine = create(connection, path);
            connection.AddMethodHandler(engine);
            Log.Info($"CreateEngine({name}) → {path}");
            using var w = context.CreateReplyWriter("o");
            w.WriteObjectPath(path);
            context.Reply(w.CreateMessage());
            return default;
        }
        if (request.InterfaceAsString == IBus.ServiceInterface && request.MemberAsString == "Destroy")
        {
            ReplyEmpty(context);
            return default;
        }
        context.ReplyUnknownMethodError();
        return default;
    }

    public static void ReplyEmpty(MethodContext context)
    {
        if (context.NoReplyExpected) return;
        using var w = context.CreateReplyWriter(null!);
        context.Reply(w.CreateMessage());
    }
}

/// <summary>
/// org.freedesktop.IBus.Engine plumbing: decodes the daemon's calls into virtual methods and encodes the signals.
/// Calls arrive one at a time on the connection's receive loop; everything runs under <see cref="Gate"/> so signals
/// leave in the order the logic emits them.
/// </summary>
/// <summary>Writes a signal or reply body.</summary>
internal delegate void Body(ref MessageWriter w);

internal abstract class EngineObject(DBusConnection connection, string path) : IPathMethodHandler
{
    protected readonly object Gate = new();
    protected readonly DBusConnection Connection = connection;

    public string Path { get; } = path;
    public bool HandlesChildPaths => false;

    public ValueTask HandleMethodAsync(MethodContext context)
    {
        var r = context.Request;
        var iface = r.InterfaceAsString ?? "";
        var member = r.MemberAsString ?? "";
        try
        {
            lock (Gate)
            {
                if (iface == IBus.EngineInterface) Engine(context, member);
                else if (iface == IBus.PropertiesInterface) Properties(context, member);
                else if (iface == IBus.ServiceInterface && member == "Destroy")
                {
                    Log.Info($"{Path}: Destroy");
                    Factory.ReplyEmpty(context);
                    Connection.RemoveMethodHandler(Path);
                    Destroyed();
                }
                else context.ReplyUnknownMethodError();
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"{member}: {ex.GetType().Name}: {ex.Message}");
            if (!context.ReplySent && !context.NoReplyExpected) context.ReplyError("org.freedesktop.DBus.Error.Failed", ex.Message);
        }
        return default;
    }

    private void Engine(MethodContext context, string member)
    {
        var reader = context.Request.GetBodyReader();
        switch (member)
        {
            case "ProcessKeyEvent":
            {
                uint keyval = reader.ReadUInt32(), keycode = reader.ReadUInt32(), state = reader.ReadUInt32();
                bool handled = ProcessKeyEvent(keyval, keycode, state);
                using var w = context.CreateReplyWriter("b");
                w.WriteBool(handled);
                context.Reply(w.CreateMessage());
                return;
            }
            case "SetCapabilities": SetCapabilities(reader.ReadUInt32()); break;
            case "SetContentType": { uint purpose = reader.ReadUInt32(), hints = reader.ReadUInt32(); SetContentType(purpose, hints); break; }
            case "FocusIn": case "FocusInId": FocusIn(); break;
            case "FocusOut": case "FocusOutId": FocusOut(); break;
            case "Reset": Reset(); break;
            case "Enable": Enable(); break;
            case "Disable": Disable(); break;
            case "PropertyActivate": { var name = reader.ReadString(); var state = reader.ReadUInt32(); PropertyActivate(name, state); break; }
            // SetCursorLocation, SetSurroundingText, PageUp/Down, CursorUp/Down, CandidateClicked, PropertyShow/Hide,
            // ProcessHandWritingEvent, CancelHandWriting, PanelExtensionReceived...: nothing to do in the spike.
        }
        Factory.ReplyEmpty(context);
    }

    /// <summary>
    /// The daemon reads FocusId and ActiveSurroundingText when it creates its proxy, and newer daemons write
    /// ContentType as a property instead of calling SetContentType.
    /// </summary>
    private void Properties(MethodContext context, string member)
    {
        var reader = context.Request.GetBodyReader();
        switch (member)
        {
            case "GetAll":
            {
                var w = context.CreateReplyWriter("a{sv}");
                var dict = w.WriteDictionaryStart();
                w.WriteDictionaryEntryStart(); w.WriteString("FocusId"); WriteBoolStruct(ref w, false);
                w.WriteDictionaryEntryStart(); w.WriteString("ActiveSurroundingText"); WriteBoolStruct(ref w, true);
                w.WriteDictionaryEnd(dict);
                context.Reply(w.CreateMessage());
                w.Dispose();
                return;
            }
            case "Get":
            {
                reader.ReadString();
                var name = reader.ReadString();
                var w = context.CreateReplyWriter("v");
                WriteBoolStruct(ref w, name == "ActiveSurroundingText");
                context.Reply(w.CreateMessage());
                w.Dispose();
                return;
            }
            case "Set":
            {
                reader.ReadString();
                var name = reader.ReadString();
                if (name == "ContentType")
                {
                    reader.ReadSignature("(uu)");
                    reader.AlignStruct();
                    uint purpose = reader.ReadUInt32(), hints = reader.ReadUInt32();
                    SetContentType(purpose, hints);
                }
                Factory.ReplyEmpty(context);
                return;
            }
        }
        context.ReplyUnknownMethodError();
    }

    private static void WriteBoolStruct(ref MessageWriter w, bool value)
    {
        w.WriteSignature("(b)");
        w.WriteStructureStart();
        w.WriteBool(value);
    }

    // ---- Calls from the daemon ----

    protected abstract bool ProcessKeyEvent(uint keyval, uint keycode, uint state);
    protected virtual void SetCapabilities(uint caps) { }
    protected virtual void SetContentType(uint purpose, uint hints) { }
    protected virtual void FocusIn() { }
    protected virtual void FocusOut() { }
    protected virtual void Reset() { }
    protected virtual void Enable() { }
    protected virtual void Disable() { }
    protected virtual void PropertyActivate(string name, uint state) { }
    protected virtual void Destroyed() { }

    // ---- Signals to the daemon ----

    private void Emit(string member, string? signature, Body? body)
    {
        var w = Connection.GetMessageWriter();
        w.WriteSignalHeader(destination: null, path: Path, @interface: IBus.EngineInterface, member: member, signature: signature);
        body?.Invoke(ref w);
        if (!Connection.TrySendMessage(w.CreateMessage())) Log.Warn($"signal {member} not sent");
        w.Dispose();
    }

    protected void CommitText(string text) => Emit("CommitText", "v", (ref MessageWriter w) => IBus.WriteTextVariant(ref w, text));

    /// <summary>Deletes <paramref name="chars"/> characters before the caret (offset −chars).</summary>
    protected void DeleteSurroundingText(int chars) => Emit("DeleteSurroundingText", "iu", (ref MessageWriter w) => { w.WriteInt32(-chars); w.WriteUInt32((uint)chars); });

    protected void ForwardKeyEvent(uint keyval, uint keycode, uint state) =>
        Emit("ForwardKeyEvent", "uuu", (ref MessageWriter w) => { w.WriteUInt32(keyval); w.WriteUInt32(keycode); w.WriteUInt32(state); });

    protected void RequireSurroundingText() => Emit("RequireSurroundingText", null, null);

    /// <summary>Shows (or with empty text hides) the preedit. Mode 1 = commit it on focus out, 0 = drop it.</summary>
    protected void UpdatePreeditText(string text, bool noUnderline) =>
        Emit("UpdatePreeditText", "vubu", (ref MessageWriter w) =>
        {
            IBus.WriteTextVariant(ref w, text, noUnderline);
            w.WriteUInt32((uint)text.EnumerateRunes().Count());
            w.WriteBool(text.Length > 0);
            w.WriteUInt32(text.Length > 0 ? 1u : 0u);
        });

    protected void RegisterProperties(IEnumerable<IBus.Property> props) => Emit("RegisterProperties", "v", (ref MessageWriter w) => IBus.WritePropListVariant(ref w, props));

    protected void UpdateProperty(IBus.Property prop) => Emit("UpdateProperty", "v", (ref MessageWriter w) => IBus.WritePropertyVariant(ref w, prop));
}
