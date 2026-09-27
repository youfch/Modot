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
    /// Loads the mod, reports what it contributed, shows its packed scene in the viewport, and then keeps
    /// running so the loaded state can be inspected in the editor's remote scene tree. It never quits, so
    /// it is deliberately absent from Invoke-E2E.ps1 - every scenario there terminates with an exit code.
    /// </summary>
    private void RunWatch(string[] modDirectories)
    {
        List<Mod> mods = ModLoader.LoadMods(modDirectories).ToList();
        this.Check(mods.Count > 0, $"at least one mod loaded (got {mods.Count})");

        foreach (Mod mod in mods)
        {
            GD.Print($"WATCH:mod '{mod.Meta.Id}' ({mod.Meta.Name}) by {mod.Meta.Author}");
            GD.Print($"WATCH:assemblies={mod.Assemblies.Count()} patches={mod.Patches.Count()}");
            GD.Print($"WATCH:data={mod.Data?.OuterXml}");
        }

        const string scenePath = "res://Scenes/BoxRot.tscn";
        if (GD.Load<PackedScene>(scenePath) is PackedScene scene)
        {
            Node instance = scene.Instantiate();
            this.AddChild(instance);
            GD.Print($"WATCH:added {scenePath} under {this.GetPath()}");
            GD.Print($"WATCH:script bound = {instance.GetScript().VariantType != Variant.Type.Nil}");
        }
        else
        {
            GD.Print($"WATCH:{scenePath} is not reachable - is Resources/assets.pck present?");
        }

        // The camera and the light are authored in Host.tscn, so the mod's 3D content shows up without the
        // host building any of it. This only reports that a camera is active, since nothing renders without one.
        GD.Print($"WATCH:camera current={this.GetViewport()?.GetCamera3D()?.IsCurrent()} viewport={this.GetViewport()?.GetVisibleRect().Size}");
    }
}
