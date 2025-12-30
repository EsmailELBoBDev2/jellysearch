namespace JellySearch.Models;

public class Item
{
    public string? Guid { get; set; }
    public string? Type { get; set; }
    public string? ParentId { get; set; }

    public short? CommunityRating { get; set; }

    public string? Name { get; set; }
    public string? Overview { get; set; }

    public int? ProductionYear { get; set; }

    public string[]? Genres { get; set; }
    public string[]? Studios { get; set; }
    public string[]? Tags { get; set; }

    public short? IsFolder { get; set; }
    public short? CriticRating { get; set; }

    public string? OriginalTitle { get; set; }

    public string? SeriesName { get; set; }

    public string[]? Artists { get; set; }
    public string[]? AlbumArtists { get; set; }

    // TopParentId stores the root library folder ID for permission filtering
    public string? TopParentId { get; set; }
}
