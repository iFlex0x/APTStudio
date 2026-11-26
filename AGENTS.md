# APTStudio

Viewer and extractor for Resonance: A Plague Tale Legacy DPC archives. Read-only: textures, meshes, materials, skins, skeletons, animation, and audio.

## Code

- `.editorconfig` wins: 4 spaces, UTF-8, CRLF, braces on new lines, double quotes, semicolons.
- Names plain and short. No abbreviations that only the author understands.
- Comments explain why, never what. No banners, no JSDoc on private members.
- Guard then throw with context. No blanket catches.
- Delete dead code. No commented-out blocks. No TODO or FIXME on `main`.
- Every parser change gets a fixture or a game-file check. Exact output beats eyeballing.
- Match a mature desktop tool, not a generated demo.
- Run the `deslop` skill on every touched file before a commit.

## Build

- .NET 10 SDK. Solution `APTStudio.slnx`.
- `dotnet build APTStudio.slnx`
- `dotnet test APTStudio.slnx`
- Zero warnings.

## Branches and commits

- `main` only gets verified, finished work.
- Features on `feat/<topic>`. Probes on `spike/<topic>`; never merge probe code.
- Commit every change. Lowercase, terse messages. No prefixes, no trailers.
- Merge after build and tests pass. Delete the branch after merge.
- No push without explicit approval.
- No game folder writes.
