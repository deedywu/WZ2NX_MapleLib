/*  MapleLib - A general-purpose MapleStory library
 * Copyright (C) 2009, 2010, 2015 Snow and haha01haha01
   
 * This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

 * This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

 * You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MapleLib;
using Microsoft.Xna.Framework.Graphics;
using Wz2Nx_MapleLib.MapleLib.WzLib.Util;
using Point = Microsoft.Xna.Framework.Point;

namespace Wz2Nx_MapleLib.MapleLib.WzLib.WzProperties
{
    /// <summary>
    /// A property that contains the information for a bitmap
    /// https://docs.microsoft.com/en-us/windows/win32/direct3d9/compressed-texture-resources
    /// https://code.google.com/archive/p/libsquish/
    /// https://github.com/svn2github/libsquish
    /// http://www.sjbrown.co.uk/2006/01/19/dxt-compression-techniques/
    /// https://en.wikipedia.org/wiki/S3_Texture_Compression
    /// </summary>
    public sealed class WzPngProperty : WzImageProperty
    {
        #region Fields

        private readonly int _format;
        private readonly int _format2;
        private byte[] _compressedImageBytes;
        private Bitmap _png;

        //private WzImage imgParent;
        private bool _listWzUsed;

        private readonly WzBinaryReader _wzReader;
        private readonly long _offs;

        #endregion

        #region Inherited Members

        /// <summary>
        /// The parent of the object
        /// </summary>
        public override WzObject Parent { get; internal set; }

        /*/// <summary>
        /// The image that this property is contained in
        /// </summary>
        public override WzImage ParentImage { get { return imgParent; } private set { imgParent = value; } }*/
        /// <summary>
        /// The name of the property
        /// </summary>
        public override string Name
        {
            get => "PNG";
            set { }
        }

        /// <summary>
        /// The WzPropertyType of the property
        /// </summary>
        public override WzPropertyType PropertyType => WzPropertyType.Png;

        /// <summary>
        /// Disposes the object
        /// </summary>
        public override void Dispose()
        {
            // compressedImageBytes = null;
            if (_png != null)
            {
                _png.Dispose();
                _png = null;
            }

            //this.wzReader.Close(); // closes at WzFile
            // this.wzReader = null;
        }

        #endregion

        #region Custom Members

        /// <summary>
        /// The width of the bitmap
        /// </summary>
        public int Width { get; private set; }

        /// <summary>
        /// The height of the bitmap
        /// </summary>
        public int Height { get; private set; }

        /// <summary>
        /// The format of the bitmap
        /// </summary>
        private int Format => _format + _format2;

        /// <summary>
        /// Creates a blank WzPngProperty 
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="parseNow"></param>
        public WzPngProperty(WzBinaryReader reader, bool parseNow)
        {
            // Read compressed bytes
            Width = reader.ReadCompressedInt();
            Height = reader.ReadCompressedInt();
            _format = reader.ReadCompressedInt();
            _format2 = reader.ReadByte();
            reader.BaseStream.Position += 4;
            _offs = reader.BaseStream.Position;
            var len = reader.ReadInt32() - 1;
            reader.BaseStream.Position += 1;

            lock (reader) // lock WzBinaryReader, allowing it to be loaded from multiple threads at once
            {
                if (len > 0)
                {
                    if (parseNow)
                    {
                        _compressedImageBytes = reader.ReadBytes(len);

                        ParsePng(true);
                    }
                    else
                        reader.BaseStream.Position += len;
                }

                _wzReader = reader;
            }
        }

        #endregion

        #region Parsing Methods

        private byte[] GetCompressedBytes(bool saveInMemory)
        {
            if (_compressedImageBytes != null) return _compressedImageBytes;
            lock (_wzReader) // lock WzBinaryReader, allowing it to be loaded from multiple threads at once
            {
                var pos = _wzReader.BaseStream.Position;
                _wzReader.BaseStream.Position = _offs;
                var len = _wzReader.ReadInt32() - 1;
                if (len <= 0) // possibility an image written with the wrong wzIv 
                    throw new Exception("The length of the image is negative. WzPngProperty. Wrong WzIV?");

                _wzReader.BaseStream.Position += 1;

                _compressedImageBytes = _wzReader.ReadBytes(len);
                _wzReader.BaseStream.Position = pos;
            }

            if (saveInMemory) return _compressedImageBytes;
            //were removing the referance to compressedBytes, so a backup for the ret value is needed
            var returnBytes = _compressedImageBytes;
            _compressedImageBytes = null;
            return returnBytes;
        }

        private Bitmap GetImage(bool saveInMemory)
        {
            if (_png == null)
            {
                ParsePng(saveInMemory);
            }

            return _png;
        }

        private void ParsePng(bool saveInMemory, Texture2D texture2d = null)
        {
            var rawBytes = GetRawImage(saveInMemory);
            if (rawBytes == null)
            {
                _png = null;
                return;
            }

            try
            {
                Bitmap bmp = null;
                var rectangle = new Rectangle(0, 0, Width, Height);

                switch (Format)
                {
                    case 1:
                    {
                        bmp = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
                        var bmpData = bmp.LockBits(rectangle, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

                        DecompressImage_PixelDataB_G_R_A4444(rawBytes, Width, Height, bmp, bmpData);
                        break;
                    }
                    case 2:
                    {
                        bmp = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
                        var bmpData = bmp.LockBits(rectangle, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

                        Marshal.Copy(rawBytes, 0, bmpData.Scan0, rawBytes.Length);
                        bmp.UnlockBits(bmpData);
                        break;
                    }
                    case 3:
                    {
                        // New format 黑白缩略图
                        // thank you Elem8100, http://forum.ragezone.com/f702/wz-png-format-decode-code-1114978/ 
                        // you'll be remembered forever <3 
                        bmp = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
                        var bmpData = bmp.LockBits(rectangle, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

                        DecompressImageDxt3(rawBytes, Width, Height, bmp, bmpData);
                        break;
                    }
                    case 257
                        : // http://forum.ragezone.com/f702/wz-png-format-decode-code-1114978/index2.html#post9053713
                    {
                        bmp = new Bitmap(Width, Height, PixelFormat.Format16bppArgb1555);
                        var bmpData = bmp.LockBits(rectangle, ImageLockMode.WriteOnly,
                            PixelFormat.Format16bppArgb1555);
                        // "Npc.wz\\2570101.img\\info\\illustration2\\face\\0"

                        CopyBmpDataWithStride(rawBytes, bmp.Width * 2, bmpData);

                        bmp.UnlockBits(bmpData);
                        break;
                    }
                    case 513: // nexon wizet logo
                    {
                        bmp = new Bitmap(Width, Height, PixelFormat.Format16bppRgb565);
                        var bmpData =
                            bmp.LockBits(rectangle, ImageLockMode.WriteOnly, PixelFormat.Format16bppRgb565);

                        Marshal.Copy(rawBytes, 0, bmpData.Scan0, rawBytes.Length);
                        bmp.UnlockBits(bmpData);
                        break;
                    }
                    case 517:
                    {
                        bmp = new Bitmap(Width, Height, PixelFormat.Format16bppRgb565);
                        var bmpData =
                            bmp.LockBits(rectangle, ImageLockMode.WriteOnly, PixelFormat.Format16bppRgb565);

                        DecompressImage_PixelDataForm517(rawBytes, Width, Height, bmp, bmpData);
                        break;
                    }
                    case 1026:
                    {
                        bmp = new Bitmap(this.Width, this.Height, PixelFormat.Format32bppArgb);
                        var bmpData = bmp.LockBits(rectangle, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

                        DecompressImageDxt3(rawBytes, this.Width, this.Height, bmp, bmpData);
                        break;
                    }
                    case 2050: // new
                    {
                        bmp = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
                        var bmpData = bmp.LockBits(rectangle, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

                        DecompressImageDxt5(rawBytes, Width, Height, bmp, bmpData);
                        break;
                    }
                    default:
                        Console.WriteLine($"Unknown PNG format {_format} {_format2}");
                        break;
                }

                if (bmp != null)
                {
                    if (texture2d != null)
                    {
                        var rect = new Microsoft.Xna.Framework.Rectangle(Point.Zero,
                            new Point(Width, Height));
                        texture2d.SetData(0, 0, rect, rawBytes, 0, rawBytes.Length);
                    }
                }

                _png = bmp;
            }
            catch (InvalidDataException)
            {
                _png = null;
            }
        }

        /// <summary>
        /// Parses the raw image bytes from WZ
        /// </summary>
        /// <returns></returns>
        private byte[] GetRawImage(bool saveInMemory)
        {
            var rawImageBytes = GetCompressedBytes(saveInMemory);

            try
            {
                using var reader = new BinaryReader(new MemoryStream(rawImageBytes));
                DeflateStream zlib;

                var header = reader.ReadUInt16();
                _listWzUsed = header != 0x9C78 && header != 0xDA78 && header != 0x0178 && header != 0x5E78;
                if (!_listWzUsed)
                {
                    zlib = new DeflateStream(reader.BaseStream, CompressionMode.Decompress);
                }
                else
                {
                    reader.BaseStream.Position -= 2;
                    var dataStream = new MemoryStream();
                    var endOfPng = rawImageBytes.Length;

                    // Read image into zlib
                    while (reader.BaseStream.Position < endOfPng)
                    {
                        var blockSize = reader.ReadInt32();
                        for (var i = 0; i < blockSize; i++)
                        {
                            dataStream.WriteByte((byte)(reader.ReadByte() ^ ParentImage.Reader.WzKey[i]));
                        }
                    }

                    dataStream.Position = 2;
                    zlib = new DeflateStream(dataStream, CompressionMode.Decompress);
                }

                var uncompressedSize = 0;
                byte[] decBuf = null;

                switch (_format + _format2)
                {
                    case 1: // 0x1
                    {
                        uncompressedSize = Width * Height * 2;
                        decBuf = new byte[uncompressedSize];
                        break;
                    }
                    case 2: // 0x2
                    {
                        uncompressedSize = Width * Height * 4;
                        decBuf = new byte[uncompressedSize];
                        break;
                    }
                    case 3: // 0x2 + 1?
                    {
                        // New format 黑白缩略图
                        // thank you Elem8100, http://forum.ragezone.com/f702/wz-png-format-decode-code-1114978/ 
                        // you'll be remembered forever <3 

                        uncompressedSize = Width * Height * 4;
                        decBuf = new byte[uncompressedSize];
                        break;
                    }
                    case 257: // 0x100 + 1?
                    {
                        // http://forum.ragezone.com/f702/wz-png-format-decode-code-1114978/index2.html#post9053713
                        // "Npc.wz\\2570101.img\\info\\illustration2\\face\\0"

                        uncompressedSize = Width * Height * 2;
                        decBuf = new byte[uncompressedSize];
                        break;
                    }
                    case 513: // 0x200 nexon wizet logo
                    {
                        uncompressedSize = Width * Height * 2;
                        decBuf = new byte[uncompressedSize];
                        break;
                    }
                    case 517: // 0x200 + 5
                    {
                        uncompressedSize = Width * Height / 128;
                        decBuf = new byte[uncompressedSize];
                        break;
                    }
                    case 1026: // 0x400 + 2?
                    {
                        uncompressedSize = Width * Height * 4;
                        decBuf = new byte[uncompressedSize];
                        break;
                    }
                    case 2050: // 0x800 + 2? new
                    {
                        uncompressedSize = Width * Height;
                        decBuf = new byte[uncompressedSize];
                        break;
                    }
                    default:
                        Console.WriteLine($"Unknown PNG format {_format} {_format2}");
                        break;
                }

                if (decBuf != null)
                {
                    using (zlib)
                    {
                        zlib.Read(decBuf, 0, uncompressedSize);
                        return decBuf;
                    }
                }
            }
            catch (InvalidDataException)
            {
            }

            return null;
        }

        #region Decoders

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Color Rgb565ToColor(ushort val)
        {
            const int rgb565MaskR = 0xf800;
            const int rgb565MaskG = 0x07e0;
            const int rgb565MaskB = 0x001f;
            var r = (val & rgb565MaskR) >> 11;
            var g = (val & rgb565MaskG) >> 5;
            var b = val & rgb565MaskB;
            var c = Color.FromArgb(
                (r << 3) | (r >> 2),
                (g << 2) | (g >> 4),
                (b << 3) | (b >> 2));
            return c;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void DecompressImage_PixelDataB_G_R_A4444(IReadOnlyList<byte> rawData, int width, int height,
            Bitmap bmp, BitmapData bmpData)
        {
            var uncompressedSize = width * height * 2;
            var decoded = new byte[uncompressedSize * 2];

            for (var i = 0; i < uncompressedSize; i++)
            {
                var byteAtPosition = rawData[i];

                var lo = byteAtPosition & 0x0F;
                var b = (byte)(lo | (lo << 4));
                decoded[i * 2] = b;

                var hi = byteAtPosition & 0xF0;
                var g = (byte)(hi | (hi >> 4));
                decoded[i * 2 + 1] = g;
            }

            Marshal.Copy(decoded, 0, bmpData.Scan0, decoded.Length);
            bmp.UnlockBits(bmpData);
        }

        /// <summary>
        /// DXT3
        /// </summary>
        /// <param name="rawData"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="bmp"></param>
        /// <param name="bmpData"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void DecompressImageDxt3(byte[] rawData, int width, int height, Bitmap bmp, BitmapData bmpData)
        {
            byte[] decoded = new byte[width * height * 4];

            if (SquishPngWrapper.CheckAndLoadLibrary())
            {
                SquishPngWrapper.DecompressImage(decoded, width, height, rawData,
                    (int)SquishPngWrapper.FlagsEnum.kDxt3);
            }
            else // otherwise decode here directly, fallback.
            {
                var colorTable = new Color[4];
                var colorIdxTable = new int[16];
                var alphaTable = new byte[16];
                for (var y = 0; y < height; y += 4)
                {
                    for (var x = 0; x < width; x += 4)
                    {
                        var off = x * 4 + y * width;
                        ExpandAlphaTableDxt3(alphaTable, rawData, off);
                        var u0 = BitConverter.ToUInt16(rawData, off + 8);
                        var u1 = BitConverter.ToUInt16(rawData, off + 10);
                        ExpandColorTable(colorTable, u0, u1);
                        ExpandColorIndexTable(colorIdxTable, rawData, off + 12);

                        for (var j = 0; j < 4; j++)
                        {
                            for (var i = 0; i < 4; i++)
                            {
                                SetPixel(decoded,
                                    x + i,
                                    y + j,
                                    width,
                                    colorTable[colorIdxTable[j * 4 + i]],
                                    alphaTable[j * 4 + i]);
                            }
                        }
                    }
                }
            }

            Marshal.Copy(decoded, 0, bmpData.Scan0, decoded.Length);
            bmp.UnlockBits(bmpData);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void DecompressImage_PixelDataForm517(IReadOnlyList<byte> rawData, int width, int height,
            Bitmap bmp, BitmapData bmpData)
        {
            var decoded = new byte[width * height * 2];

            var lineIndex = 0;
            for (int j0 = 0, j1 = height / 16; j0 < j1; j0++)
            {
                var dstIndex = lineIndex;
                for (int i0 = 0, i1 = width / 16; i0 < i1; i0++)
                {
                    var idx = (i0 + j0 * i1) * 2;
                    var b0 = rawData[idx];
                    var b1 = rawData[idx + 1];
                    for (var k = 0; k < 16; k++)
                    {
                        decoded[dstIndex++] = b0;
                        decoded[dstIndex++] = b1;
                    }
                }

                for (var k = 1; k < 16; k++)
                {
                    Array.Copy(decoded, lineIndex, decoded, dstIndex, width * 2);
                    dstIndex += width * 2;
                }

                lineIndex += width * 32;
            }

            Marshal.Copy(decoded, 0, bmpData.Scan0, decoded.Length);
            bmp.UnlockBits(bmpData);
        }

        /// <summary>
        /// DXT5
        /// </summary>
        /// <param name="rawData"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="bmp"></param>
        /// <param name="bmpData"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void DecompressImageDxt5(byte[] rawData, int width, int height, Bitmap bmp, BitmapData bmpData)
        {
            var decoded = new byte[width * height * 4];

            if (SquishPngWrapper.CheckAndLoadLibrary())
            {
                SquishPngWrapper.DecompressImage(decoded, width, height, rawData,
                    (int)SquishPngWrapper.FlagsEnum.kDxt5);
            }
            else // otherwise decode here directly, fallback
            {
                var colorTable = new Color[4];
                var colorIdxTable = new int[16];
                var alphaTable = new byte[8];
                var alphaIdxTable = new int[16];
                for (var y = 0; y < height; y += 4)
                {
                    for (var x = 0; x < width; x += 4)
                    {
                        var off = x * 4 + y * width;
                        ExpandAlphaTableDxt5(alphaTable, rawData[off + 0], rawData[off + 1]);
                        ExpandAlphaIndexTableDxt5(alphaIdxTable, rawData, off + 2);
                        var u0 = BitConverter.ToUInt16(rawData, off + 8);
                        var u1 = BitConverter.ToUInt16(rawData, off + 10);
                        ExpandColorTable(colorTable, u0, u1);
                        ExpandColorIndexTable(colorIdxTable, rawData, off + 12);

                        for (var j = 0; j < 4; j++)
                        {
                            for (var i = 0; i < 4; i++)
                            {
                                SetPixel(decoded,
                                    x + i,
                                    y + j,
                                    width,
                                    colorTable[colorIdxTable[j * 4 + i]],
                                    alphaTable[alphaIdxTable[j * 4 + i]]);
                            }
                        }
                    }
                }
            }

            Marshal.Copy(decoded, 0, bmpData.Scan0, decoded.Length);
            bmp.UnlockBits(bmpData);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetPixel(IList<byte> pixelData, int x, int y, int width, Color color, byte alpha)
        {
            var offset = (y * width + x) * 4;
            pixelData[offset + 0] = color.B;
            pixelData[offset + 1] = color.G;
            pixelData[offset + 2] = color.R;
            pixelData[offset + 3] = alpha;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CopyBmpDataWithStride(byte[] source, int stride, BitmapData bmpData)
        {
            if (bmpData.Stride == stride)
            {
                Marshal.Copy(source, 0, bmpData.Scan0, source.Length);
            }
            else
            {
                for (var y = 0; y < bmpData.Height; y++)
                {
                    Marshal.Copy(source, stride * y, bmpData.Scan0 + bmpData.Stride * y, stride);
                }
            }
        }

        #endregion

        #region DXT1 Color

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ExpandColorTable(IList<Color> color, ushort c0, ushort c1)
        {
            color[0] = Rgb565ToColor(c0);
            color[1] = Rgb565ToColor(c1);
            if (c0 > c1)
            {
                color[2] = Color.FromArgb(0xff, (color[0].R * 2 + color[1].R + 1) / 3,
                    (color[0].G * 2 + color[1].G + 1) / 3, (color[0].B * 2 + color[1].B + 1) / 3);
                color[3] = Color.FromArgb(0xff, (color[0].R + color[1].R * 2 + 1) / 3,
                    (color[0].G + color[1].G * 2 + 1) / 3, (color[0].B + color[1].B * 2 + 1) / 3);
            }
            else
            {
                color[2] = Color.FromArgb(0xff, (color[0].R + color[1].R) / 2, (color[0].G + color[1].G) / 2,
                    (color[0].B + color[1].B) / 2);
                color[3] = Color.FromArgb(0xff, Color.Black);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ExpandColorIndexTable(IList<int> colorIndex, IReadOnlyList<byte> rawData, int offset)
        {
            for (var i = 0; i < 16; i += 4, offset++)
            {
                colorIndex[i + 0] = (rawData[offset] & 0x03);
                colorIndex[i + 1] = (rawData[offset] & 0x0c) >> 2;
                colorIndex[i + 2] = (rawData[offset] & 0x30) >> 4;
                colorIndex[i + 3] = (rawData[offset] & 0xc0) >> 6;
            }
        }

        #endregion

        #region DXT3/DXT5 Alpha

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ExpandAlphaTableDxt3(IList<byte> alpha, IReadOnlyList<byte> rawData, int offset)
        {
            for (var i = 0; i < 16; i += 2, offset++)
            {
                alpha[i + 0] = (byte)(rawData[offset] & 0x0f);
                alpha[i + 1] = (byte)((rawData[offset] & 0xf0) >> 4);
            }

            for (var i = 0; i < 16; i++)
            {
                alpha[i] = (byte)(alpha[i] | (alpha[i] << 4));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ExpandAlphaTableDxt5(IList<byte> alpha, byte a0, byte a1)
        {
            // get the two alpha values
            alpha[0] = a0;
            alpha[1] = a1;

            // compare the values to build the codebook
            if (a0 > a1)
            {
                for (var i = 2; i < 8; i++) // // use 7-alpha codebook
                {
                    alpha[i] = (byte)(((8 - i) * a0 + (i - 1) * a1 + 3) / 7);
                }
            }
            else
            {
                for (var i = 2; i < 6; i++) // // use 5-alpha codebook
                {
                    alpha[i] = (byte)(((6 - i) * a0 + (i - 1) * a1 + 2) / 5);
                }

                alpha[6] = 0;
                alpha[7] = 255;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ExpandAlphaIndexTableDxt5(IList<int> alphaIndex, IReadOnlyList<byte> rawData, int offset)
        {
            // write out the indexed codebook values
            for (var i = 0; i < 16; i += 8, offset += 3)
            {
                var flags = rawData[offset]
                            | (rawData[offset + 1] << 8)
                            | (rawData[offset + 2] << 16);

                // unpack 8 3-bit values from it
                for (var j = 0; j < 8; j++)
                {
                    var mask = 0x07 << (3 * j);
                    alphaIndex[i + j] = (flags & mask) >> (3 * j);
                }
            }
        }

        #endregion

        #endregion

        #region Cast Values

        public Bitmap GetBitmap()
        {
            return GetImage(false);
        }

        #endregion
    }
}