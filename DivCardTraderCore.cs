using ExileCore;
using ExileCore.Shared;
using ImGuiNET;
using System.Collections;
using System.Windows.Forms;
using ExileCore.PoEMemory.Elements.InventoryElements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using SharpDX;

#pragma warning disable CA1416 // Validate platform compatibility
namespace DivCardTrader
{
    public class DivCardTraderCore : BaseSettingsPlugin<Settings>
    {
        #region Private Variables
        private const string CoroutineName = "DivCardTraderCoroutine";
        private Coroutine _coroutineWorker;
        private ItemRepo _fullStackDiv;
        private Vector2 _clientOffset;
        private Inventory _inventory;
        private int _delay;
        private int _latency;
        private int _maxWaitTime;

        #endregion

        #region Public Methods
        public override bool Initialise()
        {
            Name = "DivCardTrader";

            Input.RegisterKey(Settings.RunKey);
            Settings.RunKey.OnValueChanged += () => { Input.RegisterKey(Settings.RunKey); };
            return base.Initialise();
        }

        public override void DrawSettings()
        {
            ImGui.Text("Plugin by Zyeesi");
            ImGui.Separator();

            base.DrawSettings();
        }

        public override void Render()
        {
            if (!GameController.Area.CurrentArea.IsHideout)
            {
                return;
            }
            
            bool uiOpen = GameController.IngameState.IngameUi.CardTradeWindow.IsVisible &&
                          GameController.Game.IngameState.IngameUi.InventoryPanel.IsVisible;
            _clientOffset = GameController.Window.GetWindowRectangle().TopLeft;
            _inventory = GameController.Game.IngameState.IngameUi.InventoryPanel[InventoryIndex.PlayerInventory];
            _delay = Settings.ExtraDelay.Value;
            _latency = GameController.Game.IngameState.ServerData.Latency;
            _maxWaitTime = _delay * 100;

            // TODO: Add current trade render, maybe, prob wouldn't bother

            if (_coroutineWorker != null && _coroutineWorker.IsDone)
            {
                Input.KeyUp(Keys.LControlKey);
                _coroutineWorker = null;
            }

            if (!uiOpen && _coroutineWorker != null && !_coroutineWorker.IsDone)
            {
                Input.KeyUp(Keys.LControlKey);
                _coroutineWorker?.Done();
                LogError($"UI was closed! Stopping...", 5);
            }

            if (Settings.RunKey.PressedOnce())
            {
                if (_coroutineWorker != null && _coroutineWorker.Running)
                {
                    Input.KeyUp(Keys.LControlKey);
                    _coroutineWorker?.Done();
                    LogMessage("Div Trading Stopping...", 5);
                }
                else
                {
                    _coroutineWorker = new Coroutine(ProcessDivCards(), this, CoroutineName);
                    Core.ParallelRunner.Run(_coroutineWorker);
                }
            }
        }
        #endregion

        #region Private Methods

        private IEnumerator ProcessDivCards()
        {
            do
            {
                yield return ParseInventory();
                yield return new WaitTime(_latency);
                if (_fullStackDiv.Total > 0)
                {
                    yield return TradeDivCard();
                    yield return new WaitTime(_latency);
                }

            } while (_fullStackDiv.Total > 0); // TODO: Add inventory full check
            _coroutineWorker?.Done();
        }

        private IEnumerator ParseInventory()
        {
            var invItems = _inventory.VisibleInventoryItems;
            if (invItems == null)
            {
                LogError("Empty Inventory!", 5);
                _coroutineWorker?.Done();

                yield break;
            }
            else
            {
                _fullStackDiv = new ItemRepo();
                foreach (var invItem in invItems)
                {
                    if (invItem.Item == null || invItem.Address == 0)
                    {
                        continue;
                    }
                    var itemType = GameController.Files.BaseItemTypes.Translate(invItem.Item.Path);
                    var tempItem = new Item(invItem, itemType);
                    if (tempItem.ClassName.Equals("DivinationCard") && tempItem.FullStack)
                    {
                        _fullStackDiv.AddItem(tempItem);
                        break;
                    }
                }
            }
        }

        private IEnumerator TradeDivCard()
        {
            var cardTradeWin = GameController.IngameState.IngameUi.CardTradeWindow;
            if (cardTradeWin.CardSlotItem == null)
            {
                var invCount = _inventory.VisibleInventoryItems.Count;
                Input.KeyDown(Keys.LControlKey);
                yield return Input.SetCursorPositionAndClick(_fullStackDiv.Items.First().ClientRect + _clientOffset, MouseButtons.Left, _latency + _delay);
                yield return new WaitFunctionTimed(() => CheckDivMovedIn(_fullStackDiv.Items.First().InventoryItem, invCount), true, _maxWaitTime);
                if (!CheckDivMovedIn(_fullStackDiv.Items.First().InventoryItem, invCount))
                {
                    LogError($"Error Occurred Moving Div card! ({(_fullStackDiv.Items.First().ClientRect + _clientOffset).X}, {(_fullStackDiv.Items.First().ClientRect + _clientOffset).Y})", 5);
                    _coroutineWorker?.Done();
                    yield break;
                }
                Input.KeyUp(Keys.LControlKey);
            }
            var itemType = GameController.Files.BaseItemTypes.Translate(cardTradeWin.CardSlotItem.Item.Path);
            var tempItem = new Item(cardTradeWin.CardSlotItem, itemType);

            if (tempItem.ClassName.Equals("DivinationCard") && tempItem.FullStack)
            {
                var itemInTradeWin = cardTradeWin.CardSlotItem;
                yield return Input.SetCursorPositionAndClick(cardTradeWin.TradeButton.GetClientRect().Center + _clientOffset, MouseButtons.Left, _latency + _delay);
                yield return new WaitFunctionTimed(() => CheckDivTraded(itemInTradeWin), true, _maxWaitTime);
                if (!CheckDivTraded(itemInTradeWin))
                {
                    LogError($"Error Clicking Trade Button!", 5);
                    _coroutineWorker?.Done();
                    yield break;
                }
            }
            
            Input.KeyDown(Keys.LControlKey);
            yield return Input.SetCursorPositionAndClick(cardTradeWin.CardSlotItem.GetClientRect().Center + _clientOffset, MouseButtons.Left, _latency + _delay);
            yield return new WaitFunctionTimed(() => CheckDivMovedOut(), true, 100); // TODO: Inv full check etc
            if (!CheckDivMovedOut())
            {
                LogMessage("Inventory Full or Error Occurred!", 5);
                _coroutineWorker?.Done();
            }
            Input.KeyUp(Keys.LControlKey);
        }

        private bool CheckDivMovedIn(NormalInventoryItem item, int previousCount)
        {
            var currentCount = _inventory.VisibleInventoryItems.Count;
            var cardTradeItemName = GameController.Files.BaseItemTypes.Translate(GameController.IngameState.IngameUi.CardTradeWindow.CardSlotItem.Item.Path);
            var passedInItemName = GameController.Files.BaseItemTypes.Translate(item.Item.Path);
            return (cardTradeItemName.Equals(passedInItemName) && currentCount < previousCount);
        }

        private bool CheckDivTraded(NormalInventoryItem item)
        {
            var cardTradeWin = GameController.IngameState.IngameUi.CardTradeWindow;
            var itemInTradeWin = cardTradeWin.CardSlotItem;
            return itemInTradeWin.Item.Address != item.Item.Address;
        }

        private bool CheckDivMovedOut()
        {
            var cardTrade = GameController.IngameState.IngameUi.CardTradeWindow.CardSlotItem;
            return cardTrade == null;
        }
        #endregion
    }
}
#pragma warning restore CA1416 // Validate platform compatibility
