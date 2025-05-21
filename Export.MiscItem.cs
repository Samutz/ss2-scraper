using Mutagen.Bethesda;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Noggog;

namespace SS2Scraper;

public partial class Export
{
    private void IndexMiscItem(IMiscItemGetter record)
    {
        // pet names, doesn't use script
        if (record.HasKeyword(FormKey.Factory("01F43E:SS2.esm"))) IndexBaseItem(record, output.petNames);

        if (
            record.VirtualMachineAdapter is null
            || record.VirtualMachineAdapter.Scripts.Count == 0
        ) return;

        foreach (var script in record.VirtualMachineAdapter.Scripts)
        {
            if (script is null) continue;

            switch (script.Name.ToLower())
            {
                case "simsettlementsv2:miscobjects:unlockablebuildingplan":
                    IndexUnlockableBuildingPlan(record);
                    continue;

                case "simsettlementsv2:miscobjects:foundation":
                    IndexFoundation(record);
                    continue;

                case "simsettlementsv2:miscobjects:powerpole":
                    IndexPowerPole(record);
                    continue;

                case "simsettlementsv2:miscobjects:furniturestoreitem":
                    IndexFurnitureStoreItem(record);
                    continue;

                case "simsettlementsv2:miscobjects:petstorecreatureitem":
                    IndexPetStoreCreature(record);
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
                    IndexHQRoomConfig(record);
                    continue;

                case "simsettlementsv2:hq:baseactiontypes:hqroomupgrade":
                    IndexHQRoomUpgrade(record);
                    continue;

                default: continue;
            }
        }
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

    private void IndexFoundation(IMiscItemGetter record)
    {
        var script = GetScript(record, "SimSettlementsV2:MiscObjects:Foundation");

        if (script is null) return;

        Foundation foundation = new()
        {
            formKey = record.FormKey.ToString(),
            editorId = record.EditorID?.ToString() ?? "",
            name = record.Name?.ToString() ?? "",
        };

        var spawnData = GetScriptProperty(script, "SpawnData") as ScriptStructProperty;
        if (spawnData?.Members.First().Properties.First() is not null)
        {
            foreach (var property in spawnData?.Members.First().Properties ?? [])
            {
                if (property.Name != "ObjectForm") continue;
                var property1 = property as ScriptObjectProperty;

                if (property1?.Object.FormKey is not null && linkCache.TryResolve<IActivatorGetter>(property1.Object.FormKey, out var activator))
                {
                    foundation.workshopName = activator.Name?.ToString() ?? "";
                    foundation.terraformer = activator.HasKeyword(FormKey.Factory("0193F8:SS2.esm")); // terraformer keyword
                    foundation.craftable = mod.ConstructibleObjects.Where(co => co.CreatedObject.FormKey == activator.FormKey).Any();
                    BoundsSize size = GetSizeFromObjectBounds(activator.ObjectBounds);
                    if (size.X >= 768 || size.Y >= 768) foundation.size = 3;
                    else if (size.X >= 512 || size.Y >= 512) foundation.size = 2;
                    else if (size.X >= 256 || size.Y >= 256) foundation.size = 1;
                }

                if (property1?.Object.FormKey is not null && linkCache.TryResolve<IStaticGetter>(property1.Object.FormKey, out var stat))
                {
                    foundation.workshopName = stat.Name?.ToString() ?? "";
                    foundation.craftable = mod.ConstructibleObjects.Where(co => co.CreatedObject.FormKey == stat.FormKey).Any();
                    BoundsSize size = GetSizeFromObjectBounds(stat.ObjectBounds);
                    if (size.X >= 768 || size.Y >= 768) foundation.size = 3;
                    else if (size.X >= 512 || size.Y >= 512) foundation.size = 2;
                    else if (size.X >= 256 || size.Y >= 256) foundation.size = 1;
                }
            }
        }

        output.foundations.Add(foundation);
        output.totalItems++;
    }

    private void IndexPowerPole(IMiscItemGetter record)
    {
        var script = GetScript(record, "SimSettlementsV2:MiscObjects:PowerPole");

        if (script is null) return;

        PowerPole pole = new()
        {
            formKey = record.FormKey.ToString(),
            editorId = record.EditorID?.ToString() ?? "",
            name = record.Name?.ToString() ?? "",
        };

        var spawnData = GetScriptProperty(script, "SpawnData") as ScriptStructProperty;
        if (spawnData?.Members.First().Properties.First() is not null)
        {
            foreach (var property in spawnData?.Members.First().Properties ?? [])
            {
                if (property.Name != "ObjectForm") continue;
                var property1 = property as ScriptObjectProperty;

                if (property1?.Object.FormKey is not null && linkCache.TryResolve<IActivatorGetter>(property1.Object.FormKey, out var activator))
                {
                    pole.workshopName = activator.Name?.ToString() ?? "";
                    pole.height = GetSizeFromObjectBounds(activator.ObjectBounds).Z;
                    pole.hasLight =
                        GetScript(activator, "SimSettlementsV2:ObjectReferences:AllowAnimationsDummyScript") is not null
                        && activator.HasKeyword(FormKey.Factory("03037E:Fallout4.esm")); // WorkshopCanBePowered keyword
                    pole.craftable = mod.ConstructibleObjects.Where(co => co.CreatedObject.FormKey == activator.FormKey).Any();
                }
            }
        }

        output.powerPoles.Add(pole);
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
            value = record.Value
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

            if (furniture.HasAnyKeyword([FormKey.Factory("030BB2:Fallout4.esm"), FormKey.Factory("1338F7:Fallout4.esm")])) // chair anim keywords
            {
                storeItem.type = "furniture_chair";
            }

            if (furniture.HasAnyKeyword([FormKey.Factory("014576:SS2.esm"), FormKey.Factory("020320:SS2.esm")])) // city planner desk keywords
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

    private void IndexPetStoreCreature(IMiscItemGetter record)
    {
        PetStoreCreature storeItem = new()
        {
            formKey = record.FormKey.ToString(),
            editorId = record.EditorID?.ToString() ?? "",
            name = record.Name?.ToString() ?? "", // shop item name
            value = record.Value
        };

        var script = GetScript(record, "SimSettlementsV2:MiscObjects:PetStoreCreatureItem");

        if (script is not null)
        {
            var iVendorLevel = GetScriptProperty(script, "iVendorLevel") as ScriptIntProperty;
            if (iVendorLevel?.Data is not null) storeItem.vendorLevel = iVendorLevel.Data;
        }

        output.petStoreCreatures.Add(storeItem);
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
        IMajorRecordGetter? workbenchBaseForm = (workshopRef is not null) ? GetFormFromUniversalForm(workshopRef) : null;
        plan.targetSettlement = workbenchBaseForm?.FormKey.ToString() ?? "";

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

    private void IndexHQRoomConfig(IMiscItemGetter record)
    {
        HQRoomConfig roomConfig = new()
        {
            formKey = record.FormKey.ToString(),
            editorId = record.EditorID?.ToString() ?? "",
            name = record.Name?.ToString() ?? "",
        };

        if (record.Keywords is not null && record.Keywords.Count > 0)
        {
            foreach (var keyword in record.Keywords)
            {
                if (!linkCache.TryResolve<IKeywordGetter>(keyword.FormKey, out var keywordKey)) continue;
                if (keywordKey.EditorID?.StartsWith("SS2C2_Tag_RoomShape_") ?? false) roomConfig.roomShape = keywordKey.EditorID;
            }
        }

        var script = GetScript(record, "simsettlementsv2:hq:library:miscobjects:requirementtypes:actiontypes:hqroomconfig");
        if (script is null) return;
        
        var PrimaryDepartment = GetScriptProperty(script, "PrimaryDepartment") as ScriptObjectProperty;
        if (PrimaryDepartment?.Object is not null && linkCache.TryResolve<IPlacedObjectGetter>(PrimaryDepartment.Object.FormKey, out var objRef))
        {
            if (objRef.Base is not null && linkCache.TryResolve<IActivatorGetter>(objRef.Base.FormKey, out var department))
            {
                roomConfig.primaryDepartment = department.Name?.ToString() ?? "";
            }
        }

        var RoomUpgradeSlots = GetScriptProperty(script, "RoomUpgradeSlots") as ScriptObjectListProperty;
        foreach (var slot in RoomUpgradeSlots?.Objects ?? [])
        {
            roomConfig.upgradeSlots.Add(slot.Object.FormKey.ToString());
        }

        output.hqRoomConfigs.Add(roomConfig);
        output.totalItems++;
    }

    private void IndexHQRoomUpgrade(IMiscItemGetter record)
    {
        HQRoomUpgrade roomUpgrade = new()
        {
            formKey = record.FormKey.ToString(),
            editorId = record.EditorID?.ToString() ?? "",
            name = record.Name?.ToString() ?? "",
        };

        var script = GetScript(record, "SimSettlementsV2:HQ:BaseActionTypes:HQRoomUpgrade");
        if (script is null) return;

        var TargetUpgradeSlot = GetScriptProperty(script, "TargetUpgradeSlot") as ScriptObjectProperty;
        roomUpgrade.targetUpgradeSlot = TargetUpgradeSlot?.Object.FormKey.ToString() ?? "";

        if (record.HasKeyword(FormKey.Factory("04B2F3:SS2.esm"))) output.hqRoomConstructions.Add(roomUpgrade);
        else if (record.HasKeyword(FormKey.Factory("04B2F4:SS2.esm"))) output.hqRoomUpgrades.Add(roomUpgrade);
        output.totalItems++;
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
            targetActor = GetBaseActor(actor),
        };

        output.unlockableCharacters.Add(character);
        output.totalItems++;
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
    

    private BaseItem GetLeaderTraitInfo(IMiscItemGetter record)
    {
        BaseItem trait = new()
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
}