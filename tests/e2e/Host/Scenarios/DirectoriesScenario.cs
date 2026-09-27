using System.Collections.Generic;

using Godot;

/// <summary>
/// Covers the directory helpers across two filesystem roots - the one part of the loading path that cannot be
/// exercised with fixtures, because it needs a second root to exist.
/// </summary>
public partial class Host
{
    private const string DirectoryScenarioTarget = "user://modot-directories";
    private const string DirectoryScenarioCopy = "user://modot-directories/copied.godot";

    /// <summary>
    /// Builds a directory tree in one root, copies a file into it from another root, lists it, and cleans up.
    /// </summary>
    /// <remarks>
    /// A DirAccess instance only operates within the root it was opened on, so crossing from res:// to user://
    /// has to go through the absolute-path forms (MakeDirRecursiveAbsolute, CopyAbsolute). The instance
    /// methods appear to work and then do nothing on the other root, which reads as a mysteriously missing file
    /// rather than as a misuse - so the assertion messages name the convention instead of only the symptom.
    /// </remarks>
    private void RunDirectories(string[] modDirectories)
    {
        const string sourceFile = "res://project.godot";

        // Start from a clean slate: a leftover from an earlier run must not be able to make this pass.
        CleanupDirectories();
        this.Check(!DirAccess.DirExistsAbsolute(DirectoryScenarioTarget),
            "the target directory does not exist before the scenario runs");

        Error made = DirAccess.MakeDirRecursiveAbsolute(DirectoryScenarioTarget);
        this.Check(made is Error.Ok, $"MakeDirRecursiveAbsolute created the tree (got {made})");
        this.Check(DirAccess.DirExistsAbsolute(DirectoryScenarioTarget),
            "the target directory exists - the recursive form, not just one level");

        Error copied = DirAccess.CopyAbsolute(sourceFile, DirectoryScenarioCopy);
        this.Check(copied is Error.Ok,
            $"CopyAbsolute copied across roots with an absolute path (got {copied})");

        string source = ReadFile(sourceFile);
        string target = ReadFile(DirectoryScenarioCopy);
        this.Check(source.Length > 0, "the source file had content to copy");
        this.Check(target == source, "the copy matches the source, across the two roots");

        List<string> entries = ListDirectory(DirectoryScenarioTarget);
        GD.Print($"DIRS:[{string.Join(",", entries)}]");
        this.Check(entries.Contains("copied.godot"), "listing the directory found the copied file");
        this.Check(entries.Count is 1, $"the listing has exactly that one entry (got {entries.Count})");

        // Cleaned up at the end rather than at the start of the next run, so a scenario that fails part way
        // does not leave the user directory populated for whatever runs after it.
        CleanupDirectories();
        this.Check(!DirAccess.DirExistsAbsolute(DirectoryScenarioTarget),
            "the scenario removed what it created");
    }

    /// <summary>
    /// Lists a directory by opening it for that root and pairing ListDirBegin with ListDirEnd.
    /// </summary>
    /// <remarks>
    /// The pairing is the point: the enumeration is state held on the DirAccess instance, so leaving it
    /// unfinished leaks that state into the next use of the same instance.
    /// </remarks>
    private static List<string> ListDirectory(string path)
    {
        List<string> entries = new();
        using DirAccess? directory = DirAccess.Open(path);
        if (directory is null)
        {
            return entries;
        }

        directory.ListDirBegin();
        for (string entry = directory.GetNext(); entry != string.Empty; entry = directory.GetNext())
        {
            // ListDirBegin includes the navigational entries unless asked not to.
            if (entry is not "." && entry is not "..")
            {
                entries.Add(entry);
            }
        }
        directory.ListDirEnd();

        return entries;
    }

    private static string ReadFile(string path)
    {
        using FileAccess? file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        return file?.GetAsText() ?? string.Empty;
    }

    private static void CleanupDirectories()
    {
        DirAccess.RemoveAbsolute(DirectoryScenarioCopy);
        DirAccess.RemoveAbsolute(DirectoryScenarioTarget);
    }
}
