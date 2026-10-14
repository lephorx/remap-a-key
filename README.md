<img src="assets/icon.svg" width="64" height="64" alt="">

# remap a key

A small Windows tray app that remaps keys: swap Ctrl and Win, turn Caps Lock into Ctrl or Esc, disable a key, and so on.

Remaps apply instantly and system-wide. There's no reboot, no admin rights and no registry hacking: quitting the app puts every key back to normal.

## Using it

1. Run `RemapAKey.exe`. A window opens and an icon appears in the system tray.
2. Click **Add…**, then choose the key you press and the key it should act as (type it in the list, or click **Press a key…** and hit it on the keyboard).
   Leave **Also do the reverse** ticked to swap the two keys, e.g. Left Ctrl ⇄ Left Win.
3. Or use **Presets ▾** for common setups (Ctrl ⇄ Win, Alt ⇄ Win, Caps Lock → Ctrl, …).
4. Close the window; the app keeps running in the tray. Right-click the tray icon to pause remapping or exit.

Left and right modifier keys are separate (Left Ctrl, Right Ctrl, …), so you can remap just one side.
Settings live in `%APPDATA%\RemapAKey\config.json`. "Start with Windows" launches the app minimized to the tray at login.

### Limitations

- Ctrl+Alt+Del and Win+L are handled by Windows itself and can't be remapped.
- Keys typed into windows running **as administrator** (and the UAC prompt) aren't seen unless Remap a Key is also run as administrator.
- Keys are matched by virtual-key code, so the key names for punctuation (Semicolon, Quote, …) follow a US layout; on other layouts they're the same physical keys, just labelled differently.

## Building

Needs the [.NET 10 SDK](https://dotnet.microsoft.com/download). Works on Windows, and the app can also be cross-compiled from macOS/Linux.

```sh
dotnet test                                          # remapping logic tests (run on any OS)
dotnet publish src/RemapAKey -c Release -o publish   # → publish/RemapAKey.exe (single file, no .NET install needed)
```

## How it works

- `src/RemapAKey.Core`: the platform-independent part: key catalog, `RemapEngine` (decides pass / block / replace for each key event), config and presets. Unit tested.
- `src/RemapAKey`: the Windows part: a `WH_KEYBOARD_LL` hook feeds each keystroke to the engine, and replacements are sent with `SendInput` (tagged so the hook ignores its own output). WinForms UI plus tray icon.

Created in [T3 Code](https://t3.codes).
