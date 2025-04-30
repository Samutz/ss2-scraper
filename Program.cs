namespace SS2Scraper;

using System.Text;
using System.Threading.Tasks;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Json;
using Mutagen.Bethesda.Oblivion;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Plugins.Order;
using Newtonsoft.Json;

class Program
{
    public class ApiConfig
    {
        public string Api_key { get; set; } = string.Empty;
        public string Base_url { get; set; } = string.Empty;
        public bool Ignore_ssl { get; set; } = false;
    }

    static async Task Main(string[] args)
    {
        string? filePath = null;
        bool doUpload = false;
        bool doJSON = false;

        foreach (string arg in args)
        {
            if (File.Exists(arg))
            {
                filePath = arg;
                continue;
            }

            switch (arg.ToLower())
            {
                case "-upload":
                    doUpload = true;
                    continue;
                case "-json":
                    doJSON = true;
                    continue;
            }
        }

        if (filePath is null) throw new ArgumentException("Plugin path missing");

        Console.WriteLine($"Plugin: {filePath}");
        Console.WriteLine($"Upload: {doUpload}");
        Console.WriteLine($"JSON: {doJSON}");

        var settings = new JsonSerializerSettings();
        settings.AddMutagenConverters();

        (var mod, var linkCache) = LoadMod(filePath);
        
        string pluginFile = Path.GetFileName(filePath) ?? throw new ArgumentException("Invalid plugin path");

        Export export = new(mod, linkCache);
        var output = export.BuildOutput();
        output.name = pluginFile;

        Console.WriteLine($"Total SS2 Items: {output.totalItems}");

        if (output.totalItems==0) throw new ArgumentException("No SS2 items found in this plugin");

        if (doJSON)
        {
            if (!Directory.Exists(".\\json")) Directory.CreateDirectory(".\\json");
            var json = JsonConvert.SerializeObject(output);
            File.WriteAllText($".\\json\\{pluginFile}.json", json);
        }

        if (doUpload)
        {
            string configPath = "config.json";
            if (!File.Exists(configPath)) // check for local path when debugging first
            {
                string exePath = AppDomain.CurrentDomain.BaseDirectory;
                configPath = Path.Combine(exePath, "config.json"); // set to exe folder for published exe
            }
            if (!File.Exists(configPath)) throw new ArgumentException("Config file is missing");
            string configJson = File.ReadAllText(configPath);
            ApiConfig config = JsonConvert.DeserializeObject<ApiConfig>(configJson) ?? new ApiConfig();
            if (config.Api_key.Equals("") || config.Base_url.Equals("")) throw new ArgumentException("Config file is empty");

            await UploadJson(config, output);
        }
    }

    private static async Task UploadJson(ApiConfig? config, Export.Output output)
    {
        if (config?.Base_url is null || config?.Api_key is null) throw new ArgumentException("Json config missing");

        Uri base_url = new(config.Base_url);

        HttpClientHandler handler = new();
        if (config?.Ignore_ssl ?? false)
        {
            Console.WriteLine($"setting ignore SSL");
            handler = new HttpClientHandler
            {
                ClientCertificateOptions = ClientCertificateOption.Manual,
                ServerCertificateCustomValidationCallback = (httpRequestMessage, cert, cetChain, policyErrors) =>
                {
                    return true;
                }
            };
        }

        HttpClient httpClient = new(handler)
        {
            BaseAddress = base_url,
        };
        using StringContent jsonContent = new(
            JsonConvert.SerializeObject(new
            {
                api_key = config?.Api_key,
                content = output
            }),
            Encoding.UTF8,
            "application/json"
        );

        Console.WriteLine($"Starting HTTP");

        using HttpResponseMessage response = await httpClient.PostAsync("api/upload_json", jsonContent);
        response.EnsureSuccessStatusCode();

        var jsonResponse = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"{jsonResponse}\n");
    }

    private static (IFallout4ModDisposableGetter, ILinkCache) LoadMod(string pluginFullpath)
    {
        string pluginDir = Path.GetDirectoryName(pluginFullpath) ?? throw new ArgumentException("Invalid plugin path");
        string pluginFile = Path.GetFileName(pluginFullpath) ?? throw new ArgumentException("Invalid plugin path");

        // Console.WriteLine("Loading " + pluginFile);

        var activeMod = Fallout4Mod.CreateFromBinaryOverlay(Path.Combine(pluginDir, pluginFile), Fallout4Release.Fallout4);

        var listings = new List<LoadOrderListing>();
        foreach (var masterFile in activeMod.ModHeader.MasterReferences)
        {
            listings.Add(new(ModKey.FromFileName(masterFile.Master.FileName), enabled: true));
        }
        listings.Add(new(ModKey.FromFileName(pluginFile), enabled: true));

        var loadOrder = LoadOrder.Import<IFallout4ModGetter>(listings, GameRelease.Fallout4);

        var env = GameEnvironment.Typical.Builder<IFallout4Mod, IFallout4ModGetter>(GameRelease.Fallout4)
            .WithTargetDataFolder(pluginDir)
            .WithLoadOrder(loadOrder)
            .Build();

        env.LoadOrder.ListedOrder.ToList().ForEach(item => { Console.WriteLine("Load Order: "+item.FileName); });

        ILinkCache linkCache = env.LoadOrder.ToImmutableLinkCache();

        return (activeMod, linkCache);
    }
}