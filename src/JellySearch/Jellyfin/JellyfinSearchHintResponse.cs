namespace JellySearch.Jellyfin;

public class JellyfinSearchHintResponse
{
    public List<JellyfinItem> SearchHints { get; set; }

    public int TotalRecordCount { get; set; }
}
