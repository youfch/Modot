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
    /// Asserts the patch kinds that operate on the data tree: setting and removing attributes,
    /// and a conditional patch taking each of its two branches.
    /// </summary>
    private void RunPatches(string[] modDirectories)
    {
        List<Mod> mods = ModLoader.LoadMods(modDirectories).ToList();
        this.Check(mods.Count is 1, $"exactly one mod loaded (got {mods.Count})");
        if (mods.Count is 0)
        {
            return;
        }

        XmlElement? root = mods[0].Data?.DocumentElement;
        this.Check(root is not null, "mod data was loaded");
        if (root is null)
        {
            return;
        }

        string mark = root.GetAttribute("mark");
        string cond = root.GetAttribute("cond");
        string cond2 = root.GetAttribute("cond2");
        GD.Print($"ATTRS:mark={mark} cond={cond} cond2={cond2}");

        this.Check(mark is "set", $"AttributeSetPatch applied (mark='{mark}')");
        this.Check(cond is "hit", $"ConditionalPatch took the success branch (cond='{cond}')");
        this.Check(cond2 is "miss", $"ConditionalPatch took the failure branch (cond2='{cond2}')");

        XmlElement? item = root.SelectSingleNode("//Item") as XmlElement;
        this.Check(item is not null, "the Item element is present");
        this.Check(item is not null && !item.HasAttribute("temp"),
            "TargetedPatch + AttributeRemovePatch removed the temp attribute");
    }
}
