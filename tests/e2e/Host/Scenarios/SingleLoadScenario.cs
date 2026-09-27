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
    /// Loads one mod through <c>LoadMod</c>, which by design ignores dependencies and load order, and pins
    /// how many times that mod's patches end up applied to its own data.
    /// </summary>
    /// <remarks>
    /// The fixture declares a dependency that is not present, so loading it here can only succeed if
    /// <c>LoadMod</c> skips the dependency check that <c>LoadMods</c> performs - which is the behaviour under
    /// assertion. The patch is a NodeAddPatch rather than an AttributeSetPatch because it appends: a set is
    /// idempotent, so it would look the same whether applied once or twice and could not show the count.
    /// </remarks>
    private void RunSingleLoad(string[] modDirectories)
    {
        Mod mod = ModLoader.LoadMod(modDirectories[0]);

        this.Check(mod.Meta.Id is "missing-dep",
            $"LoadMod loaded the mod that LoadMods rejects for its missing dependency (got '{mod.Meta.Id}')");

        XmlElement? root = mod.Data?.DocumentElement;
        this.Check(root is not null, "the individually loaded mod has data");
        if (root is null)
        {
            return;
        }

        int applied = root.SelectNodes("Applied")?.Count ?? -1;
        GD.Print($"APPLIED:{applied}");
        this.Check(applied is 1, $"LoadMod applied the mod's own patch exactly once (got {applied})");
    }
}
