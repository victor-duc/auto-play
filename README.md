# auto-play
Configure and run a sequence of clicks.

AutoPlay is a Windows desktop application that records screens and click locations of any application,
assembles them into sequences and replays them, checking with image matching that each element is
where it is expected before clicking.

## Requirements

- Windows 10 (1809) or later
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Visual Studio 2026 (or Visual Studio 2022 17.14+, or VS Code with C# Dev Kit) — optional

## Build and run

```powershell
dotnet build AutoPlay.slnx
dotnet test AutoPlay.slnx
dotnet run --project src/AutoPlay.App
```

Profiles are stored in `%LOCALAPPDATA%\AutoPlay\Profiles`.

## Solution layout

| Project | Description |
|---|---|
| `src/AutoPlay.Core` | Domain model, JSON/PNG storage, sequence engine and abstractions (cross-platform). |
| `src/AutoPlay.Vision` | Template matching and PNG encoding with OpenCvSharp (cross-platform). |
| `src/AutoPlay.Windows` | Win32 screen capture, mouse input, power requests and global hotkeys. |
| `src/AutoPlay.App` | WPF application. |
| `tests/*` | xUnit tests for Core and Vision. |

## Documentation

- [Specification](docs/specification.md)
