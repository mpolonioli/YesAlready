using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using System.Linq;

namespace YesAlready.Features;

[AddonFeature(AddonEvent.PostRefresh)]
[Bother(nameof(Configuration.MiragePrismPrismSetConvert), BotherCategory.Glamour, "Automatically store glamours. Only activates if you have all pieces.")]
[Bother(nameof(Configuration.AllowPartialFilling), BotherCategory.Glamour, "Automatically store glamours regardless of having all the pieces.",
    label: "Allow partial fills",
    ContributesToEnable = false,
    RequiresEnabledProperty = nameof(Configuration.MiragePrismPrismSetConvert))]
public class MiragePrismPrismSetConvert : AddonFeature
{
    private enum ItemFlag : uint
    {
        Missing = 0,
        Unfilled = 2,
        Filled = 3,
        AlreadyInOutfit = 6,
    }

    protected override unsafe void HandleAddonEvent(AddonEvent eventType, AddonArgs addonInfo)
    {
        var addon = addonInfo.GetAddon<AddonMiragePrismPrismSetConvert>();
        if (!addon->AtkUnitBase.IsAddonReady() || addon->AtkValues == null) return;

        if (addon->AlreadyInDresserText != null && addon->AlreadyInDresserText->AtkResNode.IsVisible())
        {
            Svc.Chat.PrintPluginMessage($"Outfit already in dresser");
            return;
        }

        var itemCount = (int)AgentMiragePrismPrismSetConvert.Instance()->Data->NumItemsInSet;
        var flags = new ItemFlag[itemCount];
        var iconIds = new uint[itemCount];
        for (var i = 0; i < itemCount; i++)
        {
            flags[i] = (ItemFlag)addon->TypedAtkValues->Items[i].Flag.UInt;
            iconIds[i] = addon->TypedAtkValues->Items[i].ItemIconId.UInt;
        }

        if (flags.Any(f => f == ItemFlag.Missing) && !C.AllowPartialFilling)
            return;

        var toFill = Enumerable.Range(0, itemCount).Where(i => flags[i] == ItemFlag.Unfilled).ToArray();

        if (toFill.Length == 0 && flags.Any(f => f == ItemFlag.Missing))
            return;
        if (toFill.Length == 0 && flags.None(f => f is ItemFlag.Filled or ItemFlag.AlreadyInOutfit))
            return;

        if (Service.TaskManager.IsBusy) return;

        foreach (var s in toFill)
        {
            var iconId = iconIds[s];
            Service.TaskManager.Enqueue(() => TryHandOver(addon, s, iconId), $"HandInSlot{s}");
            Service.TaskManager.Enqueue(() => addon->AtkValues != null && (ItemFlag)addon->TypedAtkValues->Items[s].Flag.UInt is ItemFlag.Filled or ItemFlag.AlreadyInOutfit);
        }

        Service.TaskManager.Enqueue(() =>
        {
            if (addon->AtkValues == null) return false;

            var count = (int)AgentMiragePrismPrismSetConvert.Instance()->Data->NumItemsInSet;
            for (var i = 0; i < count; i++)
                if ((ItemFlag)addon->TypedAtkValues->Items[i].Flag.UInt == ItemFlag.Unfilled)
                    return false; // more slots to fill

            var btn = addon->StoreAsGlamourButton;
            if (btn == null || !btn->IsEnabled) return false;
            btn->Click();
            return true;
        });
    }

    private static unsafe bool? TryHandOver(AddonMiragePrismPrismSetConvert* addon, int slot, uint itemIconId)
    {
        if (addon->AtkValues == null) return false;

        var flag = (ItemFlag)addon->TypedAtkValues->Items[slot].Flag.UInt;
        if (flag is ItemFlag.Filled or ItemFlag.AlreadyInOutfit)
            return true;

        var contextMenu = (AtkUnitBase*)Svc.GameGui.GetAddonByName("ContextIconMenu", 1).Address;
        if (contextMenu is null || !contextMenu->IsVisible)
        {
            Callback.Fire((AtkUnitBase*)addon, true, 13, slot);
            return false;
        }

        Callback.Fire(contextMenu, true, 0, 0, itemIconId, 0u, 0);
        PluginLog.Debug($"Filled slot {slot}");
        return true;
    }
}
