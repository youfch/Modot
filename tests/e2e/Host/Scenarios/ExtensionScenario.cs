using System.Collections.Generic;
using System.Linq;

using Godot;

using Godot.Modding;

using ModotE2E.Api;

/// <summary>
/// Asserts that the host ends up holding real instances of an interface a mod implements.
/// </summary>
public partial class Host
{
    /// <remarks>
    /// This is the arrangement a host application wants: the host owns the interface and the registry, the mod
    /// implements and registers, and the host never names the mod's types or constructs them by reflection.
    ///
    /// Everything asserted here happens after LoadMods returns - which is when every mod's [ModStartup] has
    /// already run, so the registry is in its final state. Counting before the load as well makes the
    /// assertion about this load rather than about whatever happened to be in the registry already.
    /// </remarks>
    private void RunExtension(string[] modDirectories)
    {
        int before = ModExtensionRegistry.Extensions.Count;

        ModLoader.LoadMods(modDirectories).ToList();

        IReadOnlyList<IModExtension> extensions = ModExtensionRegistry.Extensions;
        GD.Print($"EXTENSIONS:before={before} after={extensions.Count}");
        this.Check(extensions.Count == before + 1,
            $"the mod registered exactly one extension (was {before}, now {extensions.Count})");
        if (extensions.Count == 0)
        {
            return;
        }

        // Calling through the interface is the point: if the mod compiled against a different copy of this
        // assembly its implementation would not have satisfied the interface at all, and the count above would
        // have been the thing that noticed.
        string description = extensions[extensions.Count - 1].Describe();
        GD.Print($"EXTENSION:describe={description}");
        this.Check(description is "AlphaMod extension",
            $"the host reached the mod's implementation through its own interface (got '{description}')");
    }
}
