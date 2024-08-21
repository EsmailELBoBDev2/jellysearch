namespace JellySearch.Jellyfin;

public static class JellyfinResponses
{
    public static string Empty { get; } = "{\"Items\": [], \"TotalRecordCount\": 0, \"StartIndex\": 0}";
    public static string EmptySearchHints { get; } = "{\"SearchHints\": [], \"TotalRecordCount\": 0}";
}
