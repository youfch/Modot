using System;
using System.IO;
using System.Runtime.CompilerServices;

using Xunit;

using Godot.Modding;

namespace Modot.Tests
{
    /// <summary>
    /// Covers the logger's loading behaviour: touching the logger type must not perform file I/O,
    /// so that it stays safe in a process without the engine.
    /// </summary>
    /// <remarks>
    /// <c>Godot.Log</c> is provided by the vendored GDLogger project, so it is referenced by type rather
    /// than looked up inside the Modot assembly.
    /// </remarks>
    public class LoggingTests
    {
        [Fact]
        public void LoggerTypeExists()
        {
            Assert.NotNull(GetLoggerType());
        }

        [Fact]
        public void LoadingLoggerTypeDoesNotThrow()
        {
            Type loggerType = GetLoggerType();

            Exception? exception = Record.Exception(() => RuntimeHelpers.RunClassConstructor(loggerType.TypeHandle));

            Assert.Null(exception);
        }

        [Fact]
        public void LoadingLoggerTypeCreatesNoLogFile()
        {
            Type loggerType = GetLoggerType();

            // Outside the engine the user:// scheme does not resolve, so a naive implementation would
            // create a file relative to the working directory. Asserting on that is a proxy for
            // "the type initialiser performs no file I/O".
            string relative = Path.Combine(Environment.CurrentDirectory, "Log.txt");
            File.Delete(relative);

            RuntimeHelpers.RunClassConstructor(loggerType.TypeHandle);

            Assert.False(File.Exists(relative));
        }

        private static Type GetLoggerType()
        {
            // Godot.Log comes from the vendored GDLogger assembly, not from the Modot assembly
            return typeof(Godot.Log);
        }
    }
}
