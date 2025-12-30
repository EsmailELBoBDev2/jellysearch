using JellySearch.Helpers;
using Microsoft.Extensions.Primitives;
using System.Text.Json;

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
    public string? CollectionType { get; set; }
    public string? ParentId { get; set; }
}

/// <summary>
/// Info from /Library/VirtualFolders endpoint
/// </summary>
public class VirtualFolderInfo
{
    public string? Name { get; set; }
    public string? ItemId { get; set; }
    public string? CollectionType { get; set; }
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

    private string GetUrl(string? userId, string query)
    {
        if(userId == null)
            return string.Format(this.JellyfinAltSearchUrl, this.JellyfinUrl, query); // Search without user ID (e.g. genres)
        else
            return string.Format(this.JellyfinSearchUrl, this.JellyfinUrl, userId, query);

    }

    /// <summary>
    /// Get the library IDs the user has access to.
    /// First gets user's accessible Views, then maps to VirtualFolders to get actual library ItemIds.
    /// </summary>
    public async Task<List<string>?> GetUserLibraryIds(string authorization, string? legacyToken, string userId)
    {
        // Step 1: Get user's accessible Views
        var viewsUrl = string.Format(this.JellyfinViewsUrl, this.JellyfinUrl, userId);
        var viewsRequest = new HttpRequestMessage(HttpMethod.Get, viewsUrl);
        viewsRequest.Headers.TryAddWithoutValidation("Authorization", authorization);
        if (legacyToken != null)
            viewsRequest.Headers.TryAddWithoutValidation("X-Mediabrowser-Token", legacyToken);

        HashSet<string> userViewNames = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> allIds = new();

        try
        {
            var viewsResponse = await this.Client.SendAsync(viewsRequest);
            if (viewsResponse.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var content = await viewsResponse.Content.ReadAsStreamAsync();
                var views = await JsonSerializer.DeserializeAsync<JellyfinViewsResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (views?.Items != null)
                {
                    foreach (var view in views.Items)
                    {
                        if (view.Name != null)
                            userViewNames.Add(view.Name);
                        if (view.Id != null)
                            allIds.Add(view.Id);
                        if (view.ParentId != null)
                            allIds.Add(view.ParentId);
                    }
                }
            }

            // Step 2: Get VirtualFolders to find actual library ItemIds
            var vfUrl = string.Format("{0}/Library/VirtualFolders", this.JellyfinUrl);
            var vfRequest = new HttpRequestMessage(HttpMethod.Get, vfUrl);
            vfRequest.Headers.TryAddWithoutValidation("Authorization", authorization);
            if (legacyToken != null)
                vfRequest.Headers.TryAddWithoutValidation("X-Mediabrowser-Token", legacyToken);

            var vfResponse = await this.Client.SendAsync(vfRequest);
            if (vfResponse.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var content = await vfResponse.Content.ReadAsStreamAsync();
                var virtualFolders = await JsonSerializer.DeserializeAsync<List<VirtualFolderInfo>>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (virtualFolders != null)
                {
                    foreach (var vf in virtualFolders)
                    {
                        // Include this library if user has access (name matches a View) or include all if we couldn't get views
                        if (userViewNames.Count == 0 || (vf.Name != null && userViewNames.Contains(vf.Name)))
                        {
                            if (vf.ItemId != null)
                                allIds.Add(vf.ItemId);
                        }
                    }
                }
            }

            // Return library IDs (GUIDs) normalized to 32-char hex strings
            if (allIds.Count > 0)
            {
                var normalizedIds = new List<string>();
                foreach(var id in allIds)
                {
                    if(Guid.TryParse(id, out var guid))
                    {
                        normalizedIds.Add(guid.ToString("N"));
                    }
                }
                
                this.Log.LogInformation("User {userId} accessible library IDs (normalized): {ids}", userId, string.Join(", ", normalizedIds));
                return normalizedIds;
            }
        }
        catch (Exception ex)
        {
            this.Log.LogError("Error fetching user libraries: {error}", ex.Message);
        }

        return null;
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
