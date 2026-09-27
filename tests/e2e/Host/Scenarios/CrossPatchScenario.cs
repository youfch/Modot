using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Godot;
using Godot.Modding;
using Godot.Serialization;

public partial class Host
{
    /// <summary>
    /// Asserts that one mod's patch reaches another mod's data - the whole point of the patch system, and
    /// until now the one thing it was never asked to do.
    /// </summary>
    /// <remarks>
    /// <c>LoadMods</c> applies each mod's patches to the data roots of every mod loaded so far, so the
    /// overlay's targeted patch selects a node inside the base mod's document. The overlay's own document is
    /// asserted to still have no such node, so a patch landing on the wrong document cannot pass.
    /// </remarks>
    private void RunCrossPatch(string[] modDirectories)
    {
        List<Mod> mods = ModLoader.LoadMods(modDirectories).ToList();
        GD.Print($"LOADED:[{string.Join(",", mods.Select(mod => mod.Meta.Id))}]");

        this.Check(mods.Count is 2, $"two mods loaded (got {mods.Count})");
        if (mods.Count is not 2)
        {
            return;
        }

        Mod? baseMod = mods.FirstOrDefault(mod => mod.Meta.Id is "cp-base");
        Mod? overlay = mods.FirstOrDefault(mod => mod.Meta.Id is "cp-overlay");
        this.Check(baseMod is not null && overlay is not null, "both cross-patch fixtures loaded");

        XmlElement? baseRoot = baseMod?.Data?.DocumentElement;
        XmlElement? overlayRoot = overlay?.Data?.DocumentElement;
        this.Check(baseRoot is not null && overlayRoot is not null, "both mods loaded their data");
        if (baseRoot is null || overlayRoot is null)
        {
            return;
        }

        XmlNode? item = baseRoot.SelectSingleNode("//Item[@id='cp-base-item']");
        this.Check(item is XmlElement, "the base mod's item was found in the base mod's data");
        this.Check(item?.Attributes?["patched-by"]?.Value is "cp-overlay",
            $"the overlay's patch reached the base mod's item (got '{item?.Attributes?["patched-by"]?.Value}')");
        this.Check(overlayRoot.SelectNodes("//Item")?.Count is 0,
            "the overlay's own data was left alone, so the patch crossed mods rather than self-applying");
    }
}
