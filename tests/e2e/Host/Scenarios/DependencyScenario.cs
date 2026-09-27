using System.Collections.Generic;
using System.Linq;

using Godot;

using Godot.Modding;

/// <summary>
/// Asserts that a dependency which is present keeps its dependent mod.
/// </summary>
public partial class Host
{
    /// <remarks>
    /// The satisfied counterpart of the missing-dep fixture, which covers the mod being dropped.
    ///
    /// Dependencies are a filter, not an ordering rule: SortModMetadata builds its graph from Before and After
    /// only, so a dependency does not say anything about load order. The input order here is deliberately the
    /// reverse of the dependency and the assertion is order-agnostic on purpose - pinning a specific order
    /// would be asserting something the feature never promised, and would break the day someone made
    /// dependencies order the load.
    /// </remarks>
    private void RunDependency(string[] modDirectories)
    {
        List<string> ids = ModLoader.LoadMods(modDirectories).Select(mod => mod.Meta.Id).ToList();
        GD.Print($"DEPENDENCY:[{string.Join(",", ids)}]");

        this.Check(ids.Count is 2, $"meeting the dependency kept both mods (got {ids.Count})");
        this.Check(ids.Contains("dep-base"), "the mod that was depended on is loaded");
        this.Check(ids.Contains("dep-needs-base"), "the mod that declared the dependency is loaded");
    }
}
