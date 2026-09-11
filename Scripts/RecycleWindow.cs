using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommonAPI.Systems;
using HarmonyLib;
using Logistix.Logistics;
using Logistix.Model;
using Logistix.ModPlayer;
using Logistix.SerDe;
using Logistix.UI;
using Logistix.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Logistix.Scripts
{
    /// <summary>
    /// Recycle panel: a 1x10 <see cref="UIStorageGrid"/> hung under the inventory window,
    /// built by cloning <c>UIGame.inventoryWindow.inventory</c> instead of loading
    /// <c>Assets/Prefab/Player Inventory Recycle.prefab</c> from the unloadable Unity 2018
    /// <c>pui</c> asset bundle. See #1/#6/#16.
    /// </summary>
    public class RecycleWindow : ManualBehaviour
    {
        private const float PanelGapBelowWindow = 4f;
        private const float TitleHeight = 20f;
        private const float HintHeight = 16f;
        private static readonly Color CheckOnColour = new(0.8f, 0.8f, 0.8f, 1f);
        private static readonly Color CheckOffColour = new(0.35f, 0.35f, 0.35f, 1f);

        private static RecycleWindow _instance;
        private static readonly int buffer = Shader.PropertyToID("_StateBuffer");
        private static readonly int indexBuffer = Shader.PropertyToID("_IndexBuffer");
        private static Texture2D texOff = Resources.Load<Texture2D>("ui/textures/sprites/icons/checkbox-off");
        private static Texture2D texOn = Resources.Load<Texture2D>("ui/textures/sprites/icons/checkbox-on");
        private readonly DelayedContainer<GridItem> _recycledItems = new(TimeSpan.FromSeconds(PluginConfig.minRecycleDelayInSeconds.Value));

        private bool _closeRequested;
        private GameObject _instanceGo;
        private bool _openRequested;
        private StorageComponent _storageComponent;

        // AddShowRecycleCheck() shrinks UIGame.inventoryWindow.titleText and Unload() never
        // restores it, so the donor's original size is captured once (first build) and applied
        // to the cloned "Recycle" title explicitly rather than inherited from whatever the
        // donor's current (possibly already-shrunk) size happens to be.
        private int _donorTitleFontSize;

        private uint[] iconIndexArray;
        private ComputeBuffer iconIndexBuffer;
        private uint[] stateArray;
        private ComputeBuffer stateBuffer;
        private UIStorageGrid uiStorageGrid;
        private Sprite sprOn;
        private Sprite sprOff;
        private GameObject txtGO;
        private GameObject chxGO;
        private Image checkBoxImage;
        private static readonly List<GridItem> _gridItems = new();

        private void Awake()
        {
            _instance = this;
            PluginConfig.minRecycleDelayInSeconds.SettingChanged += RecycleTimeConfigPropertyChanged;
        }

        private void RecycleTimeConfigPropertyChanged(object sender, EventArgs e)
        {
            var delayValue = PluginConfig.minRecycleDelayInSeconds.Value;
            Log.Debug($"Got update event for delay property. New value: {delayValue}. Old value {_recycledItems.MinAgeSeconds()}");
            if (_recycledItems.MinAgeSeconds() != delayValue)
            {
                Log.Debug($"updating delay property");
                _recycledItems.UpdateMinAgeSeconds(delayValue);
            }
        }

        private void Update()
        {
            if (_instanceGo != null && _instanceGo.activeSelf && uiStorageGrid != null)
            {
                uiStorageGrid.OnStorageContentChanged();
            }

            if (PluginConfig.IsPaused() && _instanceGo != null && _instance.gameObject.activeSelf)
            {
                _instanceGo.SetActive(false);
                SetCheckBoxState(false);
            }

            // remove recycle window as target for shift clicking logistics vessels/bots when another station window is open
            if (UIRoot.instance.uiGame.stationWindow != null && UIRoot.instance.uiGame.stationWindow.gameObject.activeSelf && uiStorageGrid != null)
            {
                UIStorageGrid.openedStorages.Remove(uiStorageGrid);
            }

            // remove recycle window as target for shift clicking if another storage window was opened (player inv open and then storage window is opened)
            if (uiStorageGrid != null && UIStorageGrid.openedStorages.Contains(uiStorageGrid) && UIStorageGrid.openedStorages.Count > 2)
            {
                UIStorageGrid.openedStorages.Remove(uiStorageGrid);
            }

            if (_instanceGo != null && !_instanceGo.activeSelf && uiStorageGrid != null)
                UIStorageGrid.openedStorages.Remove(uiStorageGrid);
         
            if (_openRequested && PluginConfig.showRecycleWindow.Value)
            {
                _openRequested = false;
                if (_instanceGo == null)
                {
                    BuildRecyclePanel();
                }

                // Add the recycle storage grid to the list of opened storages so items can be shift-clicked into it. Only do this if another storage is not open since
                // the preference should be to move items into an open storage bin over recycling
                if (uiStorageGrid != null && UIStorageGrid.openedStorages.Count == 1 && (UIRoot.instance.uiGame.stationWindow == null || !UIRoot.instance.uiGame.stationWindow.gameObject.activeSelf))
                    UIStorageGrid.openedStorages.Add(uiStorageGrid);
            }
            else if (_closeRequested)
            {
                _closeRequested = false;
                if (uiStorageGrid != null)
                {
                    UIStorageGrid.openedStorages.Remove(uiStorageGrid);
                }
            }
        }

        private void RecordStorageChange()
        {
            if (uiStorageGrid == null || _storageComponent == null)
            {
                // not sure how this would happen
                Log.Warn("Storage component notified of change but null reference found");
                return;
            }

            var itemsToRecycle = _storageComponent;
            for (var index = 0; index < itemsToRecycle.size; ++index)
            {
                var itemId = itemsToRecycle.grids[index].itemId;
                if (itemId == 0)
                {
                    continue;
                }

                var count = itemsToRecycle.grids[index].count;
                if (count < 1)
                {
                    continue;
                }

                var inc = itemsToRecycle.grids[index].inc;
                var gridItem = GridItem.From(index, itemId, count, inc);
                if (_recycledItems.HasItem(gridItem))
                {
                    continue;
                }

                _recycledItems.AddItems(gridItem);
            }
        }

        /// <summary>
        /// Builds the recycle panel programmatically by cloning
        /// <c>UIGame.inventoryWindow.inventory</c> into a panel hung under the inventory
        /// window, replacing the old load of the (unloadable on 0.10.34) Unity 2018
        /// asset bundle prefab. Leaves <see cref="_instanceGo"/>/<see cref="uiStorageGrid"/>
        /// null and retries on the next open if the inventory window isn't ready yet, instead
        /// of the unguarded null-dereference chain the prefab-load code used to have.
        /// </summary>
        private void BuildRecyclePanel()
        {
            var inventoryWindow = UIRoot.instance != null && UIRoot.instance.uiGame != null
                ? UIRoot.instance.uiGame.inventoryWindow
                : null;
            if (inventoryWindow == null || inventoryWindow.windowTrans == null ||
                inventoryWindow.inventory == null || inventoryWindow.titleText == null)
            {
                Log.Warn("Recycle window: inventory window not ready yet, deferring panel build");
                return;
            }

            Log.Debug("Building Recycle window panel");
            var panelGo = new GameObject("Logistix Recycle Panel", typeof(RectTransform), typeof(Image));

            // PopulateRecyclePanel progressively assigns uiStorageGrid/_storageComponent as it
            // builds, and only clears the persisted _gridItems once everything below has
            // succeeded. A throw partway through (the clone's first-ever _OnInit()/
            // OnStorageDataChanged() call against a grid with its pooled Text children
            // destroyed, or AddShowRecycleCheck's resource loads, are not verifiable without
            // the game) must not orphan imported items or leave uiStorageGrid pointing at a
            // half-built clone that Export()/RemoveFromStorageImpl() would read from.
            try
            {
                PopulateRecyclePanel(panelGo, inventoryWindow);
            }
            catch (Exception e)
            {
                Log.Warn($"Recycle window: panel build failed, unwinding so the next open retries. {e.Message}\n{e.StackTrace}");
                if (uiStorageGrid != null && uiStorageGrid.storage != null)
                    uiStorageGrid.storage.onStorageChange -= RecordStorageChange;
                uiStorageGrid = null;
                _storageComponent = null;
                Destroy(panelGo);
                return;
            }

            _gridItems.Clear();
            _instanceGo = panelGo;
        }

        private void PopulateRecyclePanel(GameObject panelGo, UIInventoryWindow inventoryWindow)
        {
            var windowTrans = inventoryWindow.windowTrans;
            var panelRect = (RectTransform)panelGo.transform;
            panelRect.SetParent(windowTrans, false);
            panelRect.anchorMin = new Vector2(0f, 0f);
            panelRect.anchorMax = new Vector2(1f, 0f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.anchoredPosition = new Vector2(0f, -PanelGapBelowWindow);

            var donorPanelImage = windowTrans.GetComponentInChildren<Image>();
            var panelImage = panelGo.GetComponent<Image>();
            if (donorPanelImage != null)
            {
                panelImage.sprite = donorPanelImage.sprite;
                panelImage.type = donorPanelImage.type;
                panelImage.color = donorPanelImage.color;
                Log.Debug($"Recycle panel background copied from {donorPanelImage.name}");
            }
            else
            {
                Log.Warn($"Recycle window: no donor Image found under {windowTrans.name}, panel background left blank");
            }

            // AddShowRecycleCheck() shrinks the donor and Unload() never restores it, so the
            // clone applies the size captured once in _donorTitleFontSize rather than
            // whatever the donor's current (possibly already-shrunk) size happens to be.
            if (_donorTitleFontSize == 0)
                _donorTitleFontSize = inventoryWindow.titleText.fontSize;
            var titleText = DspUiClone.CloneText(inventoryWindow.titleText, panelRect, "recycle-title", "PLOGrecycle".Translate());
            titleText.fontSize = _donorTitleFontSize;
            var titleRect = (RectTransform)titleText.transform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = Vector2.zero;
            titleRect.sizeDelta = new Vector2(0f, TitleHeight);

            var hintText = DspUiClone.CloneText(inventoryWindow.titleText, panelRect, "recycle-hint", "Drop items here to recycle".Translate());
            hintText.fontSize = 11;
            var hintRect = (RectTransform)hintText.transform;
            hintRect.anchorMin = new Vector2(0f, 1f);
            hintRect.anchorMax = new Vector2(1f, 1f);
            hintRect.pivot = new Vector2(0.5f, 1f);
            hintRect.anchoredPosition = new Vector2(0f, -TitleHeight);
            hintRect.sizeDelta = new Vector2(0f, HintHeight);

            var gridGo = DspUiClone.CloneInactive(inventoryWindow.inventory.gameObject, panelRect, "recycle-grid");
            uiStorageGrid = gridGo.GetComponent<UIStorageGrid>();
            uiStorageGrid.primary = false;
            uiStorageGrid.numTexts = null;
            uiStorageGrid.tip = null;
            StripClonedNumberTexts(gridGo, uiStorageGrid.prefabNumText);

            _storageComponent = new StorageComponent(10);
            uiStorageGrid._OnCreate();
            uiStorageGrid.data = _storageComponent;

            uiStorageGrid.storage = _storageComponent;
            uiStorageGrid.rowCount = 1;
            uiStorageGrid.colCount = 10;
            uiStorageGrid._OnInit();

            UpdateMaterials();

            uiStorageGrid.storage = _storageComponent;
            // copy the persisted grid items into array
            foreach (var persistedGridItem in _gridItems.Where(item => item != null))
            {
                _storageComponent.grids[persistedGridItem.Index] = new StorageComponent.GRID
                {
                    itemId = persistedGridItem.ItemId,
                    count = persistedGridItem.Count,
                    inc = persistedGridItem.ProliferatorPoints,
                    stackSize = ItemUtil.GetItemProto(persistedGridItem.ItemId).StackSize,
                };
                Log.Debug($"Imported item to recycle window {persistedGridItem}");
            }

            RecordStorageChange();

            uiStorageGrid.OnStorageDataChanged();
            uiStorageGrid.storage.onStorageChange += RecordStorageChange;

            var gridRect = uiStorageGrid.rectTrans;
            gridRect.anchorMin = new Vector2(0.5f, 1f);
            gridRect.anchorMax = new Vector2(0.5f, 1f);
            gridRect.pivot = new Vector2(0.5f, 1f);
            gridRect.anchoredPosition = new Vector2(0f, -(TitleHeight + HintHeight));

            var gridHeight = UIStorageGrid.kGridSize + 2 * UIStorageGrid.kPadding;
            panelRect.sizeDelta = new Vector2(0f, TitleHeight + HintHeight + gridHeight);

            AddShowRecycleCheck(inventoryWindow);

            panelGo.SetActive(true);
            gridGo.SetActive(true);
        }

        /// <summary>
        /// The cloned grid carries the live inventory grid's pooled per-cell count labels
        /// (<c>numTexts</c>); nulling that field reference alone leaves the actual label
        /// objects in the scene showing stale counts, so they're destroyed outright. The
        /// pooled label prefab (<c>prefabNumText</c>) itself is kept, since the grid clones it
        /// back out as cells fill.
        /// </summary>
        private static void StripClonedNumberTexts(GameObject gridGo, Text prefabNumText)
        {
            foreach (var text in gridGo.GetComponentsInChildren<Text>(true).Where(text => text != prefabNumText))
            {
                Destroy(text.gameObject);
            }
        }

        /// <summary>
        /// Sets the checkbox's sprite for the on/off state; when <see cref="texOn"/>/
        /// <see cref="texOff"/> weren't found and <see cref="sprOn"/>/<see cref="sprOff"/> are
        /// both null, the sprite alone can't show the state (it's null either way), so the
        /// colour carries it instead.
        /// </summary>
        private void SetCheckBoxState(bool on)
        {
            if (checkBoxImage == null)
                return;
            checkBoxImage.sprite = on ? sprOn : sprOff;
            checkBoxImage.color = on || sprOn != null ? CheckOnColour : CheckOffColour;
        }

        private void AddShowRecycleCheck(UIInventoryWindow inventoryWindow)
        {
            if (texOn != null && texOff != null)
            {
                sprOn = Sprite.Create(texOn, new Rect(0, 0, texOn.width, texOn.height), new Vector2(0.5f, 0.5f));
                sprOff = Sprite.Create(texOff, new Rect(0, 0, texOff.width, texOff.height), new Vector2(0.5f, 0.5f));
            }
            else
            {
                Log.Warn("Recycle window: checkbox textures not found under Resources, falling back to a plain coloured indicator");
            }

            // first shrink down inventory label and move up slightly
            var titleText = inventoryWindow.titleText;
            titleText.fontSize = 14;
            var titleTextRT = (RectTransform)titleText.transform;
            titleTextRT.anchoredPosition = new Vector2(titleTextRT.anchoredPosition.x, -8);

            // add checkbox
            chxGO = new GameObject("displayRecycleWindowCheck");

            RectTransform checkBoxRectTransform = chxGO.AddComponent<RectTransform>();
            checkBoxRectTransform.SetParent(inventoryWindow.windowTrans, false);

            checkBoxRectTransform.anchorMax = new Vector2(0, 1);
            checkBoxRectTransform.anchorMin = new Vector2(0, 1);
            checkBoxRectTransform.sizeDelta = new Vector2(15, 15);
            checkBoxRectTransform.pivot = new Vector2(0, 0.5f);
            checkBoxRectTransform.anchoredPosition = new Vector2(6, titleTextRT.anchoredPosition.y + 20);

            Button _btn = checkBoxRectTransform.gameObject.AddComponent<Button>();
            _btn.onClick.AddListener(() =>
            {
                if (_instanceGo != null)
                {
                    _instanceGo.SetActive(!_instanceGo.activeSelf);
                    SetCheckBoxState(_instanceGo.activeSelf);
                    if (_instanceGo.activeSelf)
                    {
                        // we just activated, make sure we've recorded all the things in our delay buffer
                        RecordStorageChange();
                    }
                }
            });
            checkBoxImage = _btn.gameObject.AddComponent<Image>();
            SetCheckBoxState(true);

            txtGO = new GameObject("displayRecycleWindowCheckText");
            var textRectTransform = txtGO.AddComponent<RectTransform>();

            textRectTransform.SetParent(chxGO.transform, false);

            textRectTransform.anchorMax = new Vector2(0, 1f);
            textRectTransform.anchorMin = new Vector2(0, 1f);
            textRectTransform.sizeDelta = new Vector2(100, 15);
            textRectTransform.pivot = new Vector2(0, 0.5f);
            textRectTransform.anchoredPosition = new Vector2(20, -5);

            Text text = textRectTransform.gameObject.AddComponent<Text>();
            text.text = "Show recycle section";
            text.fontStyle = FontStyle.Normal;
            text.fontSize = 11;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.color = new Color(0.8f, 0.8f, 0.8f, 1);
            Font fnt = Resources.Load<Font>("ui/fonts/SAIRASB");
            if (fnt != null)
                text.font = fnt;
        }

        private void UpdateMaterials()
        {
            uiStorageGrid.iconImage.texture = GameMain.iconSet.texture;
            if (stateArray == null)
            {
                stateArray = uiStorageGrid.stateArray ?? new uint[1024];

                stateBuffer = uiStorageGrid.stateBuffer ?? new ComputeBuffer(stateArray.Length, 4);

                iconIndexArray = uiStorageGrid.iconIndexArray ?? new uint[1024];

                iconIndexBuffer = uiStorageGrid.iconIndexBuffer ?? new ComputeBuffer(iconIndexArray.Length, 4);
            }

            var bgImage = uiStorageGrid.bgImage;
            if (bgImage != null)
            {
                uiStorageGrid.bgImageMat = ProtoRegistry.CreateMaterial("UI Ex/Storage Bg", "storage-bg", "#FFFFFFFF", null, new string[] { });
                uiStorageGrid.bgImageMat.SetBuffer(buffer, stateBuffer);
                bgImage.material = uiStorageGrid.bgImageMat;
            }

            var iconImage = uiStorageGrid.iconImage;
            if (iconImage != null)
            {
                uiStorageGrid.iconImageMat = ProtoRegistry.CreateMaterial("UI Ex/Storage Icons", "storage-icons", "#FFFFFFFF", null, new string[] { });
                uiStorageGrid.iconImageMat.SetBuffer(indexBuffer, iconIndexBuffer);
                iconImage.material = uiStorageGrid.iconImageMat;
            }
        }


        public void Unload()
        {
            if (uiStorageGrid != null)
            {
                uiStorageGrid.storage.onStorageChange -= RecordStorageChange;
                uiStorageGrid.bgImageMat = null;
                uiStorageGrid.bgImage = null;
                Destroy(uiStorageGrid.gameObject);
                uiStorageGrid = null;
            }

            if (txtGO != null)
                Destroy(txtGO);
            if (chxGO != null)
                Destroy(chxGO);
            if (_instanceGo != null)
            {
                Destroy(_instanceGo);
                _instanceGo = null;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIStorageGrid), "_OnOpen")]
        public static void UIStorageGrid__OnOpen_Postfix(UIStorageGrid __instance)
        {
            if (__instance.primary && _instance != null)
            {
                _instance._openRequested = true;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIStorageGrid), "_OnClose")]
        public static void UIStorageGrid__OnOClose_Postfix(UIStorageGrid __instance)
        {
            if (__instance.primary && _instance != null)
            {
                _instance._closeRequested = true;
            }
        }

        public static GridItem GetItemToRecycle()
        {
            if (_instance == null)
            {
                return null;
            }

            return _instance.GetItemToRecycleImpl();
        }

        private GridItem GetItemToRecycleImpl()
        {
            if (PluginConfig.IsPaused())
            {
                return null;
            }
            var poppedItem = _recycledItems.PopAvailableItem();
            if (poppedItem == null)
            {
                return null;
            }

            if (poppedItem is GridItem item)
            {
                if (!LogisticsNetwork.HasItem(item.ItemId))
                {
                    // put it back since we can't really do anything with it 
                    _recycledItems.AddItems(item);
                    return null;
                }
                return item;
            }

            throw new Exception($"Object is not null and not a griditem? wtf: {poppedItem}");
        }

        public static void RemoveFromStorage(GridItem gridItem)
        {
            if (_instance == null)
            {
                return;
            }

            _instance.RemoveFromStorageImpl(gridItem);
        }

        private void RemoveFromStorageImpl(GridItem gridItem)
        {
            if (uiStorageGrid == null || uiStorageGrid.storage == null)
                return;
            uiStorageGrid.storage.TakeItemFromGrid(gridItem.Index, ref gridItem.ItemId, ref gridItem.Count, out int inc);
        }

        public static void InitOnLoad()
        {
            // nothing to do
            _gridItems.Clear();
        }

        public static void Import(BinaryReader r)
        {
            _gridItems.Clear();
            try
            {
                var gridItemCount = r.ReadInt32();
                for (var i = 0; i < gridItemCount; i++)
                {
                    var gridItem = GridItem.Import(r);
                    _gridItems.Add(gridItem);
                    if (_instance != null)
                    {
                        _instance._recycledItems.AddItems(gridItem);
                    }
                }
                Log.Debug($"Imported {gridItemCount} recycled items");
            }
            catch (Exception e)
            {
                // most likely grid items were not persisted, don't worry too much
                Log.Debug($"Skipping recycle window import. {e.Message}");
            }
        }

        public static void Export(BinaryWriter w)
        {
            if (_instance == null || _instance.uiStorageGrid == null)
            {
                Log.Warn($"somehow instance of recycle window is null for export");
                w.Write(0);
                return;
            }
            var items = _instance.uiStorageGrid.storage;
            var itemsToExport = new List<GridItem>();
            for (var index = 0; index < items.size; ++index)
            {
                var itemId = items.grids[index].itemId;
                if (itemId == 0)
                {
                    continue;
                }

                var count = items.grids[index].count;
                if (count < 1)
                {
                    continue;
                }

                var inc = items.grids[index].inc;

                itemsToExport.Add(GridItem.From(index, itemId, count, inc));
            }
            Log.Debug($"Exporting {itemsToExport.Count} items in recycle area");
            w.Write(itemsToExport.Count);
            foreach (var gridItem in itemsToExport)
            {
                gridItem.Export(w);
            }
        }
#if DEBUG
        public static void AddItemForTest(GridItem gi)
        {
            _gridItems.Add(gi);
        }  
#endif        
    }

    public class RecycleWindowPersistence : InstanceSerializer
    {
        private readonly PlogPlayerId playerId;

        public RecycleWindowPersistence(PlogPlayerId playerId)
        {
            this.playerId = playerId;
        }

        public override void ExportData(BinaryWriter w)
        {
            RecycleWindow.Export(w);
        }

        public override void ImportData(BinaryReader reader)
        {
            RecycleWindow.Import(reader);
        }

        public override PlogPlayerId GetPlayerId() => playerId;

        public override string GetExportSectionId() => "RW";

        public override void InitOnLoad()
        {
            RecycleWindow.InitOnLoad();           
        }

        public override string SummarizeState() => "RW: NA";
    }
}