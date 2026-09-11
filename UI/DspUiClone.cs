using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Logistix.UI
{
    /// <summary>
    /// Helpers for building UI elements out of DSP's own UGUI objects instead of a shipped
    /// AssetBundle. See issue #1: the mod's Unity 2018 bundle cannot load on the 2022.3
    /// runtime, so windows/rows are cloned or constructed from donor objects reached through
    /// the game's live UI tree.
    /// </summary>
    public static class DspUiClone
    {
        /// <summary>
        /// Clones a donor <see cref="Text"/> (font/material/style come along for free) and
        /// strips any <c>Localizer</c> component the donor carries, since that component
        /// overwrites <see cref="Text.text"/> from a translation key on enable/language change
        /// and would fight the caller's own text.
        /// </summary>
        public static Text CloneText(Text template, Transform parent, string name, string value)
        {
            var clone = Object.Instantiate(template, parent, false);
            clone.name = name;
            clone.text = value;
            StripLocalizers(clone.gameObject);
            return clone;
        }

        /// <summary>
        /// Removes any <see cref="Localizer"/> from <paramref name="go"/> so a caller-assigned
        /// <see cref="Text.text"/> survives the next <c>OnEnable</c>/language change. Public so
        /// callers relabeling a harvested (not cloned) donor text -- which still carries
        /// whatever <c>Localizer</c> the donor had -- can apply the same guard <see cref="CloneText"/>
        /// applies automatically.
        /// </summary>
        public static void StripLocalizers(GameObject go)
        {
            foreach (var localizer in go.GetComponents<Localizer>())
            {
                Object.Destroy(localizer);
            }
        }

        /// <summary>
        /// Clones a donor <see cref="GameObject"/> and immediately deactivates the clone, so
        /// callers can finish configuring it (grid dimensions, storage backing, materials)
        /// before anything renders or runs <c>Update()</c> against half-built state.
        /// </summary>
        public static GameObject CloneInactive(GameObject source, Transform parent, string name)
        {
            var clone = Object.Instantiate(source, parent, false);
            clone.SetActive(false);
            clone.name = name;
            return clone;
        }

        /// <summary>
        /// Clones an arbitrary donor <see cref="Component"/> (e.g. a <see cref="UIButton"/> used
        /// as a stand-in for a control that has no dedicated prefab of its own, such as the
        /// Recycle spinner or the Settings button). Unlike <see cref="CloneText"/> this does not
        /// touch <c>Localizer</c>s or set any value -- callers that clone a component carrying a
        /// child <see cref="Text"/> must call <see cref="StripLocalizers"/> themselves before
        /// relabeling it.
        /// </summary>
        public static T CloneComponent<T>(T template, Transform parent, string name) where T : Component
        {
            var clone = Object.Instantiate(template, parent, false);
            clone.name = name;
            return clone;
        }

        /// <summary>
        /// Turns off every inspector-assigned (persistent) listener on a cloned <see cref="Button"/>.
        /// <see cref="UnityEngine.Events.UnityEventBase.RemoveAllListeners"/> only clears
        /// listeners added at runtime via <c>AddListener</c>; persistent listeners serialized on
        /// the donor's prefab still fire after cloning and still target the donor's original
        /// component instance, because Unity's cloning re-points references to other objects
        /// inside the cloned hierarchy but leaves references to external objects untouched. Left
        /// alone, a cloned button silently drives the original DSP window instead of the mod's.
        /// </summary>
        public static void DisablePersistentListeners(Button button) => DisablePersistentListeners(button.onClick);

        /// <summary>
        /// Same hazard as <see cref="DisablePersistentListeners(Button)"/>, for a cloned
        /// <see cref="Toggle"/> (the fuel toggle donor, <c>UIPlayerDeliveryPanel.deliveryToggle</c>,
        /// carries an inspector-assigned <c>onValueChanged</c> listener that targets the real
        /// delivery panel's own toggle handler -- left enabled, clicking the cloned fuel toggle
        /// would silently mutate the player's actual delivery settings).
        /// </summary>
        public static void DisablePersistentListeners(Toggle toggle) => DisablePersistentListeners(toggle.onValueChanged);

        private static void DisablePersistentListeners(UnityEngine.Events.UnityEventBase unityEvent)
        {
            for (var i = 0; i < unityEvent.GetPersistentEventCount(); i++)
            {
                unityEvent.SetPersistentListenerState(i, UnityEventCallState.Off);
            }
        }
    }
}
