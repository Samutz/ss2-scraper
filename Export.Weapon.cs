using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins.Records;
using Noggog;

namespace SS2Scraper;

public partial class Export
{
    public void IndexWeapon(IWeaponGetter record)
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
                case "simsettlementsv2:weapons:buildingplan":
                    IndexBuildingPlan(record, new());
                    continue;

                case "simsettlementsv2:weapons:buildingskin":
                    IndexBuildingSkin(record);
                    continue;

                case "simsettlementsv2:weapons:leadercard":
                    IndexLeaderCard(record);
                    continue;

                default: continue;
            }
        }
    }

    private void IndexBuildingPlan(IWeaponGetter record, UnlockableRequirements requirements)
    {
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
            foreach (var keyword in record.Keywords)
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
        if (script is null) return;
    
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
        if (script is null) return null;
        
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
        

        return buildingLevelPlan;
    }

    private void IndexLeaderCard(IWeaponGetter record)
    {
        LeaderCard leaderCard = new()
        {
            formKey = record.FormKey.ToString(),
            editorId = record.EditorID?.ToString() ?? "",
            name = record.Name?.ToString() ?? "",
        };

        var script = GetScript(record, "SimSettlementsV2:Weapons:LeaderCard");
        if (script is null) return;

        var actorBaseFormProperty = GetScriptProperty(script, "ActorBaseForm") as ScriptStructProperty;
        IMajorRecordGetter? actorBaseForm = (actorBaseFormProperty is not null) ? GetFormFromUniversalForm(actorBaseFormProperty) : null;
        if (actorBaseForm is INpcGetter actor)
        {
            leaderCard.targetActor = GetBaseActor(actor);
        }
        else if (actorBaseFormProperty?.Members.First() is not null)
        {
            var pluginNameProperty = GetScriptProperty(actorBaseFormProperty.Members.First(), "sPluginName") as ScriptStringProperty;
            leaderCard.targetActor = new()
            {
                plugin = pluginNameProperty?.Data ?? ""
            };
        }

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
                        var traits = GetLeaderTraitInfo(record1);
                        if (traits is not null) leaderCard.minorTraits.Add(traits);
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
                        var traits = GetLeaderTraitInfo(record1);
                        if (traits is not null) leaderCard.weaknesses.Add(traits);
                    }
                }
            }
        }


        output.leaderCards.Add(leaderCard);
        output.totalItems++;
    }

    private void IndexBuildingSkin(IWeaponGetter record)
    {
        var script = GetScript(record, "SimSettlementsV2:Weapons:BuildingSkin");
        if (script is null) return;
        
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
            BaseItem newLevelSkin = new()
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