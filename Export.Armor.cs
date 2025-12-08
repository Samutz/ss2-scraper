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

            switch (script.Name.ToLower().Trim('\0'))
            {
                case "simsettlementsv2:armors:themedefinition_flags":
                    IndexDynamicFlag(record, new());
                    continue;

                default:
                    if (bOutputUnknowns) Console.WriteLine($"Found UNKNOWN armor script: {script.Name.ToLower()} on {record.FormKey} ({record.EditorID})");
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
}