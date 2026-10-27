# Agent instructions for this repository

Read `README.md` and `NOTICE.md` first. This public repository contains Windows application source, not the original media.

On Windows 10/11 x64, run `powershell -ExecutionPolicy Bypass -File .\scripts\Fetch-Upstream-Assets.ps1` to acquire and SHA256-check the unchanged upstream assets. Then run `powershell -ExecutionPolicy Bypass -File .\scripts\Build-Windows.ps1` and launch `publish\SunAlanPet.exe`.

Keep the original audio, avatar PNGs, and clip manifests byte-for-byte. `Assets/`, `publish/`, and `dist/` are local outputs; do not commit or upload them. The upstream media is outside the code's MIT license, as explained in `NOTICE.md`.