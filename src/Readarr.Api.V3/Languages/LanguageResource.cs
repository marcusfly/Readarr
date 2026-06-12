using System.Text.Json.Serialization;
using Readarr.Http.REST;

namespace Readarr.Api.V3.Languages
{
    public class LanguageResource : RestResource
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public new int Id { get; set; }
        public string Name { get; set; }
        public string NameLower => Name.ToLowerInvariant();
    }
}
