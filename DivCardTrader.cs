using DivCardTrader.Utils;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using ExileCore.Shared.Helpers;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading;
using System.Windows.Forms;
using Vector2 = System.Numerics.Vector2;

namespace DivCardTrader;

public class DivCardTrader : BaseSettingsPlugin<DivCardTraderSettings>
{
    private SyncTask<bool> _currentOperation;
    private bool MoveCancellationRequested => Settings.CancelWithRightMouseButton && (Control.MouseButtons & MouseButtons.Right) != 0;
    private SharpDX.Vector2 WindowOffset => GameController.Window.GetWindowRectangleTimeCache.TopLeft;

    public override bool Initialise()
    {
        Name = "DivCardTrader";

        Input.RegisterKey(Settings.RunKey);
        Settings.RunKey.OnValueChanged += () => { Input.RegisterKey(Settings.RunKey); };

        return base.Initialise();
    }

    public override void AreaChange(AreaInstance area)
    {
        //Perform once-per-zone processing here
        //For example, Radar builds the zone map texture here
    }

    public override Job Tick()
    {
        return null;
    }

    public override void Render()
    {
        if (_currentOperation != null)
        {
            if (Settings.DebugMode)
                DebugWindow.LogMsg("Running the DivCardTrading...");
            TaskUtils.RunOrRestart(ref _currentOperation, () => null);
            return;
        }


        if (!Settings.Enable)
        {
            return;
        }

        if (!GameController.Area.CurrentArea.IsHideout)
        {
            return;
        }


        if (Settings.RunKey.PressedOnce())
        {
            var uiOpen = GameController.IngameState.IngameUi.CardTradeWindow.IsVisible &&
                GameController.Game.IngameState.IngameUi.InventoryPanel.IsVisible;
            if (!uiOpen)
            {
                DebugWindow.LogMsg("CardTradewindow and Inventory Must be Open!", 5);
                return;
            }

            var inventoryItems = GameController.IngameState.ServerData.PlayerInventories[0].Inventory.InventorySlotItems
                .Where(x => GameController.Files.BaseItemTypes.Translate(x.Item.Path)?.ClassName == "DivinationCard" && x.Item.GetComponent<Stack>().FullStack)
                .OrderBy(x => x.PosX)
                .ThenBy(x => x.PosY)
                .ToList();

            _currentOperation = TradeDivCards(inventoryItems);
        }
    }

    private async SyncTask<bool> TradeDivCards(List<ServerInventory.InventSlotItem> items)
    {
        var prevMousePos = Mouse.GetCursorPosition();
        foreach (var item in items)
        {
            if (MoveCancellationRequested)
            {
                return false;
            }

            if (!GameController.Game.IngameState.IngameUi.InventoryPanel.IsVisible)
            {
                DebugWindow.LogMsg("DivCardTrader: Inventory Panel closed, aborting loop", 3);
                break;
            }

            if (!GameController.IngameState.IngameUi.CardTradeWindow.IsVisible)
            {
                DebugWindow.LogMsg("DivCardTrader: Card Trader Inventory closed, aborting loop", 3);
                break;
            }

            if (IsInventoryFull())
            {
                DebugWindow.LogMsg("DivCardTrader: Inventory full, aborting loop", 3);
                break;
            }

            if (GameController.IngameState.IngameUi.CardTradeWindow.CardSlotItem != null)
            {
                if (Settings.DebugMode)
                    DebugWindow.LogMsg("CardTradeWindow not empty! Removing first.", 5);

                await RemoveItem(null);
            }

            await MoveItem(item.GetClientRect().ClickRandom());

            await TradeItem();

            await RemoveItem(item.Item);
        }

        Mouse.moveMouse(prevMousePos);
        await Wait(MouseMoveDelay, true);
        return true;
    }

    private static readonly TimeSpan KeyDelay = TimeSpan.FromMilliseconds(10);
    private static readonly TimeSpan MouseMoveDelay = TimeSpan.FromMilliseconds(20);
    private TimeSpan MouseDownDelay => TimeSpan.FromMilliseconds(5 + Settings.ExtraDelay.Value);
    private static readonly TimeSpan MouseUpDelay = TimeSpan.FromMilliseconds(5);

    private async SyncTask<bool> MoveItem(SharpDX.Vector2 itemPosition)
    {
        itemPosition += WindowOffset;

        Keyboard.KeyDown(Keys.LControlKey);
        await Wait(KeyDelay, true);
        Mouse.moveMouse(itemPosition);
        await Wait(MouseMoveDelay, true);
        Mouse.LeftDown();
        await Wait(MouseDownDelay, true);
        Mouse.LeftUp();
        await Wait(MouseUpDelay, true);
        Keyboard.KeyUp(Keys.LControlKey);
        await Wait(KeyDelay, false);

        return true;
    }

    private async SyncTask<bool> TradeItem()
    {
        var retry = 0;
        do
        {
            if (GameController.IngameState.IngameUi.CardTradeWindow.CardSlotItem != null)
            {
                break;
            }

            await Wait(TimeSpan.FromMilliseconds(5 + Settings.ExtraDelay.Value), true);
            retry++;

            if (Settings.DebugMode)
                DebugWindow.LogMsg($"Retrying Trade Item: {retry}", 5);

        } while (retry < 10);

        Mouse.moveMouse(GameController.IngameState.IngameUi.CardTradeWindow.TradeButton.GetClientRect().ClickRandom() + WindowOffset);
        await Wait(MouseMoveDelay, true);
        Mouse.LeftDown();
        await Wait(MouseDownDelay, true);
        Mouse.LeftUp();
        await Wait(MouseUpDelay, true);

        return true;
    }

    private async SyncTask<bool> RemoveItem(Entity item)
    {
        var retry = 0;
        do
        {
            var cardSlotItem = GameController.IngameState.IngameUi.CardTradeWindow.CardSlotItem;

            if (cardSlotItem != null && 
                (item == null || 
                cardSlotItem.Item.Path != item.Path || 
                !cardSlotItem.Item.GetComponent<Stack>().FullStack))
            {
                await MoveItem(cardSlotItem.GetClientRect().ClickRandom());
                return true;
            }

            await Wait(TimeSpan.FromMilliseconds(5 + Settings.ExtraDelay.Value), true);
            retry++;

            if (Settings.DebugMode)
                DebugWindow.LogMsg($"Retrying Remove Item: {retry}", 5);

        } while (retry < 10);

        DebugWindow.LogError("Failed to remove item from trade window!", 5);
        return false;
    }

    private async SyncTask<bool> Wait(TimeSpan period, bool canUseThreadSleep)
    {
        if (canUseThreadSleep && Settings.UseThreadSleep)
        {
            Thread.Sleep(period);
            return true;
        }

        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < period)
        {
            await TaskUtils.NextFrame();
        }

        return true;
    }

    private bool IsInventoryFull()
    {
        var inventoryItems = GameController.IngameState.ServerData.PlayerInventories[0].Inventory.InventorySlotItems;

        // quick sanity check
        if (inventoryItems.Count < 12)
        {
            return false;
        }

        // track each inventory slot
        bool[,] inventorySlot = new bool[12, 5];

        // iterate through each item in the inventory and mark used slots
        foreach (var inventoryItem in inventoryItems)
        {
            int x = inventoryItem.PosX;
            int y = inventoryItem.PosY;
            int height = inventoryItem.SizeY;
            int width = inventoryItem.SizeX;
            for (int row = x; row < x + width; row++)
            {
                for (int col = y; col < y + height; col++)
                {
                    inventorySlot[row, col] = true;
                }
            }
        }

        // check for any empty slots
        for (int x = 0; x < 12; x++)
        {
            for (int y = 0; y < 5; y++)
            {
                if (inventorySlot[x, y] == false)
                {
                    return false;
                }
            }
        }

        // no empty slots, so inventory is full
        return true;
    }
}