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

        // Metadata.IsValid runs as an [AfterDeserialization] hook and rejects metadata that contradicts
        // itself. It only started running once GDSerializer's hook lookup stopped matching public members
        // only - before that, all three cases below loaded without complaint. These tests are the guard on
        // that fix, so they have to fail again if the lookup regresses to BindingFlags.Default.

        [Fact]
        public void RejectsLoadOrderListsSharingAnId()
        {
            AssertRejected("""
                <?xml version="1.0" encoding="UTF-8"?>
                <Mod>
                  <Id>probe</Id>
                  <Name>Probe</Name>
                  <Author>probe</Author>
                  <Before Type="System.Collections.Generic.IEnumerable&lt;System.String&gt;">
                    <item>shared</item>
                  </Before>
                  <After Type="System.Collections.Generic.IEnumerable&lt;System.String&gt;">
                    <item>shared</item>
                  </After>
                </Mod>
                """);
        }

        [Fact]
        public void RejectsOwnIdDeclaredInALoadOrderList()
        {
            AssertRejected("""
                <?xml version="1.0" encoding="UTF-8"?>
                <Mod>
                  <Id>probe</Id>
                  <Name>Probe</Name>
                  <Author>probe</Author>
                  <Incompatible Type="System.Collections.Generic.IEnumerable&lt;System.String&gt;">
                    <item>probe</item>
                  </Incompatible>
                </Mod>
                """);
        }

        [Fact]
        public void RejectsDependencyThatIsAlsoIncompatible()
        {
            AssertRejected("""
                <?xml version="1.0" encoding="UTF-8"?>
                <Mod>
                  <Id>probe</Id>
                  <Name>Probe</Name>
                  <Author>probe</Author>
                  <Dependencies Type="System.Collections.Generic.IEnumerable&lt;System.String&gt;">
                    <item>shared</item>
                  </Dependencies>
                  <Incompatible Type="System.Collections.Generic.IEnumerable&lt;System.String&gt;">
                    <item>shared</item>
                  </Incompatible>
                </Mod>
                """);
        }

        /// <summary>
        /// Asserts that metadata contradicting itself is rejected rather than silently accepted.
        /// </summary>
        /// <remarks>
        /// A mod that both requires and conflicts with the same id has no valid load order, so coming back
        /// with loadable metadata is the failure mode this guards against.
        /// </remarks>
        private static void AssertRejected(string modXml)
        {
            string directory = Path.Combine(Path.GetTempPath(), $"modot-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "Mod.xml"), modXml);

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