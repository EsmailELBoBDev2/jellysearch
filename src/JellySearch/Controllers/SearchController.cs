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
    [HttpGet("/Persons")]
    [HttpGet("/Artists/AlbumArtists")]
    [HttpGet("/Artists")]
    [HttpGet("/Genres")]
    public async Task<IActionResult> Search([FromHeader]string? authorization, [FromQuery]string? searchTerm, [FromQuery]string? includeItemTypes, [FromRoute(Name = "UserId")]string? routeUserId, [FromQuery(Name = "UserId")] string? queryUserId)
    {
        // Get the requested path
        var path = this.Request.Path.Value;

        // Get the user id from either the route or the query
        var userId = routeUserId ?? queryUserId;

        if(authorization == null)
        {
            this.Log.LogWarning("Received request without Authorization header");
            //return Content(JellyfinResponses.Empty, "application/json");
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
            ).ToDictionary();

            var orFilters = new List<string>();
            var andFilters = new List<string>();

            if(includeItemTypes == null)
            {
                if (path != null)
                {
                    // Handle direct endpoints and their types
                    if (path.EndsWith("/Persons"))
                    {
                        orFilters.Add("type = MediaBrowser.Controller.Entities.Person");
                    }
                    else if (path.EndsWith("/Artists"))
                    {
                        orFilters.Add("type = MediaBrowser.Controller.Entities.Audio.MusicArtist");
                    }
                    else if (path.EndsWith("/AlbumArtists"))
                    {
                        orFilters.Add("type = MediaBrowser.Controller.Entities.Audio.MusicArtist");
                        andFilters.Add("isFolder = 1"); // Album artists are marked as folder
                    }
                    else if (path.EndsWith("/Genres"))
                    {
                        orFilters.Add("type = MediaBrowser.Controller.Entities.Genre"); // TODO: Handle genre search properly
                    }
                }
            }
            else
            {
                // Get item type(s) from URL
                foreach (var includeItemType in includeItemTypes.Split(','))
                {
                    var type = JellyfinHelper.GetFullItemType(includeItemType);

                    if(type == null)
                    {
                        this.Log.LogWarning("Got invalid type: {type}", includeItemType);
                    }
                    else
                    {
                        orFilters.Add("type = " + type);
                    }
                }
            }

            string filters = ""; // TODO: Make this nicer

            if(orFilters.Count > 0)
            {
                filters += string.Join(" OR ", orFilters);
            }

            if(andFilters.Count > 0)
            {
                if(filters == "")
                    filters += string.Join(" AND ", andFilters);
                else
                    filters += " AND " + string.Join(" AND ", andFilters);
            }

            var results = await this.Index.SearchAsync<Item>(searchTerm, new SearchQuery()
            {
                Filter = filters != "" ? filters : null,
                Limit = 25,
            });

            if (results.Hits.Count > 0)
            {
                this.Log.LogInformation("Proxying search request with {hits} results", results.Hits.Count);

                query.Add("ids", string.Join(',', results.Hits.Select(x => x.Guid)));

                return Content(await this.Proxy.ProxySearchRequest(authorization, userId, query), "application/json");
            }
            else
            {
                this.Log.LogInformation("No hits, not proxying");
                return Content(JellyfinResponses.Empty, "application/json");
            }
        }
    }
}
