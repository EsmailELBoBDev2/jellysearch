namespace JellySearch.Jellyfin;

public static class JellyfinHelper
{
    public static string? GetFullItemType(string itemType)
    {
        switch(itemType)
        {
            case "Movie":
                return "MediaBrowser.Controller.Entities.Movies.Movie";
            case "LiveTvProgram":
                return "unknown"; // TODO
            case "AudioBook":
                return "unknown"; // TODO
            case "AudioBookBoxSet":
                return "unknown"; // TODO
            case "Episode":
                return "MediaBrowser.Controller.Entities.TV.Episode";
            case "Series":
                return "MediaBrowser.Controller.Entities.TV.Series";
            case "Playlist":
                return "MediaBrowser.Controller.Playlists.Playlist";
            case "MusicAlbum":
                return "MediaBrowser.Controller.Entities.Audio.MusicAlbum";
            case "Audio":
                return "MediaBrowser.Controller.Entities.Audio.Audio";
            case "Video":
                return "MediaBrowser.Controller.Entities.Video";
            case "TvChannel":
                return "unknown"; // TODO
            case "PhotoAlbum":
                return "unknown"; // TODO
            case "Photo":
                return "unknown"; // TODO
            default:
                return null;
        }
    }
}
