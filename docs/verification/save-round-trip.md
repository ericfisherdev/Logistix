# DSPModSave round-trip verification on 1.2.0

Tracks issue #8. `DSPModSave` moved from 1.1.3 to 1.2.0 and Logistix's `SerDe/` handles
versioned import/export against it. Static reading of the decompiled 1.2.0 assembly
found and fixed four defects (mismatched export/import counts in
`Shipping/ShippingManager.cs`, stale player state carried across save slots in
`ModPlayer/PlogPlayerRegistry.cs`, a table-of-contents offset miscalculation and a
throwaway-object import both in `SerDe/TocSerDe.cs`) and added a guard so an unknown
save version can never throw out of `SerDe/SerDeManager.Import`. None of that proves the
format round-trips through a real save/load cycle: DSP has no headless mode, the
`DysonSphereProgram.GameLibs` package ships stub assemblies for everything that touches
`GameMain`, and there is no Mono on the dev box, so this half has to be run by a person,
once, with the log attached to the issue.

## In-game round-trip harness (no save/load needed)

`Scripts/TestPersistence.cs` is compiled only into Debug builds (`#if DEBUG`). It already
has a `Ctrl+M` probe that populates a scratch player and writes an on-disk save. This
issue adds `Ctrl+Shift+M`, which needs no save/load cycle at all: it populates the same
scratch state, then for every SerDe version 1 through `SerDeManager.Latest` exports it to
a `MemoryStream`, imports that back into a fresh local player, re-exports that player, and
logs a pass/fail comparison of the re-exported bytes against the original export to the
BepInEx log. Run this first — it catches format regressions (like the `ShippingManager`
count bug this issue fixes) in one keypress, without touching disk.

Byte equality of `export(import(export(x)))` against `export(x)` is used instead of
comparing `PlogPlayer.SummarizeState()` before and after: state that is deliberately not
persisted (recycle-area requests in every version, desired-inventory in v1) is legitimately
absent after import, so a summary-based comparison would report a spurious `FAIL` for
every version that drops such state and could mask a real regression behind an expected
diff. `SummarizeState()` is still logged alongside each result as diagnostic context.

## Setup

1. Install DSP 0.10.34.28529 and BepInEx 5, matching the versions pinned in
   `Logistix.csproj`:
   - xiaoye97-BepInEx 5.4.17
   - xiaoye97-LDBTool 3.0.0
   - CommonAPI 1.6.5
   - DSPModSave 1.2.0
   - nebula-NebulaMultiplayerModApi 2.1.0
2. Build the mod in **Debug** configuration so the `#if DEBUG` test harness and key
   binds are compiled in: `dotnet build Logistix.csproj -c Debug`.
3. Copy `bin/Debug/net48/Logistix.dll` to `BepInEx/plugins/Logistix/`.
4. In `BepInEx/config/BepInEx.cfg`, set `[Logging.Console] Enabled = true`, and set the
   `[Logistix]` logger to `Debug` level so section-by-section import/export lines show
   up in `BepInEx/LogOutput.log`.

## Checklist

Work through these in order against acceptance criteria in issue #8. Note anything that
deviates before checking a box, and attach the log to the issue when done.

1. **In-memory round trip.** Load any save, press `Ctrl+Shift+M`. Confirm the log shows
   a `PASS (N bytes stable)` line for versions 1-4. Any `FAIL` here means a defect in
   `SerDe/` and blocks the rest of this checklist. The harness clears `RecycleWindow`'s
   staged and grid items after each version's import (`RecycleWindow.InitOnLoad()`), so
   running it against a save with a populated recycle grid no longer leaves duplicate
   staged items behind.
2. **New game defaults.** Start a new galaxy, note the seed. Confirm `Enter New Game` in
   the log followed by Logistix state at defaults — this exercises the `GameData.NewGame`
   hook that calls `IntoOtherSave()` on every registered mod.
3. **Populate state.** Queue several load requests and several store requests
   (`Ctrl+E` request window), set a recycle limit, leave items in the recycle grid
   (drop into the Recycle panel), and let at least one shipment go in-flight. Include at
   least one item dropped into the recycle grid that becomes a request originating from
   the recycle area — that's the case the `ShippingManager` export count bug broke.
   Press `Ctrl+N` to dump the buffer/network table to the log.
4. **Save.** Save to slot A. Confirm the log shows each of `PLM`, `SM`, `DINV`, `RW`,
   `PSC` written, and no `mod data export error`.
5. **Reload same slot.** Quit to desktop, relaunch, load slot A. Confirm a `Postload`
   import, each section read back with a non-empty `SummarizeState`, requests still
   pending, recycle limits intact, and shipment arrival times counting down rather than
   firing instantly. *(AC: "Save and reload preserves requests, recycle limits and
   in-flight shipments".)*
6. **Slot isolation.** Without quitting, load slot B in the same galaxy (a save taken
   before any Logistix use). Confirm buffer, requests and recycle grid are empty, not
   slot A's. Then load slot A again and confirm the reverse — slot B's emptiness didn't
   leak into slot A. *(AC: "Switching save slots does not carry state across".)*
7. **No mod data.** Load a save from a galaxy that never ran Logistix. Confirm
   `Game mod save not exist` (or a missing section) is followed by clean defaults, no
   exception, and no `UIMessageBox` popup. *(AC: "Loading a save with no mod data does
   not error".)*
8. **Autosave / exit-save paths.** Trigger an autosave and a `_lastexit_` save and
   reload each; those go through `OnAutoSave`, a separate hook from `OnSave`.
9. **Legacy fixtures.** For each version 1-4, set `Internal -> TEST Export override
   version` in the config to that version, save, and confirm the resulting `.moddsv`
   loads back cleanly (steps 4-5 above, repeated per version). Keep the four resulting
   `.moddsv` files as fixtures under `Examples/` (e.g. `Examples/v1.moddsv` ..
   `Examples/v4.moddsv`) and commit them — loading each with the current build is the
   concrete, on-disk form of "the version-tagged import path still reads earlier schema
   versions". *(AC: "A round-trip for every SerDe version 1-4 passes in the in-game
   harness, with the fixtures kept in the repo".)* Reset the override to `-1` afterwards
   so normal saves aren't affected.

10. **Unknown save version.** The `TEST Export override version` config can only select
    an existing version (`SerDeManager.Export` indexes the `versions` dictionary
    directly), so it cannot produce a save with a version tag newer than `Latest`. Save
    normally, then hand-edit the leading version `int` of the resulting `.moddsv` to `99`
    and load it. Confirm the log shows `unknown save version 99` and the game comes up
    with clean defaults — including an empty recycle grid — rather than a `FAIL` or a
    `UIMessageBox`.

## Recording the result

Record the outcome of each step above in a comment on issue #8, attaching
`BepInEx/LogOutput.log` (or the relevant excerpts) and the four `Examples/vN.moddsv`
fixtures from step 9.
