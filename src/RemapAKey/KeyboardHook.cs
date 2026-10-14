using System.Runtime.InteropServices;
using RemapAKey.Core;

namespace RemapAKey;

/// <summary>
/// Installs a global low-level keyboard hook and routes every keystroke through the <see cref="RemapEngine"/>.
/// Must be created on the UI thread: Windows calls the hook on the thread that installed it, via that
/// thread's message loop, which is also what keeps the engine single-threaded.
/// </summary>
internal sealed class KeyboardHook : IDisposable
{
    // Tags the keystrokes we inject so the hook can recognise and ignore them (otherwise remapped keys
    // would be remapped again, endlessly). Spells "RAK1".
    private static readonly UIntPtr InjectedMarker = (UIntPtr)0x52414B31;

    private readonly RemapEngine _engine;
    private readonly NativeMethods.LowLevelKeyboardProc _proc; // field, so the GC can't collect the delegate
    private IntPtr _hook;

    private Action<KeyStroke>? _capture;
    private Action? _onCaptureReplaced;
    private KeyStroke? _captured; // swallow its release too, once capture has consumed the press

    public KeyboardHook(RemapEngine engine)
    {
        _engine = engine;
        _proc = HookCallback;
    }

    public void Install()
    {
        _hook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL, _proc, NativeMethods.GetModuleHandle(null), 0);

        if (_hook == IntPtr.Zero)
            throw new InvalidOperationException(
                "Could not install the keyboard hook (error " + Marshal.GetLastWin32Error() + ").");
    }

    /// <summary>
    /// Swallows the next key press (so it does nothing in the active app) and reports which key it was.
    /// Used by the "press a key" buttons so the user doesn't have to find a key in a list.
    /// </summary>
    /// <param name="onReplaced">Called if another capture starts before a key is pressed.</param>
    public void BeginCapture(Action<KeyStroke> onKey, Action onReplaced)
    {
        _onCaptureReplaced?.Invoke();
        _capture = onKey;
        _onCaptureReplaced = onReplaced;
    }

    public void CancelCapture()
    {
        _capture = null;
        _onCaptureReplaced = null;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0)
            return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);

        try
        {
            var info = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
            if (info.dwExtraInfo == InjectedMarker)
                return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);

            var isUp = (info.flags & NativeMethods.LLKHF_UP) != 0;
            var key = new KeyStroke(
                (ushort)info.vkCode, (info.flags & NativeMethods.LLKHF_EXTENDED) != 0);

            if (_capture is { } capture && !isUp)
            {
                CancelCapture();
                _captured = key;
                capture(key);
                return (IntPtr)1;
            }

            if (_captured == key)
            {
                if (isUp) _captured = null;
                return (IntPtr)1;
            }

            var decision = _engine.Process(key, isUp);
            switch (decision.Action)
            {
                case KeyAction.Block:
                    return (IntPtr)1;

                case KeyAction.Replace:
                    SendKey(decision.Replacement, isUp);
                    return (IntPtr)1;
            }
        }
        catch (Exception)
        {
            // A bug here must never swallow the user's keyboard; fall through and let the key pass.
        }

        return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private static void SendKey(KeyStroke key, bool isUp)
    {
        var flags = 0u;
        if (key.Extended) flags |= NativeMethods.KEYEVENTF_EXTENDEDKEY;
        if (isUp) flags |= NativeMethods.KEYEVENTF_KEYUP;

        var input = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            u = new NativeMethods.INPUT_UNION
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = key.Vk,
                    wScan = (ushort)NativeMethods.MapVirtualKey(key.Vk, NativeMethods.MAPVK_VK_TO_VSC),
                    dwFlags = flags,
                    dwExtraInfo = InjectedMarker,
                },
            },
        };

        NativeMethods.SendInput(1, [input], Marshal.SizeOf<NativeMethods.INPUT>());
    }

    public void Dispose()
    {
        if (_hook == IntPtr.Zero) return;
        NativeMethods.UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }
}
