namespace JellySearch.Jellyfin;

public class JellyfinItemResponse
{
    public List<JellyfinItem> Items { get; set; }

    public int TotalRecordCount { get; set; }
    public int StartIndex { get; set; }
}
