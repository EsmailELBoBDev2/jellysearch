using JellySearch.Helpers;
using Microsoft.Extensions.Primitives;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JellySearch.Services;

/// <summary>
/// Response from Jellyfin's /Users/{userId}/Views endpoint
/// </summary>
public class JellyfinViewsResponse
{
    public List<JellyfinViewItem> Items { get; set; } = new();
}

public class JellyfinViewItem
{
    public string? Id { get; set; }
    public string? Name { get; set; }
}

public class JellyfinProxyService : IHostedService, IDisposable
{
    private HttpClient Client { get; }
    private ILogger Log { get; set; }

    private string? JellyfinUrl { get; set; } = Environment.GetEnvironmentVariable("JELLYFIN_URL");
    //private string? JellyfinToken { get; set; } = Environment.GetEnvironmentVariable("JELLYFIN_TOKEN");

    private string JellyfinSearchUrl { get; } = "{0}/Users/{1}/Items{2}";
    private string JellyfinAltSearchUrl { get; } = "{0}/Items{1}";
    private string JellyfinViewsUrl { get; } = "{0}/Users/{1}/Views";

    public JellyfinProxyService(ILoggerFactory logFactory)
    {
        this.Client = new HttpClient();
        this.Log = logFactory.CreateLogger<JellyfinProxyService>();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        this.Dispose();
    }

    /// <summary>
    /// Get the library IDs the user has access to
    /// </summary>
    public async Task<List<string>?> GetUserLibraryIds(string authorization, string? legacyToken, string userId)
    {
        var url = string.Format(this.JellyfinViewsUrl, this.JellyfinUrl, userId);
        var request = new HttpRequestMessage(HttpMethod.Get, url);

        request.Headers.TryAddWithoutValidation("Authorization", authorization);

        if (legacyToken != null)
            request.Headers.TryAddWithoutValidation("X-Mediabrowser-Token", legacyToken);

        try
        {
            var response = await this.Client.SendAsync(request);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var content = await response.Content.ReadAsStreamAsync();
                var viewsResponse = await JsonSerializer.DeserializeAsync<JellyfinViewsResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (viewsResponse?.Items != null)
                {
                    return viewsResponse.Items
                        .Where(x => x.Id != null)
                        .Select(x => x.Id!)
                        .ToList();
                }
            }
            else
            {
                this.Log.LogError("Failed to get user views: {error}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            this.Log.LogError("Error fetching user libraries: {error}", ex.Message);
        }

        return null;
    }

    private string GetUrl(string? userId, string query)
    {
        if(userId == null)
            return string.Format(this.JellyfinAltSearchUrl, this.JellyfinUrl, query); // Search without user ID (e.g. genres)
        else
            return string.Format(this.JellyfinSearchUrl, this.JellyfinUrl, userId, query);

    }

    /*
    public async Task<string?> ProxySearchRequest(string authorization, string? legacyToken, string? userId, Dictionary<string, StringValues> arguments)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, this.GetUrl(userId, HttpHelper.GetQueryString(arguments)));

        request.Headers.Add("Authorization", authorization);

        if(legacyToken != null)
            request.Headers.Add("X-Mediabrowser-Token", legacyToken);

        var response = await this.Client.SendAsync(request);

        if(response.StatusCode == System.Net.HttpStatusCode.OK)
            return await response.Content.ReadAsStringAsync();
        else
        {
            this.Log.LogError("Got error from Jellyfin: {error}", response.StatusCode);
            this.Log.LogError("{error}", await response.Content.ReadAsStringAsync());
            return null;
        }
    }
    */

    public async Task<Stream?> ProxySearchRequest(string authorization, string? legacyToken, string? userId, Dictionary<string, StringValues> arguments)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, this.GetUrl(userId, HttpHelper.GetQueryString(arguments)));

        request.Headers.TryAddWithoutValidation("Authorization", authorization);

        if(legacyToken != null)
            request.Headers.TryAddWithoutValidation("X-Mediabrowser-Token", legacyToken);

        var response = await this.Client.SendAsync(request);

        if(response.StatusCode == System.Net.HttpStatusCode.OK)
            return await response.Content.ReadAsStreamAsync();
        else
        {
            this.Log.LogError("Got error from Jellyfin: {error}", response.StatusCode);
            this.Log.LogError("{error}", await response.Content.ReadAsStringAsync());
            return null;
        }
    }

    public async Task<string?> ProxyRequest(string authorization, string? legacyToken, string path, string query)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, string.Format("{0}{1}{2}", this.JellyfinUrl, path, query));

        request.Headers.TryAddWithoutValidation("Authorization", authorization);

        if(legacyToken != null)
            request.Headers.TryAddWithoutValidation("X-Mediabrowser-Token", legacyToken);

        var response = await this.Client.SendAsync(request);

        if(response.StatusCode == System.Net.HttpStatusCode.OK)
            return await response.Content.ReadAsStringAsync();
        else
        {
            this.Log.LogError("Got error from Jellyfin: {error}", response.StatusCode);
            this.Log.LogError("{error}", await response.Content.ReadAsStringAsync());
            return null;
        }
    }

    public void Dispose()
    {
        this.Client.Dispose();
    }
}
