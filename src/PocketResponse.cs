public class PocketResponse
{
    public Dictionary<string, PocketItem> list { get; set; }

    public int total { get; set; }
    public int count { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public string RawResponse { get; set; }
}
