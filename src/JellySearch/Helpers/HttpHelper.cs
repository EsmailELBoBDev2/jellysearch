using System.Web;
using Microsoft.Extensions.Primitives;

namespace JellySearch.Helpers;

public class HttpHelper
{
    /// <summary>
    /// Convert the key value pairs to a query string (without leading ?)
    /// </summary>
    /// <param name="data">The data to put in the query string.</param>
    /// <returns>The query string.</returns>
    public static string GetQueryString(IEnumerable<KeyValuePair<string, string>> data)
    {
        var query = "?";
        var first = true;

        foreach (KeyValuePair<string, string> parameter in data)
        {
            if (!first)
                query += "&";

            query += HttpUtility.UrlEncode(parameter.Key) + "=" + HttpUtility.UrlEncode(parameter.Value);

            if (first)
                first = false;
        }

        return query;
    }

    /// <summary>
    /// Convert the key value pairs to a query string (without leading ?)
    /// </summary>
    /// <param name="data">The data to put in the query string.</param>
    /// <returns>The query string.</returns>
    public static string GetQueryString(IEnumerable<KeyValuePair<string, StringValues>> data)
    {
        var query = "?";
        var first = true;

        foreach (KeyValuePair<string, StringValues> parameter in data)
        {
            if (!first)
                query += "&";

            query += HttpUtility.UrlEncode(parameter.Key) + "=" + HttpUtility.UrlEncode(parameter.Value);

            if (first)
                first = false;
        }

        return query;
    }
}
