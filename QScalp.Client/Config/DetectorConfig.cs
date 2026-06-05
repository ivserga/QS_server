// ==========================================================================
//  DetectorConfig.cs — Сериализуемая настройка одного детектора.
// ==========================================================================
//  Хранит имя детектора (Name из ISignalDetector.Name), флаг Enabled и
//  словарь параметров. Параметры применяются к экземпляру детектора через
//  рефлексию по совпадению имён публичных свойств.
// ==========================================================================

using System.Collections.Generic;

using Newtonsoft.Json;

namespace QScalp.Client.Config
{
    public sealed class DetectorConfig
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonProperty("params")]
        public Dictionary<string, object> Params { get; set; } = new Dictionary<string, object>();
    }
}
