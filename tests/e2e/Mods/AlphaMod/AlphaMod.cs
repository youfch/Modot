using Godot;

using Godot.Modding;

using ModotE2E.Api;

namespace AlphaMod
{
    /// <summary>
    /// The code a mod ships. Used by the e2e suite to prove that a separately compiled mod assembly
    /// has its <c>[ModStartup]</c> method discovered and invoked through Modot.
    /// </summary>
    public static class AlphaMod
    {
        /// <summary>
        /// Writes a marker file so the host can observe that this method really ran.
        /// </summary>
        [ModStartup]
        public static void Startup()
        {
            using FileAccess? file = FileAccess.Open("user://alpha-startup.marker", FileAccess.ModeFlags.Write);
            file?.StoreString("AlphaMod startup ran");
        }

        /// <summary>
        /// Writes the arguments it was handed to a second marker file, so the host can observe that a
        /// parameterised <c>[ModStartup]</c> runs and receives exactly what the attribute declared.
        /// </summary>
        /// <remarks>
        /// The values are written rather than merely a flag: the attribute stores its arguments as an
        /// <c>object[]</c> and the invocation hands them to the method by reflection, so "did it run" and "did
        /// it receive the right arguments" are two different things that would look identical from a boolean
        /// marker alone.
        /// </remarks>
        [ModStartup("alpha-mod", 7)]
        public static void StartupWithParameters(string name, int count)
        {
            using FileAccess? file = FileAccess.Open("user://alpha-params.marker", FileAccess.ModeFlags.Write);
            file?.StoreString($"{name}:{count}");
        }

        /// <summary>
        /// Hands the host an implementation of its own interface, from a third <c>[ModStartup]</c> method.
        /// </summary>
        /// <remarks>
        /// This is the direction that keeps the host out of the mod's business: the mod constructs its own
        /// implementation (so it controls whatever dependencies it needs) and the host only ever sees the
        /// interface. Nothing here names the type from the host's side.
        /// </remarks>
        [ModStartup]
        public static void RegisterExtension()
        {
            ModExtensionRegistry.Add(new AlphaModExtension());
        }

        private sealed class AlphaModExtension : IModExtension
        {
            public string Describe() => "AlphaMod extension";
        }
    }
}
