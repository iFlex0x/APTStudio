# APTStudio

Read-only DPC archive browser for Resonance: A Plague Tale Legacy. Indexes every asset row across the game (`Bitmap_Z`, `Mesh_Z`, `Skin_Z`, `Animation_Z`), previews BC1 and BC3 textures, and exports PNG. Meshes, materials, skeletons, audio, and animations are listed but not decoded yet.

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
dotnet run --project src/AptStudio.Cli -- assets <gameRoot>
dotnet run --project src/AptStudio.Cli -- export <gameRoot> <nameHash> <outputPath>
```

Game files are opened read-only.

## license

MIT, see `LICENSE`.

Format facts cross-checked against bff (widberg, MIT). Texture decode by BCnEncoder.Net. Notices in `THIRD-PARTY-NOTICES.md`.
