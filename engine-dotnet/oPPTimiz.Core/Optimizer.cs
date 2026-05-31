//oPPTimiz is a tool that reduces Office documents size.
//Copyright (C) 2025 EDF
//This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
//This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
//You should have received a copy of the GNU General Public License along with this program. If not, see https://www.gnu.org/licenses/.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace oPPTimiz.Core
{
    public static class Optimizer
    {
        private static readonly string[] SupportedSuffixes = { ".pptx", ".docx", ".xlsx" };

        /// <summary>
        /// Optimizes an in-memory OOXML document by recompressing images under
        /// */media/, dropping unreferenced media and repacking the archive with
        /// maximum DEFLATE compression.
        /// </summary>
        public static OptimizationResult OptimizeBytes(byte[] content, OptimizationLevel level, bool pruneUnused, out byte[] output)
        {
            int maxDimension = level == OptimizationLevel.Maximal ? 1920 : 2560;
            long jpegQuality = level == OptimizationLevel.Maximal ? 72 : 85;

            var order = new List<string>();
            var parts = new Dictionary<string, byte[]>();

            using (var input = new MemoryStream(content))
            using (var archive = new ZipArchive(input, ZipArchiveMode.Read))
            {
                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName.EndsWith("/"))
                        continue;
                    order.Add(entry.FullName);
                    using (var entryStream = entry.Open())
                    using (var buffer = new MemoryStream())
                    {
                        entryStream.CopyTo(buffer);
                        parts[entry.FullName] = buffer.ToArray();
                    }
                }
            }

            var mediaNames = order.Where(IsMedia).ToList();

            int imagesOptimized = 0;
            foreach (var name in mediaNames)
            {
                byte[] smaller = ImageRecompressor.Recompress(parts[name], Path.GetExtension(name), maxDimension, jpegQuality);
                if (smaller != null)
                {
                    parts[name] = smaller;
                    imagesOptimized++;
                }
            }

            int mediaRemoved = 0;
            if (pruneUnused)
            {
                foreach (var name in FindUnusedMedia(parts, mediaNames))
                {
                    parts.Remove(name);
                    mediaRemoved++;
                }
            }

            byte[] newContent;
            using (var outputStream = new MemoryStream())
            {
                using (var archive = new ZipArchive(outputStream, ZipArchiveMode.Create, true))
                {
                    foreach (var name in order)
                    {
                        if (!parts.ContainsKey(name))
                            continue;
                        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
                        using (var entryStream = entry.Open())
                        {
                            byte[] data = parts[name];
                            entryStream.Write(data, 0, data.Length);
                        }
                    }
                }
                newContent = outputStream.ToArray();
            }

            output = newContent;
            return new OptimizationResult(content.Length, newContent.Length, imagesOptimized, mediaRemoved);
        }

        public static OptimizationResult OptimizeFile(string source, OptimizationLevel level, bool keepOriginal, string output, out string destination)
        {
            string extension = Path.GetExtension(source).ToLowerInvariant();
            if (!SupportedSuffixes.Contains(extension))
                throw new ArgumentException("Unsupported file type: " + source);

            byte[] content = File.ReadAllBytes(source);

            byte[] newContent;
            OptimizationResult result = OptimizeBytes(content, level, true, out newContent);

            if (!string.IsNullOrEmpty(output))
            {
                destination = output;
            }
            else if (keepOriginal)
            {
                string directory = Path.GetDirectoryName(source);
                string name = Path.GetFileNameWithoutExtension(source);
                destination = Path.Combine(directory, name + "_oPPTimiz" + Path.GetExtension(source));
            }
            else
            {
                destination = source;
            }

            File.WriteAllBytes(destination, newContent);
            return result;
        }

        private static bool IsMedia(string name)
        {
            return name.ToLowerInvariant().Contains("/media/") && !name.EndsWith("/");
        }

        private static IEnumerable<string> FindUnusedMedia(Dictionary<string, byte[]> parts, List<string> mediaNames)
        {
            byte[] haystack;
            using (var buffer = new MemoryStream())
            {
                foreach (var part in parts)
                {
                    if (!IsMedia(part.Key))
                        buffer.Write(part.Value, 0, part.Value.Length);
                }
                haystack = buffer.ToArray();
            }

            var unused = new List<string>();
            foreach (var name in mediaNames)
            {
                byte[] basename = Encoding.UTF8.GetBytes(Path.GetFileName(name));
                if (basename.Length > 0 && ByteIndexOf(haystack, basename) < 0)
                    unused.Add(name);
            }
            return unused;
        }

        private static int ByteIndexOf(byte[] haystack, byte[] needle)
        {
            if (needle.Length == 0 || haystack.Length < needle.Length)
                return -1;

            for (int i = 0; i <= haystack.Length - needle.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < needle.Length; j++)
                {
                    if (haystack[i + j] != needle[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match)
                    return i;
            }
            return -1;
        }
    }
}
