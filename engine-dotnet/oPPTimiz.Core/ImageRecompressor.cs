//oPPTimiz is a tool that reduces Office documents size.
//Copyright (C) 2025 EDF
//This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
//This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
//You should have received a copy of the GNU General Public License along with this program. If not, see https://www.gnu.org/licenses/.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace oPPTimiz.Core
{
    internal static class ImageRecompressor
    {
        private static readonly string[] SupportedExtensions = { ".png", ".jpg", ".jpeg" };

        /// <summary>
        /// Downscales then re-encodes an image in its original format, so that
        /// relationships and [Content_Types].xml never have to change. Returns the
        /// new bytes only when they are smaller than the input, otherwise null.
        /// </summary>
        public static byte[] Recompress(byte[] data, string extension, int maxDimension, long jpegQuality)
        {
            extension = (extension ?? string.Empty).ToLowerInvariant();
            if (!SupportedExtensions.Contains(extension))
                return null;

            try
            {
                using (var input = new MemoryStream(data))
                using (var original = Image.FromStream(input))
                {
                    Image rendered = original;
                    Bitmap resized = null;

                    int longestSide = Math.Max(original.Width, original.Height);
                    if (longestSide > maxDimension)
                    {
                        double ratio = (double)maxDimension / longestSide;
                        int width = Math.Max(1, (int)Math.Round(original.Width * ratio));
                        int height = Math.Max(1, (int)Math.Round(original.Height * ratio));

                        resized = new Bitmap(width, height);
                        using (var graphics = Graphics.FromImage(resized))
                        {
                            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            graphics.SmoothingMode = SmoothingMode.HighQuality;
                            graphics.DrawImage(original, 0, 0, width, height);
                        }
                        rendered = resized;
                    }

                    byte[] result;
                    using (var output = new MemoryStream())
                    {
                        if (extension == ".png")
                        {
                            rendered.Save(output, ImageFormat.Png);
                        }
                        else
                        {
                            ImageCodecInfo encoder = GetEncoder(ImageFormat.Jpeg);
                            using (var parameters = new EncoderParameters(1))
                            {
                                parameters.Param[0] = new EncoderParameter(Encoder.Quality, jpegQuality);
                                rendered.Save(output, encoder, parameters);
                            }
                        }
                        result = output.ToArray();
                    }

                    if (resized != null)
                        resized.Dispose();

                    return result.Length < data.Length ? result : null;
                }
            }
            catch
            {
                return null;
            }
        }

        private static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            return ImageCodecInfo.GetImageEncoders().FirstOrDefault(codec => codec.FormatID == format.Guid);
        }
    }
}
