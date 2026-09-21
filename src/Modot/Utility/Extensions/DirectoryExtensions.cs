using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

using JetBrains.Annotations;

namespace Godot.Utility.Extensions
{
    /// <summary>
    /// Contains extension methods for <see cref="DirAccess"/>.
    /// </summary>
    /// <remarks>
    /// This type replaces the Godot 3 <c>Godot.Directory</c> helpers, which Godot 4 removed.
    /// Unlike Godot 3's <c>Directory</c>, a <see cref="DirAccess"/> is not a reusable global instance:
    /// <see cref="DirAccess.Open"/> creates a new instance bound to one path, so every member here opens the directory it needs
    /// rather than mutating shared state. Paths that cross filesystem roots (for example <c>res://</c> to <c>user://</c>) are
    /// handled with the absolute-path forms (<see cref="DirAccess.CopyAbsolute"/> and <see cref="DirAccess.MakeDirRecursiveAbsolute"/>),
    /// because an instance method only operates inside the root its instance was opened at.
    /// </remarks>
    [PublicAPI]
    public static class DirectoryExtensions
    {
        /// <summary>
        /// Copies all files from the directory at <paramref name="from"/> to the directory at <paramref name="to"/>.
        /// </summary>
        /// <param name="directory">The <see cref="DirAccess"/> to use when copying files. It is retained for call-shape compatibility with the Godot 3 API; the source directory is opened from <paramref name="from"/>.</param>
        /// <param name="from">The source directory path. It can be an absolute path, or relative to <paramref name="directory"/>.</param>
        /// <param name="to">The destination directory path. It can be an absolute path, or relative to <paramref name="directory"/>.</param>
        /// <param name="recursive">Whether the contents should be copied recursively (i.e. copy files inside subdirectories and so on) or not.</param>
        /// <returns>An array of the paths of all files that were copied from <paramref name="from"/> to <paramref name="to"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string[] CopyContents(this DirAccess directory, string from, string to, bool recursive = false)
        {
            return directory.CopyContentsLazy(from, to, recursive).ToArray();
        }

        /// <summary>
        /// Returns the complete file paths of all files inside <paramref name="directory"/>.
        /// </summary>
        /// <param name="directory">The <see cref="DirAccess"/> to search in.</param>
        /// <param name="recursive">Whether the search should be conducted recursively (return paths of files inside <paramref name="directory"/>'s subdirectories and so on) or not.</param>
        /// <returns>An array of the paths of all files inside <paramref name="directory"/>.</returns>
        [MustUseReturnValue]
        public static string[] GetFiles(this DirAccess directory, bool recursive = false)
        {
            return recursive
                ? directory
                    .GetDirectories(true)
                    .SelectMany(path =>
                    {
                        using DirAccess recursiveDirectory = DirectoryExtensions.Open(path);
                        return recursiveDirectory.GetElementsNonRecursive(true).ToArray();
                    })
                    .Concat(directory.GetElementsNonRecursive(true))
                    .ToArray()
                : directory
                    .GetElementsNonRecursive(true)
                    .ToArray();
        }

        /// <summary>
        /// Returns the complete file paths of all files inside <paramref name="directory"/> whose extensions match any of <paramref name="fileExtensions"/>.
        /// </summary>
        /// <param name="directory">The <see cref="DirAccess"/> to search in.</param>
        /// <param name="recursive">Whether the search should be conducted recursively (return paths of files inside <paramref name="directory"/>'s subdirectories and so on) or not.</param>
        /// <param name="fileExtensions">The file extensions to search for. If none are provided, all file paths are returned.</param>
        /// <returns>An array of the complete file paths of all files inside <paramref name="directory"/> whose extensions match any of <paramref name="fileExtensions"/>.</returns>
        [MustUseReturnValue]
        public static string[] GetFiles(this DirAccess directory, bool recursive = false, params string[] fileExtensions)
        {
            return fileExtensions.Any()
                ? Array.FindAll(directory.GetFiles(recursive), file => fileExtensions.Any(file.EndsWith))
                : directory.GetFiles(recursive);
        }

        /// <summary>
        /// Returns the complete directory paths of all directories inside <paramref name="directory"/>.
        /// </summary>
        /// <param name="directory">The <see cref="DirAccess"/> to search in.</param>
        /// <param name="recursive">Whether the search should be conducted recursively (return paths of directories inside <paramref name="directory"/>'s subdirectories and so on) or not.</param>
        /// <returns>An array of the complete directory paths of all directories inside <paramref name="directory"/>.</returns>
        [MustUseReturnValue]
        public static string[] GetDirectories(this DirAccess directory, bool recursive = false)
        {
            return recursive
                ? directory
                    .GetElementsNonRecursive(false)
                    .SelectMany(path =>
                    {
                        using DirAccess recursiveDirectory = DirectoryExtensions.Open(path);
                        return recursiveDirectory
                            .GetDirectories(true)
                            .Prepend(path);
                    })
                    .ToArray()
                : directory
                    .GetElementsNonRecursive(false)
                    .ToArray();
        }

        /// <summary>
        /// Opens the directory at <paramref name="path"/>.
        /// </summary>
        /// <param name="path">The directory path to open.</param>
        /// <returns>The opened <see cref="DirAccess"/>.</returns>
        /// <exception cref="ErrorException">Thrown if the directory could not be opened.</exception>
        private static DirAccess Open(string path)
        {
            // DirAccess.Open() returns null on failure and leaves the reason in GetOpenError()
            return DirAccess.Open(path) ?? throw new ErrorException(DirAccess.GetOpenError());
        }

        private static IEnumerable<string> GetElementsNonRecursive(this DirAccess directory, bool trueIfFiles)
        {
            directory.ListDirBegin().Throw();
            try
            {
                while (true)
                {
                    string next = directory.GetNext();
                    if (next is "")
                    {
                        yield break;
                    }
                    // Continue if the current element is a file or directory depending on which one is being queried
                    if (directory.CurrentIsDir() == trueIfFiles)
                    {
                        continue;
                    }
                    string current = directory.GetCurrentDir();
                    yield return current.EndsWith("/") ? $"{current}{next}" : $"{current}/{next}";
                }
            }
            finally
            {
                // The enumerator owns the listing stream, so it is closed even when the caller stops enumerating early
                directory.ListDirEnd();
            }
        }

        private static IEnumerable<string> CopyContentsLazy(this DirAccess directory, string from, string to, bool recursive = false)
        {
            using DirAccess source = DirectoryExtensions.Open(from);

            // Create destination directory if it doesn't already exist
            DirAccess.MakeDirRecursiveAbsolute(to).Throw();

            // Replace only the first instance of the destination directory in file and subdirectory paths using regex (string.Replace() replaces all instances)
            Regex fromReplacement = new(Regex.Escape(from));

            // Copy all files inside the source directory non-recursively
            foreach (string fromFile in source.GetElementsNonRecursive(true))
            {
                string toFile = fromReplacement.Replace(fromFile, to, 1);
                DirAccess.CopyAbsolute(fromFile, toFile).Throw();
                yield return toFile;
            }

            if (!recursive)
            {
                yield break;
            }

            // Copy all files recursively
            foreach (string fromSubDirectory in source.GetDirectories(true))
            {
                string toSubDirectory = fromReplacement.Replace(fromSubDirectory, to, 1);
                DirAccess.MakeDirRecursiveAbsolute(toSubDirectory).Throw();

                using DirAccess innerDirectory = DirectoryExtensions.Open(fromSubDirectory);
                foreach (string fromFile in innerDirectory.GetElementsNonRecursive(true))
                {
                    string toFile = fromReplacement.Replace(fromFile, to, 1);
                    DirAccess.CopyAbsolute(fromFile, toFile).Throw();
                    yield return toFile;
                }
            }
        }
    }
}
