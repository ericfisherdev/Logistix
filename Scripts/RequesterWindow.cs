using System;
using System.Collections.Generic;
using System.Linq;
using CommonAPI.Systems;
using HarmonyLib;
using Logistix.ModPlayer;
using Logistix.UI;
using Logistix.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Logistix.Scripts
{
    public class RequesterWindow : MonoBehaviour
    {
        private const string WindowName = "Logistix Request Window";
        private static readonly Color RecycleCountColor = new(1f, 0.55f, 0.2f, 1f);

        private GameObject _instanceGo;

        // Set when BuildWindow's guarded sequence throws for a reason that will not resolve
        // itself next frame (as opposed to the donor UI simply not being ready yet, which
        // returns before this flag is ever touched). Without this, a deterministic failure
        // would re-clone the entire replicator hierarchy and log a full stack trace every
        // single frame. Reset in Unload() so a new game session gets one fresh attempt.
        private bool _buildFailed;

        private UIItemRequestWindow uiItemRequestWindow;
        public static RequesterWindow Instance;

        private void Awake()
        {
            Instance = this;
        }

        private void Update()
        {
            if (GameUtil.HideUiElements())
            {
                Hide();
            }
            if (PlogPlayerRegistry.LocalPlayer() == null)
                return;
            if (_instanceGo == null)
            {
                if (_buildFailed)
                    return;
                BuildWindow();
                // Donor UI not ready yet, or the build threw and unwound itself; retry next frame.
                if (_instanceGo == null)
                    return;
            }

            if (_instanceGo.activeSelf)
            {
                uiItemRequestWindow._OnUpdate();
            }
            if (CustomKeyBindSystem.GetKeyBind("ShowPlogWindow").keyValue)
            {
                Toggle();
            }
        }

        /// <summary>
        /// Builds the request window by cloning <c>UIGame.replicator</c> (the live DSP
        /// replicator window) instead of loading <c>Assets/prefab/Request Window.prefab</c> from
        /// the unloadable Unity 2018 <c>pui</c> AssetBundle. See #1/#6/#17. Leaves
        /// <see cref="_instanceGo"/>/<see cref="uiItemRequestWindow"/> null and retries on the
        /// next frame if the donor isn't ready yet, instead of the unguarded null-dereference
        /// chain the prefab-load code used to have. A build failure sets
        /// <see cref="_buildFailed"/> instead of retrying forever.
        /// </summary>
        private void BuildWindow()
        {
            var uiGame = UIRoot.instance != null ? UIRoot.instance.uiGame : null;
            var donor = uiGame != null ? uiGame.replicator : null;
            var inventoryWindow = uiGame != null ? uiGame.inventoryWindow : null;
            if (donor == null || donor.gameObject == null || inventoryWindow == null || inventoryWindow.windowTrans == null)
            {
                Log.Debug("Requester window: donor UI (replicator/inventory window) not ready yet, deferring build");
                return;
            }

            var go = DspUiClone.CloneInactive(donor.gameObject, inventoryWindow.windowTrans.parent, WindowName);
            UIItemRequestWindow itemRequestWindow = null;

            // PopulateWindow and the _Create/_Init/_Close lifecycle calls that follow it all
            // progressively mutate or allocate against the clone (destroying donor sub-objects,
            // AddComponent, the ComputeBuffers/materials _OnCreate allocates, the
            // TabSystem/Resources/GameMain lookups _OnCreate and _OnInit make). A throw
            // anywhere in that sequence must not leave a committed, active, half-initialized
            // window that _OnUpdate() then NREs on every frame, so nothing is committed to
            // _instanceGo/uiItemRequestWindow until the whole sequence has succeeded, and
            // anything _OnCreate already allocated is released via _Destroy() before the clone
            // itself is destroyed.
            try
            {
                itemRequestWindow = PopulateWindow(go, donor);
                itemRequestWindow._Create();
                itemRequestWindow._Init(GameMain.mainPlayer);
                itemRequestWindow._Close();
            }
            catch (Exception e)
            {
                Log.Warn($"Requester window: window build failed, giving up for this session. {e.Message}\n{e.StackTrace}");
                _buildFailed = true;
                if (itemRequestWindow != null && itemRequestWindow.created)
                    itemRequestWindow._Destroy();
                Destroy(go);
                return;
            }

            uiItemRequestWindow = itemRequestWindow;
            _instanceGo = go;
        }

        private UIItemRequestWindow PopulateWindow(GameObject go, UIReplicatorWindow donor)
        {
            var clonedReplicator = go.GetComponent<UIReplicatorWindow>();

            // Harvest the controls this window reuses before the rest of the donor's fields are
            // torn down; assigning them to locals first means they survive the
            // DestroyImmediate(clonedReplicator) call below.
            var windowRect = clonedReplicator.windowRect;
            var itemGroup = clonedReplicator.recipeGroup;
            var itemBg = clonedReplicator.recipeBg;
            var recipeIcons = clonedReplicator.recipeIcons;
            var recipeSelImage = clonedReplicator.recipeSelImage;
            var typeButton1 = clonedReplicator.typeButton1;
            var typeButton2 = clonedReplicator.typeButton2;
            var minPlusButton = clonedReplicator.plusButton;
            var minMinusButton = clonedReplicator.minusButton;
            var multiValueText = clonedReplicator.multiValueText;
            var confirmButton = clonedReplicator.okButton;
            var prefabNumText = clonedReplicator.prefabNumText;
            if (windowRect == null || itemGroup == null || itemBg == null || recipeIcons == null || recipeSelImage == null
                || typeButton1 == null || typeButton2 == null || minPlusButton == null || minMinusButton == null
                || multiValueText == null || confirmButton == null || prefabNumText == null)
                throw new InvalidOperationException("cloned UIReplicatorWindow is missing a harvested control");

            // The additive controls (#20: Recycle spinner, selected-item icon, Current/Update,
            // play/pause, Settings, fuel toggle) and the queue/tree/sandbox/batch controls the
            // request window never uses are not part of this window; destroy them so they don't
            // linger as dead objects (and dead persistent-listener targets) in the clone.
            // Immediate: the clone is still inactive (CloneInactive), so there are no
            // OnDisable/render side effects, and the searches later in this method (the title
            // search, the Button sweep, WireCloseButton) must not still see these discarded
            // subtrees -- a deferred Destroy() only takes effect at end of frame.
            DestroyChildIfNotNull(clonedReplicator.queueGroup);
            DestroyChildIfNotNull(clonedReplicator.treeGroup);
            if (clonedReplicator.currPredictGroup != null)
                DestroyImmediate(clonedReplicator.currPredictGroup);
            DestroyChildIfNotNull(clonedReplicator.batchSwitch);
            DestroyChildIfNotNull(clonedReplicator.instantItemSwitch);
            DestroyChildIfNotNull(clonedReplicator.sandboxAddUsefulItemButton);
            DestroyChildIfNotNull(clonedReplicator.sandboxClearPackageButton);

            DestroyImmediate(clonedReplicator);

            var uiItemRequest = go.AddComponent<UIItemRequestWindow>();
            uiItemRequest.windowRect = windowRect;
            uiItemRequest.itemGroup = itemGroup;
            uiItemRequest.itemBg = itemBg;
            uiItemRequest.recipeIcons = recipeIcons;
            uiItemRequest.recipeSelImage = recipeSelImage;
            uiItemRequest.typeButton1 = typeButton1;
            uiItemRequest.typeButton2 = typeButton2;
            uiItemRequest.minPlusButton = minPlusButton;
            uiItemRequest.minMinusButton = minMinusButton;
            uiItemRequest.multiValueText = multiValueText;
            uiItemRequest.confirmButton = confirmButton;
            uiItemRequest.prefabNumText = prefabNumText;

            // A distinct colour so cells showing a recycle-over-N count (built once real config
            // exists, from RefreshItemIcons) read differently from cells showing a request count.
            uiItemRequest.prefabNumRecycleText = DspUiClone.CloneText(prefabNumText, prefabNumText.transform.parent, "prefab-num-recycle-text", "");
            uiItemRequest.prefabNumRecycleText.color = RecycleCountColor;
            uiItemRequest.prefabNumRecycleText.gameObject.SetActive(false);

            // Label the harvested spinner and Save button; the donor carries no labels for
            // these since the replicator drives them by icon/position alone.
            var requestLabel = DspUiClone.CloneText(multiValueText, multiValueText.transform.parent, "request-label", "PLOGrequest".Translate());
            requestLabel.rectTransform.anchoredPosition = multiValueText.rectTransform.anchoredPosition + new Vector2(0, 20);
            var saveLabel = confirmButton.button.GetComponentInChildren<Text>();
            if (saveLabel != null)
            {
                // The donor's Localizer (if any) re-applies its own translation key on the next
                // OnEnable/language change and would silently undo this relabel otherwise -- the
                // same reason CloneText strips it from every text it clones.
                DspUiClone.StripLocalizers(saveLabel.gameObject);
                saveLabel.text = "PLOGsavechanges".Translate();
            }
            else
                Log.Warn("Requester window: Save button has no child Text to relabel");

            RetitleWindow(windowRect);

            // Cloning a Unity prefab carries over inspector-assigned (persistent) UnityEvent
            // listeners, which keep targeting the original UIGame's replicator components even
            // after those components are destroyed above. RemoveAllListeners() doesn't touch
            // them; only explicitly disabling each persistent listener does.
            var allButtons = go.GetComponentsInChildren<Button>(true);
            foreach (var button in allButtons)
            {
                DspUiClone.DisablePersistentListeners(button);
            }

            WireCloseButton(go, uiItemRequest, allButtons, typeButton1, typeButton2, minPlusButton, minMinusButton, confirmButton);

            go.SetActive(true);
            return uiItemRequest;
        }

        private static void DestroyChildIfNotNull(Component component)
        {
            if (component != null)
                // Immediate for the same reason as currPredictGroup above: a deferred Destroy()
                // would still be visible to the searches later in PopulateWindow.
                DestroyImmediate(component.gameObject);
        }

        /// <summary>
        /// Best-effort retitle: looks for a child <see cref="Text"/> whose GameObject name marks
        /// it as the window's title bar. Logs instead of throwing if none is found, since the
        /// exact chrome layout can only be confirmed by opening the window in a live session
        /// (#10) -- the window is still fully usable with the donor's original title showing.
        /// </summary>
        private static void RetitleWindow(RectTransform windowRect)
        {
            if (windowRect == null)
                return;
            var titleText = windowRect.GetComponentsInChildren<Text>(true)
                .FirstOrDefault(t => t.gameObject.name.IndexOf("title", StringComparison.OrdinalIgnoreCase) >= 0);
            if (titleText == null)
            {
                Log.Warn("Requester window: could not find a title text under the cloned window; leaving the donor's title in place");
                return;
            }
            // Same reason as the Save label: strip the donor's Localizer before writing so it
            // doesn't reassert the donor's own title key on the next OnEnable/language change.
            DspUiClone.StripLocalizers(titleText.gameObject);
            titleText.text = "PLOGplrequests".Translate();
        }

        /// <summary>
        /// The window frame's close button isn't tracked by any <see cref="UIReplicatorWindow"/>
        /// field (it's shared window chrome), so it's identified by elimination: the only
        /// <see cref="Button"/> left once the harvested controls' own buttons are excluded. Logs
        /// the candidate names once so a wrong guess is visible without a live session (#10).
        /// </summary>
        private static void WireCloseButton(GameObject go, UIItemRequestWindow uiItemRequestWindow, IEnumerable<Button> allButtons, params UIButton[] harvestedButtons)
        {
            var harvestedSet = new HashSet<Button>(harvestedButtons.Where(b => b != null).Select(b => b.button));
            var candidates = allButtons.Where(b => !harvestedSet.Contains(b)).ToList();
            if (candidates.Count == 0)
            {
                Log.Warn("Requester window: no close button candidate found under the cloned window; it can only be closed via the keybind");
                return;
            }

            Log.Debug($"Requester window: close button candidates: {string.Join(", ", candidates.Select(c => c.gameObject.name))}");
            var closeButton = candidates[0];
            closeButton.onClick.AddListener(() => uiItemRequestWindow._Close());
        }


        public void Unload()
        {
            // A fresh game session (or the plugin unloading and reloading) gets one new build
            // attempt, even if the previous one failed and set _buildFailed.
            _buildFailed = false;
            if (_instanceGo != null)
            {
                if (uiItemRequestWindow != null && uiItemRequestWindow.gameObject != null)
                {
                    Destroy(uiItemRequestWindow.gameObject);
                    uiItemRequestWindow = null;
                }
                Destroy(_instanceGo);
                _instanceGo = null;
            }
        }

        public void Toggle()
        {
            if (uiItemRequestWindow == null)
            {
                Log.Debug($"window not instantiated");
                return;
            }

            if (uiItemRequestWindow.gameObject.activeSelf)
            {
                Log.Debug($"closing request window");
                uiItemRequestWindow._Close();
            }
            else
            {
                Log.Debug($"opening request window");
                uiItemRequestWindow._Open();
            }
        }

        public void Hide()
        {
            if (uiItemRequestWindow == null)
                return;
            uiItemRequestWindow._Close();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIGame), "_OnFree")]
        public static void UIGame__OnFree_Postfix(UIGame __instance)
        {
            if (Instance == null)
                return;

            // A build failure never commits uiItemRequestWindow, so the branch below (and
            // Unload's own reset) would never run for a session where the previous build
            // failed; reset here unconditionally so the next session still gets a fresh attempt.
            Instance._buildFailed = false;

            if (Instance.uiItemRequestWindow != null && Instance.uiItemRequestWindow.gameObject != null)
            {
                Log.Debug($"called req window _Free");
                Instance.uiItemRequestWindow._Free();
                Instance.Unload();
            }
            else
            {
                Log.Debug($"Taking no action for ui game free instance null: {Instance = null}");
            }
        }
        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIGame), "get_isAnyFunctionWindowActive")]
        public static void UIGame_isAnyFunctionWindowActive_Postfix(ref bool __result)
        {
            if (Instance == null || Instance.uiItemRequestWindow == null || Instance.uiItemRequestWindow.gameObject == null)
            {
                return;
            }

            __result = __result || Instance.uiItemRequestWindow.gameObject.activeSelf;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIGame), nameof(UIGame.ShutAllFunctionWindow))]
        public static void UIGame_ShutAllFunctionWindow_Postfix()
        {
            if (Instance == null || Instance.uiItemRequestWindow == null || Instance.uiItemRequestWindow.gameObject == null)
            {
                return;
            }

            Instance.uiItemRequestWindow._Close();
        }
    }
}
