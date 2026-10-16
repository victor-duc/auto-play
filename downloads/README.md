# Downloads

Ready-to-use builds of AutoPlay for Windows (64-bit), so you can try it on any PC without compiling it.

| File | Size | Use it when |
|---|---|---|
| `AutoPlay-1.1.1-win-x64.zip` | 21 MB | The PC has the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (or you can install it). Smallest download. |
| `AutoPlay-1.1.1-win-x64-self-contained.zip` | 74 MB | Nothing must be installed: one `AutoPlay.exe` that includes .NET. |

Requirements: Windows 10 version 1809 or later (2004+ recommended), or Windows 11, 64-bit.

Most of the size is the image recognition library (OpenCV, 55 MB uncompressed) and, in the
self-contained build, the .NET runtime. AutoPlay uses the **slim** build of OpenCV, which leaves out
the modules it does not need (see [Package size](../README.md#package-size)).

## Install

1. Download the zip file: open it on GitHub and click **Download raw file** (the ↓ button).
2. Right-click the zip file > **Properties**, check **Unblock** if it is shown, and click **OK**. Otherwise
   Windows may block the application because it comes from the Internet.
3. Extract the zip file to a folder of your choice (e.g. `Documents\AutoPlay`).
4. Run `AutoPlay.exe`.
   - With the small build, if the .NET 10 Desktop Runtime is missing, Windows shows a message with a
     link to download it: install it, then run `AutoPlay.exe` again.
   - The builds are not signed, so Windows SmartScreen may show **Windows protected your PC**: click
     **More info**, then **Run anyway**.
   - The self-contained build takes a few seconds longer to start the first time: it unpacks its
     components (in `%TEMP%\.net`).

Your profiles and settings are stored in `%LOCALAPPDATA%\AutoPlay`, not in the application folder: to
update AutoPlay, replace the application files with the new version; to uninstall it, delete its folder
(and `%LOCALAPPDATA%\AutoPlay` to remove your data too).

## Check a download

`SHA256SUMS.txt` lists the SHA-256 checksum of each zip file. In PowerShell:

```powershell
Get-FileHash .\AutoPlay-1.1.1-win-x64.zip -Algorithm SHA256
```

The hash must match the line of the file in `SHA256SUMS.txt`.

## Build these files

The files are produced by [`scripts/publish.ps1`](../scripts/publish.ps1), from the version set in
`Directory.Build.props` (see the [README](../README.md#build-the-downloadable-versions)). The folder only
keeps the latest version; previous versions are in the Git history.
