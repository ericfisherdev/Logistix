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
                itemRequestWindow = PopulateWindow(go, donor, uiGame);
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

        private UIItemRequestWindow PopulateWindow(GameObject go, UIReplicatorWindow donor, UIGame uiGame)
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

            // #20's additive controls (Recycle spinner, selected-item icon, Current/Update
            // summary, play/pause, Settings, fuel toggle). Built before the persistent-listener
            // sweep and WireCloseButton below so both also see these new clones -- each one
            // carries the same donor persistent-listener hazard the harvested controls do, and
            // WireCloseButton's elimination search must exclude them or it can pick one of them
            // as the close button.
            BuildAdditiveControls(uiItemRequest, uiGame, minPlusButton, minMinusButton, multiValueText, confirmButton, typeButton2);

            // Cloning a Unity prefab carries over inspector-assigned (persistent) UnityEvent
            // listeners, which keep targeting the original UIGame's replicator components even
            // after those components are destroyed above. RemoveAllListeners() doesn't touch
            // them; only explicitly disabling each persistent listener does.
            var allButtons = go.GetComponentsInChildren<Button>(true);
            foreach (var button in allButtons)
            {
                DspUiClone.DisablePersistentListeners(button, go.transform);
            }

            WireCloseButton(go, uiItemRequest, allButtons, typeButton1, typeButton2, minPlusButton, minMinusButton, confirmButton,
                uiItemRequest.maxPlusButton, uiItemRequest.maxMinusButton, uiItemRequest.pauseButton, uiItemRequest.playButton, uiItemRequest.settingsButton);

            go.SetActive(true);
            return uiItemRequest;
        }

        /// <summary>
        /// Builds the controls the original prefab had that the live replicator's own controls
        /// don't (#20): the Recycle spinner, the selected item's icon, the Current/Update summary
        /// texts, play/pause, Settings, and the per-item fuel toggle. Each sub-builder is
        /// independent and best-effort -- a donor piece that can't be found (fuel toggle) logs a
        /// warning and leaves its fields null rather than failing the whole window build, same
        /// contract #17 established for these fields.
        /// </summary>
        private static void BuildAdditiveControls(UIItemRequestWindow uiItemRequest, UIGame uiGame,
            UIButton minPlusButton, UIButton minMinusButton, Text multiValueText, UIButton confirmButton, UIButton typeButton2)
        {
            BuildRecycleSpinner(uiItemRequest, minPlusButton, minMinusButton, multiValueText);
            BuildSelectedItemDisplay(uiItemRequest, multiValueText);
            BuildPlayPauseButtons(uiItemRequest, confirmButton);
            BuildSettingsButton(uiItemRequest, typeButton2);
            BuildFuelToggle(uiItemRequest, uiGame, multiValueText);
        }

        /// <summary>
        /// The Recycle spinner (max stacks before auto-recycle; Request 0 + Recycle 0 = ban) is a
        /// clone of the harvested Request spinner, offset beside it. Donor positions are read up
        /// front into locals before anything is cloned -- #17 only guarantees these three share a
        /// common parent, not a specific layout, and capturing them explicitly instead of
        /// re-deriving them later keeps this independent of that layout (see #16's review finding
        /// on capturing donor state explicitly rather than trusting it implicitly).
        /// </summary>
        private static void BuildRecycleSpinner(UIItemRequestWindow uiItemRequest, UIButton minPlusButton, UIButton minMinusButton, Text multiValueText)
        {
            const float columnOffset = 160f;
            var minPlusPos = ((RectTransform)minPlusButton.transform).anchoredPosition;
            var minMinusPos = ((RectTransform)minMinusButton.transform).anchoredPosition;
            var multiValuePos = multiValueText.rectTransform.anchoredPosition;

            var maxPlusButton = DspUiClone.CloneComponent(minPlusButton, minPlusButton.transform.parent, "max-plus-button");
            ((RectTransform)maxPlusButton.transform).anchoredPosition = minPlusPos + new Vector2(columnOffset, 0);

            var maxMinusButton = DspUiClone.CloneComponent(minMinusButton, minMinusButton.transform.parent, "max-minus-button");
            ((RectTransform)maxMinusButton.transform).anchoredPosition = minMinusPos + new Vector2(columnOffset, 0);

            var multiValueMaxText = DspUiClone.CloneText(multiValueText, multiValueText.transform.parent, "multi-value-max-text", "Inf");
            multiValueMaxText.rectTransform.anchoredPosition = multiValuePos + new Vector2(columnOffset, 0);

            var recycleLabel = DspUiClone.CloneText(multiValueText, multiValueText.transform.parent, "recycle-label", "PLOGrecycle".Translate());
            recycleLabel.rectTransform.anchoredPosition = multiValuePos + new Vector2(columnOffset, 20);

            uiItemRequest.maxPlusButton = maxPlusButton;
            uiItemRequest.maxMinusButton = maxMinusButton;
            uiItemRequest.multiValueMaxText = multiValueMaxText;
        }

        /// <summary>
        /// Selected-item icon and the Current/Update summary texts. selectedItemRequestSummary
        /// needs its own dedicated parent RectTransform: OnSelectedItemChange toggles
        /// <c>selectedItemRequestSummary.transform.parent.gameObject</c> directly, so sharing a
        /// parent with anything else would show/hide that control too.
        /// </summary>
        private static void BuildSelectedItemDisplay(UIItemRequestWindow uiItemRequest, Text multiValueText)
        {
            var anchor = multiValueText.rectTransform.anchoredPosition;
            var parent = multiValueText.transform.parent;

            // A programmatically created RectTransform defaults to 100x100 and silently squashes
            // its content (see #6's review finding); size it explicitly to the grid's own cell
            // size instead of leaving the default.
            var iconGo = new GameObject("selected-item-icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(parent, false);
            var iconRect = (RectTransform)iconGo.transform;
            iconRect.sizeDelta = new Vector2(UIReplicatorWindow.kGridSize, UIReplicatorWindow.kGridSize);
            iconRect.anchoredPosition = anchor + new Vector2(80, 70);
            var selectItemIcon = iconGo.GetComponent<Image>();
            selectItemIcon.preserveAspect = true;

            var currentLabel = DspUiClone.CloneText(multiValueText, parent, "current-label", "PLOGCurrent".Translate());
            currentLabel.rectTransform.anchoredPosition = anchor + new Vector2(0, -40);
            var selectedItemCurrentState = DspUiClone.CloneText(multiValueText, parent, "current-value", "");
            selectedItemCurrentState.rectTransform.anchoredPosition = anchor + new Vector2(0, -60);

            var updateLabel = DspUiClone.CloneText(multiValueText, parent, "update-label", "PLOGUpdated".Translate());
            updateLabel.rectTransform.anchoredPosition = anchor + new Vector2(0, -90);

            var summaryContainerGo = new GameObject("update-value-container", typeof(RectTransform));
            summaryContainerGo.transform.SetParent(parent, false);
            var summaryContainerRect = (RectTransform)summaryContainerGo.transform;
            summaryContainerRect.sizeDelta = new Vector2(200, 40);
            summaryContainerRect.anchoredPosition = anchor + new Vector2(0, -110);
            var selectedItemRequestSummary = DspUiClone.CloneText(multiValueText, summaryContainerRect, "update-value", "");
            selectedItemRequestSummary.rectTransform.anchoredPosition = Vector2.zero;

            uiItemRequest.selectItemIcon = selectItemIcon;
            uiItemRequest.selectedItemCurrentState = selectedItemCurrentState;
            uiItemRequest.selectedItemRequestSummary = selectedItemRequestSummary;
        }

        /// <summary>
        /// Clones of the harvested Save button; only one of the two is ever active at a time
        /// (UIItemRequestWindow.SyncPlayPauseButtons), so both occupy the same position.
        /// </summary>
        private static void BuildPlayPauseButtons(UIItemRequestWindow uiItemRequest, UIButton confirmButton)
        {
            var anchor = ((RectTransform)confirmButton.transform).anchoredPosition;
            var parent = confirmButton.transform.parent;
            var position = anchor + new Vector2(160, -40);

            var pauseButton = DspUiClone.CloneComponent(confirmButton, parent, "pause-button");
            ((RectTransform)pauseButton.transform).anchoredPosition = position;
            RelabelButton(pauseButton, "PLOGPause".Translate());

            var playButton = DspUiClone.CloneComponent(confirmButton, parent, "play-button");
            ((RectTransform)playButton.transform).anchoredPosition = position;
            RelabelButton(playButton, "PLOGPlay".Translate());
            // Not paused is the common default (PluginConfig.IsPaused() == false); Pause visible,
            // Play hidden matches what SyncPlayPauseButtons would set on the first _OnUpdate, so
            // there's no frame where both show before that runs.
            playButton.gameObject.SetActive(false);

            uiItemRequest.pauseButton = pauseButton;
            uiItemRequest.playButton = playButton;
        }

        private static void RelabelButton(UIButton button, string text)
        {
            var label = button.button.GetComponentInChildren<Text>();
            if (label == null)
            {
                Log.Warn($"Requester window: {button.name} has no child Text to relabel");
                return;
            }

            // Same reason as the Save label in PopulateWindow: strip the donor's Localizer before
            // writing so it doesn't reassert the donor's own text on the next OnEnable/language
            // change.
            DspUiClone.StripLocalizers(label.gameObject);
            label.text = text;
        }

        /// <summary>
        /// Clone of typeButton2 (the Buildings tab), reused purely for its DSP-styled UIButton
        /// chrome. Keeps the donor's own icon; swapping it for the embedded Logistix logo is
        /// #18's AssetBundle-removal work, not this issue's.
        /// </summary>
        private static void BuildSettingsButton(UIItemRequestWindow uiItemRequest, UIButton typeButton2)
        {
            var anchor = ((RectTransform)typeButton2.transform).anchoredPosition;
            var settingsButton = DspUiClone.CloneComponent(typeButton2, typeButton2.transform.parent, "settings-button");
            ((RectTransform)settingsButton.transform).anchoredPosition = anchor + new Vector2(140, 0);
            // typeButton2 is cloned mid-tab-selection styling (highlighted/non-interactable
            // depending on whatever currentType happened to be on the live replicator when this
            // was cloned -- see #16's review finding); Settings isn't a tab, so force it back to
            // its own steady state instead of carrying that over.
            settingsButton.highlighted = false;
            settingsButton.button.interactable = true;
            settingsButton.tips.tipTitle = "PLOGSettingsTipTitle".Translate();
            settingsButton.tips.tipText = "PLOGSettingsTipText".Translate();

            uiItemRequest.settingsButton = settingsButton;
        }

        /// <summary>
        /// Per-item mecha-fuel toggle, cloned from the real player delivery panel's own toggle
        /// (UIGame.inventoryWindow.deliveryPanel.deliveryToggle) so its styling matches DSP. Every
        /// hop of that chain is checked (see #6's review finding on unguarded UIRoot.instance
        /// chains) since none of it is covered by BuildWindow's own donor-readiness check, which
        /// only confirms the replicator and inventory window themselves are present.
        /// </summary>
        private static void BuildFuelToggle(UIItemRequestWindow uiItemRequest, UIGame uiGame, Text multiValueText)
        {
            var deliveryPanel = uiGame != null ? uiGame.inventoryWindow?.deliveryPanel : null;
            var deliveryToggleDonor = deliveryPanel != null ? deliveryPanel.deliveryToggle : null;
            if (deliveryToggleDonor == null || deliveryToggleDonor.gameObject == null)
            {
                Log.Warn("Requester window: delivery panel fuel toggle donor not found; fuel toggle will not be built");
                return;
            }

            // OnSelectedItemChange shows/hides fuel controls by toggling enableFuelContainer's
            // GameObject wholesale, so the toggle and its label must share one container rather
            // than being independently positioned siblings -- otherwise the label stays on screen
            // for every non-fuel item once the toggle itself is hidden.
            var anchor = multiValueText.rectTransform.anchoredPosition;
            var containerGo = new GameObject("fuel-toggle-container", typeof(RectTransform));
            containerGo.transform.SetParent(multiValueText.transform.parent, false);
            var container = (RectTransform)containerGo.transform;
            container.sizeDelta = new Vector2(200, 30);
            container.anchoredPosition = anchor + new Vector2(0, -140);

            // CloneInactive leaves the clone itself inactive; that used to be the only thing
            // gating visibility, back when enableFuelContainer was the toggle's own rectTransform.
            // Now that the container is the show/hide switch (see the comment above), the clone
            // must be reactivated once it's fully configured, further down -- OnSelectedItemChange
            // only toggles the container's GameObject and never reaches the clone itself.
            var clone = DspUiClone.CloneInactive(deliveryToggleDonor.gameObject, container, "fuel-toggle");
            var clonedToggle = clone.GetComponent<UIToggle>();
            if (clonedToggle == null || clonedToggle.toggle == null)
            {
                Log.Warn("Requester window: cloned fuel toggle is missing its UIToggle/Toggle component; fuel toggle will not be built");
                // Immediate for the same reason as DestroyChildIfNotNull: the Button sweep and
                // WireCloseButton later in PopulateWindow must not still see this subtree.
                DestroyImmediate(containerGo);
                return;
            }

            // The donor's inspector-assigned onValueChanged listener still targets the real
            // UIPlayerDeliveryPanel.OnDeliveryToggleChange after cloning (same hazard as the
            // Button persistent-listener issue DisablePersistentListeners(Button, Transform)
            // already guards against elsewhere in this method) -- left enabled, clicking this
            // clone would silently mutate the player's actual delivery settings. Scoped to the
            // toggle's own clone so a listener DSP wired *within* that hierarchy (e.g. the
            // UIToggle's own on/off sprite swap) is left alone rather than disabled by mistake.
            DspUiClone.DisablePersistentListeners(clonedToggle.toggle, clone.transform);

            // The donor is the player's real delivery toggle, so its current isOn reflects
            // whatever the player has that set to right now, not this item's fuel state (see
            // #16's review finding on capturing donor state instead of trusting whatever it
            // happens to hold at clone time). OnSelectedItemChange sets the real value once an
            // item is selected; until then this must not show a leftover value from the donor.
            // SetIsOnWithoutNotify rather than a plain assignment: _OnRegEvent (which wires
            // OnFuelToggleValueChanged) hasn't run yet at this point in the build, so nothing is
            // listening either way today, but the reset shouldn't depend on that lifecycle detail
            // staying true.
            clonedToggle.toggle.SetIsOnWithoutNotify(false);

            // Neither sprite is guaranteed non-null on every donor styling, and a toggle with both
            // null renders identically on/off (see #6's review finding); fall back to a colour
            // tint so the control is never silently invisible.
            if (clonedToggle.onSprite == null && clonedToggle.offSprite == null && clonedToggle.image != null)
            {
                var image = clonedToggle.image;
                image.color = clonedToggle.toggle.isOn ? Color.green : Color.gray;
                clonedToggle.toggle.onValueChanged.AddListener(isOn => image.color = isOn ? Color.green : Color.gray);
            }

            // The container -- not the clone -- is what OnSelectedItemChange shows/hides; the
            // clone must be active inside it now that its persistent listeners and isOn are
            // settled, or the container/label render with no visible toggle.
            clone.SetActive(true);

            clonedToggle.rectTransform.anchoredPosition = Vector2.zero;

            var fuelLabel = DspUiClone.CloneText(multiValueText, container, "fuel-toggle-label", "PLOGenableFuel".Translate());
            fuelLabel.rectTransform.anchoredPosition = new Vector2(40, 0);

            uiItemRequest.enableFuelContainer = container;
            uiItemRequest.enableFuelToggle = clonedToggle.toggle;
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
