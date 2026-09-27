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
    /// Loads the same mod directory twice in one process and pins what the second load does.
    /// </summary>
    /// <remarks>
    /// MEASURED: the second load reports the duplicate and loads nothing. Before the fix this threw
    /// <see cref="ArgumentException"/> from the registry itself ("An item with the same key has already been
    /// added"), because the duplicate check only looked at the directories of the call it was in and never at
    /// the already-loaded registry - so a cross-call duplicate bypassed the "Duplicate ID" handling that the
    /// within-a-call case already had.
    ///
    /// The reason is asserted through the runner's ExpectOutput, because it lives in the log rather than in
    /// the return value: asserting only "nothing loaded" would not tell a duplicate apart from a mod that
    /// failed for some other reason.
    /// </remarks>
    private void RunReload(string[] modDirectories)
    {
        List<Mod> first = ModLoader.LoadMods(modDirectories).ToList();
        this.Check(first.Count is 1, $"the first load loaded exactly one mod (got {first.Count})");
        if (first.Count is not 1)
        {
            return;
        }

        XmlElement? firstRoot = first[0].Data?.DocumentElement;
        this.Check(firstRoot is not null, "the first load's mod has data");
        if (firstRoot is null)
        {
            return;
        }

        int before = firstRoot.SelectNodes("Boosted")?.Count ?? -1;
        GD.Print($"RELOAD-BEFORE:{before}");

        Exception? secondThrew = null;
        int secondCount = -1;
        try
        {
            secondCount = ModLoader.LoadMods(modDirectories).Count();
        }
        catch (Exception exception)
        {
            secondThrew = exception;
        }

        GD.Print($"RELOAD-SECOND:threw={secondThrew?.GetType().Name ?? "none"} mods={secondCount}");
        GD.Print($"RELOAD-AFTER:{firstRoot.SelectNodes("Boosted")?.Count ?? -1}");

        this.Check(before is 1, $"the first load applied the patch exactly once (got {before})");
        this.Check(secondThrew is null,
            $"the second load reports the duplicate without throwing (threw {secondThrew?.GetType().Name ?? "nothing"})");
        this.Check(secondCount is 0,
            $"the second load loaded nothing, because the ID is already registered (got {secondCount})");
        this.Check(firstRoot.SelectNodes("Boosted")?.Count is 1,
            "the second load did not patch the first load's data again");
    }
}
