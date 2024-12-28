namespace Bloxstrap.Models.BloxstrapRPC;

public class WindowMessage
{
    [JsonPropertyName("windowId")]
    public int? WindowID { get; set; }

    [JsonPropertyName("x")]
    public int? X { get; set; }

    [JsonPropertyName("y")]
    public int? Y { get; set; }

    [JsonPropertyName("width")]
    public int? Width { get; set; }
    
    [JsonPropertyName("height")]
    public int? Height { get; set; }

    [JsonPropertyName("scaleWidth")]
    public int? ScaleWidth { get; set; }

    [JsonPropertyName("scaleHeight")]
    public int? ScaleHeight { get; set; }

    [JsonPropertyName("reset")]
    public bool? Reset { get; set; }
}
