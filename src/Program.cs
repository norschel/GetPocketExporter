using System.Text;
using Newtonsoft.Json;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;

class Program
{
    private static object _consumerKey;
    private static object _accessToken;

    private static int skippedItems = 0;

    static async Task Main(string[] args)
    {
        // Konfiguration laden
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var pocketConfig = config.GetSection("Pocket").Get<PocketConfig>();
        if (string.IsNullOrEmpty(pocketConfig?.ConsumerKey) || string.IsNullOrEmpty(pocketConfig?.AccessToken))
        {
            Console.WriteLine("Please enter ConsumerKey and AccessToken in appsettings.json."); return;
        }

        _accessToken = pocketConfig.AccessToken;
        _consumerKey = pocketConfig.ConsumerKey;

        var results = await GetGetPocketLinks();

        ExportToConsole(results);
        ExportMozillaBookmarksFile(results);
        ExportRawJsonFiles(results);
    }

    private static void ExportRawJsonFiles(IList<PocketResponse?> results)
    {
        int count = 0;
        Console.WriteLine("Exporting raw JSON files...");
        foreach (var result in results)
        {
            count++;
            if (result != null)
            {
                var exportDir = Path.Combine(Directory.GetCurrentDirectory(), "export");
                Directory.CreateDirectory(exportDir);
                var filename = Path.Combine(exportDir, $"{DateTime.UtcNow:yyyyMMdd}_pocket_raw_{count}.json"); System.IO.File.WriteAllText(filename, result.RawResponse);
                Console.WriteLine($"Exported {filename}");
            }
            else
            {
                Console.WriteLine("No valid response to export.");
            }
        }
    }

    private static void ExportToConsole(IList<PocketResponse?> results)
    {
        foreach (var result in results)
        {
            if (result?.list != null)
            {
                // output all items to console
                foreach (var item in result.list.Values)
                {
                    Console.WriteLine($"Item ID: {item.item_id}, Title: {item.resolved_title}, URL: {item.given_url}");
                }
            }
            else
            {
                Console.WriteLine("No items found or error in response.");
            }
        }
    }

    private static void ExportMozillaBookmarksFile(IList<PocketResponse> results)
    {
        Console.WriteLine("Exporting items to bookmarks.html...");

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE NETSCAPE-Bookmark-file-1>");
        sb.AppendLine("<META HTTP-EQUIV=\"Content-Type\" CONTENT=\"text/html; charset=UTF-8\">\n");
        sb.AppendLine("<TITLE>Bookmarks</TITLE>");
        sb.AppendLine("<H1>Bookmarks</H1>");
        sb.AppendLine("<DL><p>");
        foreach (var result in results)
        {
            foreach (var item in result.list.Values)
            {
                if (item.status == 2)
                {
                    skippedItems++;
                    continue; // Skip items that are not saved
                }

                var url = item.given_url;
                var title = item.given_title ?? string.Empty;
                var tags = item.tags != null ? string.Join(",", item.tags.Keys) : "";
                var addDate = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                sb.Append("<DT><A HREF=\"")
                    .Append(System.Net.WebUtility.HtmlEncode(url))
                    .Append("\" ADD_DATE=\"")
                    .Append(item.time_added)
                    .Append("\" LAST_MODIFIED=\"")
                    .Append(item.time_updated)
                    .Append("\" PRIVATE=\"1\"")
                    .Append(" TOREAD=\"0\""); if (!string.IsNullOrEmpty(tags))
                {
                    sb.Append(" TAGS=\"").Append(tags.Replace("\"", "&quot;")).Append("\"");
                }
                sb.Append(">").Append(System.Net.WebUtility.HtmlEncode(title)).Append("</A>\n");

                Console.WriteLine(url);
            }

        }

        sb.AppendLine("</DL><p>");

        var exportDir = Path.Combine(Directory.GetCurrentDirectory(), "export");
        Directory.CreateDirectory(exportDir);
        var filename = Path.Combine(exportDir, $"{DateTime.UtcNow:yyyyMMdd}_bookmarks.html"); System.IO.File.WriteAllText(filename, sb.ToString());
        Console.WriteLine($"Exported {filename}"); Console.WriteLine("Exported bookmarks.html");
        Console.WriteLine($"Skipped {skippedItems} items that were not saved in bookmarks.htlm file.");
    }

    private static async Task<IList<PocketResponse?>> GetGetPocketLinks(int count = 15)
    {
        int offset = 0;
        int total = 0;
        IList<PocketResponse?> items = new List<PocketResponse?>();
        int waitCount = count;

        do
        {
            PocketResponse? pocketResponse = await QueryPocketPage(count, offset);
            if (pocketResponse == null)
            {
                Console.WriteLine("No response from Pocket API or an error occurred.");
                break;
            }
            items.Add(pocketResponse);

            total = pocketResponse?.total ?? 0;
            offset += count;

            Console.WriteLine($"percent: {offset * 100 / total}% | Fetched {pocketResponse?.list.Count()} items, total so far: {total}, next offset/already fetched: {offset}");

            waitCount--;
            if (waitCount == 0)
            {
                Console.WriteLine("To avoid reaching the limit of requests, waiting for 10 seconds before next batch of requests.");
                Task.Delay(10000).Wait();
                waitCount = count;
            }
        } while ((total > offset));
        return items;
    }

    private static async Task<PocketResponse?> QueryPocketPage(int count, int offset)
    {
        var client = new HttpClient();
        // Details see https://getpocket.com/developer/docs/v3/retrieve
        var requestBody = new
        {
            consumer_key = _consumerKey,
            access_token = _accessToken,
            detailType = "complete",
            count = count.ToString(),
            offset = offset.ToString(),
            total = "1".ToString(),
            sort = "newest",
            //,contentType = "image"
        };

        var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

        HttpResponseMessage? response = null;
        int retryCount = 3;
        do
        {
            if (response != null)
            {
                Debug.WriteLine($"Retrying due to status code: {response.StatusCode}");
                Task.Delay(1000).Wait(); // Wait for 1 second before retrying
                retryCount--;
                if (retryCount == 0)
                {
                    Console.WriteLine("Max retries reached. Exiting.");
                    return null;

                }
            }

            response = await client.PostAsync("https://getpocket.com/v3/get", content);

        } while (!response.IsSuccessStatusCode);
        var responseString = await response.Content.ReadAsStringAsync();


        if (response.Headers.Contains("X-Limit-User-Remaining"))
        {
            Debug.WriteLine($"X-Limit-User-Remaining: {response.Headers.GetValues("X-Limit-User-Remaining").FirstOrDefault()}");
        }

        if (response.Headers.Contains("X-Limit-Key-Remaining"))
        {
            Debug.WriteLine($"X-Limit-User-Remaining: {response.Headers.GetValues("X-Limit-Key-Remaining").FirstOrDefault()}");
        }

        Debug.WriteLine("Response from Pocket API:");
        Debug.WriteLine(responseString);

        var pocketResponse = JsonConvert.DeserializeObject<PocketResponse>(responseString);
        pocketResponse.RawResponse = responseString;

        return pocketResponse;
    }
}
