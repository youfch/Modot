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
        string logged = root.GetAttribute("logged");
        string multi = root.GetAttribute("multi");
        string all = root.GetAttribute("all");
        string any = root.GetAttribute("any");
        string neg = root.GetAttribute("neg");
        string modLoaded = root.GetAttribute("modloaded");
        GD.Print($"ATTRS:mark={mark} cond={cond} cond2={cond2} logged={logged} multi={multi} " +
            $"all={all} any={any} neg={neg} modloaded={modLoaded}");

        this.Check(mark is "set", $"AttributeSetPatch applied (mark='{mark}')");
        this.Check(cond is "hit", $"ConditionalPatch took the success branch (cond='{cond}')");
        this.Check(cond2 is "miss", $"ConditionalPatch took the failure branch (cond2='{cond2}')");
        this.Check(logged is "yes", $"LogPatch applied the patch it wraps (logged='{logged}')");
        this.Check(multi is "two",
            $"MultiPatch applied both patches in sequence, the last one winning (multi='{multi}')");

        // The condition kinds. And and Or take the same pair of branches with opposite expectations, which is
        // what pins the difference between them; the two ModLoadedCondition branches are split across the Not
        // fixture (a mod that is not loaded) and this one (a mod that is loaded).
        this.Check(all is "miss", $"AndCondition required every condition to hold (all='{all}')");
        this.Check(any is "hit", $"OrCondition accepted one condition holding (any='{any}')");
        this.Check(neg is "hit", $"NotCondition inverted a failing condition (neg='{neg}')");
        this.Check(modLoaded is "hit",
            $"ModLoadedCondition saw a mod that is registered, including the one being loaded (modloaded='{modLoaded}')");

        XmlElement? item = root.SelectSingleNode("//Item") as XmlElement;
        this.Check(item is not null, "the Item element is present");
        this.Check(item is not null && !item.HasAttribute("temp"),
            "TargetedPatch + AttributeRemovePatch removed the temp attribute");

        this.Check(root.SelectSingleNode("//Item[@id='z']") is null,
            "NodeRemovePatch removed the element it targeted");

        // Two Data files are merged into one document, each file's root becoming a child of the data root. Only
        // presence is asserted: GetFiles does not promise an order, so pinning one would be pinning noise.
        this.Check(root.SelectSingleNode("Items") is not null,
            "the first data file's root is present after the merge");
        this.Check(root.SelectSingleNode("Extra") is not null,
            "the second data file's root was merged in as well");
        this.Check(root.SelectSingleNode("Extra/Marker") is not null,
            "the merged content is intact, not just the second file's root element");

        // NodeReplacePatch is deliberately not asserted here. Adding it produced
        // ArgumentException: The node to be inserted is from a different document context - its Replacement
        // node belongs to the patch file's document while it is inserted into the data document, and XmlNode
        // refuses that. It throws for every real use, since a patch file is always a different document from
        // the data it patches. That is a product defect rather than a fixture problem; pinning it needs its
        // own fixture and scenario, which this change still has to add. See the README's defect table.
    }
}
