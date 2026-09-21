# Nebula 2.x multiplayer sync verification

Tracks issue #9. #3 made the multiplayer layer compile; it does not prove it works.
Nebula's 2.x wire protocol and lifecycle hooks may have changed behaviourally, and this
mod registers eleven packet types across host and client. DSP has no headless client and
Nebula sessions need a real host and a real joining client, so the pass/fail for this
issue can only come from a person running two game instances.

## What this issue lands (agent-verifiable)

- `Nebula/NebulaDiagnostics.cs`: a debug-only packet trace, gated behind
  `PluginConfig.logNebulaPacketTraffic` (bound only in Debug builds, off by default, so a
  Release player can't enable a trace whose only readout — the `Ctrl+B` dump below — is
  compiled out). Counts sends, receives and handler failures per packet type, and on
  every receive re-checks whether the `BasePacketProcessor<T>.IsHost` field agrees with
  the live `NebulaLoadState.IsMultiplayerHost()` session check, logging on the first
  observation or on any change. The check is re-evaluated per receive rather than latched
  once per process, because the stale-role failure this harness targets only shows up on
  a *second* session in the same process (leave/rejoin, or the host restarting into a
  joined-as-client session) — a once-per-process latch would never re-compare after
  session one. `RecordSend` is called from every send site: the 6 in `RequestClient`, the
  1 in `ShippingManager.AddRemoteRequest`, and all 5 host-side `conn.SendPacket` replies
  across `ClientStateRequestProcessor`, `AddToNetworkRequestProcessor` and
  `RemoveFromNetworkRequestProcessor`. `Ctrl+B` (alongside the existing `TestPersistence`
  `Ctrl+N`/`Ctrl+M` probes, Debug builds only) dumps the summary to the log.
- `NebulaLoadState.Register()` now reflects the executing assembly after
  `NebulaModAPI.RegisterPackets()` and logs the count of types carrying
  `[RegisterPacketProcessor]`, expecting eleven. A silent drop in that count is how a
  reflection-registration regression would present itself.
- Four defects found by static reading, fixed without needing a live session to
  reproduce:
  - `SerDeRemoteUserState.GetSections()` no longer constructs a throwaway
    `RecycleWindowPersistence` for a remote player. `RecycleWindowPersistence.ImportData`
    calls the static `RecycleWindow.Import`, which mutates the single, local-UI recycle
    grid; importing a remote player's state (e.g. `PlayerStateContainerPersistence`
    loading several remote players from a host save) was overwriting that grid with
    whichever remote player's data was imported last. Remote players never have a real
    `recycleWindowPersistence` (only `PlogLocalPlayer` sets one), so the section is now
    simply omitted for them instead of faked.
  - `NebulaLoadState.Reset()` dereferenced `instance` before nulling it; it now returns
    early when `instance` is already null, so ending a game that never started (or ending
    twice) no longer throws inside the `GameMain.End` Harmony postfix.
  - `LogistixPlugin.Update()` called `NebulaLoadState.instance.RequestStateFromHost()`
    with no null guard; changed to `?.`.
  - `ClientStateProcessor.ProcessPacket()` called
    `NebulaLoadState.instance.SetClientStateLoaded()` with no null guard — reachable if the
    session ends (`GameMain.End` → `NebulaLoadState.Reset()` nulls `instance`) between a
    client's `ClientStateRequest` and the host's reply landing. A bare `?.` would be wrong
    here (it would silently skip `SetClientStateLoaded()`, leaving a live client paused
    forever via `PluginConfig.IsPaused()` → `IsWaitingClient()`), so this one guards
    explicitly and logs instead of unpausing nothing.
  - `ClientStateRequestProcessor` replied to a client's state request by broadcasting
    (`NebulaModAPI.MultiplayerSession.Network.SendPacket`) instead of unicasting
    (`conn.SendPacket`), sending one client's full serialised mod state to every connected
    client. Changed to `conn.SendPacket`, matching the two other host processors.
- **Not fixed, by design:** `BasePacketProcessor<T>.IsHost`/`IsClient` role staleness.
  Reflecting `NebulaAPI.dll` 2.1.0 confirms `IsHost` is a plain field set once by
  `Initialize(bool)`, and `Register()` runs from `LogistixPlugin.Awake()` long before any
  session exists — but the API assembly only ships the interface surface, not Nebula's
  own implementation, so whether `Initialize` is called once at registration or again per
  session can't be determined by reading code on this machine. That's exactly what the
  role-check log in `NebulaDiagnostics` exists to catch; the manual session below is the
  only way to resolve it. If the session logs a mismatch, file it as a bug rather than
  guessing at a fix here.

Confirmed via `dotnet build Logistix.csproj -c Release`, the `Tools/PatchTargetVerifier`
Harmony-patch check, and `Tools/PatchTargetVerifier.Tests` — all green. None of that
exercises a live Nebula session.

## Setup

1. Two DSP 0.10.34.28529 installs, both with BepInEx 5.4.17,
   `nebula-NebulaMultiplayerModApi-2.1.0`, the full Nebula Multiplayer Mod, and this build
   of Logistix. `LogistixPlugin.CheckVersion` (`LogistixPlugin.cs`) requires exact string
   equality between host and client mod versions, so a mismatch is rejected at join —
   build the same commit for both sides.
2. Build **Debug** so the diagnostics keybind and `TestPersistence` are compiled in:
   `dotnet build Logistix.csproj -c Debug`. Copy `bin/Debug/net48/Logistix.dll` to
   `BepInEx/plugins/Logistix/` on both installs.
3. In each install's config, set `[Debug] LogNebulaPacketTraffic = true` and enable
   `BepInEx.cfg`'s `[Logging.Console] Enabled = true` so the trace lines are visible live
   as well as in the log file.
4. Capture both `BepInEx/LogOutput.log` files for the whole session.

## Checklist

Work through these in order against issue #9's acceptance criteria. Note anything that
deviates before checking a box.

1. **Host starts a save with stocked logistics stations. Client joins.** Confirm the
   client receives `ClientState` and `NebulaLoadState.IsWaitingClient()` goes false —
   `PluginConfig.IsPaused()` pauses all inventory management until it does, so a stuck
   flag looks like "the mod does nothing".
2. **Client requests an item the host's network has.** Exercises
   `RemoveFromNetworkRequest` → host `RemoveFromNetworkRequestProcessor` →
   `RemoveFromNetworkResponse` → `CompleteRemoteRequestRemove`. Item must arrive in the
   client's buffer and then inventory. *(AC: "Client can request items and receives
   them".)*
3. **Client requests an item the host's network does not have.** Must land in
   `RequestState.Failed` with the "Host told us that the item could not be found" warning,
   and must not consume a warper — check the `RemoveFromNetworkResponse` handling in
   `ShippingManager.CompleteRemoteRequestRemove` sets `warperNeeded` appropriately even on
   the zero-`removedCount` path.
4. **Client trashes an item with `sendLitterToLogisticsNetwork` on.** Exercises
   `AddToNetworkRequest` → host add → `AddToNetworkResponse` only when the network could
   not take everything. Verify both the fully-absorbed case (no response packet, silent
   success) and the partial case (remainder returns to the client buffer via
   `CompleteRemoteAdd`). *(AC: "Client trash reaches the host's logistics network".)*
5. **Client edits a request/recycle threshold.** Exercises `DesiredItemUpdate`; host-side
   `PlogRemotePlayer` state must change and survive a host save/load.
6. **Host stations change while the client watches.** Exercises `StationInfoUpdate` and
   `ItemSummaryUpdate`; the client's network summary must track the host's.
7. **Duplicate user id.** Start a second client whose `multiplayerUserId` config value
   matches the first. Host `ClientStateRequestProcessor` should reply
   `RegenerateUserIdRequest` and the client should mint a new id and re-request state.
8. **Client leaves and rejoins mid-session; then host ends and restarts the game.**
   Targets the `NebulaLoadState.Reset()`/`instance` null-guard fixes above (including
   `ClientStateProcessor`'s guard against a reply landing after the session already ended)
   — confirm no path throws in the log, and that this second session's `Ctrl+B` role-check
   lines are re-evaluated rather than showing session one's values. *(AC: "Joining
   mid-session syncs existing mod state".)*
9. **Dedicated server, if available.** `IMultiplayerSession.IsDedicated` exists in 2.1.0
   and `LocalPlayer` may behave differently there. If a dedicated server cannot be stood
   up, say so on the issue rather than silently skipping.
10. **Packet trace review.** Throughout the session, press `Ctrl+B` on both instances
    periodically and at the end. Confirm:
    - Every packet type shows `received > 0` matching where it's expected to fire (a type
      that never receives after being sent is a lost-packet symptom).
    - `failures = 0` for every packet type across the whole session. *(AC: "No packet
      handler throws across a full session".)*
    - The role-check line for every packet type shows the `IsHost` field agreeing with the
      live `IsMultiplayerHost()` check on both host and client. Any `MISMATCH` line is the
      stale-role risk materialising — file it as a bug referencing this issue rather than
      trying to fix it here. *(AC: "`BasePacketProcessor.IsHost` is confirmed to match the
      live session role, or the mismatch is filed as a bug".)*
    - The startup log on both instances shows `registered 11 packet processors as
      expected`, not a mismatch warning. *(AC: "Registration check confirms all eleven
      `[RegisterPacketProcessor]` types bind under 2.1.0".)*

## Recording the result

Post the outcome of each step above as a comment on issue #9, attaching both
`BepInEx/LogOutput.log` files and the `Ctrl+B` packet-traffic summaries. *(AC: "Both
`LogOutput.log` files and the packet summary are attached to this issue".)* Open a
separate bug issue per confirmed defect (including any `IsHost` role mismatch), referencing
#9 — this issue's own deliverable is the harness plus a verdict, not further fixes.
