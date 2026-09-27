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
    private const string StartupParamsMarker = "user://alpha-params.marker";

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
        foreach (string marker in new[] { StartupMarker, StartupParamsMarker })
        {
            if (FileAccess.FileExists(marker))
            {
                DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(marker));
            }
        }
    }

    /// <summary>
    /// Reads a marker file the mod wrote, returning an empty string when it is absent.
    /// </summary>
    /// <remarks>
    /// Returning a string rather than the file lets a caller assert on the content in one step, and makes a
    /// missing file fail as "wrong content" rather than as a null dereference.
    /// </remarks>
    private static string ReadMarker(string marker)
    {
        using FileAccess? file = FileAccess.Open(marker, FileAccess.ModeFlags.Read);
        return file?.GetAsText() ?? string.Empty;
    }
}
