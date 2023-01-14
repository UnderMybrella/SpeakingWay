using System.Runtime.InteropServices;
using Dalamud.Game.Text;

namespace SpeakingWay
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using Dalamud.Data;
    using Dalamud.Game;
    using Dalamud.Game.ClientState;
    using Dalamud.Game.Command;
    using Dalamud.Game.Gui;
    using Dalamud.Game.Text.SeStringHandling;
    using Dalamud.Game.Text.SeStringHandling.Payloads;
    using Dalamud.Hooking;
    using Dalamud.IoC;
    using Dalamud.Logging;
    using Dalamud.Plugin;
    using Dalamud.Utility;
    using Lumina.Excel.CustomSheets;
    using Newtonsoft.Json;

    public unsafe class SpeakingWayPlugin : IDalamudPlugin, IDisposable
    {
        public Dictionary<string, byte[]> replacements = new();

        private readonly DalamudPluginInterface _pluginInterface;
        private readonly CommandManager _commandManager;
        private readonly ClientState _clientState;
        private readonly Framework _framework;
        private readonly ChatGui _chatGui;

        internal readonly DataManager DataManager;

        private bool enabled = true;
        private bool debug = false;

        public string Name => "SpeakingWay";
        private const string CommandName = "/speakingway";

        private delegate int GetStringPrototype(void* unknown, byte* text, void* unknown2, void* stringStruct);

        private readonly Hook<GetStringPrototype> _getStringHook;

        public SpeakingWayPlugin(
            [RequiredVersion("1.0")] SigScanner sigScanner,
            [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
            [RequiredVersion("1.0")] CommandManager commandManager,
            [RequiredVersion("1.0")] ClientState clientState,
            [RequiredVersion("1.0")] Framework framework,
            [RequiredVersion("1.0")] DataManager dataManager,
            [RequiredVersion("1.0")] ChatGui chatGui
        )
        {
            this._pluginInterface = pluginInterface;
            this._commandManager = commandManager;
            this._clientState = clientState;
            this._framework = framework;
            this.DataManager = dataManager;
            this._chatGui = chatGui;

            this._chatGui.ChatMessage += ChatGuiOnChatMessage;

            this._commandManager.AddHandler(CommandName, new CommandInfo(OnCommand));

            var getStringStr =
                "48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC 20 83 B9 ?? ?? ?? ?? ?? 49 8B F9 49 8B F0 48 8B EA 48 8B D9 75 09 48 8B 01 FF 90";
            var getStringPtr = sigScanner.ScanText(getStringStr);
            this._getStringHook = Hook<GetStringPrototype>.FromAddress(getStringPtr, GetStringDetour);

            this._getStringHook.Enable();

            Reload();
        }

        private void ChatGuiOnChatMessage(XivChatType type, uint senderid, ref SeString sender, ref SeString message,
            ref bool ishandled)
        {
            if (!debug) return;

            PluginLog.Log($"{message} ({Convert.ToHexString(message.Encode())})");
            foreach (var messagePayload in message.Payloads)
            {
                PluginLog.Log($"- {messagePayload}");
            }
        }

        private int GetStringDetour(void* unknown, byte* text, void* unknown2, void* stringStruct)
        {
            if (enabled) this.HandlePtr(ref text);

            return this._getStringHook.Original(unknown, text, unknown2, stringStruct);
        }

        private void HandlePtr(ref byte* ptr)
        {
            var byteList = new List<byte>();
            var i = 0;
            while (ptr[i] != 0)
                byteList.Add(ptr[i++]);
            var byteArr = byteList.ToArray();
            var ptrHash = Convert.ToHexString(SHA256.HashData(byteArr));
            if (!this.replacements.TryGetValue(ptrHash, out var encoded)) return;
            if (encoded.SequenceEqual(byteArr))
                return;

            if (encoded.Length <= byteArr.Length)
            {
                int j;
                for (j = 0; j < encoded.Length; j++)
                    ptr[j] = encoded[j];
                ptr[j] = 0;
            }
            else
            {
                var newStr = (byte*) Marshal.AllocHGlobal(encoded.Length + 1);
                int j;
                for (j = 0; j < encoded.Length; j++)
                    newStr[j] = encoded[j];
                newStr[j] = 0;
                ptr = newStr;
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            this._chatGui.ChatMessage -= ChatGuiOnChatMessage;

            this._commandManager.RemoveHandler(CommandName);
            this._getStringHook?.Disable();
        }

        private void Reload()
        {
            this.replacements.Clear();

            var excel = this.DataManager.Excel;
            var replacements =
                JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, StringReplacement>>>(
                    File.ReadAllText("B:\\PrefProThem\\ffxiv_they_them.json"));

            foreach (var (sheetName, sheetRows) in replacements!)
            {
                var sheet = excel.GetSheet<QuestDialogueText>(sheetName)!;

                foreach (var (sheetRowIndex, replacement) in sheetRows)
                {
                    var row = sheet.GetRow(uint.Parse(sheetRowIndex))!;
                    var baseHash = Convert.ToHexString(SHA256.HashData(row.Value.RawData));
                    var payloads = row.Value.ToDalamudString().Payloads;

                    foreach (var replaceOp in replacement.Replace)
                    {
                        var replacingWith = replaceOp.AsPayloads(this);
                        var total = replaceOp.Total;
                        var at = replaceOp.At;
                        var first = replaceOp.First;
                        var last = replaceOp.Last;

                        if (total)
                        {
                            payloads.Clear();
                            payloads.AddRange(replacingWith);
                        }
                        else if (at is not null)
                        {
                            payloads.RemoveAt((int) at);
                            payloads.InsertRange((int) at, replacingWith);
                        }
                        else if (first is not null && last is not null)
                        {
                            payloads.RemoveRange((int) first, (int) (last - first + 1));
                            payloads.InsertRange((int) first, replacingWith);
                        }
                    }

                    this.replacements.Add(baseHash, new SeString(payloads).Encode());
                    PluginLog.Information($"Loaded replacement @ {sheetName}[{sheetRowIndex}]");
                }
            }
        }

        private void OnCommand(string command, string args)
        {
            var list = args.Split(" ");
            switch (list.FirstOrDefault())
            {
                case "reload":
                {
                    Reload();
                    this._chatGui.Print("Reloaded!");
                    break;
                }

                case "enable":
                {
                    enabled = true;
                    this._chatGui.Print("Enabled SpeakingWay");
                    break;
                }

                case "disable":
                {
                    enabled = false;
                    this._chatGui.Print("Disabled SpeakingWay");
                    break;
                }

                case "debug":
                    debug = !debug;
                    this._chatGui.Print(debug ? "Debug Enabled" : "Debug disabled");
                    break;

                default:
                {
                    this._chatGui.Print($"No op for {args}");
                    break;
                }
            }
        }
    }
}