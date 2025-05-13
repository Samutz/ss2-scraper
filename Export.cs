namespace SS2Scraper;

using System;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Strings;
using Noggog;

public class Export(IFallout4ModDisposableGetter mod, ILinkCache linkCache)
{
    private readonly IFallout4ModDisposableGetter mod = mod;
    private readonly ILinkCache linkCache = linkCache;

    public class ModMetadata
    {
        public string pluginFile = "";
        public int nexusId = 0;
        public string name = "";
        public string version = "";
    }

    public class CobjCache
    {
        List<IConstructibleObjectGetter> foundations = [];
    }

    public class Output
    {
        public string name = "";
        public bool isMaster;
        public bool isLight;
        public List<string> masters = [];
        public int totalItems = 0;
        public ModMetadata? metadata;
        public List<BuildingPlan> buildingPlans = [];
        public List<BuildingPlanSkin> buildingPlanSkins = [];
        public List<SimpleObject> dynamicFlags = [];
        public List<SimpleObject> foundations = [];
        public List<SimpleObject> powerPoles = [];
        public List<FurnitureStoreItem> furnitureStoreItems = [];
        public List<LeaderCard> leaderCards = [];
        public List<SimpleObject> petStoreCreatures = [];
        public List<UnlockableCharacter> unlockableCharacters = [];
        public List<SimpleObject> beerRecipes = [];
        public List<CityPlan> cityPlans = [];
        public List<WorldRepopulationCell> worldRepopCells = [];
        public List<SimpleObject> hqRoomConfigs = [];
        public List<SimpleObject> hqRoomUpgrades = [];
    }

    public class SimpleObject
    {
        public string formKey = "";
        public string editorId = "";
        public string name = "";
        public string description = "";
    }

    public class BuildingPlan : SimpleObject
    {
        public UnlockableRequirements? requirements;
        public bool isPlayerSelectOnly = false;
        public string type = "";
        public string typeSubClass = "";
        public string size = "";
        public List<string> tags = [];
        public List<BuildingLevelPlan> levelPlans = [];
        public int maxLevel = 3;
        public int maxOccupants = 1;
    }

    public class UnlockableRequirements
    {
        public bool hasRequirements = false;
        public List<string> plugins = [];
        public bool hasOtherRequirements = false;
    }

    public class BuildingLevelPlan : SimpleObject
    {
        public int level = 1;
        public int maxOccupants = 1;
        public List<string> jobTitles = [];
        public List<string> jobUniform = [];
    }

    public class BuildingPlanSkin : SimpleObject
    {
        public string targetPlan = "";
        public List<SimpleObject> levelSkins = [];
        public bool isPlayerSelectOnly = false;
        public List<string> tags = [];
    }

    public class LeaderCard : SimpleObject
    {
        public SimpleObject? majorTrait;
        public List<SimpleObject> minorTraits = [];
        public List<SimpleObject> weaknesses = [];
    }

    public class UnlockableCharacter : SimpleObject
    {
        public int[] special = [-1, -1, -1, -1, -1, -1, -1];
    }

    public class DynamicFlag : SimpleObject
    {
        public UnlockableRequirements? requirements;
        public bool hasFlagWaving = false;
        public bool hasFlagDown = false;
        public bool hasFlagWall = false;
        public bool hasFlagHalfCircleFlag01 = false;
        public bool hasFlagHalfCircleFlag02 = false;
        public bool hasFlagBannerTownStatic = false;
        public bool hasFlagBannerTownTorn = false;
        public bool hasFlagBannerTownTornWaving = false;
    }

    public class CityPlan : SimpleObject
    {
        public int maxLevel = 1;
        public bool isPlayerSelectOnly = false;
        public List<int> supportedNPCs = [0];
        public string targetSettlement = "";
        public List<string> plugins = [];
        public string author = "";
    }

    public class WorldRepopulationCell : SimpleObject
    {
        public int maxPopulation = 0;
        public string author = "";
    }

    public class FurnitureStoreItem : SimpleObject
    {
        public string workshopName = ""; // name as appears in workshop menu, might be different from shop inventory name
        public int vendorLevel = 1;
        public string type = "other";
    }

    public Output output = new();

    public Output BuildOutput()
    {
        TranslatedString.DefaultLanguage = Language.English;
        
        output = new()
        {
            isMaster = GetIsMaster(),
            isLight = GetIsLight(),
            masters = GetMasters(),
        };

        CacheConstructibleObjects();
        IndexAddonItems();

        return output;
    }

    public bool GetIsMaster() 
    { 
        // Console.WriteLine($"Has ESM Flag: {mod.IsMaster}");
        return mod.IsMaster; 
    }
    public bool GetIsLight() 
    { 
        // Console.WriteLine($"Has ESL Flag: {mod.IsSmallMaster}");
        return mod.IsSmallMaster; 
    }
    
    public List<string> GetMasters()
    {
        List<string> masters = [.. mod.MasterReferences.Select(master => master.Master.FileName.String)];
        // masters.ForEach(master => Console.WriteLine($"Uses master: {master}"));
        return masters;
    }

    public List<FormKey?> GetAddonConfigFormKeys()
    {
        List<FormKey?> addonConfigKeys = [];

        foreach (var quest in mod.Quests)
        {
            if (
                quest.Data is null
                || !quest.Data.Flags.HasFlag(Quest.Flag.StartGameEnabled)
            ) continue;

            var script = GetScript(quest, "SimSettlementsV2:quests:AddonPack");
            if (script is not null) 
            {
                var property = GetScriptProperty(script, "MyAddonConfig");
                addonConfigKeys.Add((property as ScriptObjectProperty)?.Object.FormKey);
            }
        }

        return [.. addonConfigKeys.Distinct()];
    }

    public List<FormKey?> GetAddonFormListFormKeys()
    {
        var addonConfigKeys = GetAddonConfigFormKeys();

        List<FormKey?> formListKeys = [];

        foreach (var formKey in addonConfigKeys)
        {
            if (
                formKey is null
                || !linkCache.TryResolve<IMiscItemGetter>(formKey.Value, out var miscItem)
            ) continue;

            // Console.WriteLine($"Found AddonConfig: {miscItem.EditorID}");

            var script = GetScript(miscItem, "SimSettlementsV2:MiscObjects:AddonPackConfiguration");
            if (script is not null) 
            {
                (GetScriptProperty(script, "MyItems") as ScriptObjectListProperty)?.Objects.ForEach(obj => {
                    formListKeys.Add(obj.Object.FormKey);
                });
            }
        }

        return [.. formListKeys.Distinct()];
    }

    public List<FormKey?> GetAddonItemFormKeys()
    {
        var formListKeys = GetAddonFormListFormKeys();

        List<FormKey?> addonItemKeys = [];

        foreach (var formKey in formListKeys)
        {
            if (
                formKey is null
                || !linkCache.TryResolve<IFormListGetter>(formKey.Value, out var formList)
            ) continue;
            
            // Console.WriteLine($"Found AddonItem FormList: {formList.EditorID} with {formList.Items.Count} items");

            if (
                formList is null 
                || formList.Items is null 
                || formList.Items.Count == 0
            ) continue;

            foreach (var key in formList.Items)
            {
                if (key is null) continue;
                addonItemKeys.Add(key.FormKey);
            }
        }

        return [.. addonItemKeys.Distinct()];
    }

    public void IndexAddonItems()
    {
        var itemKeys = GetAddonItemFormKeys();

        foreach (var formKey in itemKeys)
        {
            if (formKey is null) continue;
            if (linkCache.TryResolve<IKeywordGetter>(formKey.Value, out var keyword)) continue; // skip keywords
            if (linkCache.TryResolve<IStaticCollectionGetter>(formKey.Value, out var scol)) continue; // skip SCOLs
            if (linkCache.TryResolve<IStaticGetter>(formKey.Value, out var staticObj)) continue; // skip statics
            if (linkCache.TryResolve<IPlacedObjectGetter>(formKey.Value, out var objRef)) continue; // skip objectrefs
            if (linkCache.TryResolve<IGlobalGetter>(formKey.Value, out var globalVar)) continue; // skip globals

            if (linkCache.TryResolve<IMiscItemGetter>(formKey.Value, out var miscItem))
            {
                IndexMiscItem(miscItem);
                continue;
            }

            if (linkCache.TryResolve<IWeaponGetter>(formKey.Value, out var weapon))
            {
                IndexWeapon(weapon);
                continue;
            }

            if (linkCache.TryResolve<IArmorGetter>(formKey.Value, out var armor))
            {
                IndexArmor(armor);
                continue;
            }

            if (linkCache.TryResolve<IActivatorGetter>(formKey.Value, out var activator))
            {
                IndexActivator(activator);
                continue;
            }

            if (linkCache.TryResolve<IBookGetter>(formKey.Value, out var book))
            {
                IndexBook(book);
                continue;
            }

            if (linkCache.TryResolve<IQuestGetter>(formKey.Value, out var quest))
            {
                IndexQuest(quest);
                continue;
            }

            Console.WriteLine($"Found UNKNOWN AddonItem: {formKey}");
        }
    }

    private void CacheConstructibleObjects()
    {
        foreach (var cobj in mod.ConstructibleObjects)
        {
            if (cobj.CreatedObject is null) continue;
            foreach (var keyword in cobj.Categories ?? [])
            {
                switch (keyword.FormKey.ToString().ToLower())
                {
                    case "018503:SS2.esm": // foundation recipe filter
                        continue;

                    case "00EE13:SS2.esm": // terraformer recipe filter
                        continue;

                    case "00F3C9:SS2.esm": // plot spacer recipe filter
                        continue;
                    
                    case "0219AC:SS2.esm": // resource storage recipe filter
                        continue;

                    case "01457B:SS2.esm": // furniture recipe filter
                        continue;

                    case "018B01:SS2.esm": // power recipe filter
                        continue;

                    case "00F3CA:SS2.esm": // stairs recipe filter
                        continue;

                    default:
                        continue;
                }
            }
        }
    }

    private void IndexBook(IBookGetter record)
    {
        if (
            record.VirtualMachineAdapter is null
            || record.VirtualMachineAdapter.Scripts.Count == 0
        ) return;

        foreach (var script in record.VirtualMachineAdapter.Scripts)
        {
            if (script is null) continue;

            switch(script.Name.ToLower())
            {
                case "simsettlementsv2:books:beerrecipe":
                    IndexSimpleObject(record, output.beerRecipes);
                    continue;

                // skip
                case "simsettlementsv2:books:beerrecipeobjectref":
                case "simsettlementsv2:books:newsarticle":
                case "simsettlementsv2:books:magazineissue":
                case "simsettlementsv2:objectreferences:newspaper":
                case "simsettlementsv2:objectreferences:magazine":
                case "simsettlementsv2:books:inboxitem":
                case "magstagescript":
                    continue;

                default:
                    Console.WriteLine($"Found AddonItem UNKNOWN Book: {record.EditorID} ({script.Name})");
                    continue;
            }
        }
    }

    private void IndexActivator(IActivatorGetter record)
    {
        if (
            record.VirtualMachineAdapter is null
            || record.VirtualMachineAdapter.Scripts.Count == 0
        ) return;

        foreach (var script in record.VirtualMachineAdapter.Scripts)
        {
            if (script is null) continue;

            switch(script.Name.ToLower())
            {
                // skip
                case "simsettlementsv2:objectreferences:subspawner":
                    continue;

                default:
                    Console.WriteLine($"Found AddonItem UNKNOWN Activator: {record.EditorID} ({script.Name})");
                    continue;
            }
        }
    }

    private void IndexQuest(IQuestGetter record)
    {
        if (
            record.VirtualMachineAdapter is null
            || record.VirtualMachineAdapter.Scripts.Count == 0
        ) return;

        foreach (var script in record.VirtualMachineAdapter.Scripts)
        {
            if (script is null) continue;
            if (script.Name.StartsWith("simsettlementsv2:quests:disasterquests:", StringComparison.CurrentCultureIgnoreCase)) continue;

            switch(script.Name.ToLower())
            {
                default:
                    Console.WriteLine($"Found AddonItem UNKNOWN Quest: {record.EditorID} ({script.Name})");
                    continue;
            }
        }
    }
    
    private void IndexMiscItem(IMiscItemGetter record)
    {
        if (
            record.VirtualMachineAdapter is null
            || record.VirtualMachineAdapter.Scripts.Count == 0
        ) return;

        foreach (var script in record.VirtualMachineAdapter.Scripts)
        {
            if (script is null) continue;

            switch(script.Name.ToLower())
            {
                case "simsettlementsv2:miscobjects:unlockablebuildingplan":
                    IndexUnlockableBuildingPlan(record);
                    continue;

                case "simsettlementsv2:miscobjects:foundation":
                    IndexSimpleObject(record, output.foundations);
                    continue;

                case "simsettlementsv2:miscobjects:powerpole":
                    IndexSimpleObject(record, output.powerPoles);
                    continue;

                case "simsettlementsv2:miscobjects:furniturestoreitem":
                    IndexFurnitureStoreItem(record);
                    continue;

                case "simsettlementsv2:miscobjects:petstorecreatureitem":
                    IndexSimpleObject(record, output.petStoreCreatures);
                    continue;

                case "simsettlementsv2:miscobjects:unlockablecharacter":
                    IndexUnlockableCharacter(record);
                    continue;

                case "simsettlementsv2:miscobjects:unlockableflag":
                    IndexUnlockableFlag(record);
                    continue;

                case "simsettlementsv2:weapons:cityplan":
                    IndexCityPlan(record);
                    continue;

                case "simsettlementsv2:miscobjects:worldrepopulationcell":
                    IndexWorldPopulationCell(record);
                    continue;

                case "simsettlementsv2:hq:library:miscobjects:requirementtypes:actiontypes:hqroomconfig":
                    IndexSimpleObject(record, output.hqRoomConfigs);
                    continue;
                
                case "simsettlementsv2:hq:baseactiontypes:hqroomupgrade":
                    IndexSimpleObject(record, output.hqRoomUpgrades);
                    continue;

                // skip
                case "simsettlementsv2:miscobjects:settlerlocationdiscovery":
                case "simsettlementsv2:miscobjects:npcpreferences":
                case "simsettlementsv2:miscobjects:unlockable": 
                case "simsettlementsv2:miscobjects:unlockablebuildingclass":
                case "simsettlementsv2:miscobjects:unlockableterritory":
                case "simsettlementsv2:miscobjects:territorytrait":
                case "simsettlementsv2:miscobjects:ideologychoice":
                case "simsettlementsv2:miscobjects:worldspaceconfig":
                case "simsettlementsv2:miscobjects:factionname":
                case "simsettlementsv2:objectreferences:petstorecreatureitemref":
                case "simsettlementsv2:hq:library:miscobjects:hqdepartmentplan":
                case "simsettlementsv2:hq:gnn:hqresearchfactionuniforms":
                case "simsettlementsv2:miscobjects:mqbattleally":
                case "simsettlementsv2:miscobjects:advisorreactions":
                case "simsettlementsv2:miscobjects:advisordefinition":
                case "simsettlementsv2:hq:baseactiontypes:departmentmanagedhqresearch":
                case "simsettlementsv2:hq:baseactiontypes:hqstafftraining":
                case "simsettlementsv2:hq:baseactiontypes:hqpolicy":
                case "simsettlementsv2:hq:baseactiontypes:hqscoutlocation":
                case "vfx:miscobjects:universalunlockable":
                case "ss2jampads2:miscobjects:universalunlockable":
                case "workshopframework:library:objectrefs:preventdroppingonground":
                    continue; 

                default:
                    Console.WriteLine($"Found AddonItem UNKNOWN MiscItem: {record.EditorID} ({script.Name})");
                    continue;
            }
        }
    }

    private void IndexArmor(IArmorGetter record)
    {
        if (
            record.VirtualMachineAdapter is null
            || record.VirtualMachineAdapter.Scripts.Count == 0
        ) return;

        foreach (var script in record.VirtualMachineAdapter.Scripts)
        {
            if (script is null) continue;

            switch(script.Name.ToLower())
            {
                case "simsettlementsv2:armors:themedefinition_flags":
                    IndexDynamicFlag(record, new());
                    continue;

                // skip
                case "simsettlementsv2:armors:themedefinition_holiday": 
                case "simsettlementsv2:armors:npcunittype": 
                case "simsettlementsv2:armors:npcloadout": 
                case "simsettlementsv2:armors:soldierrank": 
                case "simsettlementsv2:armors:uniform": 
                case "simsettlementsv2:armors:factiondefinition":
                    continue;

                default:
                    Console.WriteLine($"Found AddonItem UNKNOWN Armor: {record.EditorID} ({script.Name})");
                    continue;
            }
        }
    }

    public void IndexWeapon(IWeaponGetter record)
    {
        if (
            record.VirtualMachineAdapter is null
            || record.VirtualMachineAdapter.Scripts.Count == 0
        ) return;

        foreach (var script in record.VirtualMachineAdapter.Scripts)
        {
            if (script is null) continue;

            switch(script.Name.ToLower())
            {
                case "simsettlementsv2:weapons:buildingplan":
                    IndexBuildingPlan(record, new());
                    continue;

                case "simsettlementsv2:weapons:buildingskin":
                    IndexBuildingSkin(record);
                    continue;

                case "simsettlementsv2:weapons:leadercard":
                    IndexLeaderCard(record);
                    continue;

                default:
                    Console.WriteLine($"Found AddonItem UNKNOWN Weapon: {record.EditorID} ({script.Name})");
                    continue;
            }
        }
    }

    private void IndexSimpleObject(IArmorGetter record, List<SimpleObject> targetList)
    {
        SimpleObject obj = new()
        {
            formKey = record.FormKey.ToString(),
            editorId = record.EditorID?.ToString() ?? "",
            name = record.Name?.ToString() ?? ""
        };
        targetList.Add(obj);
        output.totalItems++;
    }

    private void IndexSimpleObject(IMiscItemGetter record, List<SimpleObject> targetList)
    {
        SimpleObject obj = new()
        {
            formKey = record.FormKey.ToString(),
            editorId = record.EditorID?.ToString() ?? "",
            name = record.Name?.ToString() ?? ""
        };
        targetList.Add(obj);
        output.totalItems++;
    }

    private void IndexSimpleObject(IBookGetter record, List<SimpleObject> targetList)
    {
        SimpleObject obj = new()
        {
            formKey = record.FormKey.ToString(),
            editorId = record.EditorID?.ToString() ?? "",
            name = record.Name?.ToString() ?? ""
        };
        targetList.Add(obj);
        output.totalItems++;
    }

    private void IndexFurnitureStoreItem(IMiscItemGetter record)
    {
        var cobjs = mod.ConstructibleObjects
            .Where(co => co.Components?[0].Component.FormKey == record.FormKey && co.Components?[0].Count == 1);

        if (cobjs is null || !cobjs.Any()) return;
        var cobj = cobjs?.First();

        FurnitureStoreItem storeItem = new()
        {
            formKey = record.FormKey.ToString(),
            editorId = record.EditorID?.ToString() ?? "",
            name = record.Name?.ToString() ?? "", // shop item name
            description = cobj?.Description?.ToString() ?? "",
        };

        var script = GetScript(record, "SimSettlementsV2:MiscObjects:FurnitureStoreItem");

        if (script is not null)
        {
            var iVendorLevel = GetScriptProperty(script, "iVendorLevel") as ScriptIntProperty;
            if (iVendorLevel?.Data is not null) storeItem.vendorLevel = iVendorLevel.Data;
        }

        // actual furniture
        if (cobj?.CreatedObject.FormKey is not null && linkCache.TryResolve<IFurnitureGetter>(cobj.CreatedObject.FormKey, out var furniture))
        {
            storeItem.workshopName = furniture.Name?.ToString() ?? "";
            storeItem.type = "furniture_other";

            if (furniture.HasKeyword(FormKey.Factory("021B18:Fallout4.esm"))) // sleep anim keyword
            {
                storeItem.type = "furniture_bed";
            }

            if (furniture.HasAnyKeyword([ FormKey.Factory("030BB2:Fallout4.esm"), FormKey.Factory("1338F7:Fallout4.esm") ])) // chair anim keywords
            {
                storeItem.type = "furniture_chair";
            }

            if (furniture.HasAnyKeyword([ FormKey.Factory("014576:SS2.esm"), FormKey.Factory("020320:SS2.esm") ])) // city planner desk keywords
            {
                storeItem.type = "furniture_city_planner_desk";
            }
        }

        // static
        if (cobj?.CreatedObject.FormKey is not null && linkCache.TryResolve<IStaticGetter>(cobj.CreatedObject.FormKey, out var stat))
        {
            storeItem.workshopName = stat.Name?.ToString() ?? "";
            storeItem.type = "static";
        }
        
        // activator
        if (cobj?.CreatedObject.FormKey is not null && linkCache.TryResolve<IActivatorGetter>(cobj.CreatedObject.FormKey, out var activator))
        {
            storeItem.workshopName = activator.Name?.ToString() ?? "";
            storeItem.type = "activator";
        }

        // light
        if (cobj?.CreatedObject.FormKey is not null && linkCache.TryResolve<ILightGetter>(cobj.CreatedObject.FormKey, out var light))
        {
            storeItem.workshopName = light.Name?.ToString() ?? "";
            storeItem.type = "light";
        }

        output.furnitureStoreItems.Add(storeItem);
        output.totalItems++;
    }

    private void IndexWorldPopulationCell(IMiscItemGetter record)
    {
        var script = GetScript(record, "SimSettlementsV2:MiscObjects:WorldRepopulationCell");
        if (script is null) return;

        WorldRepopulationCell cell = new()
        {
            formKey = record.FormKey.ToString(),
            editorId = record.EditorID?.ToString() ?? "",
            name = record.Name?.ToString() ?? ""
        };

        var iPopulationSupported = GetScriptProperty(script, "iPopulationSupported") as ScriptIntProperty;
        cell.maxPopulation = iPopulationSupported?.Data ?? 0;

        var author = GetScriptProperty(script, "DesignerNameForm") as ScriptObjectProperty;
        if (author?.Object.FormKey is not null && linkCache.TryResolve<IMiscItemGetter>(author.Object.FormKey, out var miscItem1))
        {
            cell.author = miscItem1.Name?.ToString() ?? "";
        }

        output.worldRepopCells.Add(cell);
        output.totalItems++;
    }

    private void IndexCityPlan(IMiscItemGetter record)
    {
        var script = GetScript(record, "SimSettlementsV2:Weapons:CityPlan");
        if (script is null) return;

        CityPlan plan = new()
        {
            formKey = record.FormKey.ToString(),
            editorId = record.EditorID?.ToString() ?? "",
            name = record.Name?.ToString() ?? ""
        };

        var maxLevel = GetScriptProperty(script, "iLevelCount") as ScriptIntProperty;
        plan.maxLevel = maxLevel?.Data ?? 1;

        var isPlayerSelectOnly = GetScriptProperty(script, "bPlayerSelectOnly") as ScriptBoolProperty;
        plan.isPlayerSelectOnly = isPlayerSelectOnly?.Data ?? false;

        var supportedNPCs = GetScriptProperty(script, "iSupportedNPCs") as ScriptIntListProperty;
        plan.supportedNPCs = supportedNPCs?.Data ?? [0];
        
        var workshopRef = GetScriptProperty(script, "workshopRef") as ScriptStructProperty;
        if (workshopRef is not null) plan.targetSettlement = GetCityPlanSettlementEditorID(workshopRef);

        var plugins = GetScriptProperty(script, "sPluginsUsed") as ScriptStringListProperty;
        plan.plugins = plugins?.Data ?? [];

        var author = GetScriptProperty(script, "DesignerNameHolder") as ScriptObjectProperty;
        if (author?.Object.FormKey is not null && linkCache.TryResolve<IMiscItemGetter>(author.Object.FormKey, out var miscItem1))
        {
            plan.author = miscItem1.Name?.ToString() ?? "";
        }

        var description = GetScriptProperty(script, "DesignersDescription") as ScriptObjectProperty;
        if (description?.Object.FormKey is not null && linkCache.TryResolve<IMessageGetter>(description.Object.FormKey, out var miscItem2))
        {
            plan.description = miscItem2.Description.ToString() ?? "";
        }

        output.cityPlans.Add(plan);
        output.totalItems++;
    }

    private static string GetCityPlanSettlementEditorID(ScriptStructProperty workshopRef)
    {
        if (workshopRef?.Members.First() is null) return "Unknown";
        
        var pluginName = GetScriptProperty(workshopRef.Members.First(), "sPluginName") as ScriptStringProperty;
        var formIdInt = GetScriptProperty(workshopRef.Members.First(), "iFormID") as ScriptIntProperty;
        if (formIdInt?.Data is null || pluginName?.Data is null) return "Unknown";
        
        string formIdHex = string.Format("{0:X6}", formIdInt.Data);
        string workbenchKeyString = $"{formIdHex}:{pluginName.Data}";
        if (!FormKey.TryFactory(workbenchKeyString, out var workbenchKey)) return workbenchKeyString;

        return workbenchKey.ToString();
    }

    private void IndexUnlockableCharacter(IMiscItemGetter record)
    {
        var script = GetScript(record, "SimSettlementsV2:MiscObjects:UnlockableCharacter");
        if (script is null) return;

        var characterFormProp = GetScriptProperty(script, "CharacterForm") as ScriptStructProperty;
        if (characterFormProp?.Members.First().Properties.First() is not ScriptObjectProperty property1) return;
        if (!linkCache.TryResolve<INpcGetter>(property1.Object.FormKey, out var actor)) return;
        if (actor is null) return;

        UnlockableCharacter character = new()
        {
            formKey = record.FormKey.ToString(),
            editorId = record.EditorID?.ToString() ?? "",
            name = actor.Name?.ToString() ?? "",
        };

        foreach (var prop in actor.Properties ?? [])
        {
            // not gonna bother looking up their forms
            switch (prop.ActorValue.FormKey.ToString())
            {
                case "0002C2:Fallout4.esm": // Strength
                    character.special[0] = (int) prop.Value;
                    continue;
                case "0002C3:Fallout4.esm": // Perception
                    character.special[1] = (int) prop.Value;
                    continue;
                case "0002C4:Fallout4.esm": // Endurace
                    character.special[2] = (int) prop.Value;
                    continue;
                case "0002C5:Fallout4.esm": // Charisma
                    character.special[3] = (int) prop.Value;
                    continue;
                case "0002C6:Fallout4.esm": // Intelligence
                    character.special[4] = (int) prop.Value;
                    continue;
                case "0002C7:Fallout4.esm": // Agility
                    character.special[5] = (int) prop.Value;
                    continue;
                case "0002C8:Fallout4.esm": // Luck
                    character.special[6] = (int) prop.Value;
                    continue;
            }
        }

        output.unlockableCharacters.Add(character);
        output.totalItems++;
    }

    private void IndexLeaderCard(IWeaponGetter record)
    {
        LeaderCard leaderCard = new()
        {
            formKey = record.FormKey.ToString(),
            editorId = record.EditorID?.ToString() ?? "",
            name = record.Name?.ToString() ?? ""
        };

        var script = GetScript(record, "SimSettlementsV2:Weapons:LeaderCard");
        if (script is not null)
        {
            var majorTraitProp = GetScriptProperty(script, "MajorTrait") as ScriptStructProperty;
            if (majorTraitProp?.Members.First().Properties.First() as ScriptObjectProperty is not null)
            {
                var property1 = majorTraitProp?.Members.First().Properties.First() as ScriptObjectProperty;
                if (property1 is not null)
                { 
                    if (linkCache.TryResolve<IMiscItemGetter>(property1.Object.FormKey, out var record1))
                    {
                        leaderCard.majorTrait = GetLeaderTraitInfo(record1);
                    }
                }
            }

            var minorTraitProp = GetScriptProperty(script, "MinorTraits") as ScriptStructListProperty;
            foreach (var struct1 in minorTraitProp?.Structs ?? [])
            {
                foreach (var member1 in struct1.Members)
                {
                    if (member1 is not null && member1 is ScriptObjectProperty)
                    {
                        if (linkCache.TryResolve<IMiscItemGetter>((member1 as ScriptObjectProperty ?? new()).Object.FormKey, out var record1))
                        {
                            leaderCard.minorTraits.Add(GetLeaderTraitInfo(record1));
                        }
                    }
                }
            }

            var weaknessProp = GetScriptProperty(script, "Weaknesses") as ScriptStructListProperty;
            foreach (var struct1 in weaknessProp?.Structs ?? [])
            {
                foreach (var member1 in struct1.Members)
                {
                    if (member1 is not null && member1 is ScriptObjectProperty)
                    {
                        if (linkCache.TryResolve<IMiscItemGetter>((member1 as ScriptObjectProperty ?? new()).Object.FormKey, out var record1))
                        {
                            leaderCard.weaknesses.Add(GetLeaderTraitInfo(record1));
                        }
                    }
                }
            }
        }

        output.leaderCards.Add(leaderCard);
        output.totalItems++;
    }

    private SimpleObject GetLeaderTraitInfo(IMiscItemGetter record)
    {
        SimpleObject trait = new()
        {
            formKey = record.FormKey.ToString(),
            editorId = record.EditorID?.ToString() ?? "",
            name = record.Name?.ToString() ?? ""
        };

        var script = GetScript(record, "SimSettlementsV2:MiscObjects:LeaderTrait");
        if (script is not null)
        {
            var descFormKey = (GetScriptProperty(script, "TraitDescriptionHolder") as ScriptObjectProperty)?.Object.FormKey;
            if (descFormKey is not null && linkCache.TryResolve<IMiscItemGetter>(descFormKey.Value, out var miscItem))
            {
                trait.description = miscItem.Name?.ToString() ?? "";
            }
        }

        return trait;
    }

    private void IndexBuildingSkin(IWeaponGetter record)
    {
        var script = GetScript(record, "SimSettlementsV2:Weapons:BuildingSkin");
        if (script is not null)
        {
            var buildingPlan = GetScriptProperty(script, "TargetBuildingPlan") as ScriptObjectProperty;
            var planKey = buildingPlan?.Object.FormKey;

            if (planKey is null) return;

            BuildingPlanSkin skin = new()
            {
                formKey = record.FormKey.ToString(),
                editorId = record.EditorID?.ToString() ?? "",
                targetPlan = planKey?.ToString() ?? "",
                name = record.Name?.ToString() ?? "",
            };

            var isPlayerSelectOnly = GetScriptProperty(script, "bPlayerSelectOnly") as ScriptBoolProperty;
            skin.isPlayerSelectOnly = isPlayerSelectOnly?.Data ?? false;

            var descFormKey = (GetScriptProperty(script, "BuildingPlanSkinDescription") as ScriptObjectProperty)?.Object.FormKey;
            if (descFormKey is not null && linkCache.TryResolve<IWeaponModificationGetter>(descFormKey.Value, out var omod))
            {
                skin.description = omod.Description?.ToString() ?? "";
            }

            var levelSkins = (GetScriptProperty(script, "LevelSkins") as ScriptObjectListProperty)?.Objects ?? [];
            foreach (var levelKey in levelSkins)
            {
                if (!linkCache.TryResolve<IWeaponGetter>(levelKey.Object.FormKey, out var levelSkin)) continue;
                SimpleObject newLevelSkin = new()
                {
                    formKey = levelSkin.FormKey.ToString(),
                    editorId = levelSkin.EditorID?.ToString() ?? "",
                    name = levelSkin.Name?.ToString() ?? "",
                };
                if (levelSkin?.Name?.String is not null) skin.levelSkins.Add(newLevelSkin);
            }

            // keywords
            if (record.Keywords is not null && record.Keywords.Count > 0)
            {
                foreach(var keyword in record.Keywords)
                {
                    if (!linkCache.TryResolve<IKeywordGetter>(keyword.FormKey, out var keywordKey)) continue;
                    if (keywordKey.EditorID?.StartsWith("SS2_ThemeTag_") ?? false) skin.tags.Add(keywordKey.EditorID[13..]);
                }
            }

            output.buildingPlanSkins.Add(skin);
            output.totalItems++;
        }
    }

    private UnlockableRequirements GetUnlockableRequirements(IScriptEntryGetter script)
    {
        UnlockableRequirements requirements = new();

        var reqProp = GetScriptProperty(script, "Requirements") as ScriptObjectProperty;
        var reqKey = reqProp?.Object.FormKey;

        if (reqKey.HasValue && linkCache.TryResolve<IMiscItemGetter>(reqKey.Value, out var reqMiscItem))
        {
            requirements.hasRequirements = true;

            var script2 = GetScript(reqMiscItem, "SimSettlementsV2:MiscObjects:UsageRequirements");
            if (script2 is not null)
            {
                var structs = GetScriptProperty(script2, "RequiredPlugins") as ScriptStructListProperty;
                if (structs?.Structs.Count > 0)
                {
                    foreach (var struct1 in structs.Structs)
                    {
                        foreach (var member in struct1.Members)
                        {
                            if (member is not null && member is ScriptStringProperty)
                            {
                                ScriptStringProperty member2 = member as ScriptStringProperty ?? new();
                                requirements.plugins.Add(member2.Data);
                            }
                        }
                    }
                }
            }

            if ((script2?.Properties.Count > 1 && requirements.plugins.Count > 0) || (script2?.Properties.Count == 1 && requirements.plugins.Count == 0))
            {
                requirements.hasOtherRequirements = true;
            }
        }

        return requirements;
    }

    private void IndexUnlockableFlag(IMiscItemGetter record)
    {
        var script = GetScript(record, "SimSettlementsV2:MiscObjects:UnlockableFlag");
        if (script is not null)
        {
            var buildingPlan = GetScriptProperty(script, "FlagThemeDefinition") as ScriptObjectProperty;
            var planKey = buildingPlan?.Object.FormKey;
            
            if (!planKey.HasValue || !linkCache.TryResolve<IArmorGetter>(planKey.Value, out var armor)) return;

            UnlockableRequirements requirements = GetUnlockableRequirements(script);

            IndexDynamicFlag(armor, requirements);
        }
    }

    private void IndexDynamicFlag(IArmorGetter record, UnlockableRequirements requirements)
    {
        DynamicFlag flag = new()
        {
            formKey = record.FormKey.ToString(),
            editorId = record.EditorID?.ToString() ?? "",
            name = record.Name?.ToString() ?? "",
            requirements = requirements
        };

        var script = GetScript(record, "SimSettlementsV2:Armors:ThemeDefinition_Flags");
        if (script is not null)
        {
            flag.hasFlagWaving = GetScriptProperty(script, "FlagWaving") as ScriptObjectProperty is not null;
            flag.hasFlagDown = GetScriptProperty(script, "FlagDown") as ScriptObjectProperty is not null;
            flag.hasFlagWall = GetScriptProperty(script, "FlagWall") as ScriptObjectProperty is not null;
            flag.hasFlagHalfCircleFlag01 = GetScriptProperty(script, "FlagHalfCircleFlag01") as ScriptObjectProperty is not null;
            flag.hasFlagHalfCircleFlag02  = GetScriptProperty(script, "FlagHalfCircleFlag02") as ScriptObjectProperty is not null;
            flag.hasFlagBannerTownStatic  = GetScriptProperty(script, "FlagBannerTownStatic") as ScriptObjectProperty is not null;
            flag.hasFlagBannerTownTorn  = GetScriptProperty(script, "FlagBannerTownTorn") as ScriptObjectProperty is not null;
            flag.hasFlagBannerTownTornWaving  = GetScriptProperty(script, "FlagBannerTownTornWaving") as ScriptObjectProperty is not null;
        }

        output.dynamicFlags.Add(flag);
        output.totalItems++;
    }

    private void IndexUnlockableBuildingPlan(IMiscItemGetter record)
    {
        var script = GetScript(record, "SimSettlementsV2:MiscObjects:UnlockableBuildingPlan");
        if (script is not null)
        {
            var buildingPlan = GetScriptProperty(script, "BuildingPlan") as ScriptObjectProperty;
            var planKey = buildingPlan?.Object.FormKey;
            
            if (!planKey.HasValue || !linkCache.TryResolve<IWeaponGetter>(planKey.Value, out var weapon)) return;

            UnlockableRequirements requirements = GetUnlockableRequirements(script);

            IndexBuildingPlan(weapon, requirements);
        }
    }

    private void IndexBuildingPlan(IWeaponGetter record, UnlockableRequirements requirements)
    {
        if (
            record.VirtualMachineAdapter is null
            || record.VirtualMachineAdapter.Scripts.Count == 0
        ) return;

        BuildingPlan buildingPlan = new()
        {
            formKey = record.FormKey.ToString(),
            editorId = record.EditorID?.ToString() ?? "",
            requirements = requirements,
            name = record.Name?.ToString() ?? ""
        };

        // keywords
        if (record.Keywords is not null && record.Keywords.Count > 0)
        {
            foreach(var keyword in record.Keywords)
            {
                if (!linkCache.TryResolve<IKeywordGetter>(keyword.FormKey, out var keywordKey)) continue;
                if (keywordKey.EditorID?.StartsWith("SS2_PlotType_") ?? false) buildingPlan.type = keywordKey.EditorID[13..];
                if (keywordKey.EditorID?.StartsWith("SS2_PlotTypeSubClass_") ?? false) buildingPlan.typeSubClass = keywordKey.EditorID[21..];
                if (keywordKey.EditorID?.StartsWith("SS2_PlotSize_") ?? false) buildingPlan.size = keywordKey.EditorID[13..];
                if (keywordKey.EditorID?.StartsWith("SS2_ThemeTag_") ?? false) buildingPlan.tags.Add(keywordKey.EditorID[13..]);
            }
        }

        // script properties
        var script = GetScript(record, "SimSettlementsV2:Weapons:BuildingPlan");
        if (script is not null)
        {
           buildingPlan.isPlayerSelectOnly = (GetScriptProperty(script, "bPlayerSelectOnly") as ScriptBoolProperty)?.Data ?? false;

            var descFormKey = (GetScriptProperty(script, "BuildingPlanDescription") as ScriptObjectProperty)?.Object.FormKey;
            if (descFormKey is not null && linkCache.TryResolve<IWeaponModificationGetter>(descFormKey.Value, out var omod))
            {
                buildingPlan.description = omod.Description?.ToString() ?? "";
            }

            var levelPlanListFormKey = (GetScriptProperty(script, "LevelPlansList") as ScriptObjectProperty)?.Object.FormKey;
            if (levelPlanListFormKey is not null && linkCache.TryResolve<IFormListGetter>(levelPlanListFormKey.Value, out var formList))
            {
                foreach (var item in formList.Items)
                {
                    if (!linkCache.TryResolve<IWeaponGetter>(item.FormKey, out var levelPlanWeapon)) continue;
                    var levelPlan = IndexBuildingLevelPlan(levelPlanWeapon);
                    if (levelPlan is not null)
                    {
                        buildingPlan.maxOccupants = (levelPlan.maxOccupants > buildingPlan.maxOccupants) ? levelPlan.maxOccupants : buildingPlan.maxOccupants;
                        buildingPlan.maxLevel = (levelPlan.level > buildingPlan.maxLevel) ? levelPlan.level : buildingPlan.maxLevel;
                        buildingPlan.levelPlans.Add(levelPlan);
                    }
                }
            }
        }

        output.buildingPlans.Add(buildingPlan);
        output.totalItems++;
    }

    private BuildingLevelPlan? IndexBuildingLevelPlan(IWeaponGetter record)
    {
        BuildingLevelPlan buildingLevelPlan = new()
        {
            formKey = record.FormKey.ToString(),
            editorId = record.EditorID?.ToString() ?? "",
            name = record.Name?.ToString() ?? ""
        };

        // Console.WriteLine($"Found AddonItem BuildingLevelPlan: {weapon.EditorID}");

        // script properties
        var script = GetScript(record, "SimSettlementsV2:Weapons:BuildingLevelPlan");
        if (script is not null)
        {
            buildingLevelPlan.maxOccupants = (GetScriptProperty(script, "iMaxOccupants") as ScriptIntProperty)?.Data ?? 1;
            buildingLevelPlan.level = (GetScriptProperty(script, "iRequiredLevel") as ScriptIntProperty)?.Data ?? 1;

            var jobTitles = (GetScriptProperty(script, "JobTitle") as ScriptObjectListProperty)?.Objects ?? [];
            foreach (var titleKey in jobTitles)
            {
                if (!linkCache.TryResolve<IMessageGetter>(titleKey.Object.FormKey, out var mesg)) continue;
                if (mesg?.Name?.String is not null) buildingLevelPlan.jobTitles.Add(mesg?.Name?.String ?? "");
            }

            var jobUniforms = (GetScriptProperty(script, "AutoEquip") as ScriptObjectListProperty)?.Objects ?? [];
            foreach (var uniformKey in jobUniforms)
            {
                if (linkCache.TryResolve<IFormListGetter>(uniformKey.Object.FormKey, out var formList))
                {
                    foreach (var item in formList.Items)
                    {
                        if (!linkCache.TryResolve<IMajorRecordGetter>(item.FormKey, out var itemForm)) continue;
                        if (itemForm is IWeaponGetter) 
                        {
                            var itemForm2 = itemForm as IWeaponGetter;
                            if (itemForm2?.Name?.String is not null) buildingLevelPlan.jobUniform.Add(itemForm2.Name.String ?? "");
                        }
                        if (itemForm is IArmorGetter) 
                        {
                            var itemForm2 = itemForm as IArmorGetter;
                            if (itemForm2?.Name?.String is not null) buildingLevelPlan.jobUniform.Add(itemForm2.Name.String ?? "");
                        }
                    }
                }
                else if (linkCache.TryResolve<IOutfitGetter>(uniformKey.Object.FormKey, out var outfit))
                {
                    if (outfit.Items is null) continue;
                    foreach (var item in outfit.Items)
                    {
                        if (!linkCache.TryResolve<IMajorRecordGetter>(item.FormKey, out var itemForm)) continue;
                        if (itemForm is IWeaponGetter) 
                        {
                            var itemForm2 = itemForm as IWeaponGetter;
                            if (itemForm2?.Name?.String is not null) buildingLevelPlan.jobUniform.Add(itemForm2.Name.String ?? "");
                        }
                        if (itemForm is IArmorGetter) 
                        {
                            var itemForm2 = itemForm as IArmorGetter;
                            if (itemForm2?.Name?.String is not null) buildingLevelPlan.jobUniform.Add(itemForm2.Name.String ?? "");
                        }
                    }
                }
            }
        }

        return buildingLevelPlan;
    }

    private static ScriptProperty? GetScriptProperty(IScriptEntryGetter script, string propertyName)
    {
        foreach (var property in script.Properties)
        {
            if (property is null) continue;
            if (property.Name==propertyName) return property as ScriptProperty;
        }
        return null;
    }

    // Begin GetScript methods for various record types
    private static IScriptEntryGetter? GetScript(IWeaponGetter record, string scriptName)
    {
        if (record.VirtualMachineAdapter is null || record.VirtualMachineAdapter.Scripts.Count == 0) return null;
        foreach (var script in record.VirtualMachineAdapter.Scripts)
        {
            if (script is null || !script.Name.Equals(scriptName, StringComparison.CurrentCultureIgnoreCase) || script.Properties.Count == 0) continue;
            return script;
        }
        return null;
    }

    private static IScriptEntryGetter? GetScript(IMiscItemGetter record, string scriptName)
    {
        if (record.VirtualMachineAdapter is null || record.VirtualMachineAdapter.Scripts.Count == 0) return null;
        foreach (var script in record.VirtualMachineAdapter.Scripts)
        {
            if (script is null || !script.Name.Equals(scriptName, StringComparison.CurrentCultureIgnoreCase) || script.Properties.Count == 0) continue;
            return script;
        }
        return null;
    }

    private static IScriptEntryGetter? GetScript(IQuestGetter record, string scriptName)
    {
        if (record.VirtualMachineAdapter is null || record.VirtualMachineAdapter.Scripts.Count == 0) return null;
        foreach (var script in record.VirtualMachineAdapter.Scripts)
        {
            if (script is null || !script.Name.Equals(scriptName, StringComparison.CurrentCultureIgnoreCase) || script.Properties.Count == 0) continue;
            return script;
        }
        return null;
    }

    private static IScriptEntryGetter? GetScript(IPlacedObjectGetter record, string scriptName)
    {
        if (record.VirtualMachineAdapter is null || record.VirtualMachineAdapter.Scripts.Count == 0) return null;
        foreach (var script in record.VirtualMachineAdapter.Scripts)
        {
            if (script is null || !script.Name.Equals(scriptName, StringComparison.CurrentCultureIgnoreCase) || script.Properties.Count == 0) continue;
            return script;
        }
        return null;
    }

    private static IScriptEntryGetter? GetScript(IFurnitureGetter record, string scriptName)
    {
        if (record.VirtualMachineAdapter is null || record.VirtualMachineAdapter.Scripts.Count == 0) return null;
        foreach (var script in record.VirtualMachineAdapter.Scripts)
        {
            if (script is null || !script.Name.Equals(scriptName, StringComparison.CurrentCultureIgnoreCase) || script.Properties.Count == 0) continue;
            return script;
        }
        return null;
    }

    private static IScriptEntryGetter? GetScript(IArmorGetter record, string scriptName)
    {
        if (record.VirtualMachineAdapter is null || record.VirtualMachineAdapter.Scripts.Count == 0) return null;
        foreach (var script in record.VirtualMachineAdapter.Scripts)
        {
            if (script is null || !script.Name.Equals(scriptName, StringComparison.CurrentCultureIgnoreCase) || script.Properties.Count == 0) continue;
            return script;
        }
        return null;
    }
}