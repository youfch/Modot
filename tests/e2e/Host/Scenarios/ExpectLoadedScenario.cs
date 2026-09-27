using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Godot;
using Godot.Modding;
using Godot.Serialization;

public partial class Host
{
    private void RunExpectLoaded(string[] modDirectories, int expected)
    {
        List<string> ids = ModLoader.LoadMods(modDirectories).Select(mod => mod.Meta.Id).ToList();
        GD.Print($"LOADED:[{string.Join(",", ids)}]");

        this.Check(ids.Count == expected, $"expected {expected} mod(s) loaded (got {ids.Count})");
    }
}
