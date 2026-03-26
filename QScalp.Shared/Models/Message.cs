using System;
using Newtonsoft.Json;

namespace QScalp.Shared.Models
{
    public struct Message
    {
        [JsonProperty("dt")]
        public DateTime DateTime;

        [JsonProperty("txt")]
        public string Text;

        public Message(string text)
        {
            DateTime = DateTime.Now;
            Text = text;
        }

        public Message(DateTime dateTime, string text)
        {
            DateTime = dateTime;
            Text = text;
        }
    }
}
