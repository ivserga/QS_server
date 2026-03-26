using Newtonsoft.Json;

namespace QScalp.Shared.Models
{
    public struct Quote
    {
        [JsonProperty("p")]
        public int Price;

        [JsonProperty("v")]
        public int Volume;

        [JsonProperty("t")]
        public QuoteType Type;

        public Quote(int price, int volume, QuoteType type)
        {
            Price = price;
            Volume = volume;
            Type = type;
        }
    }
}
