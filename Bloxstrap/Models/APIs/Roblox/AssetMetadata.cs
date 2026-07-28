namespace Bloxstrap.Models.APIs.Roblox
{
    // lmao its just one property
    public class AssetMetadata
    {
        [JsonPropertyName("metadataType")]
        public long MetadataType { get; set; }

        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;
    }
}