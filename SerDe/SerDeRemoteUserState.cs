using System.Collections.Generic;
using Logistix.ModPlayer;

namespace Logistix.SerDe
{
    /// <summary> remote user state  </summary>
    public class SerDeRemoteUserState : TocBasedSerDe
    {
        private readonly PlogPlayer _remotePlayer;

        public SerDeRemoteUserState(PlogPlayer remotePlayer)
        {
            _remotePlayer = remotePlayer;
            skipWritingVersion = true;
        }

        /// <summary>No-op: a remote player's sections are supplied by the ctor, and clearing the
        /// local player here would destroy it mid-import of the enclosing local save.</summary>
        protected override void PrepareForImport()
        {
        }

        public override List<InstanceSerializer> GetSections()
        {
            var result = new List<InstanceSerializer>
            {
                _remotePlayer.personalLogisticManager,
                _remotePlayer.shippingManager,
                _remotePlayer.inventoryManager,
            };
            // Deliberately no throwaway RecycleWindowPersistence fallback here: RecycleWindow's
            // grid is a single local UI element, not per-player state, and its Import/Export
            // are static methods that read/write that one shared instance. A remote player
            // never has a real recycleWindowPersistence (only PlogLocalPlayer sets one), so
            // constructing a stand-in and importing into it used to clobber whichever player's
            // recycle grid the local UI was showing with a different player's persisted data.
            if (_remotePlayer.recycleWindowPersistence != null)
            {
                result.Add(_remotePlayer.recycleWindowPersistence);
            }

            return result;
        }

        protected override int GetVersion() => -1;
    }
}