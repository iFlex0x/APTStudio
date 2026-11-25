# APTStudio

DPC archive browser for Resonance: A Plague Tale Legacy. Textures only: list `Bitmap_Z` rows, preview BC1 and BC3, export PNG. Audio, meshes, materials, animations, and scripts are not decoded.

## build

.NET 10 SDK on Windows.

```
dotnet build APTStudio.slnx
dotnet test APTStudio.slnx
```

## run

```
dotnet run --project src/AptStudio.Desktop
```

CLI:

```
dotnet run --project src/AptStudio.Cli -- scan <gameRoot>
dotnet run --project src/AptStudio.Cli -- list <gameRoot> [--rebuild]
dotnet run --project src/AptStudio.Cli -- export <gameRoot> <nameHash> <outputPath>
```

Game files are opened read-only.

## license

MIT, see `LICENSE`.

Format facts cross-checked against bff (widberg, MIT). Texture decode by BCnEncoder.Net. Notices in `THIRD-PARTY-NOTICES.md`.
