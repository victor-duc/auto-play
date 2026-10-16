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
  the click locations on the frozen capture (with zoom and pan) and name them.
- **Quick capture**: while playing, press a shortcut (Ctrl+Shift+F9 by default) to capture the
  current screen in one keystroke, and annotate the captures later.
- **Maintenance**: re-capture a screen after a game update (locations are re-positioned
  automatically), redraw a location, test a step on the live screen without clicking.
- **Sequence editor**: chain locations from any screen, add waits, run other sequences, reorder by
  drag and drop, set delays and verification per step, repeat a sequence N times or until stopped,
  run it from any step.
- **Scheduling**: start a sequence automatically at a time of the day, on chosen days.
- **Reliable execution**:
  - random delay before each click, for a human-like rhythm;
  - image verification before each click, tolerant to resizing, moderate position changes and
    transient visual effects, optionally comparing colors (to tell an enabled button from a greyed-out
    one);
  - coordinates that follow the target area when the window is moved or resized (proportional
    positioning).
- **Safety**: **F12** or **Esc** stops a run at any time, moving the mouse pauses it, and the PC does
  not go to sleep while a sequence runs.
- **Diagnostics**: when an element is not found, the capture is saved with the expected position
  drawn in red; the main window shows the outcome of the last run of each sequence; a log file records
  every run.
- **Settings, export and themes**: profile defaults and shortcuts editable in a settings window,
  export of a profile as a `.zip`, light and dark themes (Windows 11 Fluent style).

## Requirements

- Windows 10 version 1809 or later (Windows 10 2004+ recommended), Windows 11
- To build from source: the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- To run a published build only: the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
- Optional: Visual Studio 2026 (or Visual Studio 2022 17.14+, or VS Code with C# Dev Kit)

## Getting started

### Download a ready-to-use version

The [downloads](downloads) folder contains compiled versions of AutoPlay for Windows 64-bit, no
compilation needed:

- [`AutoPlay-1.1.1-win-x64.zip`](downloads/AutoPlay-1.1.1-win-x64.zip) (21 MB) needs the
  [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (Windows offers to
  install it on first launch);
- [`AutoPlay-1.1.1-win-x64-self-contained.zip`](downloads/AutoPlay-1.1.1-win-x64-self-contained.zip)
  (74 MB) runs without installing anything.

Download a zip file (**Download raw file** on GitHub), unblock it (right-click > **Properties** >
**Unblock**), extract it and run `AutoPlay.exe`. See [downloads/README.md](downloads/README.md) for
the details (SmartScreen warning, checksums, update and uninstall).

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

The `publish` folder can be copied to any PC that has the .NET 10 Desktop Runtime.

### Build the downloadable versions

To update the [downloads](downloads) folder (for example after changing the version in
`Directory.Build.props`), run with PowerShell 7 and the .NET 10 SDK (on Windows, Linux or macOS):

```powershell
pwsh scripts/publish.ps1
```

The script publishes the two builds, zips them into `downloads`, removes the previous version and
updates `SHA256SUMS.txt`; then update the file names and sizes in `downloads/README.md` and in this
README, and commit the folder. The self-contained build includes the .NET runtime and is packed into a
single compressed `AutoPlay.exe`; only the English resources of .NET and WPF are included.

### Package size

The application is small; most of a build is made of:

| Part | Size (uncompressed) | How it is kept small |
|---|---|---|
| OpenCV native library (`OpenCvSharpExtern.dll`), used for image matching and PNG files | 55 MB | AutoPlay uses `OpenCvSharp4.runtime.win.slim` instead of `OpenCvSharp4.runtime.win`: same OpenCvSharp project, same OpenCV version, but built only with the modules AutoPlay may need (core, image processing, image files, and a few others), without neural networks, machine learning, video, user interface and the extra "contrib" modules. This saves 12 MB (67 MB with the full package) and removes the 28 MB ffmpeg video DLL. |
| .NET and WPF runtime (self-contained build only) | about 130 MB | Only the English resources are published (9 MB less), and the files are packed into a single compressed `AutoPlay.exe`. WPF cannot be trimmed. |

If a future feature needs an OpenCV module that the slim package leaves out (for example `dnn` or
`video`), switch back to the full packages `OpenCvSharp4.runtime.win` and
`OpenCvSharp4.official.runtime.linux-x64` in `Directory.Packages.props` and the project files.

## User guide

On first use, the main window guides you through the three steps below: each empty list explains
what to do.

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
   | Redraw the selected location | Ctrl+drag |
   | Set the click point (red cross) | Shift+click inside the selected location (default: center) |
   | Delete the selected location | Delete key |
   | Zoom / pan | Mouse wheel / middle-drag or Space+drag; **Fit** (Ctrl+0), **100%** (Ctrl+1) |
   | Rename / threshold / colors | Locations panel on the right |
   | Save / cancel | Enter / Esc |

5. Give the screen a name, check the default delay before clicks, and click **Save**.

Draw each rectangle around a distinctive part of the element (a button with its label rather than a
plain background), and avoid areas that change often (counters, timers, animations). Check
**Colors** for a location whose color matters, for example a button that is greyed out when it cannot
be used.

Use **Edit** (or double-click) to reopen a screen, **Re-capture** to capture it again after the
application changed (locations found in the new capture are moved automatically, the others are shown
in red for you to check), and **Delete** to remove it.

**Quick capture**: once a target area is known for the profile, press **Ctrl+Shift+F9** while
playing to capture it without leaving the game (a sound confirms it). The captures appear under
**Pending captures** in the main window: **Annotate…** opens them in the screen editor,
**Discard** deletes them.

### 3. Build a sequence

1. Click **New sequence**, type a name and a repeat count (0 = repeat until stopped).
2. In the **Add a step** panel, pick a screen, then a location, and click **Add step** (or
   double-click the location). **Add wait** inserts a pause, and **Run another sequence** inserts a
   step that runs another sequence of the profile. Steps are inserted after the selected step.
3. Reorder by drag and drop, or with **Move up** / **Move down** (Alt+Up / Alt+Down); **Duplicate**
   (Ctrl+D) or **Delete** a step.
4. For the selected step, you can set a custom delay, disable the verification, or set a custom
   verification timeout (useful after a long loading screen).
5. **Test** (F6) checks that the location of the selected step is found on the screen right now,
   without clicking. **Run from here** (F5) saves the sequence and runs it from the selected step.
6. To run the sequence automatically, check **Start automatically at**, type a time and choose the
   days (none = every day).
7. Click **Save**.

### 4. Run a sequence

1. Display the first screen of the sequence in the target application.
2. Select the sequence and click **▶ Run**. The blue overlay reappears at the last position used:
   adjust it if the application moved or was resized, then click **Start**.
3. A small status window, placed beside the target area, shows the progress and the match score of
   each click.

| Control | Effect |
|---|---|
| **F12** or **Esc** (from any window) or **Stop** | Stops the run |
| Moving the mouse | Pauses the run before the next click |
| **Resume** | Continues from the interrupted step (a failed step is retried) |
| **View capture** | After a failure, opens the capture with the expected position in red |
| **Close** | Returns to the main window |

The main window then shows the outcome of the run next to the sequence (✔ completed, ✖ failed…).

While a sequence runs, AutoPlay moves the real mouse: do not use the PC (other than to stop or pause
the run), and keep the target application visible and the session unlocked.

**Scheduled runs** start only while AutoPlay is open and idle, without the overlay, on the last target
area used by the profile: run the sequence once by hand first, and keep the application at the same
place on the screen.

### Settings

**File > Settings…** sets the theme (system, light or dark), whether Esc stops a run (F12 always
does), the quick capture shortcut, the data folder, and the defaults of the selected profile:

- **Match threshold** (0 to 1, default 0.8): the minimum similarity required to click. Lower it for
  a location that often fails because of visual effects; raise it if a similar element is clicked by
  mistake. The score of each click is shown in the status window and by **Test**. A location can also
  have its own threshold, in the screen editor.
- **Verification timeout** (default 10 s): the maximum wait for an element before a click; increase
  the delay before a click when the previous action triggers an animation or a loading screen.
- **Retry interval**, **search margin** (how far from its recorded position an element is looked
  for) and **mouse tolerance** (how far the mouse may move before the run pauses).

### Data, export and logs

Profiles are stored in `%LOCALAPPDATA%\AutoPlay\Profiles` (or the folder chosen in the settings),
one folder per profile, as JSON and PNG files (see the
[specification](docs/specification.md#6-data-model-and-storage)). Captures of failed verifications
are saved in each profile's `logs` folder. The JSON format is versioned (`schemaVersion`) and
described by JSON Schemas in [docs/schemas](docs/schemas).

- **File > Export profile…** saves the selected profile (screens, images, sequences) as a `.zip`
  file, for a backup or to share it.
- **File > Open data folder** and **Open logs folder** open the folders in the Explorer. The
  application log (`%LOCALAPPDATA%\AutoPlay\Logs`, one file per day, kept 14 days) records every run;
  attach it when reporting a problem.

Only one instance of AutoPlay can run at a time.

### Troubleshooting

| Problem | Solution |
|---|---|
| Clicks are slightly off | Make sure the overlay exactly covers the same area as when recording. |
| A location is never found | Use **Test** in the sequence editor, or open **View capture** after a failure: if the element is visible but the score is low, lower the threshold, redraw the location around a more distinctive area, or re-capture the screen. |
| Everything fails after a game update | **Re-capture** the screens: the locations are re-positioned automatically. |
| F12 or Esc does nothing | Another application already uses the key: the status window says so. Use **Stop** or move the mouse. |
| The quick capture shortcut does nothing | Another application uses it (the status bar says so): choose another one in the settings. A target area must be known: record a screen or run a sequence first. |
| A scheduled run did not start | AutoPlay must be open and idle; the status bar says why a run was skipped. |
| The run stops when the PC locks | Capture and clicks do not work on a locked session: disable the automatic lock while running. |

## Development

```powershell
dotnet build AutoPlay.slnx
dotnet test AutoPlay.slnx
```

The code follows a **hexagonal architecture** (ports and adapters): see
[docs/architecture.md](docs/architecture.md). The Domain, Application, Persistence, Vision and
Presentation projects (and their tests) are cross-platform; the Windows adapter and the App build on any OS
(`EnableWindowsTargeting`) but run only on Windows. Warnings are treated as errors. A GitHub Actions
workflow builds and tests the solution on Windows for every pull request.

| Project | Description |
|---|---|
| `src/AutoPlay.Domain` | Model and business rules (coordinates, rectangle editing, screen and sequence validation, sequence expansion). No dependency. |
| `src/AutoPlay.Application` | Use cases, sequence engine, settings and the ports they need. Depends only on the Domain (and the logging abstractions). |
| `src/AutoPlay.Adapters.Persistence` | JSON/PNG file storage of profiles, screens, sequences, run history and settings; zip export. |
| `src/AutoPlay.Adapters.Vision` | Template matching with OpenCvSharp. |
| `src/AutoPlay.Adapters.Windows` | Win32 screen capture, mouse input, power requests, global hotkeys, window geometry and clock. |
| `src/AutoPlay.Presentation` | View models, without WPF, so that they are unit tested on any OS. |
| `src/AutoPlay.App` | WPF application (views) and composition root. |
| `tests/AutoPlay.*.Tests` | Unit tests of each layer and adapter. |
| `tests/AutoPlay.ArchitectureTests` | Checks the dependency rules of the architecture. |
| `tests/AutoPlay.TestDoubles` | Fakes of the ports shared by the tests. |

## Documentation

- [Specification](docs/specification.md): functional behavior, data model and image matching.
- [Architecture](docs/architecture.md): layers, ports, adapters and dependency rules.

## Disclaimer

Automating a game may be against its terms of service and can lead to sanctions on your account.
Check the rules of the application you automate; you use AutoPlay at your own risk.

## License

[MIT](LICENSE)
