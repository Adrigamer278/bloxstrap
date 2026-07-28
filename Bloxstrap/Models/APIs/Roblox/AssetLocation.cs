namespace Bloxstrap.Models.APIs.Roblox
{
    // lmao its just one property
    public class AssetLocation
    {
        [JsonPropertyName("assetFormat")]
        public string AssetFormat { get; set; } = string.Empty;

        [JsonPropertyName("location")]
        public string? Location { get; set; }

        [JsonPropertyName("assetMetadatas")]
        public List<AssetMetadata> AssetMetadatas { get; set; } = new List<AssetMetadata>();
    }
}