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
    /// Asserts that a resource pack shipped by a mod became reachable, and — the part that decides
    /// whether packing scenes with C# scripts is viable at all — that a scene loaded out of that pack
    /// binds its script while the mod assembly is one Modot loaded at runtime.
    /// </summary>
    private void RunPack(string[] modDirectories)
    {
        List<Mod> mods = ModLoader.LoadMods(modDirectories).ToList();
        this.Check(mods.Count is 1, $"exactly one mod loaded (got {mods.Count})");
        if (mods.Count is 0)
        {
            return;
        }

        const string scenePath = "res://Scenes/BoxRot.tscn";

        this.Check(ResourceLoader.Exists(scenePath), $"the packed scene is reachable at {scenePath}");

        PackedScene? scene = GD.Load<PackedScene>(scenePath);
        this.Check(scene is not null, "the packed scene loaded as a PackedScene");
        if (scene is null)
        {
            return;
        }

        Node instance = scene.Instantiate();
        this.Check(instance is not null, "the packed scene instantiated");

        this.AddChild(instance);
        this.Check(instance.IsInsideTree(), "the instance entered the scene tree");

        // Registering the mod assembly with Godot's script bridge (see Mod.LoadAssembly) puts its generated
        // [ScriptPath] types into the registry Godot consults, so the packed scene's script binds and its
        // _Ready runs. Before that registration this assertion was inverted: the script silently did not bind.
        this.Check(instance.GetScript().VariantType != Variant.Type.Nil,
            "the packed scene's C# script bound");
    }
}
