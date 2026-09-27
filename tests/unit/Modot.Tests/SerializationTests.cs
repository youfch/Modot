using System;
using System.Collections.Generic;
using System.Xml;

using Xunit;

using Godot;
using Godot.Serialization;

namespace Modot.Tests
{
    /// <summary>
    /// Covers the serializer contract on the paths Modot itself uses. All of these are engine-free.
    /// </summary>
    public class SerializationTests
    {
        [Fact]
        public void RoundTripsScalars()
        {
            ScalarHolder original = new("hello", 3, true);

            ScalarHolder result = SerializeAndDeserialize(original);

            Assert.Equal(original.Text, result.Text);
            Assert.Equal(original.Count, result.Count);
            Assert.Equal(original.Flag, result.Flag);
        }

        [Fact]
        public void RoundTripsCollections()
        {
            CollectionHolder original = new(new List<string> {"a", "b", "c"});

            CollectionHolder result = SerializeAndDeserialize(original);

            Assert.Equal(original.Items, result.Items);
        }

        [Fact]
        public void RoundTripsVector2()
        {
            Vector2Holder original = new(new Vector2(1.5f, -2.25f));

            Vector2Holder result = SerializeAndDeserialize(original);

            Assert.Equal(original.Position.X, result.Position.X, 5);
            Assert.Equal(original.Position.Y, result.Position.Y, 5);
        }

        [Fact]
        public void RoundTripsVector3()
        {
            Vector3Holder original = new(new Vector3(1.5f, -2.25f, 3f));

            Vector3Holder result = SerializeAndDeserialize(original);

            Assert.Equal(original.Position.X, result.Position.X, 5);
            Assert.Equal(original.Position.Y, result.Position.Y, 5);
            Assert.Equal(original.Position.Z, result.Position.Z, 5);
        }

        [Fact]
        public void KeepsVectorTextForm()
        {
            Serializer serializer = new();

            XmlNode node = serializer.Serialize(new Vector2Holder(new Vector2(1.5f, -2.25f)), typeof(Vector2Holder));

            string text = node.OuterXml;
            Assert.Contains("(1.5, -2.25)", text, StringComparison.Ordinal);
        }

        private static T SerializeAndDeserialize<T>(T value) where T : class
        {
            Serializer serializer = new();
            XmlNode node = serializer.Serialize(value, typeof(T));
            return serializer.Deserialize<T>(node)!;
        }

        private sealed class ScalarHolder
        {
            private ScalarHolder()
            {
            }

            public ScalarHolder(string text, int count, bool flag)
            {
                this.Text = text;
                this.Count = count;
                this.Flag = flag;
            }

            [Serialize]
            public string Text
            {
                get;
                private set;
            } = null!;

            [Serialize]
            public int Count
            {
                get;
                private set;
            }

            [Serialize]
            public bool Flag
            {
                get;
                private set;
            }
        }

        private sealed class CollectionHolder
        {
            private CollectionHolder()
            {
            }

            public CollectionHolder(IEnumerable<string> items)
            {
                this.Items = items;
            }

            [Serialize]
            public IEnumerable<string> Items
            {
                get;
                private set;
            } = null!;
        }

        private sealed class Vector2Holder
        {
            private Vector2Holder()
            {
            }

            public Vector2Holder(Vector2 position)
            {
                this.Position = position;
            }

            [Serialize]
            public Vector2 Position
            {
                get;
                private set;
            }
        }

        private sealed class Vector3Holder
        {
            private Vector3Holder()
            {
            }

            public Vector3Holder(Vector3 position)
            {
                this.Position = position;
            }

            [Serialize]
            public Vector3 Position
            {
                get;
                private set;
            }
        }
    }
}
