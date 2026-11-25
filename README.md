# APTStudio

Windows viewer and extractor for A Plague Tale assets. V1 supports Resonance: browse DPC archives, preview BC1/BC3 textures, and export PNG. The tool reads game files and never writes to the game folder.

## Build

.NET 10 SDK.

```
dotnet build APTStudio.slnx
dotnet test tests/AptStudio.Core.Tests
dotnet run --project src/AptStudio.Desktop
```

## Credits

DPC container format cross-checked against [bff](https://github.com/widberg/bff) by widberg, MIT licensed. No bff code is included. The full notice is in `THIRD-PARTY-NOTICES.md`.
