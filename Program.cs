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
using IniParser;
using IniParser.Model;

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
        bool doMO2 = false;
        string modListPath = "";
        List<Export.ModMetadata> metadataCache = [];

        for (int i = 0; i < args.Length; i++)
        {
            string nextArg = i+1 < args.Length ? args[i+1] : "";

            switch (args[i].ToLower())
            {
                case "-upload":
                    doUpload = true;
                    continue;
                case "-json":
                    doJSON = true;
                    continue;
                case "-mo2":
                    if (!File.Exists(nextArg) || !(Path.GetFileName(nextArg) == "modlist.txt")) throw new ArgumentException("-mo2 requires a valid path to modlist.txt as the next argument");
                    doMO2 = true;
                    modListPath = nextArg;
                    continue;
                case "-plugin":
                    // strip quotes and slahes in case of "C:\foobar\"
                    string inputPath = nextArg.Trim().Trim('"').TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    string fullPath = Path.GetFullPath(inputPath);
                    if (!File.Exists(fullPath) && !Directory.Exists(fullPath)) throw new ArgumentException("Plugin path is invalid");
                    filePath = fullPath;
                    continue;
            }
        }
        
        Console.WriteLine($"Upload: {doUpload}");
        Console.WriteLine($"JSON: {doJSON}");

        List<string> modPaths = [];

        ApiConfig config = new();

        // check config early
        if (doUpload)
        {
            var settings = new JsonSerializerSettings();
            settings.AddMutagenConverters();

            string configPath = "config.json";
            if (!File.Exists(configPath)) // check for local path when debugging first
            {
                string exePath = AppDomain.CurrentDomain.BaseDirectory;
                configPath = Path.Combine(exePath, "config.json"); // set to exe folder for published exe
            }
            if (!File.Exists(configPath)) throw new ArgumentException("Config file is missing");
            string configJson = File.ReadAllText(configPath);
            config = JsonConvert.DeserializeObject<ApiConfig>(configJson) ?? new ApiConfig();
            if (config.Api_key.Equals("") || config.Base_url.Equals("")) throw new ArgumentException("Config has empty values");
        }
        
        string[] allowedExtensions = [".esm", ".esp", ".esl"];

        if (doMO2)
        {
            //Console.WriteLine($"Checking dir for enabled mods {modListPath}");
            List<string> enabledMods = [.. File.ReadLines(modListPath).Where(line => line.StartsWith('+'))];
            foreach (string mod in enabledMods)
            {
                string modName = mod.StartsWith('+') ? mod[1..] : mod;
                string modDir = Path.GetFullPath(Path.GetDirectoryName(modListPath)+"\\..\\..\\mods\\"+modName);
                string iniPath = Path.GetFullPath(modDir+"\\meta.ini");
                //Console.WriteLine($"Checking for {iniPath}");
                if (!File.Exists(iniPath)) continue;
                var iniParser = new FileIniDataParser();
                IniData data = iniParser.ReadFile(iniPath);
                int nexusId = int.Parse(data["General"]["modid"]);
                if (nexusId<=0) continue;
                List<string> pluginFiles = [.. Directory.GetFiles(modDir).Where(file => allowedExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))];
                foreach (var pluginFile in pluginFiles)
                {
                    Console.WriteLine($"Found meta.ini for {Path.GetFileName(pluginFile)}");
                    Export.ModMetadata metadata = new(){
                        pluginFile = Path.GetFileName(pluginFile),
                        nexusId = nexusId,
                        name = modName,
                        version = data["General"]["version"]
                    };
                    metadataCache.Add(metadata);
                }
            }
        }

        if (File.Exists(filePath) && allowedExtensions.Contains(Path.GetExtension(filePath)))
        {
            Console.WriteLine($"Single plugin mode: {filePath}");
            modPaths.Add(filePath);
        }
        else if (Directory.Exists(filePath))
        {
            Console.WriteLine($"Directory mode: {filePath}");
            modPaths = [.. Directory.GetFiles(filePath).Where(file => allowedExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))];
        }
        else
        {
            throw new ArgumentException($"Invalid path: {filePath}");
        }

        foreach (string modPath in modPaths)
        {
            string pluginFile = Path.GetFileName(modPath);
            Console.WriteLine($"Checking Plugin: {pluginFile}");

            (var mod, var linkCache) = LoadMod(modPath);

            if (mod is null || linkCache is null) continue;

            Export export = new(mod, linkCache);
            var output = export.BuildOutput();
            output.name = pluginFile;
            output.metadata = metadataCache.FirstOrDefault(data => data.pluginFile == pluginFile);

            Console.WriteLine($"Total SS2 Items: {output.totalItems}");

            if (output.totalItems==0) Console.WriteLine("No SS2 items found in this plugin");

            if (doJSON)
            {
                if (!Directory.Exists(".\\json")) Directory.CreateDirectory(".\\json");
                var json = JsonConvert.SerializeObject(output);
                File.WriteAllText($".\\json\\{pluginFile}.json", json);
            }

            if (doUpload)
            {
                await UploadJson(config, output);
            }
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

    private static (IFallout4ModDisposableGetter?, ILinkCache?) LoadMod(string pluginFullpath)
    {
        string pluginDir = Path.GetDirectoryName(pluginFullpath) ?? throw new ArgumentException("Invalid plugin path");
        string pluginFile = Path.GetFileName(pluginFullpath) ?? throw new ArgumentException("Invalid plugin path");

        // Console.WriteLine("Loading " + pluginFile);

        var activeMod = Fallout4Mod.CreateFromBinaryOverlay(Path.Combine(pluginDir, pluginFile), Fallout4Release.Fallout4);

        var listings = new List<LoadOrderListing>();
        foreach (var masterFile in activeMod.ModHeader.MasterReferences)
        {
            string masterPath = Path.Combine(pluginDir, masterFile.Master.FileName);
            if (!File.Exists(masterPath))
            {
                Console.WriteLine("Could not find required master: "+masterPath);
                return (null, null);
            }
            listings.Add(new(ModKey.FromFileName(masterFile.Master.FileName), enabled: true));
        }
        listings.Add(new(ModKey.FromFileName(pluginFile), enabled: true));

        var loadOrder = LoadOrder.Import<IFallout4ModGetter>(listings, GameRelease.Fallout4);

        var env = GameEnvironment.Typical.Builder<IFallout4Mod, IFallout4ModGetter>(GameRelease.Fallout4)
            .WithTargetDataFolder(pluginDir)
            .WithLoadOrder(loadOrder)
            .Build();

        //env.LoadOrder.ListedOrder.ToList().ForEach(item => { Console.WriteLine("Load Order: "+item.FileName); });

        ILinkCache linkCache = env.LoadOrder.ToImmutableLinkCache();

        return (activeMod, linkCache);
    }
}