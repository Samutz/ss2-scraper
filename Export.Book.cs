using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins.Aspects;

namespace SS2Scraper;

public partial class Export
{
    private void IndexBook(IBookGetter record)
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
                case "simsettlementsv2:books:beerrecipe":
                    IndexBeerRecipe(record);
                    continue;

                default: continue;
            }
        }
    }

    private void IndexBeerRecipe(IBookGetter record)
    {
        BeerRecipe beerRecipe = new()
        {
            formKey = record.FormKey.ToString(),
            editorId = record.EditorID?.ToString() ?? "",
            recipeName = record.Name?.ToString() ?? "",
        };

        var script = GetScript(record, "SimSettlementsV2:Books:BeerRecipe");

        if (script is null) return;

        var Mash = GetScriptProperty(script, "Mash") as ScriptObjectProperty;
        if (Mash?.Object is not null && linkCache.TryResolve<INamedGetter>(Mash.Object.FormKey, out var mashPotion))
        {
            beerRecipe.mash = mashPotion.Name?.ToString() ?? "";
        }

        var CreatedBeer = GetScriptProperty(script, "CreatedBeer") as ScriptObjectProperty;
        if (CreatedBeer?.Object is not null && linkCache.TryResolve<IIngestibleGetter>(CreatedBeer.Object.FormKey, out var beerPotion))
        {
            beerRecipe.name = beerPotion.Name?.ToString() ?? "";
        }

        var Flavorings = GetScriptProperty(script, "Flavorings") as ScriptObjectListProperty;
        foreach (var falvoring in Flavorings?.Objects ?? [])
        {
            if (!linkCache.TryResolve<INamedGetter>(falvoring.Object.FormKey, out var flavoringForm)) continue;
            beerRecipe.flavorings.Add(flavoringForm.Name?.ToString() ?? $"(Name Missing: {falvoring.Object.FormKey})");
        }

        output.beerRecipes.Add(beerRecipe);
        output.totalItems++;
    }
}