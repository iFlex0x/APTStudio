# APTStudio

Read-only asset explorer for Resonance: A Plague Tale Legacy. Indexes every row across the game's DPC archives, previews BC1 and BC3 textures, renders meshes in an orbitable software 3D view, reads material slots and skin parts, browses Wwise audio with playback and Ogg export, shows skeleton bone tables and bind poses, poses skinned skins by rotating individual bones or by playing a decoded animation clip. Cross-links show which materials bind a texture, which skins use a mesh, and which skins name a skeleton.

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
dotnet run --project src/AptStudio.Cli -- animation <gameRoot> <nameHash>
dotnet run --project src/AptStudio.Cli -- animclip <gameRoot> <nameHash> [frame]
dotnet run --project src/AptStudio.Cli -- skeletons <gameRoot> [--rebuild]
dotnet run --project src/AptStudio.Cli -- skeleton <gameRoot> <nameHash>
dotnet run --project src/AptStudio.Cli -- skelbind <gameRoot>
dotnet run --project src/AptStudio.Cli -- pose <gameRoot> <skinHash> <boneHash> <degrees> [x|y|z]
dotnet run --project src/AptStudio.Cli -- audio <gameRoot>
dotnet run --project src/AptStudio.Cli -- links <gameRoot>
dotnet run --project src/AptStudio.Cli -- export <gameRoot> <nameHash> <outputPath>
dotnet run --project src/AptStudio.Cli -- audioconvert <gameRoot> <pack> <mediaId> <output.ogg>
```

Game files are opened read-only.

## license

MIT, see `LICENSE`.

Format facts cross-checked against bff (widberg, MIT). Texture decode by BCnEncoder.Net. Wwise Vorbis conversion by Ww2Ogg.Core, playback by NVorbis. Notices in `THIRD-PARTY-NOTICES.md`.
