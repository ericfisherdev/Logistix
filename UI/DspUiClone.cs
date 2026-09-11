using System.Reflection;
using Logistix.Util;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Logistix.UI
{
    /// <summary>
    /// Helpers for building UI elements out of DSP's own UGUI objects instead of a shipped
    /// asset bundle. See issue #1: the mod's Unity 2018 bundle cannot load on the 2022.3
    /// runtime, so windows/rows are cloned or constructed from donor objects reached through
    /// the game's live UI tree.
    /// </summary>
    public static class DspUiClone
    {
        /// <summary>
        /// Loads a <see cref="Sprite"/> from a PNG embedded in this assembly (see #18), replacing
        /// the old load of the menu-button logo out of the unloadable Unity 2018 asset bundle.
        /// Returns <c>null</c> and logs a warning if <paramref name="logicalName"/> isn't found.
        /// </summary>
        public static Sprite LoadEmbeddedSprite(string logicalName)
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(logicalName);
            if (stream == null)
            {
                Log.Warn($"Embedded resource not found: {logicalName}");
                return null;
            }

            var buffer = new byte[stream.Length];
            var offset = 0;
            int bytesRead;
            while (offset < buffer.Length && (bytesRead = stream.Read(buffer, offset, buffer.Length - offset)) > 0)
            {
                offset += bytesRead;
            }

            var texture = new Texture2D(2, 2);
            if (!ImageConversion.LoadImage(texture, buffer))
            {
                Log.Warn($"Failed to decode embedded resource: {logicalName}");
                return null;
            }

            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        }

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
                // Immediate: every caller strips while the clone is still inactive (CloneInactive)
                // and the whole window is activated later in the same frame (PopulateWindow's
                // go.SetActive(true)). A deferred Destroy() only takes effect at end of frame, so
                // the Localizer would still be alive for that first OnEnable and -- if it reapplies
                // its translation key on enable the way this class assumes -- would silently
                // overwrite the text just assigned here.
                Object.DestroyImmediate(localizer);
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
        /// Turns off every inspector-assigned (persistent) listener on a cloned <see cref="Button"/>
        /// whose target lives outside <paramref name="cloneRoot"/>.
        /// <see cref="UnityEngine.Events.UnityEventBase.RemoveAllListeners"/> only clears
        /// listeners added at runtime via <c>AddListener</c>; persistent listeners serialized on
        /// the donor's prefab still fire after cloning. Unity's own cloning docs say <c>Instantiate</c>
        /// re-points references to other objects <i>inside</i> the cloned hierarchy but leaves
        /// references to external objects untouched, so a listener whose target is still under
        /// <paramref name="cloneRoot"/> is the clone's own internal wiring (e.g. a control that
        /// updates its own sibling's visuals) and must keep firing; only a listener that still
        /// targets the donor's original external component is disabled. Left undiscriminated, a
        /// cloned button would either keep silently driving the original DSP window (target left
        /// on) or lose its own internal wiring (target turned off by mistake).
        /// </summary>
        public static void DisablePersistentListeners(Button button, Transform cloneRoot) => DisablePersistentListeners(button.onClick, cloneRoot);

        /// <summary>Same as <see cref="DisablePersistentListeners(Button, Transform)"/>, for a cloned <see cref="Toggle"/>.</summary>
        public static void DisablePersistentListeners(Toggle toggle, Transform cloneRoot) => DisablePersistentListeners(toggle.onValueChanged, cloneRoot);

        private static void DisablePersistentListeners(UnityEventBase unityEvent, Transform cloneRoot)
        {
            for (var i = 0; i < unityEvent.GetPersistentEventCount(); i++)
            {
                if (!TargetsHierarchy(unityEvent.GetPersistentTarget(i), cloneRoot))
                    unityEvent.SetPersistentListenerState(i, UnityEventCallState.Off);
            }
        }

        private static bool TargetsHierarchy(Object target, Transform root)
        {
            // Unity's overloaded null: a destroyed target (e.g. PopulateWindow's
            // DestroyImmediate(clonedReplicator), whose persistent listeners this same sweep may
            // still be walking) compares equal to null here even though the C# reference itself
            // isn't null, so this must run before the pattern match below -- `is Component`
            // still matches a destroyed object's stale wrapper, and dereferencing .transform on
            // it throws MissingReferenceException.
            if (target == null || root == null)
                return false;

            var targetTransform = target switch
            {
                Component component => component.transform,
                GameObject gameObject => gameObject.transform,
                _ => null
            };
            return targetTransform != null && targetTransform.IsChildOf(root);
        }
    }
}
