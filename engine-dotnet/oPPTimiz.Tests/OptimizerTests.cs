//oPPTimiz tests.
//Copyright (C) 2025 EDF. GNU General Public License v3 or later.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using oPPTimiz.Core;
using Xunit;

namespace oPPTimiz.Tests
{
    public class OptimizerTests
    {
        private static byte[] BigJpeg(int width = 2400, int height = 1800)
        {
            using (var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb))
            {
                var rectangle = new Rectangle(0, 0, width, height);
                BitmapData data = bitmap.LockBits(rectangle, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
                int byteCount = Math.Abs(data.Stride) * height;
                var noise = new byte[byteCount];
                new Random(1234).NextBytes(noise);
                Marshal.Copy(noise, 0, data.Scan0, byteCount);
                bitmap.UnlockBits(data);

                using (var stream = new MemoryStream())
                {
                    ImageCodecInfo encoder = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
                    using (var parameters = new EncoderParameters(1))
                    {
                        parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 98L);
                        bitmap.Save(stream, encoder, parameters);
                    }
                    return stream.ToArray();
                }
            }
        }

        private static byte[] SmallPng()
        {
            using (var bitmap = new Bitmap(10, 10))
            using (var stream = new MemoryStream())
            {
                bitmap.Save(stream, ImageFormat.Png);
                return stream.ToArray();
            }
        }

        private static byte[] SyntheticOoxml(Dictionary<string, byte[]> media, string relsTarget, string mediaFolder = "ppt/media")
        {
            using (var stream = new MemoryStream())
            {
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
                {
                    AddText(zip, "[Content_Types].xml",
                        "<?xml version=\"1.0\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                        "<Default Extension=\"png\" ContentType=\"image/png\"/>" +
                        "<Default Extension=\"jpeg\" ContentType=\"image/jpeg\"/>" +
                        "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                        "<Default Extension=\"xml\" ContentType=\"application/xml\"/></Types>");
                    AddText(zip, "doc/part.xml", "<?xml version=\"1.0\"?><root embed=\"rId1\"/>");
                    AddText(zip, "doc/_rels/part.xml.rels",
                        "<?xml version=\"1.0\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                        "<Relationship Id=\"rId1\" Type=\"image\" Target=\"../media/" + relsTarget + "\"/></Relationships>");
                    foreach (var item in media)
                        AddBytes(zip, mediaFolder + "/" + item.Key, item.Value);
                }
                return stream.ToArray();
            }
        }

        private static void AddText(ZipArchive zip, string name, string content)
        {
            using (var stream = zip.CreateEntry(name).Open())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(content);
                stream.Write(bytes, 0, bytes.Length);
            }
        }

        private static void AddBytes(ZipArchive zip, string name, byte[] data)
        {
            using (var stream = zip.CreateEntry(name).Open())
                stream.Write(data, 0, data.Length);
        }

        private static Dictionary<string, byte[]> ReadNonMedia(byte[] document)
        {
            var entries = new Dictionary<string, byte[]>();
            using (var stream = new MemoryStream(document))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                foreach (var entry in zip.Entries)
                {
                    if (entry.FullName.ToLowerInvariant().Contains("/media/"))
                        continue;
                    using (var entryStream = entry.Open())
                    using (var buffer = new MemoryStream())
                    {
                        entryStream.CopyTo(buffer);
                        entries[entry.FullName] = buffer.ToArray();
                    }
                }
            }
            return entries;
        }

        [Theory]
        [InlineData("ppt/media")]
        [InlineData("word/media")]
        [InlineData("xl/media")]
        public void OptimizeBytes_ShrinksDocumentWithLargeImage(string mediaFolder)
        {
            var media = new Dictionary<string, byte[]> { { "image1.jpeg", BigJpeg() } };
            byte[] document = SyntheticOoxml(media, "image1.jpeg", mediaFolder);

            byte[] output;
            OptimizationResult result = Optimizer.OptimizeBytes(document, OptimizationLevel.Maximal, true, out output);

            Assert.True(result.NewSize < result.InitialSize);
            Assert.True(result.ImagesOptimized >= 1);
        }

        [Fact]
        public void OptimizeBytes_ProducesValidZip()
        {
            var media = new Dictionary<string, byte[]> { { "image1.jpeg", BigJpeg() } };
            byte[] document = SyntheticOoxml(media, "image1.jpeg");

            byte[] output;
            Optimizer.OptimizeBytes(document, OptimizationLevel.Maximal, true, out output);

            using (var stream = new MemoryStream(output))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                Assert.Contains(zip.Entries, e => e.FullName == "[Content_Types].xml");
                foreach (var entry in zip.Entries)
                {
                    using (var entryStream = entry.Open())
                        entryStream.CopyTo(Stream.Null);
                }
            }
        }

        [Fact]
        public void UnusedMedia_IsPruned()
        {
            var media = new Dictionary<string, byte[]>
            {
                { "image1.png", SmallPng() },
                { "orphan.png", SmallPng() },
            };
            byte[] document = SyntheticOoxml(media, "image1.png");

            byte[] output;
            OptimizationResult result = Optimizer.OptimizeBytes(document, OptimizationLevel.Maximal, true, out output);

            var names = ReadAllNames(output);
            Assert.Contains("ppt/media/image1.png", names);
            Assert.DoesNotContain("ppt/media/orphan.png", names);
            Assert.Equal(1, result.MediaRemoved);
        }

        [Fact]
        public void NoPrune_KeepsOrphans()
        {
            var media = new Dictionary<string, byte[]>
            {
                { "image1.png", SmallPng() },
                { "orphan.png", SmallPng() },
            };
            byte[] document = SyntheticOoxml(media, "image1.png");

            byte[] output;
            OptimizationResult result = Optimizer.OptimizeBytes(document, OptimizationLevel.Maximal, false, out output);

            Assert.Contains("ppt/media/orphan.png", ReadAllNames(output));
            Assert.Equal(0, result.MediaRemoved);
        }

        [Fact]
        public void NonMediaParts_ArePreservedBitForBit()
        {
            var media = new Dictionary<string, byte[]> { { "image1.jpeg", BigJpeg() } };
            byte[] document = SyntheticOoxml(media, "image1.jpeg");

            Dictionary<string, byte[]> before = ReadNonMedia(document);

            byte[] output;
            Optimizer.OptimizeBytes(document, OptimizationLevel.Maximal, true, out output);
            Dictionary<string, byte[]> after = ReadNonMedia(output);

            Assert.Equal(before.Keys.OrderBy(k => k), after.Keys.OrderBy(k => k));
            foreach (var key in before.Keys)
                Assert.Equal(before[key], after[key]);
        }

        [Fact]
        public void OptimizeFile_KeepOriginal_WritesSuffixedFile()
        {
            var media = new Dictionary<string, byte[]> { { "image1.jpeg", BigJpeg() } };
            byte[] document = SyntheticOoxml(media, "image1.jpeg");

            string source = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".pptx");
            string expected = source.Substring(0, source.Length - ".pptx".Length) + "_oPPTimiz.pptx";
            File.WriteAllBytes(source, document);

            try
            {
                string destination;
                OptimizationResult result = Optimizer.OptimizeFile(source, OptimizationLevel.Maximal, true, null, out destination);

                Assert.Equal(expected, destination);
                Assert.True(File.Exists(expected));
                Assert.Equal(document, File.ReadAllBytes(source));
                Assert.True(result.NewSize < result.InitialSize);
            }
            finally
            {
                File.Delete(source);
                if (File.Exists(expected))
                    File.Delete(expected);
            }
        }

        [Fact]
        public void OptimizeFile_UnsupportedExtension_Throws()
        {
            string destination;
            Assert.Throws<ArgumentException>(
                () => Optimizer.OptimizeFile("notes.txt", OptimizationLevel.Maximal, false, null, out destination));
        }

        private static List<string> ReadAllNames(byte[] document)
        {
            using (var stream = new MemoryStream(document))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
                return zip.Entries.Select(e => e.FullName).ToList();
        }
    }
}
