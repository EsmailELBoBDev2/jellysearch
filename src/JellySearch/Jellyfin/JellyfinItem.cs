using System.Text.Json.Serialization;
using JellySearch.Helpers;

namespace JellySearch.Jellyfin;

// See https://github.com/jellyfin/jellyfin/blob/ffecdfc18cbe4e97f6812f00ee97364c99fc5726/MediaBrowser.Model/Search/SearchHint.cs
public class JellyfinItem
{
    /* For items */
    public Dictionary<string, string>? ImageTags { get; set; } // ImageTags.Primary becomes PrimaryImageTag
    public List<string>? BackdropImageTags { get; set; } // BackdropImageTags[0] becomes BackdropImageTag
    public List<string>? ParentBackdropImageTags { get; set; } // If above not available: ParentBackdropImageTags[0] becomes BackdropImageTag
    public string? ParentBackdropItemId { get; set; } // ParentBackDropitemId becomes BackdropItemId
    public string? SeriesName { get; set; } // SeriesName becomes Series

    /* For hints */
    public string ItemId { get; set; }

    public string? PrimaryImageTag { get; set; }
    //public string? ThumbImageTag { get; set; } // Unused?
    //public string? ThumbImageItemId { get; set; } // Unused?
    public string? BackdropImageTag { get; set; }
    public string? BackdropImageItemId { get; set; } // Missing from /Items endpoint result? - Sometimes it's the regular ID, sometimes not

    //public string? MatchedTerm { get; set; } // Unused?

    public string? Series { get; set; }

    //public DateTime? StartDate { get; set; } // Unused?
    //public DateTime? EndDate { get; set; } // Unused?

    //public string? Status { get; set; } // Unused?
    //public int? SongCount { get; set; } // Unused?
    //public int? EpisodeCount { get; set; } // Unused?
    //public string? ChannelName { get; set; } // Unused?

    /* For both */
    public string? Id { get; set; }
    public string? ParentId { get; set; }
    public string? Name { get; set; }

    public string? Type { get; set; }
    public bool? IsFolder { get; set; }
    public long? RunTimeTicks { get; set; }
    public string? MediaType { get; set; }

    public int? IndexNumber { get; set; }
    public int? ParentIndexNumber { get; set; }
    public int? ProductionYear { get; set; }

    public string? Album { get; set; }
    public string? AlbumId { get; set; }
    public string? AlbumArtist { get; set; }
    public string[]? Artists { get; set; }

    public string? ChannelId { get; set; }

    public double? PrimaryImageAspectRatio { get; set; }
}
