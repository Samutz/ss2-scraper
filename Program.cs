using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Oblivion;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Plugins.Order;
using Newtonsoft.Json;
using IniParser;
using IniParser.Model;

namespace SS2Scraper;

class Program
{
    static void Main(string[] args)
    {
        string? filePath = null;
        bool doMO2 = false;
        bool doLoadOrderMode = true;
        bool doSingleMode = false;
        bool doUnknowns = false;
        string modListPath = "";
        List<Export.ModMetadata> metadataCache = [];

        for (int i = 0; i < args.Length; i++)
        {
            string nextArg = i + 1 < args.Length ? args[i + 1] : "";

            switch (args[i].ToLower())
            {
                case "-mo2":
                    if (!File.Exists(nextArg) || !(Path.GetFileName(nextArg) == "modlist.txt")) throw new ArgumentException("-mo2 requires a valid path to modlist.txt as the next argument");
                    doMO2 = true;
                    modListPath = nextArg;
                    continue;
                case "-data":
                    // strip quotes and slahes in case of "C:\foobar\"
                    string inputPath = nextArg.Trim().Trim('"').TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    string fullPath = Path.GetFullPath(inputPath);
                    if (!File.Exists(fullPath) && !Directory.Exists(fullPath)) throw new ArgumentException("Plugin path is invalid");
                    filePath = fullPath;
                    continue;
                case "-lomode":
                    doLoadOrderMode = true;
                    doSingleMode = false;
                    continue;
                case "-singlemode":
                    doSingleMode = true;
                    doLoadOrderMode = false;
                    continue;
                case "-unknowns":
                    doUnknowns = true;
                    continue;
            }
        }

        List<string> modPaths = [];

        // dedicated log for stuff more important than the general output
        string logPath = "warnings.log";
        if (!File.Exists(logPath)) // check for local path when debugging first
        {
            string exePath = AppDomain.CurrentDomain.BaseDirectory;
            logPath = Path.Combine(exePath, logPath); // set to exe folder for published exe
        }

        string[] allowedExtensions = [".esm", ".esp", ".esl"];

        if (doMO2)
        {
            //Console.WriteLine($"Checking dir for enabled mods {modListPath}");
            List<string> enabledMods = [.. File.ReadLines(modListPath).Where(line => line.StartsWith('+'))];
            foreach (string mod in enabledMods)
            {
                string modName = mod.StartsWith('+') ? mod[1..] : mod;
                string modDir = Path.GetFullPath(Path.GetDirectoryName(modListPath) + "\\..\\..\\mods\\" + modName);
                string iniPath = Path.GetFullPath(modDir + "\\meta.ini");
                //Console.WriteLine($"Checking for {iniPath}");
                if (!File.Exists(iniPath)) continue;
                var iniParser = new FileIniDataParser();
                IniData data = iniParser.ReadFile(iniPath);
                int nexusId = int.Parse(data["General"]["modid"]);
                if (nexusId <= 0) continue;
                List<string> pluginFiles = [.. Directory.GetFiles(modDir).Where(file => allowedExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))];
                foreach (var pluginFile in pluginFiles)
                {
                    //Console.WriteLine($"Found meta.ini for {Path.GetFileName(pluginFile)}");
                    Export.ModMetadata metadata = new()
                    {
                        pluginFile = Path.GetFileName(pluginFile),
                        nexusId = nexusId,
                        name = modName,
                        version = data["General"]["version"]
                    };
                    metadataCache.Add(metadata);
                }
            }
        }

        if (Directory.Exists(filePath))
        {
            Console.WriteLine($"Directory mode: {filePath}");
            modPaths = [.. Directory.GetFiles(filePath).Where(file => allowedExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))];
        }
        else
        {
            throw new ArgumentException($"Invalid path: {filePath}");
        }

        if (doSingleMode)
        {
            Console.WriteLine($"Single mode: {filePath}");

            foreach (string modPath in modPaths)
            {
                string pluginFile = Path.GetFileName(modPath);
                Console.WriteLine($"Checking Plugin: {pluginFile}");

                (var mod, var linkCache) = LoadMod(modPath);

                if (mod is null || linkCache is null) continue;

                Export export = new(mod, linkCache, doUnknowns);
                var output = export.BuildOutput();
                output.name = pluginFile;
                output.metadata = metadataCache.FirstOrDefault(data => data.pluginFile == pluginFile);

                Console.WriteLine($"Total SS2 Items: {output.totalItems}");

                if (output.totalItems == 0) continue;

                if (!Directory.Exists(".\\json")) Directory.CreateDirectory(".\\json");
                var json = JsonConvert.SerializeObject(output);
                File.WriteAllText($".\\json\\{pluginFile}.json", json);
            }
        }

        if (doLoadOrderMode)
        {
            Console.WriteLine($"LO mode: {filePath}");

            var linkCache = LoadMods(filePath, modPaths);

            foreach (string modPath in modPaths)
            {
                string pluginDir = Path.GetDirectoryName(modPath) ?? throw new ArgumentException("Invalid plugin path");
                string pluginFile = Path.GetFileName(modPath) ?? throw new ArgumentException("Invalid plugin path");
                var activeMod = Fallout4Mod.CreateFromBinaryOverlay(Path.Combine(pluginDir, pluginFile), Fallout4Release.Fallout4);
                
                Console.WriteLine($"Checking Plugin: {pluginFile}");

                Export export = new(activeMod, linkCache, doUnknowns);

                var output = export.BuildOutput();
                output.name = pluginFile;
                output.metadata = metadataCache.FirstOrDefault(data => data.pluginFile == pluginFile);

                Console.WriteLine($"Total SS2 Items: {output.totalItems}");

                if (output.totalItems == 0)
                {
                    Console.WriteLine("No SS2 items found in this plugin. Skipping json export.");
                    continue;
                }

                if (!Directory.Exists(".\\json")) Directory.CreateDirectory(".\\json");
                var json = JsonConvert.SerializeObject(output);
                File.WriteAllText($".\\json\\{pluginFile}.json", json);
            }
        }
        
    }

    private static void Log(string message)
    {
        string logPath = "log.txt";
        File.AppendAllText(logPath, $"{DateTime.Now}: {message}" + Environment.NewLine);
    }

    private static ILinkCache LoadMods(string pluginDir, List<string> plugins)
    {
        var listings = new List<LoadOrderListing>();
        foreach (string plugin in plugins)
        {
            listings.Add(new(ModKey.FromFileName(Path.GetFileName(plugin)), enabled: true));
        }
        listings = [.. listings.Distinct()];

        var loadOrder = LoadOrder.Import<IFallout4ModGetter>(listings, GameRelease.Fallout4);

        var env = GameEnvironment.Typical.Builder<IFallout4Mod, IFallout4ModGetter>(GameRelease.Fallout4)
            .WithTargetDataFolder(pluginDir)
            .WithLoadOrder(loadOrder)
            .Build();

        ILinkCache linkCache = env.LoadOrder.ToImmutableLinkCache();
        
        return linkCache;
    }

    private static (IFallout4ModDisposableGetter?, ILinkCache?) LoadMod(string pluginFullpath)
    {
        string pluginDir = Path.GetDirectoryName(pluginFullpath) ?? throw new ArgumentException("Invalid plugin path");
        string pluginFile = Path.GetFileName(pluginFullpath) ?? throw new ArgumentException("Invalid plugin path");

        // Console.WriteLine("Loading " + pluginFile);

        var activeMod = Fallout4Mod.CreateFromBinaryOverlay(Path.Combine(pluginDir, pluginFile), Fallout4Release.Fallout4);

        if (!activeMod.ModHeader.MasterReferences.Any(m => m.Master.FileName == "SS2.esm") && pluginFile != "SS2.esm")
        {
            Console.WriteLine("Plugin does not require SS2.esm, skipping.");
            return (null, null);
        }

        // force load DLCs as some addons (even SS2.esm) reference them without having them as master
        List<string> dlcs = [
            "DLCRobot.esm",
            "DLCCoast.esm",
            "DLCNukaWorld.esm",
            "DLCworkshop01.esm",
            "DLCworkshop02.esm",
            "DLCworkshop03.esm",
        ];

        var listings = new List<LoadOrderListing>();
        foreach (var dlc in dlcs) listings.Add(new(ModKey.FromFileName(dlc), enabled: true));
        foreach (var masterFile in activeMod.ModHeader.MasterReferences)
        {
            string masterPath = Path.Combine(pluginDir, masterFile.Master.FileName);
            if (!File.Exists(masterPath))
            {
                Console.WriteLine("Could not find required master: " + masterPath);
                Log($"Missing master '{masterPath}' for plugin '{pluginFile}'");
                return (null, null);
            }
            listings.Add(new(ModKey.FromFileName(masterFile.Master.FileName), enabled: true));
        }
        listings.Add(new(ModKey.FromFileName(pluginFile), enabled: true));

        listings = [.. listings.Distinct()];

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