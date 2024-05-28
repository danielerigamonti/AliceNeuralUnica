using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AliceNeural.Models
{
    public class Parse1
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("pageid")]
        public int PageId { get; set; }

        [JsonPropertyName("wikitext")]
        public Wikitext? WikiText { get; set; }
    }

    public class SectionSummary
    {
        [JsonPropertyName("parse")]
        public Parse1? Parse { get; set; }
    }

    public class Wikitext
    {
        [JsonPropertyName("*")]
        public string? Testo { get; set; }
}
}
