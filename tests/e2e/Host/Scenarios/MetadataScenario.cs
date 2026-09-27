using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Godot;
using Godot.Modding;
using Godot.Serialization;

public partial class Host
{
    /// <summary>
    /// Diagnostic scenario: prints how a Mod.xml actually deserialized, then prints the canonical XML
    /// the serializer produces for it. Used to derive fixture shapes from measurement instead of guessing.
    /// </summary>
    private void RunMetadata(string[] modDirectories)
    {
        foreach (string directory in modDirectories)
        {
            Mod.Metadata metadata = Mod.Metadata.Load(directory);
            GD.Print($"META:{metadata.Id}");
            GD.Print($"  Dependencies=[{string.Join(",", metadata.Dependencies)}]");
            GD.Print($"  Before=[{string.Join(",", metadata.Before)}]");
            GD.Print($"  After=[{string.Join(",", metadata.After)}]");
            GD.Print($"  Incompatible=[{string.Join(",", metadata.Incompatible)}]");

            try
            {
                Serializer serializer = new();
                XmlNode reserialized = serializer.Serialize(metadata, typeof(Mod.Metadata));
                GD.Print($"  RESERIALIZED:{reserialized.OuterXml}");
            }
            catch (Exception exception)
            {
                GD.Print($"  RESERIALIZE-FAILED:{exception.GetType().Name}: {exception.Message}");
            }
        }

        this.Check(true, "metadata dump completed");
    }
}
