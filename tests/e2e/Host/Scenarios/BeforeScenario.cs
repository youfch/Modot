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
    /// Asserts the <c>Before</c> direction of a declared load-order relationship.
    /// </summary>
    /// <remarks>
    /// Companion to <see cref="RunOrder"/>. Both scenarios deliberately hand the directories over in the
    /// order the declaration does *not* want, so passing means the declaration actively reordered the load
    /// rather than the input order having been right by accident. The same measured inversion applies:
    /// <c>Before: X</c> loads X *before* the declaring mod, so "order-a declares Before: order-b" is
    /// satisfied by [order-b, order-a] - the reverse of the input, which is why the input is handed over that
    /// way round. Before was the inverted list until the SortModMetadata fix.
    /// </remarks>
    private void RunBefore(string[] modDirectories)
    {
        List<string> ids = ModLoader.LoadMods(modDirectories).Select(mod => mod.Meta.Id).ToList();
        GD.Print($"ORDER:[{string.Join(",", ids)}]");
        GD.Print($"INPUT:[{string.Join(",", modDirectories.Select(System.IO.Path.GetFileName))}]");

        this.Check(ids.Count is 2, $"two mods loaded (got {ids.Count})");
        this.Check(ids.Count is 2 && ids[0] is "order-b" && ids[1] is "order-a",
            $"the declared relationship reordered the load to [order-b,order-a] (got [{string.Join(",", ids)}])");
    }
}
