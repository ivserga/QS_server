using Newtonsoft.Json;

namespace QScalp.Shared.Models
{
    public struct Spread
    {
        [JsonProperty("a")]
        public int Ask;

        [JsonProperty("b")]
        public int Bid;

        public Spread(int ask, int bid)
        {
            Ask = ask;
            Bid = bid;
        }
    }
}
