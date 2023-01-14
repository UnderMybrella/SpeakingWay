using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Text.SeStringHandling;
using Newtonsoft.Json.Serialization;

namespace SpeakingWay
{
    using System;
    using Newtonsoft.Json;

    public class StringReplacement
    {
        [JsonProperty("replace")] public ReplaceOp[] Replace = Array.Empty<ReplaceOp>();
    }

    public class ReplaceOp : PayloadSpec
    {
        /// <summary>
        /// The indice to replace from (inclusive)
        /// </summary>
        [JsonProperty("first")] public uint? First;

        /// <summary>
        /// The index to replace to (inclusive)
        /// </summary>
        [JsonProperty("last")] public uint? Last;

        /// <summary>
        /// The index to replace at
        /// </summary>
        [JsonProperty("at")] public uint? At;

        [JsonProperty("total")] public bool Total;

        [JsonProperty("payloads")] public PayloadSpec[] Payloads = Array.Empty<PayloadSpec>();

        public override IEnumerable<Payload> AsPayloads(SpeakingWayPlugin plugin)
        {
            return this.Payloads.Length > 0
                ? this.Payloads.SelectMany(payload => payload.AsPayloads(plugin))
                : base.AsPayloads(plugin);
        }
    }
}