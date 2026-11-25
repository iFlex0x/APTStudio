# APTStudio

Windows asset tool for the A Plague Tale games. V1 is a DPC browser with texture preview and export for Resonance. It reads and extracts only; it never patches, injects, or installs mods. This is a clean-room rewrite. The old code and notes are leads, not truth.

## Read order

Local working copy only:

1. `live.md` for the current session state.
2. `INDEX.md` section 1, then the topic table for the task.
3. The exact source files named by the index.

A status answer without reading `live.md` is a guess. Do not give it.

These files are gitignored. They do not ship in the public repo.

## Truth rules

- The game files and our own reproduced tests are the only oracles. Old findings are claims until re-run.
- Every claim carries a status: verified, claimed, refuted, blocked, missing-source.
- A verification record names the input path or hash, the command, expected, actual, date, and artifact. No record, no verified.
- Never invent offsets, counts, hashes, or timings. Measure, or write unknown.
- Static analysis only for foreign binaries: vendor tools, other people's mods, installers, loaders. Never execute them.
- The user runs the game. We do not launch it, inject into it, or write to the game folder without explicit approval.
- No "done". Show the evidence and let the user judge.

## Product map

Three different things. Never conflate them.

1. TextureStudio, the vendor tool: format and interaction truth for Material_Z and Bitmap_Z and the swap workflow. Static only. The source folder is currently missing.
2. BigFileFriend: the DPC archive parsing reference. Closest code to our container work.
3. AssetStudio and AssetStudioMod: GUI layout and preview style only. A Unity extractor. Never format truth.

If a prompt mixes these, stop and pick one.

## Public repo

- New GitHub repo. The old APTStudio remote is not reused.
- Tracked: app code, tests, build files, a real README, this file, and a plain changelog if it earns one.
- Not tracked: research notes, the source index, the live session file, local agent config, spikes.
- Default is no research in the public tree. A note may enter only if it is clean, factual, and useful to users. No chat residue.
- This file is the only AI-authored file in the public tree. Keep it free of local paths, chat history, and research pointers.
- The repo is judged by the code. Comments must read like a working dev wrote them.

## Main branch

- `main` only contains verified code. No guessed offsets, formats, APIs, or counts.
- If a question needs a probe, cut a `spike/<topic>` branch and work in a separate worktree or `spikes/<topic>/`. Spike code is throwaway.
- Never merge spike code. Port the verified finding, rewrite it clean, test it.
- Features go on `feat/<topic>`. Review before `main`.
- The agent commits on the working branch. No pushes without explicit approval.
- No game folder writes without explicit approval.

## No slop

- Comments: only why, measured constants, offsets, gotchas. No narration, no banners, no JSDoc on private members, no restating signatures.
- Code: shallow, explicit, testable. New file owns a new concept. Delete dead code. No commented-out blocks. No TODO or FIXME on `main`.
- Banned: single-use interfaces, one-product factories, manager/helper/utils classes, DI containers, speculative options, blanket catches, defensive layers that hide errors.
- Match a mature desktop tool, not a generated demo. AssetStudioGUI forks are the density reference.
- Before any commit, run the `deslop` skill on every touched file and fix every hit.
- Run `verification-before-completion` before claiming anything works. Run `systematic-debugging` before changing code for a bug. Run `requesting-code-review` before merge.

## Memory

- `live.md` is updated after every material change, before any long tool run, and before compaction risk.
- New notes go to `research/NNN-slug.md`, numbered from the highest existing file. `INDEX.md` gets one line.
- Never let a fact live only in chat.
- A cold start must be able to continue from `live.md`, `INDEX.md`, and the linked files alone. No "as discussed".

## Scope

- One format, one feature, one slice at a time.
- Subagents get one narrow question, explicit paths, and an output file. They quote `file:line` or a symbol.
- Prefer a small verified core over a broad guessed one.
- Cut scope when the evidence is not there. A named unknown beats a fake answer.

## Code

- `.editorconfig` wins: 4 spaces, UTF-8, CRLF, braces on new lines, double quotes, semicolons.
- Names plain and short. No abbreviations that only the author understands.
- Guard then throw with context. No blanket catches.
- Every parser change gets a fixture or a game-file check. Exact output beats eyeballing.
- Original values only. Never ship numbers or layouts lifted from another mod.

## Local setup

- Local agents: `.opencode/agents/`
- Local skills: `.opencode/skills/`
- Global skills are allowed when they fit: REA for binary reversal, systematic-debugging, verification-before-completion, test-driven-development, writing-plans, deslop.

## Commits and branches

- Commit every change. Small, lowercase, terse messages. No prefixes, no trailers, no Co-authored-by.
- `main` only gets verified, finished work. Start each feature or test on `feat/<topic>` or `spike/<topic>`.
- Finish the branch, run build and tests, then merge to `main`. Delete the branch after merge.
- Spike branches are throwaway. Merge the verified result, never the probe code.
- No push without explicit approval.
- Local memory and agent files stay out of public git. This file is the exception.
