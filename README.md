# AutoPlay

Configure and run a sequence of clicks.

AutoPlay is a Windows desktop application that automates repetitive clicks in any application —
typically the "farming" parts of a game. You record the **screens** of the application and the
**locations** to click on them, assemble those locations into **sequences**, and AutoPlay replays them.
Before each click, it checks with image matching that the expected element is really there, so a
slow screen, a moved button or a popup makes it wait or stop instead of clicking blindly.

It works on anything displayed on screen, including browser games rendered in a canvas (where
DOM-based tools such as Playwright cannot target elements).

## Features

- **Screen recording**: frame the target area with a semi-transparent overlay, capture it, then draw
  the click locations on the frozen capture and name them.
- **Sequence editor**: chain locations from any screen, reorder, duplicate, set delays and verification
  per step, repeat a sequence N times or until stopped.
- **Reliable execution**:
  - random delay before each click, for a human-like rhythm;
  - image verification before each click, tolerant to resizing, moderate position changes and
    transient visual effects;
  - coordinates that follow the target area when the window is moved or resized (proportional
    positioning).
- **Safety**: **F12** stops a run at any time, moving the mouse pauses it, and the PC does not go to
  sleep while a sequence runs.
- **Diagnostics**: when an element is not found, the capture is saved with the expected position
  drawn in red.

## Requirements

- Windows 10 version 1809 or later (Windows 10 2004+ recommended), Windows 11
- To build from source: the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- To run a published build only: the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
- Optional: Visual Studio 2026 (or Visual Studio 2022 17.14+, or VS Code with C# Dev Kit)

## Getting started

### Run from source

```powershell
git clone https://github.com/victor-duc/auto-play.git
cd auto-play
dotnet run --project src/AutoPlay.App
```

The first run restores the NuGet packages, which takes a moment.

With Visual Studio, open `AutoPlay.slnx`, set **AutoPlay.App** as the startup project and press F5.

### Build an executable

```powershell
dotnet publish src/AutoPlay.App -c Release -r win-x64 --self-contained false -o publish
.\publish\AutoPlay.exe
```

The `publish` folder can be copied to any PC that has the .NET 10 Desktop Runtime. To produce a
version that runs without installing .NET, use `--self-contained true` (the folder is then larger).

## User guide

### 1. Create a profile

A profile groups the screens and sequences of one application (for example "Hero Wars"). Type a name
under the **Profiles** list and click **Add**.

### 2. Record a screen

1. Display the screen to record in the target application.
2. Select the profile and click **Record screen**. The main window hides and a blue overlay appears.
3. Move the overlay (drag it) and resize it (drag its edges) so that it **exactly** covers the
   target area — for a browser game, the game area only, not the whole browser window. Click
   **Capture**.
4. The screen editor opens on the capture:

   | Action | How |
   |---|---|
   | Create a location | Drag around the element (a simple click creates a 48 × 48 px square) |
   | Move / resize a location | Drag it / drag a corner of the selected location |
   | Set the click point (red cross) | Shift+click inside the selected location (default: center) |
   | Delete the selected location | Delete key |
   | Rename / set the match threshold | Locations panel on the right |
   | Save / cancel | Enter / Esc |

5. Give the screen a name, check the default delay before clicks, and click **Save**.

Draw each rectangle around a distinctive part of the element (a button with its label rather than a
plain background), and avoid areas that change often (counters, timers, animations).

Use **Edit** (or double-click) to reopen a screen and **Delete** to remove it.

### 3. Build a sequence

1. Click **New sequence**, type a name and a repeat count (0 = repeat until stopped).
2. In the **Add a step** panel, pick a screen, then a location, and click **Add step** (or
   double-click the location). The step is inserted after the selected step.
3. Reorder with **Move up** / **Move down** (Alt+Up / Alt+Down), **Duplicate** (Ctrl+D) or **Delete**.
4. For the selected step, you can set a custom delay, disable the verification, or set a custom
   verification timeout (useful after a long loading screen).
5. Click **Save**.

### 4. Run a sequence

1. Display the first screen of the sequence in the target application.
2. Select the sequence and click **Run**. The blue overlay reappears at the last position used:
   adjust it if the application moved or was resized, then click **Start**.
3. A small status window, placed beside the target area, shows the progress and the match score of
   each click.

| Control | Effect |
|---|---|
| **F12** (from any window) or **Stop** | Stops the run |
| Moving the mouse | Pauses the run before the next click |
| **Resume** | Continues from the interrupted step (a failed step is retried) |
| **View capture** | After a failure, opens the capture with the expected position in red |
| **Close** | Returns to the main window |

While a sequence runs, AutoPlay moves the real mouse: do not use the PC (other than to stop or pause
the run), and keep the target application visible and the session unlocked.

### Tuning

- **Match threshold** (0 to 1, default 0.8): the minimum similarity required to click. Lower it for
  a location that often fails because of visual effects; raise it if a similar element is clicked by
  mistake. The score of each click is shown in the status window.
- **Delays**: increase the delay before a click when the previous action triggers an animation or a
  loading screen; the verification timeout (default 10 s) is the maximum wait for the element.
- **Profile defaults** (threshold, verification timeout, retry interval, search margin, mouse
  tolerance) have no editing screen yet: they can be changed in the profile's `profile.json` file
  while AutoPlay is closed.

### Data and logs

Everything is stored in `%LOCALAPPDATA%\AutoPlay\Profiles`, one folder per profile, as JSON and PNG
files (see the [specification](docs/specification.md#6-data-model-and-storage)). Captures of failed
verifications are saved in each profile's `logs` folder. Back up this folder to keep your profiles.

### Troubleshooting

| Problem | Solution |
|---|---|
| Clicks are slightly off | Make sure the overlay exactly covers the same area as when recording. |
| A location is never found | Open **View capture**: if the element is visible but the score is low, lower the threshold or re-record the location around a more distinctive area. |
| F12 does nothing | Another application already uses F12: the status window says so. Use **Stop** or move the mouse. |
| The run stops when the PC locks | Capture and clicks do not work on a locked session: disable the automatic lock while running. |

## Development

```powershell
dotnet build AutoPlay.slnx
dotnet test AutoPlay.slnx
```

The Core and Vision projects (and their tests) are cross-platform; the Windows and App projects build
on any OS (`EnableWindowsTargeting`) but run only on Windows. Warnings are treated as errors.
A GitHub Actions workflow builds and tests the solution on Windows for every pull request.

| Project | Description |
|---|---|
| `src/AutoPlay.Core` | Domain model, JSON/PNG storage, sequence engine, recording and sequence validation (cross-platform). |
| `src/AutoPlay.Vision` | Template matching and PNG encoding with OpenCvSharp (cross-platform). |
| `src/AutoPlay.Windows` | Win32 screen capture, mouse input, power requests, global hotkeys and window geometry. |
| `src/AutoPlay.App` | WPF application (main window, framing overlay, screen and sequence editors, status window). |
| `tests/AutoPlay.Core.Tests` | Unit tests of the Core project. |
| `tests/AutoPlay.Vision.Tests` | Unit tests of the image matching and codec. |

## Documentation

- [Specification](docs/specification.md): functional behavior, data model, image matching and
  architecture.

## Disclaimer

Automating a game may be against its terms of service and can lead to sanctions on your account.
Check the rules of the application you automate; you use AutoPlay at your own risk.

## License

[MIT](LICENSE)
