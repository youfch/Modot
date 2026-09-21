using System;

namespace Godot
{
    /// <summary>
    /// Provides the logging members that Modot uses internally.
    /// </summary>
    /// <remarks>
    /// This type replaces the <c>GDLogger</c> package, which is bound to Godot 3 and has no Godot 4 release.
    /// It is declared <see langword="internal"/> so that it cannot collide with a consumer that still references <c>GDLogger</c>,
    /// and it keeps the <c>Godot.Log</c> name so that every existing call site stays unchanged.
    /// Only the two members Modot uses are provided. <c>GDLogger</c>'s file logging is deliberately not reimplemented,
    /// because it relied on the Godot 3 file API, which Godot 4 removed.
    /// </remarks>
    internal static class Log
    {
        /// <summary>
        /// Writes <paramref name="message"/> to the Godot log.
        /// </summary>
        /// <param name="message">The message to write.</param>
        public static void Write(string message)
        {
            GD.Print(message);
        }

        /// <summary>
        /// Writes the text representation of <paramref name="exception"/> to the Godot log as an error.
        /// </summary>
        /// <param name="exception">The <see cref="Exception"/> to write.</param>
        public static void Error(Exception exception)
        {
            GD.PushError(exception.ToString());
        }
    }
}
