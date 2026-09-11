using Logistix.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Logistix.UI
{
    /// <summary>
    /// One row of the incoming-items status list: a sibling item-icon <see cref="Image"/>
    /// followed by the translated status message. Legacy <see cref="Text"/> has no sprite tag
    /// (<c>&lt;quad&gt;</c> is text-mesh only), so this replaces the old TMPro
    /// <c>&lt;sprite name="..."&gt;</c> inline icon with a real sibling image; every status
    /// message names exactly one item, so one icon per row is a faithful swap. See #1/#6.
    /// </summary>
    public class IncomingItemRow : MonoBehaviour
    {
        private const float IconSize = 20f;
        private const float RowSpacing = 4f;

        private Image _icon;
        private Text _message;

        public static IncomingItemRow Create(Transform parent, Text textTemplate)
        {
            var rowGo = new GameObject("IncomingItemRow", typeof(RectTransform));
            rowGo.transform.SetParent(parent, false);

            var layoutGroup = rowGo.AddComponent<HorizontalLayoutGroup>();
            layoutGroup.spacing = RowSpacing;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;
            layoutGroup.childForceExpandWidth = false;
            layoutGroup.childForceExpandHeight = false;

            var row = rowGo.AddComponent<IncomingItemRow>();

            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(rowGo.transform, false);
            row._icon = iconGo.AddComponent<Image>();
            row._icon.preserveAspect = true;
            var iconLayout = iconGo.AddComponent<LayoutElement>();
            iconLayout.preferredWidth = IconSize;
            iconLayout.preferredHeight = IconSize;

            row._message = DspUiClone.CloneText(textTemplate, rowGo.transform, "Message", "");
            row._message.supportRichText = true;
            row._message.horizontalOverflow = HorizontalWrapMode.Overflow;

            return row;
        }

        public void Show(int itemId, string message)
        {
            var itemProto = ItemUtil.GetItemProto(itemId);
            _icon.sprite = itemProto?.iconSprite;
            _icon.enabled = _icon.sprite != null;
            _message.text = message;
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
