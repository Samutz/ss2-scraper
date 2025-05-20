using Mutagen.Bethesda.Fallout4;

namespace SS2Scraper;

public partial class Export
{
    private void IndexArmor(IArmorGetter record)
    {
        if (
            record.VirtualMachineAdapter is null
            || record.VirtualMachineAdapter.Scripts.Count == 0
        ) return;

        foreach (var script in record.VirtualMachineAdapter.Scripts)
        {
            if (script is null) continue;

            switch (script.Name.ToLower())
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
            flag.hasFlagHalfCircleFlag02 = GetScriptProperty(script, "FlagHalfCircleFlag02") as ScriptObjectProperty is not null;
            flag.hasFlagBannerTownStatic = GetScriptProperty(script, "FlagBannerTownStatic") as ScriptObjectProperty is not null;
            flag.hasFlagBannerTownTorn = GetScriptProperty(script, "FlagBannerTownTorn") as ScriptObjectProperty is not null;
            flag.hasFlagBannerTownTornWaving = GetScriptProperty(script, "FlagBannerTownTornWaving") as ScriptObjectProperty is not null;
        }

        output.dynamicFlags.Add(flag);
        output.totalItems++;
    }
}