using System;
using System.Linq;
using Forge.Data;
using Forge.Data.Records;
using Forge.Data.Terrain;
using NUnit.Framework;

namespace Forge.Tests.Data
{
    /// <summary>Ground layers and a chunk's paint in memory.</summary>
    public class GroundPaintTests
    {
        const int Texels = GroundPaint.TexelCount;

        static readonly RecordId Grass = GroundLayerRecord.IdFor("core", "grass");
        static readonly RecordId Dirt = GroundLayerRecord.IdFor("core", "dirt");
        static readonly RecordId Rock = GroundLayerRecord.IdFor("core", "rock");

        static RecordId Layer(int i) => GroundLayerRecord.IdFor("core", "layer" + i);

        [Test]
        public void GroundLayer_IsNamedUnderGround_WithANameAndAnAssetKey()
        {
            var grass = new GroundLayerRecord(Grass, "Grass", "ground/grass");

            Assert.That((grass.Id.ToString(), grass.DisplayName, grass.AssetKey), Is.EqualTo(("core:ground/grass", "Grass", "ground/grass")));
            Assert.Throws<ArgumentException>(() => new GroundLayerRecord(RecordId.Parse("core:grass"), "Grass", "ground/grass"));
            Assert.Throws<ArgumentException>(() => new GroundLayerRecord(Grass, " ", "ground/grass"));
            Assert.Throws<ArgumentException>(() => new GroundLayerRecord(Grass, "Grass", ""));
        }

        [Test]
        public void GroundLayer_SaysWhatItIsUnderfoot_AsALowercaseWord()
        {
            Assert.That(new GroundLayerRecord(Rock, "Rock", "ground/rock", surface: "stone").Surface, Is.EqualTo("stone"));
            Assert.That(new GroundLayerRecord(Rock, "Rock", "ground/rock").Surface, Is.Null);
            Assert.Throws<ArgumentException>(() => new GroundLayerRecord(Rock, "Rock", "ground/rock", surface: "Stone"));
            Assert.Throws<ArgumentException>(() => new GroundLayerRecord(Rock, "Rock", "ground/rock", surface: "wet stone"));
            Assert.Throws<ArgumentException>(() => new GroundLayerRecord(Rock, "Rock", "ground/rock", surface: ""));
        }

        [Test]
        public void APaintTexel_IsTheMeterOfItsChunkAPointLiesIn()
        {
            Assert.That(WorldGrid.PaintTexelOf(0.5f, 63.9f), Is.EqualTo((0, 63)));
            Assert.That(WorldGrid.PaintTexelOf(64f + 10.2f, -0.5f), Is.EqualTo((10, 63)), "Its own chunk's: the next one east, the one south.");
        }

        [Test]
        public void GroundLayer_GrowsGrassOnlyWhenGivenSome_WithinSaneBounds()
        {
            Assert.That(new GroundLayerRecord(Rock, "Rock", "ground/rock").Grass, Is.Null);

            var grass = new GroundLayerRecord(Grass, "Grass", "ground/grass", new GrassSettings(3f, 0.45f)).Grass;
            Assert.That((grass.Density, grass.Height), Is.EqualTo((3f, 0.45f)));

            Assert.Throws<ArgumentOutOfRangeException>(() => new GrassSettings(0f, 0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GrassSettings(GrassSettings.MaxDensity + 1f, 0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GrassSettings(1f, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GrassSettings(1f, GrassSettings.MaxHeight + 0.1f));
        }

        [Test]
        public void Uniform_ShowsOneLayerEverywhere()
        {
            var paint = GroundPaint.Uniform(Grass);

            Assert.That(paint.Layers, Is.EqualTo(new[] { Grass }));
            Assert.That(paint[0, 0, 0], Is.EqualTo(GroundPaint.Full));
            Assert.That(paint[0, 63, 63], Is.EqualTo(GroundPaint.Full));
            Assert.That(paint.DominantAt(17, 40), Is.EqualTo(Grass));
            Assert.That((paint.IndexOf(Grass), paint.IndexOf(Rock)), Is.EqualTo((0, -1)));
            Assert.That(paint.Load(), Is.SameAs(paint));
        }

        [Test]
        public void Constructor_WantsEachTexelToAddUpToFull_AndValidLayers()
        {
            var half = Enumerable.Repeat((byte)127, Texels).Concat(Enumerable.Repeat((byte)128, Texels)).ToArray();
            Assert.DoesNotThrow(() => new GroundPaint(new[] { Grass, Rock }, half));

            var short1 = (byte[])half.Clone();
            short1[5]--;
            Assert.Throws<ArgumentException>(() => new GroundPaint(new[] { Grass, Rock }, short1), "A texel adding up to 254.");
            Assert.Throws<ArgumentException>(() => new GroundPaint(new[] { Grass }, half), "Weights for two layers, one named.");
            Assert.Throws<ArgumentException>(() => new GroundPaint(new[] { Grass, Grass }, half), "A layer named twice.");
            Assert.Throws<ArgumentException>(() => new GroundPaint(Array.Empty<RecordId>(), Array.Empty<byte>()));
            Assert.Throws<ArgumentException>(() => new GroundPaint(new[] { Grass, default(RecordId) }, half));

            var nine = Enumerable.Range(0, ChunkPaint.MaxLayers + 1).Select(Layer).ToArray();
            Assert.Throws<ArgumentException>(() => new GroundPaint(nine, new byte[nine.Length * Texels]));
        }

        [Test]
        public void FromWeights_ScalesEachTexelToFull_Exactly()
        {
            var weights = new float[3 * Texels];
            for (var t = 0; t < Texels; t++)
            {
                weights[t] = 1f;
                weights[Texels + t] = 1f;
                weights[2 * Texels + t] = 1f;
            }

            weights[0] = 3f; // texel (0, 0): 3 : 1 : 1

            var paint = GroundPaint.FromWeights(new[] { Grass, Dirt, Rock }, weights);

            Assert.That(paint.Layers, Is.EqualTo(new[] { Grass, Dirt, Rock }));
            Assert.That(paint[0, 0, 0] + paint[1, 0, 0] + paint[2, 0, 0], Is.EqualTo(GroundPaint.Full));
            Assert.That(paint[0, 0, 0], Is.EqualTo(153));
            Assert.That(new[] { paint[0, 5, 5], paint[1, 5, 5], paint[2, 5, 5] }, Is.EquivalentTo(new byte[] { 85, 85, 85 }));
            Assert.That(paint.DominantAt(0, 0), Is.EqualTo(Grass));
        }

        [Test]
        public void FromWeights_AlwaysAddsUpExactly_ForAnyAmounts()
        {
            var random = new Random(7);
            var layers = new[] { Grass, Dirt, Rock, Layer(3) };
            var weights = Enumerable.Range(0, layers.Length * Texels).Select(_ => (float)random.NextDouble()).ToArray();

            var paint = GroundPaint.FromWeights(layers, weights);

            for (var z = 0; z < GroundPaint.Resolution; z += 7)
            for (var x = 0; x < GroundPaint.Resolution; x += 5)
                Assert.That(Enumerable.Range(0, paint.Layers.Count).Sum(l => paint[l, x, z]), Is.EqualTo(GroundPaint.Full));
        }

        [Test]
        public void FromWeights_LeavesOutLayersThatShowNowhere_AndTexelsWithNothingShowTheStrongest()
        {
            var weights = new float[3 * Texels];
            for (var t = 1; t < Texels; t++)
                weights[2 * Texels + t] = t < 10 ? 1f : 0.25f; // rock everywhere but texel 0
            for (var t = 1; t < 10; t++)
                weights[Texels + t] = 1f;                       // dirt on a few texels

            var paint = GroundPaint.FromWeights(new[] { Grass, Dirt, Rock }, weights);

            Assert.That(paint.Layers, Is.EqualTo(new[] { Dirt, Rock }), "Grass shows nowhere.");
            Assert.That(paint.DominantAt(0, 0), Is.EqualTo(Rock), "Texel 0 had nothing: the strongest layer overall.");
            Assert.That(paint[paint.IndexOf(Rock), 0, 0], Is.EqualTo(GroundPaint.Full));
        }

        [Test]
        public void FromWeights_KeepsTheStrongestLayers_WhenMoreThanTheMostShow()
        {
            var count = ChunkPaint.MaxLayers + 1;
            var layers = Enumerable.Range(0, count).Select(Layer).ToArray();
            var weights = new float[count * Texels];
            for (var l = 0; l < count; l++)
            for (var t = 0; t < Texels; t++)
                weights[l * Texels + t] = l == 4 ? 0.01f : 1f; // layer 4 is the faintest

            var paint = GroundPaint.FromWeights(layers, weights);

            Assert.That(paint.Layers.Count, Is.EqualTo(ChunkPaint.MaxLayers));
            Assert.That(paint.Layers, Has.No.Member(Layer(4)));
            Assert.That(paint.Layers, Is.Ordered.By(nameof(RecordId.Name)), "Kept in their given order.");
        }

        [Test]
        public void FromWeights_RefusesNothing_NegativeAmounts_AndRepeatedLayers()
        {
            Assert.Throws<ArgumentException>(() => GroundPaint.FromWeights(new[] { Grass }, new float[Texels]), "All zero.");
            var negative = Enumerable.Repeat(1f, Texels).ToArray();
            negative[3] = -1f;
            Assert.Throws<ArgumentOutOfRangeException>(() => GroundPaint.FromWeights(new[] { Grass }, negative));
            Assert.Throws<ArgumentException>(() => GroundPaint.FromWeights(new[] { Grass, Grass }, new float[2 * Texels]));
            Assert.Throws<ArgumentException>(() => GroundPaint.FromWeights(new[] { Grass }, new float[3]));
        }

        [Test]
        public void TerrainChunk_KeepsItsPaint_WhenItsHeightsChange()
        {
            var space = SpaceRecord.IdFor("core", "main");
            var coord = new ChunkCoord(0, 0);
            var paint = GroundPaint.Uniform(Rock);
            var chunk = new TerrainChunkRecord(TerrainChunkRecord.IdFor("core", space, coord), space, coord, HeightField.Flat(50f), paint);

            Assert.That(chunk.WithHeights(HeightField.Flat(60f)).Paint, Is.SameAs(paint));
            Assert.That(chunk.WithPaint(null).Paint, Is.Null);
            Assert.That(chunk.WithPaint(GroundPaint.Uniform(Grass)).Heights, Is.SameAs(chunk.Heights));
        }
    }
}
