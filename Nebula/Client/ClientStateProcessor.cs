using System;
using NebulaAPI;
using NebulaAPI.DataStructures;
using NebulaAPI.GameState;
using NebulaAPI.Interfaces;
using NebulaAPI.Networking;
using NebulaAPI.Packets;
using Logistix.ModPlayer;
using Logistix.Nebula.Packets;
using Logistix.SerDe;
using Logistix.Util;

namespace Logistix.Nebula.Client
{
    [RegisterPacketProcessor]
    public class ClientStateProcessor : BasePacketProcessor<ClientState>
    {
        /// <summary>
        /// Used by clients when the server sends in their state
        /// </summary>
        public override void ProcessPacket(ClientState packet, INebulaConnection conn)
        {
            NebulaDiagnostics.RecordReceive(nameof(ClientState), IsHost, IsClient);
            try
            {
                if (!IsClient)
                {
                    Log.Debug($"ignoring client state as host");
                    return;
                }
                var (playerId, state) = ClientState.DecodePacket(packet);
                if (playerId != PlogPlayerId.ComputeLocalPlayerId())
                {
                    Log.Debug($"ignoring state packet for other player {playerId}");
                    return;
                }

                // Captured once and reused below rather than re-reading NebulaLoadState.instance
                // at the point of use: the import that follows (a full TOC parse and
                // multi-section deserialise) gives GameMain.End -> NebulaLoadState.Reset() or
                // RegenerateUserIdRequestProcessor time to null or replace the static in
                // between, so checking and using the same reference is what makes the guard
                // actually cover the dereference it protects.
                var loadState = NebulaLoadState.instance;
                if (loadState == null)
                {
                    // The session ended between our request and this reply (GameMain.End ->
                    // NebulaLoadState.Reset() nulls instance). Nothing left to unpause.
                    Log.Warn($"Dropping client state for {playerId}: no NebulaLoadState, session already ended");
                    return;
                }

                var importRemoteUser = SerDeManager.ImportRemoteUser(playerId, state);
                PlogPlayerRegistry.RegisterLocal(PlogPlayerId.ComputeLocalPlayerId());
                var localPlayer = PlogPlayerRegistry.LocalPlayer();
                Log.Debug($"decoded state from remote: {playerId} {importRemoteUser.inventoryManager.desiredInventoryState.DesiredItems.Count}");
                localPlayer.personalLogisticManager = importRemoteUser.personalLogisticManager;
                localPlayer.shippingManager = importRemoteUser.shippingManager;
                localPlayer.inventoryManager = importRemoteUser.inventoryManager;

                loadState.SetClientStateLoaded();
                Log.Info($"Setting local state as loaded {playerId}");
            }
            catch (Exception e)
            {
                NebulaDiagnostics.RecordFailure(nameof(ClientState), e);
                throw;
            }
        }
    }
}