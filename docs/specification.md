# AutoPlay — Functional and Technical Specification

> Status: draft for version 1 (V1). This document records the decisions made so far and the
> points still open. It is the reference for implementation.

## 1. Purpose

AutoPlay is a Windows desktop application that records **screens** and **click locations** of
any application, assembles them into **sequences**, and replays those sequences reliably.

The initial use case is automating repetitive resource farming in the browser game *Hero Wars*,
which renders inside an HTML canvas (so DOM-based automation is not an option). The application
is deliberately generic and must not contain anything specific to one game.

### 1.1 Goals (V1)

1. Record a screen and the click locations it contains.
2. Build click sequences from recorded screens and locations.
3. Execute a sequence, verifying before each click that the expected element is present.

### 1.2 Non-goals (V1)

- Automatic screen recognition (screens are identified by a user-supplied name only).
- Conditional branches (e.g. "if popup X appears, close it").
- Background execution without moving the real mouse (see §9, *Browser driver*).
- Keyboard input, drag-and-drop, scrolling.

## 2. Glossary

| Term | Definition |
|---|---|
| **Profile** | A group of screens and sequences belonging to one target application (e.g. "Hero Wars"). |
| **Target region** | The rectangle of the physical screen, in pixels, where the target application is displayed (e.g. the game canvas). All coordinates are relative to it. |
| **Screen** | A named state of the target application (e.g. "Campaign map"), with a reference capture. |
| **Location** | A named clickable element of a screen (button, list, image…), defined by a rectangle and a click point. Its rectangle content is the **template image**. |
| **Sequence** | An ordered list of steps to execute. |
| **Step** | One action of a sequence. In V1: click a location. |
| **Match** | The result of searching for a location's template image in a live capture. |

## 3. Recording a screen

Recording happens on a **frozen capture**, so the target application never receives clicks
during recording and cannot change screen by accident.

### 3.1 Framing

1. The user starts "Record screen" from the main window.
2. A borderless, resizable, **semi-transparent blue overlay** (≈30 % opacity) appears, with a
   single **Capture** button in its center. If a target region was used before in the profile, the
   overlay opens at that position and size.
3. The user moves and resizes the overlay to exactly cover the target region (for Hero Wars: the
   game canvas, not the whole browser window).
4. The user clicks **Capture** (or presses Enter).

### 3.2 Capture

1. The overlay is hidden (fully transparent) for a short moment.
2. The application captures the pixels of the target region.
3. The overlay becomes opaque and displays the frozen capture at 1:1 scale, at the same position.

### 3.3 Annotation

A floating toolbar is shown above the capture, and a side panel lists the locations.

- **Toolbar**: screen name field, default delay min/max (ms), **Save** (Enter), **Cancel** (Esc).
- **Create a location**: click-and-drag draws a rectangle around the element. A simple click
  without dragging creates a rectangle of default size (48 × 48 px) centered on the click.
- **Edit a location**: rectangles can be moved and resized with handles. The click point defaults to
  the rectangle center and can be moved (it must stay inside the rectangle).
- **Side panel**: one row per location, with an editable name (default `Location N`), the match
  threshold and a delete button. Selecting a row highlights the rectangle and vice versa.
- **Save** validates that the screen name is not empty and unique within the profile, and that
  location names are not empty and unique within the screen. It then persists the screen (§6).
- **Cancel** discards everything after confirmation if locations were created.

Existing screens can be reopened in the same editor (on their stored capture) to add, edit or remove
locations.

## 4. Building a sequence

The sequence editor is a regular window of the main application.

- A sequence has a name, a **repeat count** (1 by default, 0 = until stopped) and an ordered list
  of steps.
- A step is added by picking a screen, then one of its locations.
- Steps can be reordered (drag-and-drop or up/down buttons), duplicated and deleted.
- Each step may override:
  - the delay before the click (min/max, ms) — defaults to the screen's values;
  - the verification timeout (ms) — defaults to the profile's value;
  - whether verification is enabled — defaults to enabled.
- The editor shows the location's template image next to each step.

## 5. Executing a sequence

### 5.1 Start

1. The user selects a sequence and clicks **Run**.
2. The framing overlay (§3.1) is shown at the last target region used by the profile, with a
   **Start** button. The user adjusts it if the target application moved or was resized, then
   confirms.
3. The target region size may differ from the size at recording time; all coordinates are scaled
   (§5.3).

### 5.2 Step algorithm

For each step:

1. Wait a random duration uniformly drawn between the step's min and max delay.
2. If verification is enabled, repeat until a match is found or the verification timeout expires:
   1. capture the search area (§5.3) of the target region;
   2. search the template image (§7);
   3. if not found, wait the retry interval (default 250 ms).
3. If no match was found: stop the sequence, save the last capture with the expected rectangle drawn
   on it in the profile's `logs` folder, and report the failure (screen, location, best score).
4. Compute the click point: the location's click point, offset by the difference between the
   expected and the matched rectangle positions.
5. Move the mouse to the click point and perform a left click.

The current step, iteration and last match score are displayed in a small always-on-top status
window.

### 5.3 Coordinate scaling

Positions are stored **normalized** (0.0–1.0) relative to the target region at recording time,
together with the recorded region size in pixels.

- V1 supports the **Proportional** positioning mode: `x = left + nx × width`, `y = top + ny × height`.
  This matches applications whose content scales with the window (e.g. Hero Wars).
- The data model reserves an **Anchored** mode (anchor corner + pixel offset) for applications
  whose elements keep a fixed distance to an edge. It is not implemented in V1, but image
  matching (§7) already compensates for moderate positional drift.

### 5.4 Safety

- **Emergency stop**: a global hotkey (default **F12**) stops execution immediately.
- **User takeover**: if the mouse is moved by the user (position differs from the last position set
  by AutoPlay by more than a few pixels), execution pauses and can be resumed or stopped.
- **Sleep prevention**: while a sequence runs, the application holds a Windows power request
  (`PowerCreateRequest` / `PowerSetRequest` with `PowerRequestSystemRequired` and
  `PowerRequestDisplayRequired`) so that the PC neither sleeps nor turns the display off. Unlike
  `SetThreadExecutionState`, a power request is not tied to a thread, which suits asynchronous code. The previous state is restored when execution
  ends, whatever the outcome. A locked session cannot be prevented if enforced by a group policy;
  capture and input do not work on a locked session, which results in a verification failure.

## 6. Data model and storage

### 6.1 Folder layout

Data is stored as JSON and PNG files under a user-configurable root folder (default
`%LOCALAPPDATA%\AutoPlay\Profiles`):

```
Profiles/
  <profile-id>/
    profile.json
    screens/
      <screen-id>/
        screen.json
        capture.png            # full frozen capture of the target region
        locations/
          <location-id>.png    # template image of the location
    sequences/
      <sequence-id>.json
    logs/
      <timestamp>-<sequence-id>.png
```

Identifiers are GUIDs; names are display values and can be renamed freely.

### 6.2 JSON schemas (illustrative)

`profile.json`

```json
{
  "id": "6f1c…",
  "name": "Hero Wars",
  "lastTargetRegion": { "left": 120, "top": 95, "width": 1280, "height": 720 },
  "defaults": {
    "verificationTimeoutMs": 10000,
    "retryIntervalMs": 250,
    "matchThreshold": 0.8,
    "searchMargin": 0.5
  }
}
```

`screen.json`

```json
{
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
      "matchThreshold": null
    }
  ]
}
```

`<sequence-id>.json`

```json
{
  "id": "e5f6…",
  "name": "Daily campaign raids",
  "repeatCount": 1,
  "steps": [
    {
      "screenId": "a2b9…",
      "locationId": "c3d4…",
      "delay": null,
      "verificationTimeoutMs": null,
      "verify": true
    }
  ]
}
```

`null` means "inherit the default" (screen for delays, profile for verification settings,
location then profile for the threshold).

## 7. Image matching

Library: **OpenCvSharp** (`Cv2.MatchTemplate` with `TemplateMatchModes.CCoeffNormed`).

1. **Scaling**: the template image is resized by the ratio between the current target region size
   and the recorded region size (separately on each axis).
2. **Search area**: the expected rectangle enlarged by `searchMargin` (default 50 % of its size on
   each side), clipped to the target region. This tolerates moderate drift and avoids matching a
   similar element elsewhere on the screen.
3. **Grayscale**: both images are converted to grayscale before matching, to reduce sensitivity to
   color effects.
4. **Threshold**: a match is accepted if the best score is ≥ the threshold (default 0.8,
   configurable per location and per profile).
5. **Transient visual effects** (sparkles, flames…): handled by the retry loop of §5.2 — a single
   successful capture within the timeout is enough.

The best score is always reported, so that thresholds can be tuned from real runs.

## 8. Technical architecture

### 8.1 Stack

- **.NET 10** (LTS), C#, **WPF** for the user interface.
- **OpenCvSharp4** (with the Windows runtime package) for image matching.
- Win32 APIs through P/Invoke (or CsWin32) for capture, input, hotkeys and power management.
- **Per-Monitor V2 DPI awareness** declared in the application manifest, so that WPF, capture and
  input all work in physical pixels with display scaling (125 %, 150 %…) and multiple monitors.

### 8.2 Solution layout

```
AutoPlay.slnx
src/
  AutoPlay.Core/        # Domain model, storage, sequence engine, abstractions (no UI, no Win32)
  AutoPlay.Vision/      # Template matching and PNG encoding (OpenCvSharp)
  AutoPlay.Windows/     # Screen capture, mouse input, global hotkeys, sleep prevention (Win32)
  AutoPlay.App/         # WPF application (overlay, editors, status window)
tests/
  AutoPlay.Core.Tests/
  AutoPlay.Vision.Tests/
```

`AutoPlay.Core` and `AutoPlay.Vision` target `net10.0` and their tests run on any OS; the Windows
projects target `net10.0-windows10.0.17763.0`.

### 8.3 Key abstractions (`AutoPlay.Core`)

```csharp
// 32-bit BGRA, top-down pixels: exchanged between capture, matching and storage
// without depending on System.Drawing or WPF.
public sealed class RawImage { /* Width, Height, Pixels, Crop(...) */ }

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
    // Searches the template, resized to templateSize, and returns the best match and its score.
    MatchResult FindBestMatch(RawImage image, RawImage templateImage, PixelSize templateSize);
}

public interface IImageCodec
{
    byte[] EncodePng(RawImage image);
    RawImage Decode(byte[] data);
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

The sequence engine (`SequenceRunner`) only depends on these interfaces, so it can be unit tested
with fakes, and a browser-based driver can be added later without changing it.

## 9. Future evolutions

- **Screen recognition**: identify the current screen automatically from its reference capture.
- **Conditional steps**: handle popups ("if location X is visible, click it, otherwise continue").
- **Anchored positioning mode** (§5.3).
- **Window binding**: attach a profile to a window (process name / title) and compute the target
  region automatically from its client area.
- **Browser driver**: drive the browser through Playwright / Chrome DevTools Protocol, which
  allows background execution and a fixed viewport size.
- **Additional actions**: keyboard input, drag, scroll, wait for a location to disappear.

## 10. Open questions

1. Execution moves the real mouse, so the PC cannot be used during a run (other than to stop it).
   Is this acceptable for V1? *Assumed yes.*
2. Default emergency stop hotkey: F12 is assumed — it must not conflict with the target
   application.
