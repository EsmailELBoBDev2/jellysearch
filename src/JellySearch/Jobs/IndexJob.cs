using Quartz;
using Microsoft.Data.Sqlite;
using JellySearch.Models;

namespace JellySearch.Jobs;

public class IndexJob : IJob
{
    private string? JellyfinConfigDir { get; } = Environment.GetEnvironmentVariable("JELLYFIN_CONFIG_DIR");
    private ILogger? Log { get; set; }

    public async Task Execute(IJobExecutionContext context)
    {
        var jobData = context.JobDetail.JobDataMap;
        var index = jobData["index"] as Meilisearch.Index;

        var logFactory = jobData["logFactory"] as ILoggerFactory;
        this.Log = logFactory.CreateLogger<IndexJob>();

        try
        {
            this.Log.LogInformation("Indexing items...");

            // Set filterable attributes (topParentId is used for library permission filtering)
            await index.UpdateFilterableAttributesAsync(
                new string[] { "type", "parentId", "topParentId", "isFolder" }
            );

            // Set sortable attributes
            await index.UpdateSortableAttributesAsync(
                new string[] { "communityRating", "criticRating" }
            );

            // Change priority of fields; Meilisearch always uses camel case!
            await index.UpdateSearchableAttributesAsync(
                new string[] { "name", "artists", "albumArtists", "originalTitle", "productionYear", "seriesName", "genres", "tags", "studios", "overview" }
            );

            // We only need the GUID to pass to Jellyfin
            await index.UpdateDisplayedAttributesAsync(
                new string[] { "guid", "name" }
            );

            // Set ranking rules to add critic rating
            await index.UpdateRankingRulesAsync(
                new string[] { "words", "typo", "proximity", "attribute", "sort", "exactness", "communityRating:desc", "criticRating:desc" }
            );

            var legacy = true;
            var databasePath = "/data/library.db";

            // If the old database does not exist, use the new one
            if (!File.Exists(Path.Join(this.JellyfinConfigDir + databasePath)))
            {
                this.Log.LogInformation("No library.db available, trying jellyfin.db");

                legacy = false;
                databasePath = "/data/jellyfin.db";
            }

            // If the new database doesn't exist either, abort
            if (!File.Exists(Path.Join(this.JellyfinConfigDir + databasePath)))
            {
                throw new FileNotFoundException("Could not find either library.db or jellyfin.db in config folder.");
            }

            // Open Jellyfin library
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = this.JellyfinConfigDir + databasePath,
                Mode = SqliteOpenMode.ReadOnly,
            };

            using var connection = new SqliteConnection(connectionString.ToString());
            await connection.OpenAsync();

            // Query all base items
            using var command = connection.CreateCommand();

            // Adjust query if querying a legacy database
            if(legacy)
                command.CommandText = "SELECT guid, type, ParentId, CommunityRating, Name, Overview, ProductionYear, Genres, Studios, Tags, IsFolder, CriticRating, OriginalTitle, SeriesName, Artists, AlbumArtists, TopParentId FROM TypedBaseItems";
            else
                command.CommandText = "SELECT id, Type, ParentId, CommunityRating, Name, Overview, ProductionYear, Genres, Studios, Tags, IsFolder, CriticRating, OriginalTitle, SeriesName, Artists, AlbumArtists, TopParentId FROM BaseItems";

            using var reader = await command.ExecuteReaderAsync();

            var items = new List<Item>();

            while (await reader.ReadAsync())
            {
                try
                {
                    var item = new Item()
                    {
                        Guid = reader.GetGuid(0).ToString(),
                        Type = !reader.IsDBNull(1) ? reader.GetString(1) : null,
                        ParentId = !reader.IsDBNull(2) ? reader.GetString(2) : null,
                        CommunityRating = !reader.IsDBNull(3) ? reader.GetInt16(3) : null,
                        Name = !reader.IsDBNull(4) ? reader.GetString(4) : null,
                        Overview = !reader.IsDBNull(5) ? reader.GetString(5) : null,
                        ProductionYear = !reader.IsDBNull(6) ? reader.GetInt32(6) : null,
                        Genres = !reader.IsDBNull(7) ? reader.GetString(7).Split('|') : null,
                        Studios = !reader.IsDBNull(8) ? reader.GetString(8).Split('|') : null,
                        Tags = !reader.IsDBNull(9) ? reader.GetString(9).Split('|') : null,
                        IsFolder = !reader.IsDBNull(10) ? reader.GetInt16(10) : null,
                        CriticRating = !reader.IsDBNull(11) ? reader.GetInt16(11) : null,
                        OriginalTitle = !reader.IsDBNull(12) ? reader.GetString(12) : null,
                        SeriesName = !reader.IsDBNull(13) ? reader.GetString(13) : null,
                        Artists = !reader.IsDBNull(14) ? reader.GetString(14).Split('|') : null,
                        AlbumArtists = !reader.IsDBNull(15) ? reader.GetString(15).Split('|') : null,
                        TopParentId = !reader.IsDBNull(16) ? reader.GetString(16) : null,
                    };

                    items.Add(item);
                }
                catch(Exception e)
                {
                    this.Log.LogError("Could not add an item to the index, ignoring item");

                    this.Log.LogError("Item index: " + (items.Count - 1));
                    if(!reader.IsDBNull(4))
                        this.Log.LogError("Item name: " + reader.GetString(4));

                    this.Log.LogDebug(e.Message);
                    this.Log.LogDebug(e.StackTrace);
                }
            }

            if (items.Count > 0)
            {
                // Log unique TopParentIds to understand the format
                var uniqueTopParents = items
                    .Where(x => x.TopParentId != null)
                    .Select(x => x.TopParentId!)
                    .Distinct()
                    .Take(20)
                    .ToList();
                this.Log.LogInformation("Sample TopParentIds from database: {ids}", string.Join(", ", uniqueTopParents));

                // Add items to search index in batches
                await index.AddDocumentsInBatchesAsync<Item>(items, 5000, "guid");
            }

            this.Log.LogInformation("Indexed {count} items, it might take a few moments for Meilisearch to finish indexing", items.Count);
        }
        catch (Exception e)
        {
            this.Log.LogError("{message}", e.Message);
            this.Log.LogError("{stacktrace}", e.StackTrace);
            throw e;
        }
    }
}
