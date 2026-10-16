# AutoPlay — Functional and Technical Specification

> Status: version 1 (V1) implemented, with the V1.1 additions (re-capture, waits, chaining,
> scheduling, quick capture, settings, export, run history). This document describes the behavior of
> the application and the decisions behind it; it is updated with each change. For installation and
> day-to-day use, see the [README](../README.md).

## 1. Purpose

AutoPlay is a Windows desktop application that records **screens** and **click locations** of
any application, assembles them into **sequences**, and replays those sequences reliably.

The initial use case is automating repetitive resource farming in the browser game *Hero Wars*,
which renders inside an HTML canvas (so DOM-based automation is not an option). The application
is deliberately generic and must not contain anything specific to one game.

### 1.1 Goals

1. Record a screen and the click locations it contains.
2. Build click sequences from recorded screens and locations, with waits, and chain sequences.
3. Execute a sequence, verifying before each click that the expected element is present.
4. Keep the recordings usable over time: re-capture a screen after a game update, re-position a
   location, test a step on the live screen.
5. Run sequences at fixed times while AutoPlay is open.

### 1.2 Non-goals

- Automatic screen recognition (screens are identified by a user-supplied name only).
- Conditional branches (e.g. "if popup X appears, close it").
- Background execution without moving the real mouse (see §9, *Browser driver*).
- Keyboard input, drag-and-drop, scrolling in the target application.

## 2. Glossary

| Term | Definition |
|---|---|
| **Profile** | A group of screens and sequences belonging to one target application (e.g. "Hero Wars"). |
| **Target region** | The rectangle of the physical screen, in pixels, where the target application is displayed (e.g. the game canvas). All coordinates are relative to it. |
| **Screen** | A named state of the target application (e.g. "Campaign map"), with a reference capture. |
| **Location** | A named clickable element of a screen (button, list, image…), defined by a rectangle and a click point. Its rectangle content is the **template image**. |
| **Sequence** | An ordered list of steps to execute, repeated a number of times. |
| **Step** | One action of a sequence: click a location, wait, or run another sequence. |
| **Match** | The result of searching for a location's template image in a live capture. |
| **Pending capture** | A capture taken with the quick capture shortcut, waiting to be annotated as a screen. |

## 3. Recording a screen

Recording happens on a **frozen capture**, so the target application never receives clicks
during recording and cannot change screen by accident.

### 3.1 Framing

1. The user selects a profile and clicks **Record screen** in the main window. The main window is
   hidden so that it neither covers the target area nor appears in the capture.
2. A borderless, resizable, **semi-transparent blue overlay** (≈30 % opacity) appears, with its size
   in physical pixels, short instructions, a **Capture** button and a **Cancel** button. If a target
   region was used before in the profile, the overlay opens at that position and size.
3. The user drags the overlay (anywhere outside the buttons) and resizes it by its edges to exactly
   cover the target region (for Hero Wars: the game canvas, not the whole browser window).
4. The user clicks **Capture** (Enter) or **Cancel** (Esc).

### 3.2 Capture

1. The overlay closes, and the application waits 300 ms for the desktop compositor to remove it.
2. The application captures the pixels of the target region and stores the region as the profile's
   last target region.
3. The **screen editor** opens on the frozen capture.

### 3.3 Quick capture

While playing, the user may not want to leave the game to record each screen. The **quick capture
shortcut** (global, **Ctrl+Shift+F9** by default, configurable in the settings, §6.4) captures the
last target region of the selected profile at once, without any window, and plays a sound (a
different one if the capture is impossible, e.g. when no target region is known yet).

The captures are kept as **pending captures** in the profile's `inbox` folder and listed above the
screens in the main window, with their time. **Annotate…** opens the screen editor on a pending
capture, as for a new screen; once the screen is saved, the pending capture is removed. **Discard**
deletes it.

### 3.4 Annotation (screen editor)

The screen editor is a maximized window with a toolbar at the top, the capture in the center
and the list of locations on the right.

- **Toolbar**: screen name, default delay before click (min/max, ms), **Save** (Enter),
  **Cancel** (Esc), **Fit** and **100%**, the zoom level, a reminder of the mouse gestures and the
  validation errors, if any.
- **Zoom and pan**: the capture is first fitted to the window (never enlarged). The mouse wheel
  zooms around the cursor, from 10 % to 1600 %; Ctrl+plus / Ctrl+minus zoom around the center,
  **Fit** (Ctrl+0) and **100%** (Ctrl+1) reset the view. A middle-button drag, or a left drag with
  Space held down, pans the capture. From 200 %, pixels are drawn sharp to place rectangles
  precisely. Frames, labels and handles keep the same size on screen whatever the zoom.
- **Create a location**: dragging on an empty area draws a rectangle around the element. A simple
  click (move below 4 px) creates a 48 × 48 px rectangle centered on the click. Rectangles are at
  least 8 × 8 px and always stay within the capture.
- **Move / resize**: dragging a location moves it; dragging a corner of the selected location
  resizes it. The cursor shows which action applies. Esc during a drag restores the location.
- **Redraw (re-position)**: **Ctrl+drag** draws a new rectangle for the selected location, which
  keeps its name, settings and identifier, so the sequences using it are unchanged.
- **Click point**: shown as a red cross. It defaults to the rectangle center and follows it;
  **Shift+click** inside the selected location sets it explicitly, after which it keeps its position
  relative to the rectangle.
- **Delete** removes the selected location (when no text field has the focus).
- **Locations panel**: one row per location, with an editable name (default `Location N`), the match
  threshold (empty = profile default; `0.85` and `0,85` are both accepted), the **Colors** option
  (§7) and a delete button. Selecting a row highlights the rectangle in orange, and vice versa.
- **Save** validates that the screen name is not empty and unique within the profile (ignoring case),
  that location names are not empty and unique within the screen, that the delays are valid and that
  thresholds are between 0 and 1. It then persists the screen (§6): the full capture and one template
  image per location, cropped from the capture.
- **Cancel**, Esc or closing the window asks for confirmation if anything was changed.

From the main window, **Edit** (or a double-click) reopens an existing screen in the same editor, on
its stored capture, to add, edit or remove locations; **Delete** removes it after confirmation,
listing the sequences that use it.

### 3.5 Re-capture

When the target application changes (game update, new layout), **Re-capture** in the main window
captures an existing screen again:

1. The framing overlay (§3.1) asks the user to display that screen and checks the target region.
2. Each location is searched in the new capture with its template image (§7): first around its
   previous position (search margin), then anywhere in the capture.
3. The screen editor opens on the new capture. Locations that were found are moved to their new
   position; the others keep their previous position and are shown in red, with a message asking the
   user to check them (move, resize or redraw them with Ctrl+drag).
4. Saving replaces the capture and the template images. Location identifiers are kept, so sequences
   keep working.

## 4. Building a sequence

From the main window, **New sequence**, **Edit** (or a double-click), **Duplicate** and **Delete**
manage the sequences of the selected profile. The sequence editor is a dialog window:

- **Toolbar**: sequence name (required, unique within the profile, ignoring case), **repeat count**
  (1 by default, 0 = until stopped), **Save** (Enter), **Cancel** (Esc), the schedule (§4.2) and the
  validation errors.
- **Add a step** (right panel):
  - pick a screen, then one of its locations, shown with its template image; **Add step** (or a
    double-click on the location) inserts a click step;
  - **Add wait** inserts a wait step (1 s by default, adjustable as a random range);
  - **Run another sequence**: pick a sequence of the profile and click **Add** (§4.1).

  New steps are inserted after the selected step, or at the end if no step is selected.
- **Steps list**: number, a large thumbnail (template image, or an icon for waits and chained
  sequences), the title and a summary of the effective settings (e.g.
  `Delay 800–1500 ms (screen default) · verified, timeout 10000 ms (default)`). Steps whose screen,
  location or sequence was deleted are shown in red and must be removed before saving. An empty list
  shows a hint on how to add steps.
- **Reorder / duplicate / delete**: drag and drop a step, or use **Move up** (Alt+Up),
  **Move down** (Alt+Down), **Duplicate** (Ctrl+D) and **Delete** (Delete key) on the selected step.
  The same actions are in the context menu of the list.
- **Test** (F6) looks for the location of the selected click step on the live screen, in the
  profile's last target region, exactly as a run would (same search area and threshold), but once
  and without clicking. The AutoPlay windows get out of the way during the capture. The result shows
  whether the location was found, its score, the threshold and its offset from the recorded position.
- **Run from here** (F5) saves the sequence, then runs it starting at the selected step (§5.1).
  Closing the editor afterwards keeps the saved sequence, even with **Cancel**.
- **Step settings** (below the list), for the selected step:
  - click step: **Custom delay before click** (min/max, ms) — otherwise the screen's default delay;
    **Verify the location before clicking** — enabled by default; **Custom timeout** (ms) for the
    verification — otherwise the profile's default;
  - wait step: the duration range, in ms (a random duration in the range).
- **Save** checks the name, the repeat count, that there is at least one step, that every step refers
  to an existing location or sequence, that custom delays, waits and timeouts are valid, and the
  chaining rules (§4.1).
- **Cancel**, Esc or closing the window asks for confirmation if anything was changed.

Deleting a sequence run by other sequences asks for confirmation and lists them.

### 4.1 Chaining sequences

A **Run sequence** step runs all the steps of another sequence of the profile, as many times as its
repeat count, then the calling sequence continues. Before a run, chained sequences are expanded into
one flat list of steps. A sequence cannot be saved (or run) if:

- it runs itself, directly or through other sequences (loop);
- it runs a sequence that repeats until stopped (it would never end);
- it runs a sequence that no longer exists;
- the expanded sequence would exceed 100,000 steps.

### 4.2 Scheduling

A sequence can **start automatically** at a time of the day (`HH:mm`), on some days of the week (no
day checked = every day). The schedule only applies while AutoPlay is running:

- every 10 seconds, the application looks for the scheduled starts since the last check;
- a due run starts when AutoPlay is idle (no editor open, no run in progress); otherwise it waits,
  and is skipped if it cannot start within 10 minutes;
- a scheduled run is **unattended**: it does not show the framing overlay, it uses the profile's last
  target region (a sequence must have been run once by hand), and the status bar of the main window
  reports what happened (started, skipped, could not start).

## 5. Executing a sequence

### 5.1 Start

1. The user selects a sequence and clicks **Run** (the main action of the main window). If the
   sequence has no steps, refers to deleted screens, locations or sequences, breaks a chaining rule
   (§4.1), or if template images cannot be loaded, an error is shown and nothing runs.
2. The AutoPlay windows get out of the way (the main window is hidden, open editors are minimized)
   and the framing overlay (§3.1) is shown at the last target region used by the profile, with a
   **Start** button. The user adjusts it if the target application moved or was resized, then
   confirms (or cancels with Esc). The region is saved as the profile's last target region.
3. After 300 ms (for the overlay to disappear), the status window opens and the run starts — from the
   first step, or from the chosen step for **Run from here** (§4).
4. The target region size may differ from the size at recording time; all coordinates are scaled
   (§5.3).

The sequence engine runs on a background thread and reports its progress to the status window, so
the user interface stays responsive.

### 5.2 Step algorithm

For each click step:

1. Wait a random duration uniformly drawn between the step's min and max delay.
2. If verification is enabled, repeat until a match is found or the verification timeout expires:
   1. capture the search area (§7) of the target region;
   2. search the template image (§7);
   3. if not found, wait the retry interval (default 250 ms).
3. If no match was found: stop the sequence, save the last capture with the expected rectangle drawn
   on it in the profile's `logs` folder, and report the failure (screen, location, best score).
4. Compute the click point: the location's click point, offset by the difference between the
   expected and the matched rectangle positions.
5. Move the mouse to the click point and perform a left click.

A wait step waits a random duration in its range, without capturing or clicking.

### 5.2.1 Status window

A small always-on-top window shows the state (running, paused, stopped, completed, failed), the
elapsed time, the iteration (`Iteration 2 / 5`, or `Iteration 2` when repeating until stopped), the
current step (`Step 3 / 8: Screen / Location`) and the last match score.

- It is placed next to the target region (right, left, below or above, whichever fits the monitor's
  work area) so that it never covers it, and it is excluded from screen captures
  (`SetWindowDisplayAffinity`, Windows 10 2004+).
- **Stop (F12 or Esc)** stops the run; **Resume** continues from the interrupted step after a stop, a
  pause or a verification failure (the failed step is retried); **Close** returns to the main window.
  Closing the window while running stops the run.
- On a verification failure, the window turns red, shows the screen, location, best score and
  threshold, and **View capture** opens the saved capture of the search area with the expected
  position drawn in red.
- When all iterations are done, the window turns green.

The outcome of each run (completed, stopped, paused, failed, with its message and time) is stored as
the **last run** of the sequence (§6.1) and shown in the main window.

### 5.3 Coordinate scaling

Positions are stored **normalized** (0.0–1.0) relative to the target region at recording time,
together with the recorded region size in pixels.

- The **Proportional** positioning mode is implemented: `x = left + nx × width`,
  `y = top + ny × height`. This matches applications whose content scales with the window (e.g.
  Hero Wars).
- Rectangles are normalized from their edges. Click points designate a pixel and are normalized from
  the pixel's center (`nx = (x + 0.5) / width`), so that conversions round-trip exactly.
- The data model reserves an **Anchored** mode (anchor corner + pixel offset) for applications
  whose elements keep a fixed distance to an edge. It is not implemented, but image matching (§7)
  already compensates for moderate positional drift.

### 5.4 Safety

- **Emergency stop**: the global hotkeys **F12** and **Esc** stop execution immediately, whichever
  window has the focus. Esc can be disabled in the settings (§6.4), for games that use it; F12 always
  stops. If a key is already registered by another application, the status window says so and the
  Stop button (or moving the mouse) must be used.
- **User takeover**: before each click after the first one, if the mouse is more than 10 px away
  from the last position set by AutoPlay, execution pauses and can be resumed or closed. Moving the
  mouse to the status window is therefore enough to pause a run.
- **Sleep prevention**: while a sequence runs, the application holds a Windows power request
  (`PowerCreateRequest` / `PowerSetRequest` with `PowerRequestSystemRequired` and
  `PowerRequestDisplayRequired`) so that the PC neither sleeps nor turns the display off. Unlike
  `SetThreadExecutionState`, a power request is not tied to a thread, which suits asynchronous code.
  The request is released when execution ends, whatever the outcome. A locked session cannot be
  prevented if enforced by a group policy; capture and input do not work on a locked session, which
  results in a verification failure.
- **Single instance**: a named mutex (`Local\AutoPlay.SingleInstance`) prevents a second instance
  from writing the same files; starting AutoPlay again shows a message and exits.

## 6. Data model and storage

### 6.1 Folder layout

Data is stored as JSON and PNG files under `%LOCALAPPDATA%\AutoPlay`. The folder of the profiles can
be moved in the settings (§6.4); the settings and the application logs always stay in
`%LOCALAPPDATA%\AutoPlay`.

```
%LOCALAPPDATA%\AutoPlay\
  settings.json                # application settings (§6.4)
  Logs/
    autoplay-<yyyyMMdd>.log    # application log (§6.5)
  Profiles/                    # default data folder
    <profile-id>/
      profile.json
      screens/
        <screen-id>/
          screen.json
          capture.png          # full frozen capture of the target region
          locations/
            <location-id>.png  # template image of the location
      sequences/
        <sequence-id>.json
      runs.json                # last run of each sequence
      inbox/
        <utc-time>_<capture-id>.png   # pending quick captures (§3.3)
      logs/
        <yyyyMMdd-HHmmss>-<sequence name>-step<N>.png   # capture of a failed verification
```

Identifiers are GUIDs; names are display values and can be renamed freely. JSON files are written
to a temporary file first, then moved, so that a crash never leaves a truncated file.

### 6.2 JSON format and schemas

The JSON files follow published **JSON Schemas** (draft 2020-12), the reference for the format:

| File | Schema |
|---|---|
| `profile.json` | [`docs/schemas/profile.schema.json`](schemas/profile.schema.json) |
| `screens/<screen-id>/screen.json` | [`docs/schemas/screen.schema.json`](schemas/screen.schema.json) |
| `sequences/<sequence-id>.json` | [`docs/schemas/sequence.schema.json`](schemas/sequence.schema.json) |
| `runs.json` | [`docs/schemas/run-history.schema.json`](schemas/run-history.schema.json) |
| `settings.json` | [`docs/schemas/settings.schema.json`](schemas/settings.schema.json) |

- Property names are camelCase, enums are strings (`"Proportional"`), `null` values are written
  explicitly, and unknown properties are rejected by the schemas.
- Only the stored data is written: computed values (such as the right edge or the center of a
  rectangle) are left out.
- Every file starts with **`schemaVersion`**, the version of the format (currently **2**):
  - version 0: files written before versioning, without `schemaVersion`;
  - version 1: `schemaVersion` added, computed values removed;
  - version 2: `matchColors` on locations, `kind` and `sequenceId` on steps, `schedule` on sequences.

  Older files are still read, with defaults for the new properties, and are upgraded the next time
  they are saved. Files with a higher version, written by a newer AutoPlay, are refused with an
  explicit message instead of being misread.
- Automated tests validate the files written by the application against the schemas, so the schemas
  cannot drift from the code: adding a property to the model makes them fail until the schema (and,
  if needed, the version) is updated.

Examples:

`profile.json`

```json
{
  "schemaVersion": 2,
  "id": "6f1c…",
  "name": "Hero Wars",
  "lastTargetRegion": { "left": 120, "top": 95, "width": 1280, "height": 720 },
  "defaults": {
    "verificationTimeoutMs": 10000,
    "retryIntervalMs": 250,
    "matchThreshold": 0.8,
    "searchMargin": 0.5,
    "userTakeoverTolerancePx": 10
  }
}
```

The profile defaults are edited in the settings window (§6.4).

`screen.json`

```json
{
  "schemaVersion": 2,
  "id": "a2b9…",
  "name": "Campaign map",
  "recordedRegionSize": { "width": 1280, "height": 720 },
  "defaultDelay": { "minMs": 800, "maxMs": 1500 },
  "locations": [
    {
      "id": "c3d4…",
      "name": "Raid button",
      "positioning": "Proportional",
      "bounds": { "x": 0.712, "y": 0.804, "width": 0.094, "height": 0.061 },
      "clickPoint": { "x": 0.759, "y": 0.834 },
      "matchThreshold": null,
      "matchColors": false
    }
  ]
}
```

`<sequence-id>.json`

```json
{
  "schemaVersion": 2,
  "id": "e5f6…",
  "name": "Daily campaign raids",
  "repeatCount": 1,
  "steps": [
    {
      "kind": "Click",
      "screenId": "a2b9…",
      "locationId": "c3d4…",
      "delay": null,
      "verificationTimeoutMs": null,
      "verify": true,
      "sequenceId": null
    },
    {
      "kind": "Wait",
      "screenId": "00000000-0000-0000-0000-000000000000",
      "locationId": "00000000-0000-0000-0000-000000000000",
      "delay": { "minMs": 2000, "maxMs": 3000 },
      "verificationTimeoutMs": null,
      "verify": true,
      "sequenceId": null
    },
    {
      "kind": "RunSequence",
      "screenId": "00000000-0000-0000-0000-000000000000",
      "locationId": "00000000-0000-0000-0000-000000000000",
      "delay": null,
      "verificationTimeoutMs": null,
      "verify": true,
      "sequenceId": "f7a8…"
    }
  ],
  "schedule": { "enabled": true, "time": "08:30:00", "days": ["Monday", "Thursday"] }
}
```

`null` means "inherit the default" (screen for delays, profile for verification settings,
location then profile for the threshold). For a wait step, `delay` is the duration of the wait. A
`schedule` of `null` means the sequence is not scheduled; an empty `days` list means every day.

`runs.json` keeps one entry per sequence: `sequenceId`, `endedAt` (with its offset from UTC),
`status` (`Completed`, `Cancelled`, `UserTookOver`, `VerificationFailed`, `InvalidStep`) and
`message`.

### 6.3 Export

**File > Export profile…** saves the selected profile as a `.zip` archive, for a backup or to share
it: `profile.json`, the screens with their images and the sequences, with the same layout as the
profile folder. Diagnostics and local state are left out: `logs`, `inbox` and `runs.json`. The
archive is built in a temporary file, then moved, so a failure never leaves a partial archive.

### 6.4 Settings

**File > Settings…** opens the settings window:

- **Application** (`settings.json`):
  - **Theme**: follow Windows, light or dark (§8.1);
  - **Esc stops a run** (on by default; F12 always does);
  - **Quick capture**: enabled (on by default) and its shortcut, typed by pressing the keys
    (Ctrl+Shift+F9 by default). A shortcut must use Ctrl or Alt, or be a function key, so that it
    does not steal ordinary keys from other applications. It applies at once; if another application
    already uses it, the status bar says so;
  - **Data folder**: the folder of the profiles (default `%LOCALAPPDATA%\AutoPlay\Profiles`), with
    **Browse…** and **Default**. It must be a full path, and applies the next time AutoPlay starts.
    Existing profiles are not moved.
- **Profile defaults** of the selected profile (`profile.json`): match threshold (0–1), verification
  timeout (ms), retry interval (≥ 1 ms), search margin (% of the location size, 0–1000) and mouse
  tolerance (px). **Reset to defaults** restores the initial values.

**Save** validates every field and shows the first error; nothing is saved while a field is invalid.
If `settings.json` cannot be read, the defaults are used.

### 6.5 Application logs

The application writes a rolling log (Serilog file sink, through Microsoft.Extensions.Logging) in
`%LOCALAPPDATA%\AutoPlay\Logs`: one file per day, 10 MB at most per file, 14 files kept. It records
the start and end of the application, each run (start, each click with its score, end and outcome),
scheduled runs, storage errors and unexpected errors. **File > Open logs folder** and
**Open data folder** open these folders in the Explorer.

## 7. Image matching

Library: **OpenCvSharp** (`Cv2.MatchTemplate` with `TemplateMatchModes.CCoeffNormed`).

1. **Scaling**: the template image is resized by the ratio between the current target region size
   and the recorded region size (separately on each axis).
2. **Search area**: the expected rectangle enlarged by `searchMargin` (default 50 % of its size on
   each side), clipped to the target region. This tolerates moderate drift and avoids matching a
   similar element elsewhere on the screen.
3. **Grayscale**: both images are converted to grayscale to find the best position, which reduces
   sensitivity to color effects.
4. **Colors** (optional, per location): grayscale matching cannot tell an enabled button from the
   same button greyed out, or a red gem from a blue one. With **Colors** checked, the matched area is
   also compared with the template in the Lab color space: the color similarity is
   `1 − mean(|Δa| + |Δb|) / 2 / 24` (0 to 1, ignoring the lightness), and the score is the lower of
   the grayscale and color scores.
5. **Threshold**: a match is accepted if the score is ≥ the threshold (default 0.8, configurable per
   location and per profile).
6. **Transient visual effects** (sparkles, flames…): handled by the retry loop of §5.2 — a single
   successful capture within the timeout is enough.

The best score is always reported, so that thresholds can be tuned from real runs or with **Test**
in the sequence editor.

## 8. Technical architecture

### 8.1 Stack

- **.NET 10** (LTS), C#, **WPF** for the user interface, with the built-in **Fluent theme**
  (`ThemeMode`), which follows the light or dark mode of Windows unless forced in the settings. Colors
  are defined as theme-neutral brushes, so the windows read well in both modes.
- **OpenCvSharp4** for image matching and PNG files, with the **slim** native packages
  (`OpenCvSharp4.runtime.win.slim`, and `OpenCvSharp4.official.runtime.linux-x64.slim` for the tests
  on Linux). They contain only the OpenCV modules AutoPlay may need (core, imgproc, imgcodecs…),
  which reduces the build size: 55 MB instead of 67 MB for the native library, and no ffmpeg video
  DLL. See the README, *Package size*.
- **CommunityToolkit.Mvvm** (MVVM source generators) and **Microsoft.Extensions.DependencyInjection**
  in the user interface.
- **Microsoft.Extensions.Logging**, with **Serilog** writing the log files.
- Win32 APIs through source-generated P/Invoke (`LibraryImport`) for capture (GDI `BitBlt`), input
  (`SendInput`), hotkeys (`RegisterHotKey`), window geometry and power management.
- **xUnit** for tests, **JsonSchema.Net** to validate the files against the schemas; package versions
  are managed centrally in `Directory.Packages.props`.
- **Per-Monitor V2 DPI awareness** declared in the application manifest, so that WPF, capture and
  input all work in physical pixels with display scaling (125 %, 150 %…) and multiple monitors.

### 8.2 Hexagonal architecture

The code follows a hexagonal architecture (ports and adapters), described in detail in
[architecture.md](architecture.md):

```
AutoPlay.slnx
src/
  AutoPlay.Domain/               # Hexagon: model and business rules, no dependency
  AutoPlay.Application/          # Hexagon: use cases (UseCases/), engine (Execution/), settings (Settings/), driven ports (Ports/)
  AutoPlay.Adapters.Persistence/ # Driven adapter: JSON and PNG files, zip export
  AutoPlay.Adapters.Vision/      # Driven adapter: OpenCV template matching
  AutoPlay.Adapters.Windows/     # Driven adapters: capture, input, power, hotkeys, window geometry, clock
  AutoPlay.Presentation/         # Driving side: view models and UI services, without WPF
  AutoPlay.App/                  # Driving adapter (WPF views) and composition root
tests/
  AutoPlay.Domain.Tests/
  AutoPlay.Application.Tests/
  AutoPlay.Adapters.Persistence.Tests/
  AutoPlay.Adapters.Vision.Tests/
  AutoPlay.Presentation.Tests/
  AutoPlay.ArchitectureTests/    # Enforces the dependency rules
  AutoPlay.TestDoubles/          # Fakes of the ports shared by the test projects
.github/workflows/build.yml      # Build and test on Windows for pull requests and pushes to main
```

The Domain, Application, Persistence, Vision and Presentation projects target `net10.0` and their
tests run on any OS; the Windows adapter and the App target `net10.0-windows`. The editor and engine
logic lives in the Domain, the Application and the view models of the Presentation project, so that
it is unit tested; the WPF code-behind only handles mouse and keyboard input, drag and drop, zoom and
window placement.

### 8.3 Key ports (`AutoPlay.Application.Ports`)

```csharp
// Driven ports implemented by the adapters (persistence ports omitted: IProfileRepository,
// IScreenRepository, ISequenceRepository, IRunLogStore, IRunHistory, ICaptureInbox,
// IProfileArchive, ISettingsStore).
public interface IScreenCapture
{
    // Captures a rectangle of the physical screen, in physical pixels.
    RawImage Capture(PixelRect area);
}

public interface IInputDriver
{
    void Click(PixelPoint point);
    PixelPoint GetCursorPosition();
}

public interface ITemplateMatcher
{
    // Searches the template, resized to templateSize, and returns the best match and its score;
    // with matchColors, the score also accounts for the colors (§7).
    MatchResult FindBestMatch(RawImage image, RawImage templateImage, PixelSize templateSize, bool matchColors);
}

public interface IPowerManager
{
    IDisposable PreventSleep();
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}
```

`RawImage` (Domain) holds 32-bit BGRA, top-down pixels, so images are exchanged between capture,
matching and storage without depending on System.Drawing or WPF. The sequence engine
(`SequenceRunner`) only depends on these ports, so it is unit tested with fakes, and a browser-based
driver can be added later as a new adapter.

## 9. Future evolutions

- **Screen recognition**: identify the current screen automatically from its reference capture.
- **Conditional steps**: handle popups ("if location X is visible, click it, otherwise continue").
- **Wait for a location**: wait until a location appears or disappears (only fixed waits for now).
- **Anchored positioning mode** (§5.3).
- **Window binding**: attach a profile to a window (process name / title) and compute the target
  region automatically from its client area.
- **Browser driver**: drive the browser through Playwright / Chrome DevTools Protocol, which
  allows background execution and a fixed viewport size.
- **Additional actions**: keyboard input, drag, scroll.
- **Profile import**: open an exported `.zip` (§6.3) as a new profile.
- **Run history**: keep more than the last run of each sequence, with statistics.

## 10. Decisions and known limitations

1. Execution moves the real mouse, so the PC cannot be used during a run, other than to stop or
   pause it. Moving the mouse pauses the run (§5.4).
2. The emergency stop keys are F12 and, optionally, Esc. If another application has registered them,
   the status window says so and the run must be stopped with the Stop button or by moving the mouse.
3. Scheduled runs only happen while AutoPlay is open, on the last target region of the profile: the
   target application must be displayed at the same place.
4. Only the proportional positioning mode is implemented (§5.3).
5. A changed data folder applies after a restart, and existing profiles are not moved to it.
6. Exported profiles cannot be imported from the application yet: with AutoPlay closed, the archive
   can be extracted by hand into a folder of the data folder named after the profile `id` (see its
   `profile.json`).
