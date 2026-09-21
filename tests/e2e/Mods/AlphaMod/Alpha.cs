using Godot;

using Godot.Modding;

namespace AlphaMod
{
    /// <summary>
    /// The code a mod ships. Used by the e2e suite to prove that a separately compiled mod assembly
    /// has its <c>[ModStartup]</c> method discovered and invoked through Modot.
    /// </summary>
    public static class Alpha
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
    }
}
