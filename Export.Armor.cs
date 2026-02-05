using Mutagen.Bethesda.Fallout4;

namespace SS2Scraper;

public partial class Export
{
    private void IndexArmor(IArmorGetter record, UnlockableRequirements? requirements)
    {
        if (
            record.VirtualMachineAdapter is null
            || record.VirtualMachineAdapter.Scripts.Count == 0
        ) return;

        foreach (var script in record.VirtualMachineAdapter.Scripts)
        {
            if (script is null) continue;

            switch (script.Name.ToLower().Trim('\0'))
            {
                case "simsettlementsv2:armors:themedefinition_flags":
                    IndexDynamicFlag(record, requirements);
                    continue;

                case "simsettlementsv2:armors:npcunittype":
                    IndexUnitType(record, requirements);
                    continue;

                default:
                    if (bOutputUnknowns) Console.WriteLine($"Found UNKNOWN armor script: {script.Name.ToLower()} on {record.FormKey} ({record.EditorID})");
                    continue;
            }
        }
    }
    private void IndexDynamicFlag(IArmorGetter record, UnlockableRequirements? requirements)
    {
        DynamicFlag flag = new()
        {
            formKey = record.FormKey.ToString(),
            editorId = record.EditorID?.ToString() ?? "",
            name = record.Name?.ToString() ?? "",
            requirements = requirements
        };

        var script = GetScript(record, "SimSettlementsV2:Armors:ThemeDefinition_Flags");
        if (script is null) return;
        
        flag.FlagWaving = (GetScriptProperty(script, "FlagWaving") as ScriptObjectProperty)?.Object.FormKey.ToString() ?? "";
        flag.FlagDown = (GetScriptProperty(script, "FlagDown") as ScriptObjectProperty)?.Object.FormKey.ToString() ?? "";
        flag.FlagWall = (GetScriptProperty(script, "FlagWall") as ScriptObjectProperty)?.Object.FormKey.ToString() ?? "";
        flag.FlagHalfCircleFlag01 = (GetScriptProperty(script, "FlagHalfCircleFlag01") as ScriptObjectProperty)?.Object.FormKey.ToString() ?? "";
        flag.FlagHalfCircleFlag02 = (GetScriptProperty(script, "FlagHalfCircleFlag02") as ScriptObjectProperty)?.Object.FormKey.ToString() ?? "";
        flag.FlagBannerTownStatic = (GetScriptProperty(script, "FlagBannerTownStatic") as ScriptObjectProperty)?.Object.FormKey.ToString() ?? "";
        flag.FlagBannerTownTorn = (GetScriptProperty(script, "FlagBannerTownTorn") as ScriptObjectProperty)?.Object.FormKey.ToString() ?? "";
        flag.FlagBannerTownTornWaving = (GetScriptProperty(script, "FlagBannerTownTornWaving") as ScriptObjectProperty)?.Object.FormKey.ToString() ?? "";
        
        output.dynamicFlags.Add(flag);
        output.totalItems++;
    }

    private void IndexUnitType(IArmorGetter record, UnlockableRequirements? requirements)
    {
        UnitType unitType = new()
        {
            formKey = record.FormKey.ToString(),
            editorId = record.EditorID?.ToString() ?? "",
            name = record.Name?.ToString() ?? "",
            requirements = requirements
        };  

        var script = GetScript(record, "SimSettlementsV2:Armors:NPCUnitType");
        if (script is null) return;

        unitType.strengthRating = (GetScriptProperty(script, "iStrengthRating") as ScriptIntProperty)?.Data ?? 1;
        unitType.defaultOutfit = (GetScriptProperty(script, "DefaultOutfit") as ScriptObjectProperty)?.Object.FormKey.ToString() ?? "";

        var descFormKey = (GetScriptProperty(script, "DescriptionMessage") as ScriptObjectProperty)?.Object.FormKey;
        if (descFormKey is not null && linkCache.TryResolve<IMessageGetter>(descFormKey.Value, out var mesg))
        {
            unitType.description = mesg.Description?.ToString() ?? "";
        }

        var shortDescFormKey = (GetScriptProperty(script, "ShortDescriptionHolder") as ScriptObjectProperty)?.Object.FormKey;
        if (shortDescFormKey is not null && linkCache.TryResolve<IMiscItemGetter>(shortDescFormKey.Value, out var mesg2))
        {
            unitType.shortDescription = mesg2.Name?.ToString() ?? "";
        }

        List<string> rankNames = [];
        var ranks = (GetScriptProperty(script, "Ranks") as ScriptObjectListProperty)?.Objects ?? [];
        int i = 0;
        foreach (var rank in ranks)
        {
            if (!linkCache.TryResolve<IArmorGetter>(rank.Object.FormKey, out var armo1)) continue;
            unitType.ranks.Add(new()
            {
                name = armo1.Name?.ToString() ?? "",
                formKey = armo1.FormKey.ToString(),
                editorId = armo1.EditorID?.ToString() ?? "",
                rank = i+1
            });
            i++;
        }

        var loadouts = (GetScriptProperty(script, "RankLoadOuts") as ScriptObjectListProperty)?.Objects ?? [];
        i = 0;
        foreach (var loadout in loadouts)
        {
            if (linkCache.TryResolve<IArmorGetter>(loadout.Object.FormKey, out var armo2))
            {
                var loadoutScript = GetScript(armo2, "SimSettlementsV2:Armors:NPCLoadout");
                if (loadoutScript is null) continue;

                unitType.loadouts.Add(new()
                {
                    name = armo2.Name?.ToString() ?? "",
                    formKey = armo2.FormKey.ToString(),
                    editorId = armo2.EditorID?.ToString() ?? "",
                    requiredRank = (GetScriptProperty(loadoutScript, "iRankRequirement") as ScriptIntProperty)?.Data ?? 1
                });
            }
            i++;
        }

        output.unitTypes.Add(unitType);
        output.totalItems++;
    }
}