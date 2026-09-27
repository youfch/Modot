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
    /// Asserts that a declared load-order relationship overrides the order the directories were passed in.
    /// </summary>
    /// <remarks>
    /// FINDING: Modot's <c>Before</c> and <c>After</c> metadata behave as each other's documented meaning.
    /// Measured on Godot 4.7.2: declaring <c>Before: X</c> loads X *after* the declaring mod, and declaring
    /// <c>After: X</c> loads X *before* it — the opposite of both doc comments in <c>Mod.Metadata</c>.
    /// This assertion therefore targets what is observable and useful (a declaration reorders the load),
    /// and pins the arrangement the documentation promises. Input order is deliberately the reverse of what
    /// the declaration asks for, so a no-op sort cannot satisfy it. After was the inverted list until the
    /// SortModMetadata fix; it now means what its own documentation says.
    /// </remarks>
    private void RunOrder(string[] modDirectories)
    {
        List<string> ids = ModLoader.LoadMods(modDirectories).Select(mod => mod.Meta.Id).ToList();
        GD.Print($"ORDER:[{string.Join(",", ids)}]");
        GD.Print($"INPUT:[{string.Join(",", modDirectories.Select(System.IO.Path.GetFileName))}]");

        this.Check(ids.Count is 2, $"two mods loaded (got {ids.Count})");
        this.Check(ids.Count is 2 && ids[0] is "after-a" && ids[1] is "after-b",
            $"the declared relationship reordered the load to [after-a,after-b] (got [{string.Join(",", ids)}])");
    }
}
