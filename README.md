# Mochi

Mochi is a tiny Windows desktop corgi for study and work sessions. Set one goal,
start a focus timer, and let Mochi keep you company. It also has notes, a clock,
alarms, reminders, six color themes, and small animations.

## Download and run

Extract `Mochi-Windows-v0.1.0.zip` and run `Perch.exe`. There is no installer or
account. Windows needs .NET Framework 4.x with WPF. The binary is currently
unsigned, so Windows may warn that the publisher is unknown.

On first launch, name Mochi. Click him to open the compact menu. Choose **Lock
in**, enter a goal, and start a focus block. Right-click for **Emergency quit**.
Your settings and notes are stored at `%LOCALAPPDATA%\Perch\settings.xml`.

## Distraction nudges

During an active focus block, Mochi can read the foreground browser's address
bar through Windows UI Automation. If it shows YouTube, TikTok, or Roblox, he
shows a brief reminder. The check runs locally, does not record history, and can
be turned off on the Lock in screen. It is a gentle nudge, not a website block;
browser differences can make it miss some pages.

Mochi also checks whether keys or mouse buttons were pressed so he can animate
while you work. He does not store keystrokes, typed text, or browsing history,
and the app makes no network requests.

## Build from source

On Windows, run `powershell -ExecutionPolicy Bypass -File .\build.ps1`. The script
uses the .NET Framework C# compiler included with Windows and writes
`outputs\Perch.exe`. To run the UI smoke test, use
`powershell -ExecutionPolicy Bypass -File .\test.ps1`.
Run `powershell -ExecutionPolicy Bypass -File .\package.ps1` after building to
make the Windows and source ZIPs.

The pet art is drawn in code in `work\PixelMochi.cs`. No stock image is used
in the application. The old experimental browser extension is not part of the
release.

## License

MIT. See `LICENSE`.
