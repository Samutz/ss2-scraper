using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SS2Scraper;

public class NexusApi(string apiKey)
{
    private readonly string apiKey = apiKey;
    private readonly string nexusApiBaseUri = "https://api.nexusmods.com/v1/";
    private readonly string nexusApiUserAgent = "SS2Scraper/dev";

    public async Task<JObject?> GetAsync(string path)
    {
        Uri baseUri = new(nexusApiBaseUri);

        HttpClientHandler handler = new();
        HttpClient httpClient = new(handler)
        {
            BaseAddress = baseUri,
        };
        httpClient.DefaultRequestHeaders.Add("apikey", apiKey);

        using HttpResponseMessage response = await httpClient.GetAsync(path);
        response.EnsureSuccessStatusCode();

        var jsonResponse = await response.Content.ReadAsStringAsync();
        //Console.WriteLine($"{jsonResponse}\n");

        return JsonConvert.DeserializeObject<JObject>(jsonResponse);
    }
}