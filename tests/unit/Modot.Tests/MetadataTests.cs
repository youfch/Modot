using System;
using System.IO;

using Xunit;

using Godot.Modding;

namespace Modot.Tests
{
    /// <summary>
    /// Covers metadata loading through Modot's real code path, which is engine-free.
    /// </summary>
    public class MetadataTests
    {
        private const string ModXml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <Mod>
              <Id>alpha</Id>
              <Name>Alpha Mod</Name>
              <Author>probe</Author>
            </Mod>
            """;

        [Fact]
        public void LoadsTwoXFormatModXml()
        {
            string directory = CreateModDirectory();

            try
            {
                Mod.Metadata metadata = Mod.Metadata.Load(directory);

                Assert.Equal("alpha", metadata.Id);
                Assert.Equal("Alpha Mod", metadata.Name);
                Assert.Equal("probe", metadata.Author);
                Assert.Equal(directory, metadata.Directory);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Fact]
        public void ReportsMissingMetadataFile()
        {
            string directory = Path.Combine(Path.GetTempPath(), $"modot-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);

            try
            {
                Assert.Throws<ModLoadException>(() => Mod.Metadata.Load(directory));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        private static string CreateModDirectory()
        {
            string directory = Path.Combine(Path.GetTempPath(), $"modot-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "Mod.xml"), ModXml);
            return directory;
        }
    }
}
