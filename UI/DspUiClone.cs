using UnityEngine;
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

        private static void StripLocalizers(GameObject go)
        {
            foreach (var localizer in go.GetComponents<Localizer>())
            {
                Object.Destroy(localizer);
            }
        }
    }
}
