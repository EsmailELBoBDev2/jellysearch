using Quartz;
using Microsoft.Data.Sqlite;
using JellySearch.Models;
using Meilisearch;

namespace JellySearch.Jobs;

public class IndexJob : IJob
{
    private string? JellyfinConfigDir { get; } = Environment.GetEnvironmentVariable("JELLYFIN_CONFIG_DIR");

    private string? MeilisearchUrl { get; } = Environment.GetEnvironmentVariable("MEILI_URL");
    private string? MeilisearchKey { get; } = Environment.GetEnvironmentVariable("MEILI_MASTER_KEY");

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            var meilisearch = new MeilisearchClient(this.MeilisearchUrl, this.MeilisearchKey);

            var index = meilisearch.Index("items");

            // Only need to filter by type
            await index.UpdateFilterableAttributesAsync(
                new string[] { "type", "parentId", "isFolder" }
            );

            // Change priority of fields; Meilisearch always uses camel case!
            await index.UpdateSearchableAttributesAsync(
                new string[] { "name", "artists", "albumArtists", "originalTitle", "productionYear", "seriesName", "overview" }
            );

            // We only need the GUID to pass to Jellyfin
            await index.UpdateDisplayedAttributesAsync(
                new string[] { "guid", "name" }
            );

            // Open Jellyfin library
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = this.JellyfinConfigDir + "/data/library.db",
                Mode = SqliteOpenMode.ReadOnly,
            };

            using var connection = new SqliteConnection(connectionString.ToString());
            await connection.OpenAsync();

            // Query all base items
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT guid, type, ParentId, Name, Overview, ProductionYear, IsFolder, OriginalTitle, SeriesName, Artists, AlbumArtists FROM TypedBaseItems";

            using var reader = await command.ExecuteReaderAsync();

            var items = new List<Item>();

            while (await reader.ReadAsync())
            {
                var item = new Item()
                {
                    Guid = reader.GetGuid(0).ToString(),
                    Type = !reader.IsDBNull(1) ? reader.GetString(1) : null,
                    ParentId = !reader.IsDBNull(2) ? reader.GetString(2) : null,
                    Name = !reader.IsDBNull(3) ? reader.GetString(3) : null,
                    Overview = !reader.IsDBNull(4) ? reader.GetString(4) : null,
                    ProductionYear = !reader.IsDBNull(5) ? reader.GetInt32(5) : null,
                    IsFolder = !reader.IsDBNull(6) ? reader.GetInt16(6) : null,
                    OriginalTitle = !reader.IsDBNull(7) ? reader.GetString(7) : null,
                    SeriesName = !reader.IsDBNull(8) ? reader.GetString(8) : null,
                    Artists = !reader.IsDBNull(9) ? reader.GetString(9) : null,
                    AlbumArtists = !reader.IsDBNull(10) ? reader.GetString(10) : null,
                };

                items.Add(item);
            }

            if (items.Count > 0)
            {
                // Add items to search index in batches
                await index.AddDocumentsInBatchesAsync<Item>(items, 5000, "guid");
            }

            Console.WriteLine("Done");
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            Console.WriteLine(e.StackTrace);
            throw e;
        }
    }
}
