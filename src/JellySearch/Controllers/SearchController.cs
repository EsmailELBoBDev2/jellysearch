using JellySearch.Jellyfin;
using JellySearch.Models;
using JellySearch.Services;
using Meilisearch;
using Microsoft.AspNetCore.Mvc;

namespace JellySearch.Controllers;

[Route("[controller]")]
[ApiController]
public class SearchController : ControllerBase
{
    private ILogger Log { get; set; }
    private JellyfinProxyService Proxy { get; }
    private Meilisearch.Index Index { get; }

    public SearchController(ILoggerFactory logFactory, JellyfinProxyService proxy, Meilisearch.Index index)
    {
        this.Log = logFactory.CreateLogger<SearchController>();
        this.Proxy = proxy;
        this.Index = index;
    }

    /// <summary>
    /// Proxy all possible search URLs to the central Items endpoint
    /// </summary>
    /// <param name="searchTerm">The term that is searched for.</param>
    /// <param name="includeItemTypes">The type of item to search</param>
    /// <param name="userId">The user id of the current user</param>
    /// <returns></returns>
    [HttpGet("/Users/{userId}/Items")]
    [HttpGet("/Items")]
    [HttpGet("/Persons")]
    [HttpGet("/Artists/AlbumArtists")]
    [HttpGet("/Artists")]
    [HttpGet("/Genres")]
    public async Task<IActionResult> Search(
        [FromHeader(Name = "Authorization")] string? headerAuthorization,
        [FromHeader(Name = "X-Emby-Authorization")] string? legacyAuthorization,
        [FromQuery]string? searchTerm,
        [FromRoute(Name = "UserId")] string? routeUserId,
        [FromQuery(Name = "UserId")] string? queryUserId)
    {
        // Get the requested path
        var path = this.Request.Path.Value;

        // Get the user id from either the route or the query
        var userId = routeUserId ?? queryUserId;

        // Get authorization from either the real "Authorization" header or from the legacy "X-Emby-Authorization" header
        var authorization = legacyAuthorization ?? headerAuthorization;

        if (Environment.GetEnvironmentVariable("JELLYSEARCH_DEBUG_REQUESTS") == "1")
        {
            Console.WriteLine("GET " + path + " from " + userId);
            Console.WriteLine("Using authorization: " + authorization);

            Console.WriteLine("HEADERS");
            foreach(var header in this.Request.Headers)
            {
                Console.WriteLine(header.Key + ": " + string.Join('|', header.Value));
            }

            Console.WriteLine("QUERY");
            foreach(var query in this.Request.Query)
            {
                Console.WriteLine(query.Key + ": " + string.Join('|', query.Value));
            }
        }

        if(authorization == null)
        {
            this.Log.LogWarning("Received request without Authorization header");
            return Content(JellyfinResponses.Empty, "application/json");
        }

        // If not searching, proxy directly for reverse proxies that cannot filter by query parameter
        // Genres are currently not supported
        if (searchTerm == null || path.EndsWith("/Genres"))
        {
            // If the search term is empty, we will proxy directly
            this.Log.LogWarning("Proxying non-search request, make sure to configure your reverse proxy correctly");
            return Content(await this.Proxy.ProxyRequest(authorization, this.Request.Path, this.Request.QueryString.ToString()), "application/json");
        }
        else
        {
            // Get all query arguments to pass along to Jellyfin
            // Remove searchterm since we already searched
            // Remove sortby and sortorder since we want to display results as Meilisearch returns them
            var query = this.Request.Query.Where(x =>
                !string.Equals(x.Key, "searchterm", StringComparison.InvariantCultureIgnoreCase) &&
                !string.Equals(x.Key, "sortby", StringComparison.InvariantCultureIgnoreCase) &&
                !string.Equals(x.Key, "sortorder", StringComparison.InvariantCultureIgnoreCase)
            ).ToDictionary(StringComparer.InvariantCultureIgnoreCase);

            var includeItemTypes = new List<string>();

            if(query.ContainsKey("IncludeItemTypes"))
            {
                if(query["IncludeItemTypes"].Count == 1)
                {
                    // If item count is 1, split by , and add all elements
                    includeItemTypes.AddRange(query["IncludeItemTypes"][0].Split(','));
                }
                else
                {
                    // If item count is more than 1, add all elements directly
                    includeItemTypes.AddRange(query["IncludeItemTypes"]);
                }
            }

            var filteredTypes = new List<string>();
            var additionalFilters = new List<string>();

            if(includeItemTypes.Count == 0)
            {
                if (path != null)
                {
                    // Handle direct endpoints and their types
                    if (path.EndsWith("/Persons"))
                    {
                        filteredTypes.Add("MediaBrowser.Controller.Entities.Person");
                    }
                    else if (path.EndsWith("/Artists"))
                    {
                        filteredTypes.Add("MediaBrowser.Controller.Entities.Audio.MusicArtist");
                    }
                    else if (path.EndsWith("/AlbumArtists"))
                    {
                        filteredTypes.Add("MediaBrowser.Controller.Entities.Audio.MusicArtist");
                        additionalFilters.Add("isFolder = 1"); // Album artists are marked as folder
                    }
                    else if (path.EndsWith("/Genres"))
                    {
                        filteredTypes.Add("MediaBrowser.Controller.Entities.Genre"); // TODO: Handle genre search properly
                    }
                }
            }
            else
            {
                // Get item type(s) from URL
                foreach (var includeItemType in includeItemTypes)
                {
                    var type = JellyfinHelper.GetFullItemType(includeItemType);

                    if(type == null)
                    {
                        this.Log.LogWarning("Got invalid type: {type}", includeItemType);
                    }
                    else
                    {
                        filteredTypes.Add(type);
                    }
                }
            }

            var items = new List<Item>();

            if (filteredTypes.Count > 0)
            {
                // Loop through each requested type and search
                foreach (var filteredType in filteredTypes)
                {
                    var filter = "type = " + filteredType;

                    if(additionalFilters.Count > 0)
                    {
                        filter += " AND " + string.Join(" AND ", additionalFilters);
                    }

                    var results = await this.Index.SearchAsync<Item>(searchTerm, new SearchQuery()
                    {
                        Filter = filter,
                        Limit = 15,
                    });

                    items.AddRange(results.Hits);
                }
            }
            else
            {
                // Search without filtering the type
                var results = await this.Index.SearchAsync<Item>(searchTerm, new SearchQuery()
                {
                    Limit = 20,
                });

                items.AddRange(results.Hits);
            }

            if (items.Count > 0)
            {
                this.Log.LogInformation("Proxying search request with {hits} results", items.Count);

                query.Add("ids", string.Join(',', items.Select(x => x.Guid.Replace("-", ""))));

                var response = await this.Proxy.ProxySearchRequest(authorization, userId, query);

                if(response == null)
                    return Content(JellyfinResponses.Empty, "application/json");
                else
                    return Content(response, "application/json");
            }
            else
            {
                this.Log.LogInformation("No hits, not proxying");
                return Content(JellyfinResponses.Empty, "application/json");
            }
        }
    }
}
