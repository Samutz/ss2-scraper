using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Strings;
using Noggog;

namespace SS2Scraper;

public partial class Export(IFallout4ModDisposableGetter mod, ILinkCache linkCache, bool bOutputUnknowns)
{
    private readonly IFallout4ModDisposableGetter mod = mod;
    private readonly ILinkCache linkCache = linkCache;
    private readonly bool bOutputUnknowns = bOutputUnknowns;

    public Output output = new();

    public Output BuildOutput()
    {
        TranslatedString.DefaultLanguage = Language.English;

        output = new()
        {
            isMaster = mod.IsMaster,
            isLight = mod.IsSmallMaster,
            masters = [.. mod.MasterReferences.Select(master => master.Master.FileName.String)],
        };

        IndexAddonItems();
        IndexHQActionLists();

        switch (mod.ModKey.FileName.ToString())
        {
            case "IDEKsLogisticsStation2.esl":
                // BPs injected by ILS2's custom quest
                IndexAddonItem(FormKey.Factory("000BC6:IDEKsLogisticsStation2.esl"), null);
                IndexAddonItem(FormKey.Factory("000BCC:IDEKsLogisticsStation2.esl"), null);
                break;

            case "SS2_XPAC_Chapter3.esm":
                IndexAddonItem(FormKey.Factory("0270EC:SS2_XPAC_Chapter3.esm"), null); // helios tower marvel
                break;
        }

        List<string> skipKeywordNames = [
            "LocTypeWorkshopSettlement",
            "LocTypeWorld",
            "LocEncGunners",
            "LocTypeBar",
            "LocSetBrewery",
            "LocSetHospital",
            "LocSetMilitary",
            "LocSetNatural",
            "LocSetQuarry",
            "LocSetSkyscraper",
            "LocEncBrotherhoodOfSteel",
            "LocEncBugs",
            "LocEncChildrenOfAtom",
            "LocEncGhouls",
            "LocEncMirelurks",
            "LocEncRaiders",
            "LocEncRobots",
            "LocEncSuperMutants",
            "LocEncSynths",
            "LocEncWildlife",
        ];

        List<string> keywordsMissingTraits = [];

        foreach (var location in mod.Locations)
        {
            if (location is null || location.Name is null || location.Name.ToString().IsNullOrEmpty()) continue;
            if (location.Keywords is null) continue;
            bool skip = false;
            List<string> validKeywords = [];
            foreach (var keyword in location.Keywords)
            {
                linkCache.TryResolve<IKeywordGetter>(keyword.FormKey, out var outKeyword);
                if (outKeyword is null) continue;
                if (outKeyword.EditorID is null) continue;
                if (skipKeywordNames.Contains(outKeyword.EditorID))
                {
                    skip = true;
                    continue;
                }
                validKeywords.Add(outKeyword.EditorID);
            }
            if (skip) continue;
            keywordsMissingTraits.Add($"Location: {location.Name} ({location.EditorID}) | Keywords: {String.Join(", ", validKeywords)}");
        }

        foreach (var name in keywordsMissingTraits.Distinct()) Console.WriteLine($"{name}");
             
        return output;
    }

    private void IndexAddonItems()
    {
        List<FormKey?> addonConfigKeys = GetAddonConfigFormKeys();
        List<FormKey?> addonFormListKeys = GetAddonFormListFormKeys(addonConfigKeys);
        IndexAddonFormLists(addonFormListKeys);
    }

    private List<FormKey?> GetAddonConfigFormKeys()
    {
        List<FormKey> addonConfigKeys = [];

        foreach (var quest in mod.Quests)
        {
            if (
                quest.Data is null
                || !quest.Data.Flags.HasFlag(Quest.Flag.StartGameEnabled)
            ) continue;

            var script = GetScript(quest, "SimSettlementsV2:quests:AddonPack");
            if (script is not null)
            {
                if (GetScriptProperty(script, "MyAddonConfig") is not ScriptObjectProperty property) continue;
                addonConfigKeys.Add(property.Object.FormKey);
            }
        }

        return [.. addonConfigKeys.Distinct()];
    }

    private List<FormKey?> GetAddonFormListFormKeys(List<FormKey?> addonConfigKeys)
    {
        List<FormKey> formListKeys = [];

        foreach (var formKey in addonConfigKeys)
        {
            if (
                formKey is null
                || !linkCache.TryResolve<IMiscItemGetter>(formKey.Value, out var miscItem)
            ) continue;

            var script = GetScript(miscItem, "SimSettlementsV2:MiscObjects:AddonPackConfiguration");
            if (script is not null)
            {
                (GetScriptProperty(script, "MyItems") as ScriptObjectListProperty)?.Objects.ForEach(obj =>
                {
                    formListKeys.Add(obj.Object.FormKey);
                });
            }
        }

        return [.. formListKeys.Distinct()];
    }

    private void IndexAddonFormLists(List<FormKey?> formListKeys)
    {
        List<FormKey?> addonItemKeys = [];

        foreach (var formKey in formListKeys)
        {
            if (
                formKey is null
                || !linkCache.TryResolve<IFormListGetter>(formKey.Value, out var formList)
            ) continue;

            if (
                formList is null
                || formList.Items is null
                || formList.Items.Count == 0
            ) continue;

            IKeywordGetter? formListKeyword = null;
            int i = 0;
            foreach (var key in formList.Items)
            {
                if (key is null) continue;
                if (formListKeyword is null && i == 0)
                {
                    linkCache.TryResolve<IKeywordGetter>(key.FormKey, out formListKeyword);
                }
                IndexAddonItem(key.FormKey, formListKeyword);
                i++;
            }
        }
    }

    private void IndexAddonItem(FormKey? formKey, IKeywordGetter? formListKeyword)
    {
        if (formListKeyword?.EditorID == "SS2_FLID_TerritoryTraits") return;
        
        if (formKey is null) return;

        linkCache.TryResolve<IMajorRecordGetter>(formKey.Value, out var record);
        if (record is null) return;

        switch (record.Type.ToString().Split(".").Last())
        {
            case "IKeyword":
                if (linkCache.TryResolve<IMiscItemGetter>(formKey.Value, out var keyword))
                {
                    if (keyword != formListKeyword && bOutputUnknowns) 
                        Console.WriteLine($"Found UNKNOWN {record.Type.ToString().Split(".").Last()}: {formKey} | KYWD: {formListKeyword?.EditorID}");;
                }
                break;

            case "IMiscItem":
                if (linkCache.TryResolve<IMiscItemGetter>(formKey.Value, out var miscItem))
                    IndexMiscItem(miscItem, formListKeyword);
                break;

            case "IWeapon":
                if (linkCache.TryResolve<IWeaponGetter>(formKey.Value, out var weapon))
                    IndexWeapon(weapon);
                break;

            case "IArmor":
                if (linkCache.TryResolve<IArmorGetter>(formKey.Value, out var armor))
                    IndexArmor(armor);
                break;

            case "IBook":
                if (linkCache.TryResolve<IBookGetter>(formKey.Value, out var book))
                    IndexBook(book);
                break;

            default:
                if (bOutputUnknowns) Console.WriteLine($"Found UNKNOWN {record.Type.ToString().Split(".").Last()}: {formKey} | KYWD: {formListKeyword?.EditorID}");
                break;
        }
    }

    // SS2 CH2 doesn't register its own HQ items in its addon config
    private void IndexHQActionLists()
    {
        if (mod.ModKey.FileName != "SS2_XPAC_Chapter2.esm") return;

        List<string> listKeys = [
            "027417:SS2_XPAC_Chapter2.esm", // SS2C2_HQGNN_DefaultActions
            "01F16F:SS2_XPAC_Chapter2.esm", // SS2C2_HQGNN_Basement_DefaultActions
            "0327DD:SS2_XPAC_Chapter2.esm", // SS2C2_HQGNN_Exterior_DefaultActions
            "02649A:SS2_XPAC_Chapter2.esm", // SS2C2_HQGNN_MidFloor_DefaultActions
            "027416:SS2_XPAC_Chapter2.esm", // SS2C2_HQGNN_TutorialDefaultActions
            "034DCB:SS2_XPAC_Chapter2.esm", // SS2C2_HQActions_GNN_MQ24RegisterPostTutorial
        ];

        // additional actions that unlock from other means
        List<string> actionKeys = [
            "02A71F:SS2_XPAC_Chapter2.esm", // SS2C2_HQGNN_Action_RoomConstruction_GNNMainHallQuadrant_MedicalLab
            "027A55:SS2_XPAC_Chapter2.esm", // SS2C2_HQGNN_Action_RoomConstruction_MQ_CommArrayRoom 
            "02F553:SS2_XPAC_Chapter2.esm", // SS2C2_HQGNN_Action_RoomConstruction_MQ_MakeshiftInfirmary
            "027A54:SS2_XPAC_Chapter2.esm", // SS2C2_HQGNN_Action_RoomConstruction_MQ_MeetingRoom
        ];

        foreach (var listKey in listKeys)
        {
            if (!linkCache.TryResolve<IFormListGetter>(FormKey.Factory(listKey), out var formlist)) continue;
            foreach (var item in formlist.Items)
            {
                if (linkCache.TryResolve<IMiscItemGetter>(item.FormKey, out var miscItem))
                {
                    IndexMiscItem(miscItem, null);
                    continue;
                }
            }
        }

        foreach (var listKey in actionKeys)
        {
            if (linkCache.TryResolve<IMiscItemGetter>(FormKey.Factory(listKey), out var miscItem))
            {
                IndexMiscItem(miscItem, null);
                continue;
            }
        }
    }

    private void IndexBaseItem(IMiscItemGetter record, List<BaseItem> targetList)
    {
        BaseItem obj = new()
        {
            formKey = record.FormKey.ToString(),
            editorId = record.EditorID?.ToString() ?? "",
            name = record.Name?.ToString() ?? ""
        };
        targetList.Add(obj);
        output.totalItems++;
    }

    private static BoundsSize GetSizeFromObjectBounds(IObjectBoundsGetter bounds)
    {
        BoundsSize size = new()
        {
            X = Math.Abs(bounds.Second.X -bounds.First.X),
            Y = Math.Abs(bounds.Second.Y -bounds.First.Y),
            Z = Math.Abs(bounds.Second.Z -bounds.First.Z),
        };
        return size;
    }

    private FormKey? GetFormKeyFromUniversalForm(ScriptStructProperty UniversalFormProperty)
    {
        if (UniversalFormProperty?.Members?.First() is null || UniversalFormProperty?.Members.First().Properties is null) return null;

        // direct reference
        var actorBaseFormProperty = GetScriptProperty(UniversalFormProperty.Members.First(), "BaseForm") as ScriptObjectProperty;
        if (actorBaseFormProperty is not null && linkCache.TryResolve<IMajorRecordGetter>(actorBaseFormProperty.Object.FormKey, out var record1))
            return record1.FormKey;

        // indirect reference
        var pluginNameProperty = GetScriptProperty(UniversalFormProperty.Members.First(), "sPluginName") as ScriptStringProperty;
        var formIdProperty = GetScriptProperty(UniversalFormProperty.Members.First(), "iFormID") as ScriptIntProperty;

        if (formIdProperty?.Data is null || pluginNameProperty?.Data is null) return null;

        string formIdHex = string.Format("{0:X6}", formIdProperty.Data);
        string formKeyString = $"{formIdHex}:{pluginNameProperty.Data}";
        if (!FormKey.TryFactory(formKeyString, out var finalFormKey)) return null;

        return finalFormKey;
    }

    private IMajorRecordGetter? GetFormFromUniversalForm(ScriptStructProperty UniversalFormProperty)
    {
        FormKey finalFormKey = GetFormKeyFromUniversalForm(UniversalFormProperty) ?? new FormKey();
        if (!linkCache.TryResolve<IMajorRecordGetter>(finalFormKey, out var record2)) return null;

        return record2;
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

    private static ScriptProperty? GetScriptProperty(IScriptEntryGetter script, string propertyName)
    {
        foreach (var property in script.Properties)
        {
            if (property is null) continue;
            if (property.Name.Equals(propertyName, StringComparison.CurrentCultureIgnoreCase))
            {
                return property as ScriptProperty;
            }
        }
        return null;
    }

    // Begin GetScript methods for various record types
    private static IScriptEntryGetter? GetScript(IWeaponGetter record, string scriptName)
    {
        if (record.VirtualMachineAdapter is null || record.VirtualMachineAdapter.Scripts.Count == 0) return null;
        foreach (var script in record.VirtualMachineAdapter.Scripts)
        {
            if (script is null || !script.Name.Equals(scriptName, StringComparison.CurrentCultureIgnoreCase)) continue;
            return script;
        }
        return null;
    }

    private static IScriptEntryGetter? GetScript(IMiscItemGetter record, string scriptName)
    {
        if (record.VirtualMachineAdapter is null || record.VirtualMachineAdapter.Scripts.Count == 0) return null;
        foreach (var script in record.VirtualMachineAdapter.Scripts)
        {
            if (script is null || !script.Name.Equals(scriptName, StringComparison.CurrentCultureIgnoreCase)) continue;
            return script;
        }
        return null;
    }

    private static IScriptEntryGetter? GetScript(IQuestGetter record, string scriptName)
    {
        if (record.VirtualMachineAdapter is null || record.VirtualMachineAdapter.Scripts.Count == 0) return null;
        foreach (var script in record.VirtualMachineAdapter.Scripts)
        {
            if (script is null || !script.Name.Equals(scriptName, StringComparison.CurrentCultureIgnoreCase)) continue;
            return script;
        }
        return null;
    }

    private static IScriptEntryGetter? GetScript(IArmorGetter record, string scriptName)
    {
        if (record.VirtualMachineAdapter is null || record.VirtualMachineAdapter.Scripts.Count == 0) return null;
        foreach (var script in record.VirtualMachineAdapter.Scripts)
        {
            if (script is null || !script.Name.Equals(scriptName, StringComparison.CurrentCultureIgnoreCase)) continue;
            return script;
        }
        return null;
    }

    private static IScriptEntryGetter? GetScript(IBookGetter record, string scriptName)
    {
        if (record.VirtualMachineAdapter is null || record.VirtualMachineAdapter.Scripts.Count == 0) return null;
        foreach (var script in record.VirtualMachineAdapter.Scripts)
        {
            if (script is null || !script.Name.Equals(scriptName, StringComparison.CurrentCultureIgnoreCase)) continue;
            return script;
        }
        return null;
    }
    
    private static IScriptEntryGetter? GetScript(IActivatorGetter record, string scriptName)
    {
        if (record.VirtualMachineAdapter is null || record.VirtualMachineAdapter.Scripts.Count == 0) return null;
        foreach (var script in record.VirtualMachineAdapter.Scripts)
        {
            if (script is null || !script.Name.Equals(scriptName, StringComparison.CurrentCultureIgnoreCase)) continue;
            return script;
        }
        return null;
    }
}