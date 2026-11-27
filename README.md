<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="assets/aptstudio-logo-dark.svg">
    <source media="(prefers-color-scheme: light)" srcset="assets/aptstudio-logo-light.svg">
    <img src="assets/aptstudio-logo-dark.svg" alt="APTStudio" width="440">
  </picture>
</p>

APTStudio is a Windows asset browser for **Resonance: A Plague Tale Legacy**.
Browse one archive or search across the game, follow links between assets, and
return to the same row and scroll position. The game folder is read-only.

<p align="center">
  <img src="assets/screenshot.png" alt="APTStudio showing archive scope, animation results, and a character preview" width="920">
</p>

## Browse and export

- Preview supported BC1/BC3 textures by mip and channel. Export PNGs individually
  or in batches.
- Inspect meshes in 3D, see skin parts in context, and pose explicitly bound
  skins; skeletons have a searchable bone table and bind-pose view.
- Follow textures to materials and their slots, meshes to skins, and explicit
  skin-to-skeleton bindings in either direction.
- Play and scrub decoded animation rotations, with an enlarged preview in the
  same window. Skin candidates are ranked by bone names; rig fit is unverified.
- Browse Wwise audio by pack or language, play clips, and export Ogg Vorbis.

The supported game build is Resonance 1.3.0.0. Other texture formats and
multi-surface textures may be listed without a preview. Animation translations
and root motion are not applied. Mesh, skin, skeleton, material, and animation
views do not export those assets.

## Build and run

Windows and the .NET 10 SDK are required to build.

```powershell
dotnet build APTStudio.slnx -c Release
dotnet test APTStudio.slnx -c Release
dotnet run --project src/AptStudio.Desktop -c Release
```

Open the folder containing `Resonance.exe`. The browser remembers that folder
and stores its cache under `%LOCALAPPDATA%\APTStudio`. The Release executable is
`src/AptStudio.Desktop/bin/Release/net10.0-windows/AptStudio.Desktop.exe`. It opens
the browser directly; pass `--game "<game-folder>"` to choose a root on launch.

`Ctrl+O` opens a game, `F5` rescans, and `Ctrl+E` exports selected textures.
The toolbar arrows and mouse side buttons move through asset history.

## CLI

```powershell
dotnet run --project src/AptStudio.Cli -- assets "<game-folder>"
dotnet run --project src/AptStudio.Cli -- materials "<game-folder>" --rebuild
dotnet run --project src/AptStudio.Cli -- export "<game-folder>" 6B6CD7B43802AB3F eye.png
```

Run `aptstudio` without arguments for the full command list.

## License

MIT, see [LICENSE](LICENSE).

Format facts cross-checked against bff by widberg. Dependency credits are in
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
