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

    [HttpGet("/Users/{userId}/Items")]
    public async Task<string> Search([FromRoute]string userId, [FromQuery]string? searchTerm, [FromQuery]string? IncludeItemTypes)
    {
        if (searchTerm == null)
        {
            this.Log.LogInformation("Proxying non-search request");
            return await this.Proxy.ProxySearchRequest(userId, Request.QueryString.ToString());
        }
        else
        {
            var query = Request.Query.Where(x => x.Key != "searchTerm").ToDictionary();

            List<string> filters = new List<string>();

            if (IncludeItemTypes != null)
            {
                foreach (var includeType in IncludeItemTypes.Split(','))
                {
                    var type = JellyfinHelper.GetFullItemType(includeType);

                    filters.Add("type = " + type);
                }
            }

            var results = await this.Index.SearchAsync<Item>(searchTerm, new SearchQuery()
            {
                Filter = string.Join(" AND ", filters),
                Limit = 20,
            });

            if (results.Hits.Count > 0)
            {
                this.Log.LogInformation("Proxying search request with {hits} results", results.Hits.Count);

                query.Add("ids", string.Join(',', results.Hits.Select(x => x.Guid)));

                return await this.Proxy.ProxySearchRequest(userId, query);
            }
            else
            {
                this.Log.LogInformation("No hits, not proxying");
                return JellyfinResponses.Empty;
            }
        }
    }

    [HttpGet("/Artists")]
    public async Task<string> SearchArtists([FromQuery]string? searchTerm, [FromQuery] string userId)
    {
        if (searchTerm == null)
        {
            this.Log.LogInformation("Proxying non-search artist request");
            return await this.Proxy.ProxySearchRequest(userId, Request.QueryString.ToString());
        }
        else
        {
            var query = Request.Query.Where(x => x.Key != "searchTerm").ToDictionary();

            var results = await this.Index.SearchAsync<Item>(searchTerm, new SearchQuery()
            {
                Filter = "type = MediaBrowser.Controller.Entities.Audio.MusicArtist",
                Limit = 20,
            });

            if (results.Hits.Count > 0)
            {
                this.Log.LogInformation("Proxying artist search request with {hits} results", results.Hits.Count);

                query.Add("ids", string.Join(',', results.Hits.Select(x => x.Guid)));

                return await this.Proxy.ProxySearchRequest(userId, query);
            }
            else
            {
                this.Log.LogInformation("No hits, not proxying");
                return JellyfinResponses.Empty;
            }
        }
    }

    [HttpGet("/Persons")]
    public async Task<string> SearchPeople([FromQuery]string? searchTerm, [FromQuery] string userId)
    {
        if (searchTerm == null)
        {
            return "PROXY";
        }
        else
        {
            var query = Request.Query.Where(x => x.Key != "searchTerm").ToDictionary();

            var results = await this.Index.SearchAsync<Item>(searchTerm, new SearchQuery()
            {
                Filter = "type = MediaBrowser.Controller.Entities.Person",
                Limit = 20,
            });

            if (results.Hits.Count > 0)
            {
                this.Log.LogInformation("Proxying people search request with {hits} results", results.Hits.Count);

                query.Add("ids", string.Join(',', results.Hits.Select(x => x.Guid)));

                return await this.Proxy.ProxySearchRequest(userId, query);
            }
            else
            {
                this.Log.LogInformation("No hits, not proxying");
                return JellyfinResponses.Empty;
            }
        }
    }
}
