using Mutagen.Bethesda;
using Mutagen.Bethesda.Fallout4;

namespace SS2Scraper;

public partial class Export
{
private UniqueNpc GetBaseActor(INpcGetter actor)
    {
        UniqueNpc baseActor = new()
        {
            formKey = actor.FormKey.ToString(),
            editorId = actor.EditorID?.ToString() ?? "",
            name = actor.Name?.ToString() ?? "",
            special = GetActorStats(actor),
        };
        return baseActor;
    }

    private ActorSpecial GetActorStats(INpcGetter actor)
    {
        List<int> stats = [0,0,0,0,0,0,0];

        List<string> AVFormKeys = [
            "0002C2:Fallout4.esm",
            "0002C3:Fallout4.esm",
            "0002C4:Fallout4.esm",
            "0002C5:Fallout4.esm",
            "0002C6:Fallout4.esm",
            "0002C7:Fallout4.esm",
            "0002C8:Fallout4.esm"
        ];

        // race record
        if (linkCache.TryResolve<IRaceGetter>(actor.Race.FormKey, out var race))
            for (var i=0; i<7; i++) stats[i] += (int?) race.Properties?.Where(p => p.ActorValue.FormKey.ToString() == AVFormKeys[i] && p.Value > 0).FirstOrDefault()?.Value ?? 0;

        // class record
        if (linkCache.TryResolve<IClassGetter>(actor.Class.FormKey, out var classRecord))
            for (var i=0; i<7; i++) stats[i] += (int?) classRecord.Properties?.Where(p => p.ActorValue.FormKey.ToString() == AVFormKeys[i] && p.Value > 0).FirstOrDefault()?.Value ?? 0;

        // actor record
        for (var i=0; i<7; i++) stats[i] = (int?) actor.Properties?.Where(p => p.ActorValue.FormKey.ToString() == AVFormKeys[i] && p.Value > 0 && p.Value >= stats[i]).FirstOrDefault()?.Value ?? stats[i];

        // actor effects
        foreach (var spell in actor.ActorEffect ?? [])
        {
            if (!linkCache.TryResolve<ISpellGetter>(spell.FormKey, out var spellRecord)) continue;
            foreach (var effect in spellRecord.Effects)
            {
                if (!linkCache.TryResolve<IMagicEffectGetter>(effect.BaseEffect.FormKey, out var effectRecord)) continue;
                for (var i=0; i<7; i++)
                {
                    if (effectRecord.Archetype.ActorValue.FormKey.ToString() == AVFormKeys[i])
                    {
                        stats[i] += (int?) effect.Data?.Magnitude ?? 0;
                    }
                }
            }
        }

        return new ActorSpecial(){
            Strength = stats[0] > 0 ? stats[0] : 1,
            Perception = stats[1] > 0 ? stats[1] : 1,
            Endurance = stats[2] > 0 ? stats[2] : 1,
            Charisma = stats[3] > 0 ? stats[3] : 1,
            Intelligence = stats[4] > 0 ? stats[4] : 1,
            Agility = stats[5] > 0 ? stats[5] : 1,
            Luck = stats[6] > 0 ? stats[6] : 1
        };
    }
}