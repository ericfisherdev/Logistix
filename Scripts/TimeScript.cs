using System;
using System.Collections.Generic;
using Logistix.Logistics;
using Logistix.Model;
using Logistix.UI;
using Logistix.Util;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace Logistix.Scripts
{
    /// <summary>
    /// Renders the incoming-items status list as pooled <see cref="IncomingItemRow"/>s instead
    /// of a single TMPro block with inline &lt;sprite&gt; tags. DSP 0.10.34 ships no
    /// TextMeshPro, and legacy Text has no sprite tag, so each row carries its own item-icon
    /// Image alongside the translated message. See #1/#6/#15.
    /// </summary>
    public class TimeScript : MonoBehaviour
    {
        private const float RowSpacing = 2f;
        private const float RootOffsetX = 20f;
        private const float RootOffsetY = -160f;

        private readonly List<IncomingItemRow> _rows = new();
        private Text _rowTextTemplate;
        private int _visibleRowCount;

        private bool _runOnce;
        private static readonly Dictionary<string, DateTime> _lastFailureMessageTime = new();
        private static readonly Dictionary<string, DateTime> _itemNameFirstShownFailureMessageTime = new();
        private int _loadFailureReadmeReferenceMentionedCountDown = 5;

        private void Awake()
        {
            var rectTransform = (RectTransform)transform;
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.anchoredPosition = new Vector2(RootOffsetX, RootOffsetY);

            var layoutGroup = gameObject.AddComponent<VerticalLayoutGroup>();
            layoutGroup.spacing = RowSpacing;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;
            layoutGroup.childForceExpandWidth = false;
            layoutGroup.childForceExpandHeight = false;

            _rowTextTemplate = UIRoot.instance.uiGame.inventoryWindow.titleText;
            if (_rowTextTemplate == null)
            {
                Log.Warn("TimeScript could not find a donor Text (uiGame.inventoryWindow.titleText); incoming item status will not render");
            }
        }

        private void Update()
        {
            if (_rowTextTemplate == null)
                return;

            if (!LogisticsNetwork.IsInitted)
                return;

            if (Time.frameCount % 60 == 0 || !_runOnce)
            {
                _runOnce = true;
                if (!PluginConfig.IsPaused() && PluginConfig.showIncomingItemProgress.Value)
                {
                    UpdateIncomingItems();
                }
                else
                {
                    gameObject.SetActive(false);
                }
            }

            if (GameUtil.HideUiElements() || PluginConfig.IsPaused())
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(PluginConfig.showIncomingItemProgress.Value && _visibleRowCount > 0);
        }

        private void UpdateIncomingItems()
        {
            try
            {
                var itemLoadStates = ItemLoadState.GetLoadState(PluginConfig.timeScriptPositionTestEnabled.Value);
                if (itemLoadStates == null)
                {
                    return;
                }

                var rowCount = 0;
                foreach (var loadState in itemLoadStates)
                {
                    string message;
                    try
                    {
                        message = FormatLoadingStatusMessage(loadState);
                    }
                    catch (Exception e)
                    {
                        Log.Warn($"Messed up placeholders in translation. {e.Message}");
                        message = loadState.ToString();
                    }

                    if (string.IsNullOrWhiteSpace(message))
                        continue;

                    GetOrCreateRow(rowCount).Show(loadState.itemId, message);
                    rowCount++;

                    if (rowCount > GetMaxLineCount())
                        break;
                }

                for (var i = rowCount; i < _rows.Count; i++)
                {
                    _rows[i].Hide();
                }

                _visibleRowCount = rowCount;
            }
            catch (Exception e)
            {
                Log.Warn($"failure while updating incoming items {e.Message} {e.StackTrace}");
            }
        }

        private IncomingItemRow GetOrCreateRow(int index)
        {
            if (index < _rows.Count)
            {
                return _rows[index];
            }

            var row = IncomingItemRow.Create(transform, _rowTextTemplate);
            _rows.Add(row);
            return row;
        }

        private int GetMaxLineCount()
        {
            // at 1920x1080 we start going into minimap after about 10 lines
            // return Math.Min(UiScaler.ScaleToDefault(10, false), 15);
            return int.MaxValue;
        }

        private string FormatLoadingStatusMessage(ItemLoadState loadState)
        {
            switch (loadState.requestState)
            {
                case RequestState.InventoryUpdated:
                case RequestState.Complete:
                {
                    Log.Warn($"TimeScript request state is invalid {loadState.requestState}");
                    return null;
                }
                case RequestState.Created:
                {
                    return string.Format("PLOGTaskCreated".Translate(), loadState.itemName, loadState.count);
                }
                case RequestState.Failed:
                {
                    if (PluginConfig.hideIncomingItemFailures.Value)
                    {
                        return null;
                    }

                    var result = string.Format("PLOGTaskFailed".Translate(), loadState.itemName);

                    if (!_itemNameFirstShownFailureMessageTime.TryGetValue(loadState.itemName, out var firstTime))
                        _itemNameFirstShownFailureMessageTime[result] = DateTime.Now;

                    _lastFailureMessageTime[result] = DateTime.Now + TimeSpan.FromSeconds(Random.RandomRangeInt(10, 65));
                    if (_loadFailureReadmeReferenceMentionedCountDown-- > 0)
                    {
                        return $"{result} (see readme for more about what this message means)";
                    }

                    return result;
                }
                case RequestState.ReadyForInventoryUpdate:
                {
                    _itemNameFirstShownFailureMessageTime.Remove(loadState.itemName);
                    return string.Format("PLOGLoadingFromBuffer".Translate(), loadState.itemName, loadState.count);
                }
                case RequestState.WaitingForShipping:
                {
                    var shippingAmount = Math.Max(loadState.cost?.shippingToBufferCount ?? loadState.count, loadState.count);
                    _itemNameFirstShownFailureMessageTime.Remove(loadState.itemName);
                    if (loadState.cost?.paid != null && loadState.cost.paid)
                    {
                        var etaStr = TimeUtil.FormatEta(Math.Max(loadState.secondsRemaining, 0.8f));
                        return string.Format("PLOGTaskWaitingForShipping".Translate(), loadState.itemName, shippingAmount, etaStr);
                    }

                    if (loadState.cost == null)
                    {
                        Log.Warn($"missed setting cost for waiting for shipping");
                        var etaStr = TimeUtil.FormatEta(loadState.secondsRemaining);
                        return string.Format("PLOGTaskWaitingForShipping".Translate(), loadState.itemName, shippingAmount, etaStr);
                    }

                    var stationInfo = LogisticsNetwork.FindStation(loadState.cost.stationGid, loadState.cost.planetId, loadState.cost.stationId);
                    var planetName = stationInfo?.PlanetName ?? "Unknown";
                    var stationType = stationInfo?.StationType.ToString() ?? "Unknown";
                    if (planetName == "Unknown" || stationType == "Unknown")
                    {
                        Log.Warn($"Failed to get station info from cost: {loadState.cost.planetId}, {loadState.cost.stationId} {stationInfo?.StationId}");
                    }

                    if (loadState.cost.processingPassesCompleted < 3)
                    {
                        // shipping isn't delayed (yet), it's just that the hasn't been checked
                        return string.Format("PLOGShippingCostProcessing".Translate(), loadState.itemName, shippingAmount);
                    }

                    if (loadState.cost.needWarper)
                    {
                        return string.Format("PLOGShippingDelayedWarper".Translate(), loadState.itemName, planetName, stationType);
                    }

                    return string.Format("PLOGShippingDelayedEnergy".Translate(), loadState.itemName, planetName, stationType);
                }
                default:
                {
                    Log.Warn($"Unexpected state: {loadState.requestState} in timescript");
                    return null;
                }
            }
        }
    }
}
