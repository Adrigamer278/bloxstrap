namespace Bloxstrap.Models.APIs.Roblox
{
    // lmao its just one property
    public class AssetDeliveryResponse
    {
        [JsonPropertyName("locations")]
        public List<AssetLocation>? Locations { get; set; }

        [JsonPropertyName("requestId")]
        public string RequestId { get; set; } = string.Empty;

        [JsonPropertyName("isArchived")]
        public bool IsArchived { get; set; }

        [JsonPropertyName("assetTypeId")]
        public long AssetTypeId { get; set; }

        [JsonPropertyName("isRecordable")]
        public bool IsRecordable { get; set; }
    }
}
