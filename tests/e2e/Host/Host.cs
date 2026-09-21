using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

using Godot;

using Godot.Modding;
using Godot.Serialization;

/// <summary>
/// Headless host that loads mod directories through Modot and asserts the outcome.
/// Receives "&lt;scenario&gt; &lt;mod-directory&gt;..." after a "--" separator.
/// Exits 0 only when every assertion of the scenario passed.
/// </summary>
public partial class Host : Node
{
    private const string StartupMarker = "user://alpha-startup.marker";

    private readonly List<string> failures = new();
    private int passed;

    public override void _Ready()
    {
        string[] arguments = OS.GetCmdlineUserArgs();
        if (arguments.Length < 2)
        {
            GD.Print("FAIL:expected <scenario> <mod-directory>... after --");
            GetTree().Quit(1);
            return;
        }

        string scenario = arguments[0];
        string[] modDirectories = arguments[1..];

        GD.Print($"SCENARIO:{scenario}");
        GD.Print($"ENGINE:{Engine.GetVersionInfo()["string"]}");

        try
        {
            switch (scenario)
            {
                case "alpha":
                    this.RunAlpha(modDirectories);
                    break;
                case "metadata":
                    this.RunMetadata(modDirectories);
                    break;
                case "order":
                    this.RunOrder(modDirectories);
                    break;
                case "cycle":
                    this.RunCycle(modDirectories);
                    break;
                case "duplicate":
                case "incompatible":
                    this.RunExpectLoaded(modDirectories, 1);
                    break;
                case "missing-dep":
                    this.RunExpectLoaded(modDirectories, 0);
                    break;
                case "patches":
                    this.RunPatches(modDirectories);
                    break;
                default:
                    this.Fail($"unknown scenario '{scenario}'");
                    break;
            }
        }
        catch (Exception exception)
        {
            this.Fail($"unhandled exception: {exception.GetType().Name}: {exception.Message}");
        }

        GD.Print($"SUMMARY:passed={this.passed} failed={this.failures.Count}");
        GD.Print($"RESULT:{(this.failures.Count is 0 ? "PASS" : "FAIL")}");
        GetTree().Quit(this.failures.Count is 0 ? 0 : 1);
    }

    private void RunAlpha(string[] modDirectories)
    {
        RemoveMarker();

        List<Mod> mods = ModLoader.LoadMods(modDirectories).ToList();

        this.Check(mods.Count is 1, $"exactly one mod loaded (got {mods.Count})");
        if (mods.Count is 0)
        {
            return;
        }

        Mod mod = mods[0];
        this.Check(mod.Meta.Id is "alpha", $"mod id is 'alpha' (got '{mod.Meta.Id}')");
        this.Check(mod.Meta.Name is "Alpha Mod", $"mod name is 'Alpha Mod' (got '{mod.Meta.Name}')");

        XmlElement? root = mod.Data?.DocumentElement;
        this.Check(root is not null, "mod data was loaded");
        if (root is null)
        {
            return;
        }

        this.Check(root.SelectSingleNode("Items") is not null, "mod data contains its own Items element");

        XmlElement? boosted = root.SelectSingleNode("Boosted") as XmlElement;
        this.Check(boosted is not null, "the patch appended a Boosted element to the data root");
        this.Check(boosted?.GetAttribute("by") is "alpha", "the patched element carries by=\"alpha\"");

        this.Check(FileAccess.FileExists(StartupMarker), "the [ModStartup] method ran (marker file exists)");
    }

    /// <summary>
    /// Diagnostic scenario: prints how a Mod.xml actually deserialized, then prints the canonical XML
    /// the serializer produces for it. Used to derive fixture shapes from measurement instead of guessing.
    /// </summary>
    private void RunMetadata(string[] modDirectories)
    {
        foreach (string directory in modDirectories)
        {
            Mod.Metadata metadata = Mod.Metadata.Load(directory);
            GD.Print($"META:{metadata.Id}");
            GD.Print($"  Dependencies=[{string.Join(",", metadata.Dependencies)}]");
            GD.Print($"  Before=[{string.Join(",", metadata.Before)}]");
            GD.Print($"  After=[{string.Join(",", metadata.After)}]");
            GD.Print($"  Incompatible=[{string.Join(",", metadata.Incompatible)}]");

            try
            {
                Serializer serializer = new();
                XmlNode reserialized = serializer.Serialize(metadata, typeof(Mod.Metadata));
                GD.Print($"  RESERIALIZED:{reserialized.OuterXml}");
            }
            catch (Exception exception)
            {
                GD.Print($"  RESERIALIZE-FAILED:{exception.GetType().Name}: {exception.Message}");
            }
        }

        this.Check(true, "metadata dump completed");
    }

    /// <summary>
    /// Asserts that a declared load-order relationship overrides the order the directories were passed in.
    /// </summary>
    /// <remarks>
    /// FINDING: Modot's <c>Before</c> and <c>After</c> metadata behave as each other's documented meaning.
    /// Measured on Godot 4.7.2: declaring <c>Before: X</c> loads X *after* the declaring mod, and declaring
    /// <c>After: X</c> loads X *before* it — the opposite of both doc comments in <c>Mod.Metadata</c>.
    /// This assertion therefore targets what is observable and useful (a declaration reorders the load),
    /// and pins the measured arrangement so that fixing the inversion trips this test on purpose.
    /// </remarks>
    private void RunOrder(string[] modDirectories)
    {
        List<string> ids = ModLoader.LoadMods(modDirectories).Select(mod => mod.Meta.Id).ToList();
        GD.Print($"ORDER:[{string.Join(",", ids)}]");
        GD.Print($"INPUT:[{string.Join(",", modDirectories.Select(System.IO.Path.GetFileName))}]");

        this.Check(ids.Count is 2, $"two mods loaded (got {ids.Count})");
        this.Check(ids.Count is 2 && ids[0] is "after-b" && ids[1] is "after-a",
            $"the declared relationship reordered the load to [after-b,after-a] (got [{string.Join(",", ids)}])");
    }

    private void RunCycle(string[] modDirectories)
    {
        List<string> ids = ModLoader.LoadMods(modDirectories).Select(mod => mod.Meta.Id).ToList();
        GD.Print($"ORDER:[{string.Join(",", ids)}]");

        this.Check(ids.Count < 2, $"the cycle kept both mods from loading (got {ids.Count})");
    }

    private void RunExpectLoaded(string[] modDirectories, int expected)
    {
        List<string> ids = ModLoader.LoadMods(modDirectories).Select(mod => mod.Meta.Id).ToList();
        GD.Print($"LOADED:[{string.Join(",", ids)}]");

        this.Check(ids.Count == expected, $"expected {expected} mod(s) loaded (got {ids.Count})");
    }

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
        GD.Print($"ATTRS:mark={mark} cond={cond} cond2={cond2}");

        this.Check(mark is "set", $"AttributeSetPatch applied (mark='{mark}')");
        this.Check(cond is "hit", $"ConditionalPatch took the success branch (cond='{cond}')");
        this.Check(cond2 is "miss", $"ConditionalPatch took the failure branch (cond2='{cond2}')");

        XmlElement? item = root.SelectSingleNode("//Item") as XmlElement;
        this.Check(item is not null, "the Item element is present");
        this.Check(item is not null && !item.HasAttribute("temp"),
            "TargetedPatch + AttributeRemovePatch removed the temp attribute");
    }

    private void Check(bool condition, string description)
    {
        if (condition)
        {
            this.passed += 1;
            GD.Print($"PASS:{description}");
            return;
        }

        this.Fail(description);
    }

    private void Fail(string description)
    {
        this.failures.Add(description);
        GD.Print($"FAIL:{description}");
    }

    private static void RemoveMarker()
    {
        if (FileAccess.FileExists(StartupMarker))
        {
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(StartupMarker));
        }
    }
}
