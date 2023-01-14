using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace SpeakingWay;

using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Logging;
using Dalamud.Utility;
using Lumina.Excel.GeneratedSheets;
using Newtonsoft.Json;

public class PayloadSpec
{
    public static IEnumerable<Payload> StatusArrowPayloads =
        new List<Payload>(new Payload[]
        {
            new UIForegroundPayload(517),
            new TextPayload("\uE05C"),
            UIForegroundPayload.UIForegroundOff,
        });

    [JsonProperty("text")] public string? Text;

    [JsonProperty("raw")] public byte[] Raw = Array.Empty<byte>();

    [JsonProperty("ui_foreground")] public ushort? UiForeground;

    [JsonProperty("ui_foreground_off")] public bool? UiForegroundOff;

    [JsonProperty("ui_glow")] public ushort? UiGlow;

    [JsonProperty("ui_glow_off")] public bool? UiGlowOff;

    [JsonProperty("icon")] public BitmapFontIcon? Icon;

    [JsonProperty("italics")] public bool? Italics;

    [JsonProperty("italics_on")] public bool? ItalicsOn;

    [JsonProperty("italics_off")] public bool? ItalicsOff;

    [JsonProperty("item_id")] public uint? ItemId;

    [JsonProperty("item_name")] public string? ItemName;

    [JsonProperty("item_kind")] public ItemPayload.ItemKind? ItemKind;

    [JsonProperty("territory_type_id")] public uint? TerritoryTypeId;
    [JsonProperty("territory_map_id")] public uint? MapId;

    [JsonProperty("map_raw_x")] public uint? MapRawX;
    [JsonProperty("map_raw_y")] public uint? MapRawY;

    [JsonProperty("map_nice_x")] public float? MapNiceX;
    [JsonProperty("map_nice_y")] public float? MapNiceY;
    [JsonProperty("map_fudge_factor")] public float? MapFudgeFactor;

    [JsonProperty("quest_id")] public uint? QuestId;
    [JsonProperty("status_id")] public uint? StatusId;

    private bool TryGetItem(SpeakingWayPlugin plugin, [NotNullWhen(true)] out Item? item)
    {
        if (this.ItemId is not null)
        {
            var tmp = plugin.DataManager.GetExcelSheet<Item>()?.GetRow(this.ItemId.Value);
            if (tmp is not null)
            {
                item = tmp;
                return true;
            }
        }

        if (this.ItemName is not null)
        {
            var tmp = item = plugin
                .DataManager
                .GetExcelSheet<Item>()?
                .FirstOrDefault(itemField =>
                    itemField.Name.ToString().Equals(this.ItemName, StringComparison.CurrentCultureIgnoreCase));

            if (tmp is not null)
            {
                item = tmp;
                return true;
            }
        }

        item = null;
        return false;
    }

    private bool TryGetTerritory(SpeakingWayPlugin plugin, [NotNullWhen(true)] out TerritoryType? territory)
    {
        if (this.TerritoryTypeId is not null)
        {
            var tmp = plugin.DataManager.GetExcelSheet<TerritoryType>()?.GetRow(this.TerritoryTypeId.Value);
            if (tmp is not null)
            {
                territory = tmp;
                return true;
            }
        }

        territory = null;
        return false;
    }

    private bool TryGetQuest(SpeakingWayPlugin plugin, [NotNullWhen(true)] out Quest? quest)
    {
        if (this.QuestId is not null)
        {
            var tmp = plugin.DataManager.GetExcelSheet<Quest>()?.GetRow(this.QuestId.Value);
            if (tmp is not null)
            {
                quest = tmp;
                return true;
            }
        }

        quest = null;
        return false;
    }

    private bool TryGetStatus(SpeakingWayPlugin plugin, [NotNullWhen(true)] out Status? quest)
    {
        if (this.StatusId is not null)
        {
            var tmp = plugin.DataManager.GetExcelSheet<Status>()?.GetRow(this.StatusId.Value);
            if (tmp is not null)
            {
                quest = tmp;
                return true;
            }
        }

        quest = null;
        return false;
    }

    public virtual IEnumerable<Payload> AsPayloads(SpeakingWayPlugin plugin)
    {
        var list = new List<Payload>();
        var text = this.Text == null ? null : (SeString) this.Text;
        var uiForeground = this.UiForeground;
        var uiGlow = this.UiGlow;
        var uiForegroundOff = this.UiForegroundOff;
        var uiGlowOff = this.UiGlowOff;
        var hasLink = false;

        if (this.Raw.Length > 0)
            list.AddRange(SeString.Parse(this.Raw).Payloads);

        if (this.TryGetItem(plugin, out var item))
        {
            var dalamudName = item.Name.ToDalamudString();

            // ReSharper disable once ConvertIfStatementToSwitchStatement
            if (this.ItemKind == ItemPayload.ItemKind.Collectible)
                dalamudName.Append(" \ue03d");
            else if (this.ItemKind == ItemPayload.ItemKind.Hq)
                dalamudName.Append(" \ue03c");

            text ??= dalamudName;

            hasLink = true;
            list.Add(new ItemPayload(item.RowId, this.ItemKind ?? ItemPayload.ItemKind.Normal));
            list.AddRange(SeString.TextArrowPayloads);

            switch (item.Rarity)
            {
                case 1:
                    uiForeground ??= 549;
                    uiGlow ??= 550;
                    break;
                case 2:
                    uiForeground ??= 551;
                    uiGlow ??= 552;
                    break;
                case 3:
                    uiForeground ??= 553;
                    uiGlow ??= 554;
                    break;

                default:
                    PluginLog.Log($"Unknown rarity {item.Rarity.ToString()}");

                    uiForeground ??= 549;
                    uiGlow ??= 550;
                    break;
            }

            uiForegroundOff ??= true;
            uiGlowOff ??= true;
        }

        if (this.TryGetTerritory(plugin, out var territory))
        {
            var dalamudName = territory.PlaceName.Value?.Name.ToDalamudString();
            text ??= dalamudName;

            hasLink = true;
            if (this.MapRawX is not null && this.MapRawY is not null)
            {
                list.Add(new MapLinkPayload(territory.RowId, territory.Map.Row, this.MapRawX.Value,
                    this.MapRawY.Value));
            }
            else if (this.MapNiceX is not null && this.MapNiceY is not null)
            {
                list.Add(new MapLinkPayload(territory.RowId, territory.Map.Row, this.MapNiceX.Value,
                    this.MapNiceY.Value, this.MapFudgeFactor ?? 0.05f));
            }
            else
            {
                list.Add(new MapLinkPayload(territory.RowId, territory.Map.Row, 0, 0));
            }

            list.AddRange(SeString.TextArrowPayloads);
        }

        if (this.TryGetQuest(plugin, out var quest))
        {
            var dalamudName = quest.Name.ToDalamudString();
            text ??= dalamudName;

            hasLink = true;
            list.Add(new QuestPayload(quest.RowId));
            list.AddRange(SeString.TextArrowPayloads);
        }

        if (this.TryGetStatus(plugin, out var status))
        {
            var dalamudName = status.Name.ToDalamudString();
            text ??= dalamudName;

            hasLink = true;
            list.Add(new StatusPayload(status.RowId));
            list.AddRange(SeString.TextArrowPayloads);
            list.AddRange(StatusArrowPayloads);
        }

        if (uiForeground is not null)
            list.Add(new UIForegroundPayload(uiForeground.Value));

        if (uiGlow is not null)
            list.Add(new UIGlowPayload(uiGlow.Value));

        if (this.ItalicsOn == true)
            list.Add(EmphasisItalicPayload.ItalicsOn);

        if (text is not null)
        {
            if (this.Italics == true)
                list.Add(EmphasisItalicPayload.ItalicsOn);

            list.AddRange(text.Payloads);

            if (this.Italics == true)
                list.Add(EmphasisItalicPayload.ItalicsOff);
        }

        if (this.Icon is not null)
            list.Add(new IconPayload(this.Icon.Value));

        if (uiForegroundOff == true)
            list.Add(UIForegroundPayload.UIForegroundOff);

        if (uiGlowOff == true)
            list.Add(UIGlowPayload.UIGlowOff);

        if (this.ItalicsOff == true)
            list.Add(EmphasisItalicPayload.ItalicsOff);

        if (hasLink)
            list.Add(RawPayload.LinkTerminator);

        return list;
    }
}