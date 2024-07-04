namespace JellySearch.Jellyfin;

public static class JellyfinHelper
{
    public static string? GetFullItemType(string itemType)
    {
        switch(itemType)
        {
            case "Movie":
                return "MediaBrowser.Controller.Entities.Movies.Movie";
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
                return "MediaBrowser.Controller.LiveTv.LiveTvChannel";
            case "LiveTvProgram":
                return "MediaBrowser.Controller.LiveTv.LiveTvProgram";
            case "PhotoAlbum":
                return "MediaBrowser.Controller.Entities.PhotoAlbum";
            case "Photo":
                return "MediaBrowser.Controller.Entities.Photo";
            case "Book":
                return "MediaBrowser.Controller.Entities.Book";
            case "AudioBook":
                return "MediaBrowser.Controller.Entities.AudioBook";
            case "BoxSet":
                return "MediaBrowser.Controller.Entities.Movies.BoxSet";
            default:
                return "MediaBrowser.Controller.Entities.Unknown";
        }
    }
}
