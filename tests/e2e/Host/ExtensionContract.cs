using System.Collections.Generic;

namespace ModotE2E.Api
{
    /// <summary>
    /// Stands in for the API a host application publishes for mods to implement.
    /// </summary>
    /// <remarks>
    /// This is the application's side of the arrangement: the application owns the interface and the registry,
    /// a mod implements the interface and registers itself, and the application ends up holding real instances
    /// without ever naming the mod's types or constructing them by reflection.
    ///
    /// The assembly this lives in matters. It is the host's own, so a mod that compiles against it resolves to
    /// the same assembly identity at runtime - and that identity is the entire requirement for the interface to
    /// match at all. Compile the mod against a different copy and the mod's implementation satisfies nothing:
    /// no error, just an empty registry.
    /// </remarks>
    public interface IModExtension
    {
        /// <summary>
        /// Called by the host once every mod has loaded and every patch has been applied.
        /// </summary>
        /// <returns>A short description, so the host can show that the call arrived.</returns>
        string Describe();
    }

    /// <summary>
    /// The registry a mod registers itself into.
    /// </summary>
    public static class ModExtensionRegistry
    {
        private static readonly List<IModExtension> extensions = new();

        /// <summary>
        /// Everything mods have registered, in the order they registered it.
        /// </summary>
        public static IReadOnlyList<IModExtension> Extensions => extensions;

        /// <summary>
        /// Registers an implementation. Called by mods, from a <c>[ModStartup]</c> method.
        /// </summary>
        public static void Add(IModExtension extension) => extensions.Add(extension);
    }
}
