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
    /// Asserts that <c>executeAssemblies: false</c> stops code from running while still applying patches.
    /// </summary>
    /// <remarks>
    /// Patch application precedes the <c>executeAssemblies</c> check in <c>LoadMods</c>, so the flag gates
    /// code execution only. Read this together with the <c>alpha</c> scenario: it loads the same mod with the
    /// default and does find the marker, so the marker's absence here is attributable to the flag.
    /// </remarks>
    private void RunNoAssemblies(string[] modDirectories)
    {
        RemoveMarker();

        List<Mod> mods = ModLoader.LoadMods(modDirectories, executeAssemblies: false).ToList();

        this.Check(mods.Count is 1, $"exactly one mod loaded (got {mods.Count})");
        if (mods.Count is 0)
        {
            return;
        }

        XmlElement? root = mods[0].Data?.DocumentElement;
        this.Check(root?.SelectSingleNode("Boosted") is not null,
            "the mod's patch was applied even though its assemblies were not executed");
        this.Check(!FileAccess.FileExists(StartupMarker),
            "the [ModStartup] method did not run (no marker file)");
        this.Check(!FileAccess.FileExists(StartupParamsMarker),
            "the parameterised [ModStartup] method did not run either (no marker file)");
    }
}
