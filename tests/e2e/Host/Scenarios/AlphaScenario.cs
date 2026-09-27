using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Godot;
using Godot.Modding;
using Godot.Serialization;

public partial class Host
{
    private void RunAlpha(string[] modDirectories)
    {
        RemoveMarker();

        List<Mod> mods = ModLoader.LoadMods(modDirectories).ToList();

        this.Check(mods.Count is 1, $"exactly one mod loaded (got {mods.Count})");
        if (mods.Count is 0)
        {
            return;
        }

        Mod mod = mods[0];
        this.Check(mod.Meta.Id is "alpha", $"mod id is 'alpha' (got '{mod.Meta.Id}')");
        this.Check(mod.Meta.Name is "Alpha Mod", $"mod name is 'Alpha Mod' (got '{mod.Meta.Name}')");

        XmlElement? root = mod.Data?.DocumentElement;
        this.Check(root is not null, "mod data was loaded");
        if (root is null)
        {
            return;
        }

        this.Check(root.SelectSingleNode("Items") is not null, "mod data contains its own Items element");

        XmlElement? boosted = root.SelectSingleNode("Boosted") as XmlElement;
        this.Check(boosted is not null, "the patch appended a Boosted element to the data root");
        this.Check(boosted?.GetAttribute("by") is "alpha", "the patched element carries by=\"alpha\"");

        this.Check(FileAccess.FileExists(StartupMarker), "the [ModStartup] method ran (marker file exists)");

        // Asserted separately from the plain form, on purpose: "the method ran" and "the method received the
        // arguments the attribute declared" are different claims, and only the second one covers the
        // parameterised attribute. A boolean marker could not tell them apart.
        string parameters = ReadMarker(StartupParamsMarker);
        GD.Print($"PARAMS:{parameters}");
        this.Check(parameters is "alpha-mod:7",
            $"the parameterised [ModStartup] received the declared arguments (got '{parameters}')");
    }
}
