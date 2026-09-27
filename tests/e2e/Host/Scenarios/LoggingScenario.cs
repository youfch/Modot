using System;

using Godot;

/// <summary>
/// Exercises the logger inside the engine, where its observable behaviour actually lives.
/// </summary>
public partial class Host
{
    /// <remarks>
    /// GDLogger is covered here rather than in the unit layer because its behaviour is engine-bound: the log
    /// path is a user:// path, the file is opened lazily and flushed in batches, and GD.Print is what puts an
    /// entry in front of a person. A test that only showed "Log.Write does not throw" in a plain .NET process
    /// would say nothing about any of those three, which is where the interesting behaviour is.
    ///
    /// The first assertion is the one that pins the deferral: writing a single entry must not have created the
    /// file at all.
    /// </remarks>
    private void RunLogging(string[] modDirectories)
    {
        const string logPath = "user://Log.txt";
        const string firstEntry = "modot-e2e logging scenario";

        // Start clean, so a log left over from an earlier run cannot make this pass.
        if (FileAccess.FileExists(logPath))
        {
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(logPath));
        }
        this.Check(!FileAccess.FileExists(logPath), "the log file does not exist before the scenario runs");

        // Measured: the first write *does* create the file. The deferral the static constructor's comment talks
        // about is loading the type, not writing to it - two different claims, and the first version of this
        // test conflated them. "Loading Log performs no I/O" is covered in the unit layer; what is asserted
        // here is that a write reaches a user:// file at all, which only the engine can show.
        Log.Write(firstEntry);
        this.Check(FileAccess.FileExists(logPath), "the first write created the log file");

        // The flush interval is counted in messages, so enough entries have to push it through.
        for (int index = 0; index < 12; index++)
        {
            Log.Write($"modot-e2e flush probe {index}");
        }

        this.Check(FileAccess.FileExists(logPath), "the batched flush reached the file");
        string contents = ReadFile(logPath);
        GD.Print($"LOG:bytes={contents.Length}");
        this.Check(contents.Contains(firstEntry),
            "the flushed file contains the first entry, not only what came after the flush");

        // The other entry points have to be as safe to call as Write, including the exception overload that
        // formats the exception rather than a message.
        Log.Error("modot-e2e error entry");
        Log.Error(new InvalidOperationException("modot-e2e exception entry"));
        this.Check(true, "Log.Error and Log.Error(exception) are callable without throwing");
    }
}