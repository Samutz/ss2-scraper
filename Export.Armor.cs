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

                default: continue;
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
        
        flag.hasFlagWaving = GetScriptProperty(script, "FlagWaving") as ScriptObjectProperty is not null;
        flag.hasFlagDown = GetScriptProperty(script, "FlagDown") as ScriptObjectProperty is not null;
        flag.hasFlagWall = GetScriptProperty(script, "FlagWall") as ScriptObjectProperty is not null;
        flag.hasFlagHalfCircleFlag01 = GetScriptProperty(script, "FlagHalfCircleFlag01") as ScriptObjectProperty is not null;
        flag.hasFlagHalfCircleFlag02 = GetScriptProperty(script, "FlagHalfCircleFlag02") as ScriptObjectProperty is not null;
        flag.hasFlagBannerTownStatic = GetScriptProperty(script, "FlagBannerTownStatic") as ScriptObjectProperty is not null;
        flag.hasFlagBannerTownTorn = GetScriptProperty(script, "FlagBannerTownTorn") as ScriptObjectProperty is not null;
        flag.hasFlagBannerTownTornWaving = GetScriptProperty(script, "FlagBannerTownTornWaving") as ScriptObjectProperty is not null;
        
        output.dynamicFlags.Add(flag);
        output.totalItems++;
    }
}