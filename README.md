<img src="assets/icon.svg" width="64" height="64" alt="">

# remap a key

A small Windows tray app that remaps keys: swap Ctrl and Win, turn Caps Lock into Ctrl or Esc, disable a key, and so on.

In the default Live mode remaps apply instantly: no reboot, no admin rights, and quitting the app puts every key back to normal. An optional System-wide mode (see below) is more robust.

## Using it

1. Run `RemapAKey.exe`. A window opens and an icon appears in the system tray.
2. Click **Add…**, then choose the key you press and the key it should act as (type it in the list, or click **Press a key…** and hit it on the keyboard).
   Leave **Also do the reverse** ticked to swap the two keys, e.g. Left Ctrl ⇄ Left Win.
3. Or use **Presets ▾** for common setups (Ctrl ⇄ Win, Alt ⇄ Win, Caps Lock → Ctrl, …).
4. Close the window; the app keeps running in the tray. Right-click the tray icon to pause remapping or exit.

Left and right modifier keys are separate (Left Ctrl, Right Ctrl, …), so you can remap just one side.
Settings live in `%APPDATA%\RemapAKey\config.json`. "Start with Windows" launches the app minimized to the tray at login.

### Two remap methods

| | **Live** (default) | **System-wide** |
|---|---|---|
| How | Remap a Key intercepts keys and re-sends the replacement while it runs | Windows' own *Scancode Map* swaps the keys inside the keyboard driver |
| Needs | nothing | admin approval (UAC) once, then a **restart** |
| Works in | most apps | **every** app: launchers like Raycast, games, admin windows, the login screen |
| App must be running | yes | no |

Live mode re-sends keys as "injected" input, and some programs (hotkey tools, games) ignore that. If a remap works in Notepad but not in some app, switch to **System-wide**: pick it in the *Remap method* box, build your list as usual, click **Apply to Windows…** and restart.

System-wide remaps apply to every user on the PC. **Remove system-wide remap…** undoes them (then restart). If you ever need to do it by hand, delete the `Scancode Map` value under `HKLM\SYSTEM\CurrentControlSet\Control\Keyboard Layout` and restart.
Don't leave a system-wide remap installed while also using Live mode for the same keys: they stack, and a swap cancels itself out. The window warns you if that happens.

### Limitations

- Ctrl+Alt+Del and Win+L are handled by Windows itself and can't be remapped.
- In Live mode, windows running **as administrator** (and the UAC prompt) aren't seen unless Remap a Key is also run as administrator. System-wide mode doesn't have this limit.
- System-wide mode can't remap the Pause key.
- Keys are matched by virtual-key code, so the key names for punctuation (Semicolon, Quote, …) follow a US layout; on other layouts they're the same physical keys, just labelled differently.

## Building

Needs the [.NET 10 SDK](https://dotnet.microsoft.com/download). Works on Windows, and the app can also be cross-compiled from macOS/Linux.

```sh
dotnet test                                          # remapping logic tests (run on any OS)
dotnet publish src/RemapAKey -c Release -o publish   # → publish/RemapAKey.exe (single file, no .NET install needed)
```

## How it works

- `src/RemapAKey.Core`: the platform-independent part: key catalog, `RemapEngine` (decides pass / block / replace for each key event), `ScancodeMap` (builds the registry value for system-wide mode), config and presets. Unit tested.
- `src/RemapAKey`: the Windows part: a `WH_KEYBOARD_LL` hook feeds each keystroke to the engine, and replacements are sent with `SendInput` (tagged so the hook ignores its own output). System-wide mode writes the Scancode Map through a short UAC-elevated re-launch of the same exe. WinForms UI plus tray icon.

Created in [T3 Code](https://t3.codes).
