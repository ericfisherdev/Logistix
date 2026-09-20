# Runtime smoke test on DSP 0.10.34.28529

Tracks issue #10. A clean compile, and a green run of `Tools/PatchTargetVerifier`, only
prove that every `[HarmonyPatch]` target still resolves against the game assembly
offline. Neither proves the patches actually *do* the right thing once BepInEx loads
them into a running game. DSP has no headless mode and Logistix's behaviour (delivery,
recycle, litter sweep, refuel) only exists inside a live save, so this half has to be
run by a person, once, with the log attached to the issue.

This runbook is written so a second person can execute it without reading the mod's
source first.

## Setup

1. Install DSP 0.10.34.28529 and BepInEx 5, matching the versions pinned in
   `Logistix.csproj`:
   - xiaoye97-BepInEx 5.4.17
   - xiaoye97-LDBTool 3.0.0
   - CommonAPI 1.6.5
   - DSPModSave 1.2.0
   - nebula-NebulaMultiplayerModApi 2.1.0
2. Build the mod: `dotnet build Logistix.csproj -c Release`.
3. Copy `bin/Release/net48/Logistix.dll` to `BepInEx/plugins/Logistix/`.
4. In `BepInEx/config/BepInEx.cfg`, set `[Logging.Console] Enabled = true` so the log is
   visible while playing, not just written to `BepInEx/LogOutput.log`.

## Checklist

Each item names the patch site or subsystem it exercises. Work through them in order;
note anything that deviates in the "Result" column of a copy of this table (or inline in
the attached log) before checking a box.

1. **Plugin loads clean.** Launch to the main menu, then load a save.
   `BepInEx/LogOutput.log` shows `Logistix Plugin Loaded 1.0.0`, then the
   `HarmonyPatchReport` bind log with exactly 11 `Harmony bound patch target:` lines and
   `Harmony bound 11 patch target(s)`, and no `Undefined target method` or exception out
   of `LogistixPlugin.Awake`.
2. **UI entry points.** The Logistix menu button appears beside the vanilla menu's 4th
   button (`gameMenu.button4`). `Ctrl+E` toggles the request window
   (`UIGame.On_E_Switch` / key bind id 211, `ShowPlogWindow`).
3. **Request flow.** Stock a PLS or ILS with an item, open the request window, set a
   request for that item, and confirm the buffer fills and an arrival shows up in the
   incoming-items list (`UIGame._OnFree`, `UIGame.get_isAnyFunctionWindowActive`,
   `UIGame.ShutAllFunctionWindow`).
4. **Recycle flow.** Open the mecha inventory, confirm the recycle panel is present
   under the inventory window, drop an item into it, and confirm the item leaves the
   inventory and arrives at a station (`UIStorageGrid._OnOpen` / `_OnClose`).
5. **Litter sweep.** With `SendLitterToLogisticsNetwork = true`, drop items on the
   ground within 1 km of a station and confirm they're collected
   (`TrashSystem.AddTrash` postfix, `TrashHandler`). Separately note whether proliferated
   litter (items with `inc` set) loses its proliferator points on collection -- this is a
   known, pre-existing gap (`inc` is discarded by the postfix), not something to fix as
   part of this run.
6. **Mecha refuel and warpers.** With `addWarpersToMecha` / `neverUseMechaWarper`
   toggled on as appropriate, confirm the mecha refuels and resupplies warpers from the
   logistics network (`MechaRefueler`).
7. **Chest open/close.** Open and close a storage chest. No exception, and the recycle
   panel's open/closed state tracks correctly (`UIStorageGrid._OnOpen` / `_OnClose`).
8. **Item tooltips.** With `showItemTooltips = true`, hover an item and confirm the
   network status tip renders (`UIItemTip.SetTip`).
9. **Planet leave / session end-to-end.** Fly to another planet
   (`GameData.LeavePlanet`), then quit to the main menu and load a save again
   (`GameMain.End` / `GameMain.Start`). No exceptions, no duplicated menu button or
   windows, and `Unload`/`OnDestroy` leave no dangling state.

## Out of scope

The following are deliberately not exercised here and are owned by their own issues:

- Deep save/load round-trip of mod state -- #8.
- Multiplayer sessions -- #9.

## Recording the result

Attach the full `BepInEx/LogOutput.log` from the session to issue #10. Anything that
throws or misbehaves becomes its own issue rather than being fixed inline during the
run -- this stays a verification task, not a bugfix task.
