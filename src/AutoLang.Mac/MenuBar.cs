using System.Diagnostics;
using System.Runtime.InteropServices;
using static AutoLang.Mac.ObjC;

namespace AutoLang.Mac;

/// <summary>
/// The menu bar icon and its menu, the counterpart of the Windows tray icon. The menu is rebuilt from
/// <see cref="State"/> each time it opens (menuWillOpen:) and whenever the host calls <see cref="Refresh"/>. Main thread.
/// </summary>
internal sealed class MenuBar
{
    public sealed record State(string Status, bool Paused, bool NeedsPermission);

    public enum Command { Pause = 1, Grant, Login, Logs, Quit }

    private const string Version = "AutoLang 0.5.0 (tester build)";
    private const long On = 1, Off = 0; // NSControlStateValue

    private readonly Func<State> _state;
    private readonly Action<Command> _command;
    private readonly nint _statusItem, _button, _status, _pause, _grant, _login;
    private readonly nint _loginService; // SMAppService.mainAppService, 0 before macOS 13
    private string? _icon;

    public MenuBar(Func<State> state, Action<Command> command)
    {
        _state = state;
        _command = command;

        var bar = Send(Class("NSStatusBar"), Sel("systemStatusBar"));
        _statusItem = Send(SendDouble(bar, Sel("statusItemWithLength:"), -1), Sel("retain")); // NSVariableStatusItemLength
        _button = Send(_statusItem, Sel("button"));

        var menu = Send(Send(Class("NSMenu"), Sel("alloc")), Sel("init"));
        SendBool(menu, Sel("setAutoenablesItems:"), 0);
        Send(menu, Sel("setDelegate:"), Bridge);

        _status = Add(menu, "", null);
        SendBool(_status, Sel("setEnabled:"), 0);
        Separator(menu);
        _pause = Add(menu, "Pause", Command.Pause);
        _grant = Add(menu, "Grant Accessibility permission…", Command.Grant);
        _login = Add(menu, "Start at login", Command.Login);
        Add(menu, "Open log folder", Command.Logs);
        Separator(menu);
        SendBool(Add(menu, Version, null), Sel("setEnabled:"), 0);
        Add(menu, "Quit AutoLang", Command.Quit, "q");

        Send(_statusItem, Sel("setMenu:"), menu);

        _loginService = LoginService();
        if (_loginService == 0) SendBool(_login, Sel("setHidden:"), 1);

        ObjC.On("menuAction:", sender => _command((Command)SendRetLong(sender, Sel("tag"))));
        ObjC.On("menuWillOpen:", _ => Refresh());
        Refresh();
    }

    /// <summary>Brings the icon and the menu up to date with the host's state.</summary>
    public void Refresh()
    {
        var s = _state();
        SetText(_status, "setTitle:", s.Status);
        SetText(_pause, "setTitle:", s.Paused ? "Resume" : "Pause");
        SendBool(_grant, Sel("setHidden:"), s.NeedsPermission ? (byte)1 : (byte)0);
        if (_loginService != 0) SendLong(_login, Sel("setState:"), SendRetLong(_loginService, Sel("status")) == 1 ? On : Off);

        SetIcon(s.NeedsPermission ? "exclamationmark.triangle" : "keyboard");
        SendBool(_button, Sel("setAppearsDisabled:"), s.Paused ? (byte)1 : (byte)0);
        SetText(_button, "setToolTip:", $"AutoLang: {s.Status}");
    }

    /// <summary>Toggles the login item (SMAppService, macOS 13+). macOS may ask the user to approve it in System Settings.</summary>
    public unsafe void ToggleLogin()
    {
        if (_loginService == 0) return;
        bool enabled = SendRetLong(_loginService, Sel("status")) == 1;
        nint error = 0;
        var ok = SendRetBool(_loginService, Sel(enabled ? "unregisterAndReturnError:" : "registerAndReturnError:"), (nint)(&error));
        if (ok == 0)
        {
            var text = error != 0 ? Native.ToManaged(Send(error, Sel("localizedDescription"))) : "unknown error";
            Log.Warn($"login item {(enabled ? "unregister" : "register")} failed: {text}");
        }
        else Log.Info($"login item {(enabled ? "removed" : "added")}");
        Refresh();
    }

    /// <summary>Opens a URL or a folder with the default handler.</summary>
    public static void Open(string target)
    {
        try { Process.Start("/usr/bin/open", [target])?.Dispose(); }
        catch (Exception ex) { Log.Warn($"open failed: {ex.Message}"); }
    }

    private void SetIcon(string symbol)
    {
        if (_icon == symbol) return;
        _icon = symbol;
        var description = Native.CreateString("AutoLang");
        var name = Native.CreateString(symbol);
        try
        {
            // SF Symbols, macOS 11+. A template image follows the menu bar's light/dark appearance.
            var image = Send(Class("NSImage"), Sel("imageWithSystemSymbolName:accessibilityDescription:"), name, description);
            if (image != 0)
            {
                SendBool(image, Sel("setTemplate:"), 1);
                Send(_button, Sel("setImage:"), image);
                SetText(_button, "setTitle:", "");
            }
            else SetText(_button, "setTitle:", symbol == "keyboard" ? "AL" : "AL!");
        }
        finally { Native.CFRelease(name); Native.CFRelease(description); }
    }

    private static nint Add(nint menu, string title, Command? command, string key = "")
    {
        var t = Native.CreateString(title);
        var k = Native.CreateString(key);
        try
        {
            var item = Send(Send(Class("NSMenuItem"), Sel("alloc")), Sel("initWithTitle:action:keyEquivalent:"),
                t, command is null ? 0 : Sel("menuAction:"), k);
            if (command is { } c)
            {
                Send(item, Sel("setTarget:"), Bridge);
                SendLong(item, Sel("setTag:"), (long)c);
            }
            Send(menu, Sel("addItem:"), item);
            return item; // kept for the app's lifetime: the menu holds a reference too
        }
        finally { Native.CFRelease(t); Native.CFRelease(k); }
    }

    private static void Separator(nint menu) => Send(menu, Sel("addItem:"), Send(Class("NSMenuItem"), Sel("separatorItem")));

    private static void SetText(nint receiver, string selector, string text) => SendString(receiver, selector, text);

    /// <summary>SMAppService.mainAppService, or 0 when ServiceManagement (macOS 13) is not there.</summary>
    private static nint LoginService()
    {
        if (!NativeLibrary.TryLoad("/System/Library/Frameworks/ServiceManagement.framework/ServiceManagement", out _)) return 0;
        var cls = Class("SMAppService");
        return cls == 0 ? 0 : Send(Send(cls, Sel("mainAppService")), Sel("retain"));
    }
}
