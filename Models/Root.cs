using Newtonsoft.Json;
using System.Collections.Generic;

namespace WpfApp3.Models
{
    public class Root
    {
        [JsonProperty("categories")]
        public List<Category> Categories { get; set; } = new();
    }
}