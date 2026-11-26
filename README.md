# APTStudio

Read-only asset explorer for Resonance: A Plague Tale Legacy. Indexes every row across the game's DPC archives, previews BC1 and BC3 textures, renders meshes in an orbitable software 3D view, reads material slots and skin parts, and browses Wwise audio with playback and Ogg export. Skeleton binding and animations are not decoded yet.

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
dotnet run --project src/AptStudio.Cli -- mesh <gameRoot> <nameHash>
dotnet run --project src/AptStudio.Cli -- materials <gameRoot>
dotnet run --project src/AptStudio.Cli -- material <gameRoot> <nameHash>
dotnet run --project src/AptStudio.Cli -- skins <gameRoot>
dotnet run --project src/AptStudio.Cli -- skin <gameRoot> <nameHash>
dotnet run --project src/AptStudio.Cli -- audio <gameRoot>
dotnet run --project src/AptStudio.Cli -- export <gameRoot> <nameHash> <outputPath>
dotnet run --project src/AptStudio.Cli -- audioconvert <gameRoot> <pack> <mediaId> <output.ogg>
```

Game files are opened read-only.

## license

MIT, see `LICENSE`.

Format facts cross-checked against bff (widberg, MIT). Texture decode by BCnEncoder.Net. Wwise Vorbis conversion by Ww2Ogg.Core, playback by NVorbis. Notices in `THIRD-PARTY-NOTICES.md`.
