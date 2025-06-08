public class PocketItem
{
    public string item_id { get; set; }
    public int status { get; set; }
    public string resolved_title { get; set; }
    public string resolved_url { get; set; }
    public string given_title { get; set; }
    public string given_url { get; set; }
    public string time_added { get; set; }
    public string time_updated { get; set; }
    public Dictionary<string, PocketTag> tags { get; set; }
}
