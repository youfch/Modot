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
public partial class Host : Node3D
{
    private const string StartupMarker = "user://alpha-startup.marker";

    private readonly List<string> failures = new();
    private int passed;

    /// <summary>
    /// The scenario used when the host is started without arguments, i.e. when it is launched from the
    /// editor. "watch" keeps the process alive after loading so the mod can actually be inspected.
    /// </summary>
    private const string DefaultScenario = "watch";

    /// <summary>
    /// The mod directory used when the host is started without arguments, which is what happens when it is
    /// launched from the editor. Derived from this project's own location rather than hardcoded to a
    /// machine path, so that a clone on another machine still works (see AGENTS.md on hardcoded paths).
    /// </summary>
    private static string DefaultModDirectory => System.IO.Path.GetFullPath(
        System.IO.Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", "Mods", "AlphaMod"));

    public override void _Ready()
    {
        string[] arguments = OS.GetCmdlineUserArgs();
        string scenario;
        string[] modDirectories;
        if (arguments.Length < 2)
        {
            // Started without a scenario, e.g. from the editor, which does not append arguments.
            scenario = Host.DefaultScenario;
            modDirectories = new[] {Host.DefaultModDirectory};
            GD.Print($"NOTE:no arguments given - defaulting to scenario '{scenario}' with '{modDirectories[0]}'");
        }
        else
        {
            scenario = arguments[0];
            modDirectories = arguments[1..];
        }

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
                case "order-before":
                    this.RunBefore(modDirectories);
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
                // The fixture's root element is not <Mod>, so loading must throw rather than be reported as
                // "nothing loaded". The host catches it and exits nonzero; Invoke-E2E.ps1 expects that.
                case "invalid-root":
                    this.RunExpectLoaded(modDirectories, 0);
                    break;
                case "cross-patch":
                    this.RunCrossPatch(modDirectories);
                    break;
                case "no-assemblies":
                    this.RunNoAssemblies(modDirectories);
                    break;
                case "single-load":
                    this.RunSingleLoad(modDirectories);
                    break;
                case "reload":
                    this.RunReload(modDirectories);
                    break;
                // These two cannot finish loading: one ships a resource pack that is not a pack, the other a
                // patch document that is not a patch. LoadMods must throw rather than quietly return a shorter
                // sequence, and Invoke-E2E.ps1 expects the nonzero exit code that follows.
                case "broken-pack":
                case "bad-patch":
                case "bad-patch-type":
                    this.RunExpectLoaded(modDirectories, 0);
                    break;
                case "patches":
                    this.RunPatches(modDirectories);
                    break;
                case "pack":
                    this.RunPack(modDirectories);
                    break;
                case "watch":
                    this.RunWatch(modDirectories);
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

        // "watch" exists to inspect a loaded mod in the editor, so it deliberately stays alive. Every
        // other scenario is a batch check whose verdict is the exit code, so it must not be used there
        // (Invoke-E2E.ps1 never runs "watch": it would hang).
        if (scenario is "watch")
        {
            GD.Print("WATCH:staying alive - inspect the remote scene tree or the window; close it to end");
            return;
        }

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

    /// <summary>
    /// Asserts that one mod's patch reaches another mod's data - the whole point of the patch system, and
    /// until now the one thing it was never asked to do.
    /// </summary>
    /// <remarks>
    /// <c>LoadMods</c> applies each mod's patches to the data roots of every mod loaded so far, so the
    /// overlay's targeted patch selects a node inside the base mod's document. The overlay's own document is
    /// asserted to still have no such node, so a patch landing on the wrong document cannot pass.
    /// </remarks>
    private void RunCrossPatch(string[] modDirectories)
    {
        List<Mod> mods = ModLoader.LoadMods(modDirectories).ToList();
        GD.Print($"LOADED:[{string.Join(",", mods.Select(mod => mod.Meta.Id))}]");

        this.Check(mods.Count is 2, $"two mods loaded (got {mods.Count})");
        if (mods.Count is not 2)
        {
            return;
        }

        Mod? baseMod = mods.FirstOrDefault(mod => mod.Meta.Id is "cp-base");
        Mod? overlay = mods.FirstOrDefault(mod => mod.Meta.Id is "cp-overlay");
        this.Check(baseMod is not null && overlay is not null, "both cross-patch fixtures loaded");

        XmlElement? baseRoot = baseMod?.Data?.DocumentElement;
        XmlElement? overlayRoot = overlay?.Data?.DocumentElement;
        this.Check(baseRoot is not null && overlayRoot is not null, "both mods loaded their data");
        if (baseRoot is null || overlayRoot is null)
        {
            return;
        }

        XmlNode? item = baseRoot.SelectSingleNode("//Item[@id='cp-base-item']");
        this.Check(item is XmlElement, "the base mod's item was found in the base mod's data");
        this.Check(item?.Attributes?["patched-by"]?.Value is "cp-overlay",
            $"the overlay's patch reached the base mod's item (got '{item?.Attributes?["patched-by"]?.Value}')");
        this.Check(overlayRoot.SelectNodes("//Item")?.Count is 0,
            "the overlay's own data was left alone, so the patch crossed mods rather than self-applying");
    }

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
    }

    /// <summary>
    /// Loads one mod through <c>LoadMod</c>, which by design ignores dependencies and load order, and pins
    /// how many times that mod's patches end up applied to its own data.
    /// </summary>
    /// <remarks>
    /// The fixture declares a dependency that is not present, so loading it here can only succeed if
    /// <c>LoadMod</c> skips the dependency check that <c>LoadMods</c> performs - which is the behaviour under
    /// assertion. The patch is a NodeAddPatch rather than an AttributeSetPatch because it appends: a set is
    /// idempotent, so it would look the same whether applied once or twice and could not show the count.
    /// </remarks>
    private void RunSingleLoad(string[] modDirectories)
    {
        Mod mod = ModLoader.LoadMod(modDirectories[0]);

        this.Check(mod.Meta.Id is "missing-dep",
            $"LoadMod loaded the mod that LoadMods rejects for its missing dependency (got '{mod.Meta.Id}')");

        XmlElement? root = mod.Data?.DocumentElement;
        this.Check(root is not null, "the individually loaded mod has data");
        if (root is null)
        {
            return;
        }

        int applied = root.SelectNodes("Applied")?.Count ?? -1;
        GD.Print($"APPLIED:{applied}");
        this.Check(applied is 1, $"LoadMod applied the mod's own patch exactly once (got {applied})");
    }

    /// <summary>
    /// Loads the same mod directory twice in one process and pins what the second load does.
    /// </summary>
    /// <remarks>
    /// <c>LoadedMods</c> is static, and the duplicate-ID check in <c>LoadModMetadata</c> only compares the
    /// directories of the call it is in - it never looks at the registry of already-loaded mods. So the
    /// second load passes that check, and the collision surfaces only when the freshly built Mod is added to
    /// the registry.
    ///
    /// MEASURED: the second load throws <see cref="ArgumentException"/> ("An item with the same key has
    /// already been added. Key: alpha"). This assertion pins that deliberately, because it is a defect: what
    /// a second load *ought* to do is a product question, but an unhandled dictionary exception escaping
    /// <c>LoadMods</c> is not the answer, and it is not the <c>ModLoadException</c> with "Duplicate ID" that
    /// the within-a-call path produces. Fixing it trips this assertion on purpose.
    ///
    /// What the second load does *not* do is patch the first load's data again: it throws before reaching the
    /// patching, which is why the Boosted count is asserted unchanged rather than merely printed.
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
        try
        {
            ModLoader.LoadMods(modDirectories).ToList();
        }
        catch (Exception exception)
        {
            secondThrew = exception;
        }

        GD.Print($"RELOAD-SECOND-THREW:{secondThrew?.GetType().Name ?? "none"}");
        GD.Print($"RELOAD-AFTER:{firstRoot.SelectNodes("Boosted")?.Count ?? -1}");

        this.Check(before is 1, $"the first load applied the patch exactly once (got {before})");
        this.Check(secondThrew is ArgumentException,
            $"the second load fails with ArgumentException, the duplicate-ID collision escaping unhandled (got {secondThrew?.GetType().Name ?? "no exception"})");
        this.Check(firstRoot.SelectNodes("Boosted")?.Count is 1,
            "the second load did not patch the first load's data again");
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
