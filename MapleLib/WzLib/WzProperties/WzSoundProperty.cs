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
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using NAudio.Wave;
using Wz2Nx_MapleLib.MapleLib.WzLib.Util;

namespace Wz2Nx_MapleLib.MapleLib.WzLib.WzProperties
{
    /// <summary>
    /// A property that contains data for an MP3 or binary file
    /// </summary>
    public class WzSoundProperty : WzExtended
    {
        #region Fields

        private string _name;
        private byte[] _mp3Bytes;
        private readonly int _lenMs;

        private byte[] _header;

        //internal WzImage imgParent;
        private readonly WzBinaryReader _wzReader;
        private bool _headerEncrypted = false;
        private readonly long _offs;

        private static readonly byte[] SoundHeader =
        {
            0x02,
            0x83, 0xEB, 0x36, 0xE4, 0x4F, 0x52, 0xCE, 0x11, 0x9F, 0x53, 0x00, 0x20, 0xAF, 0x0B, 0xA7, 0x70,
            0x8B, 0xEB, 0x36, 0xE4, 0x4F, 0x52, 0xCE, 0x11, 0x9F, 0x53, 0x00, 0x20, 0xAF, 0x0B, 0xA7, 0x70,
            0x00,
            0x01,
            0x81, 0x9F, 0x58, 0x05, 0x56, 0xC3, 0xCE, 0x11, 0xBF, 0x01, 0x00, 0xAA, 0x00, 0x55, 0x59, 0x5A
        };

        private WaveFormat _wavFormat;

        #endregion

        #region Inherited Members

        /// <summary>
        /// The parent of the object
        /// </summary>
        public override WzObject Parent { get; internal set; }

        /*/// <summary>
		/// The image that this property is contained in
		/// </summary>
		public override WzImage ParentImage { get { return imgParent; } internal set { imgParent = value; } }*/
        /// <summary>
        /// The name of the property
        /// </summary>
        public override string Name
        {
            get => _name;
            set { }
        }

        /// <summary>
        /// The WzPropertyType of the property
        /// </summary>
        public override WzPropertyType PropertyType => WzPropertyType.Sound;

        /// <summary>
        /// Disposes the object
        /// </summary>
        public override void Dispose()
        {
            _name = null;
            _mp3Bytes = null;
        }

        #endregion

        #region Custom Members

        public int SoundLength { get; }

        /// <summary>
        /// BPS of the mp3 file
        /// </summary>
        //public byte BPS { get { return bps; } set { bps = value; } }
        /// <summary>
        /// Creates a WzSoundProperty with the specified name
        /// </summary>
        /// <param name="name">The name of the property</param>
        /// <param name="reader">The wz reader</param>
        /// <param name="parseNow">Indicating whether to parse the property now</param>
        public WzSoundProperty(string name, WzBinaryReader reader, bool parseNow)
        {
            _name = name;
            _wzReader = reader;
            reader.BaseStream.Position++;

            //note - soundDataLen does NOT include the length of the header.
            SoundLength = reader.ReadCompressedInt();
            _lenMs = reader.ReadCompressedInt();

            var headerOff = reader.BaseStream.Position;
            reader.BaseStream.Position += SoundHeader.Length; //skip GUIDs
            int wavFormatLen = reader.ReadByte();
            reader.BaseStream.Position = headerOff;

            _header = reader.ReadBytes(SoundHeader.Length + 1 + wavFormatLen);
            ParseWzSoundPropertyHeader();

            //sound file offs
            _offs = reader.BaseStream.Position;
            if (parseNow)
                _mp3Bytes = reader.ReadBytes(SoundLength);
            else
                reader.BaseStream.Position += SoundLength;
        }

        private static T BytesToStruct<T>(byte[] data) where T : new()
        {
            GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                return Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject());
            }
            finally
            {
                handle.Free();
            }
        }

        private static T BytesToStructConstructorless<T>(byte[] data)
        {
            GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                T obj = (T)FormatterServices.GetUninitializedObject(typeof(T));
                Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject(), obj);
                return obj;
            }
            finally
            {
                handle.Free();
            }
        }

        private void ParseWzSoundPropertyHeader()
        {
            var wavHeader = new byte[_header.Length - SoundHeader.Length - 1];
            Buffer.BlockCopy(_header, SoundHeader.Length + 1, wavHeader, 0, wavHeader.Length);

            if (wavHeader.Length < Marshal.SizeOf<WaveFormat>())
                return;

            var wavFmt = BytesToStruct<WaveFormat>(wavHeader);
            if (Marshal.SizeOf<WaveFormat>() + wavFmt.ExtraSize != wavHeader.Length)
            {
                //try decrypt
                for (int i = 0; i < wavHeader.Length; i++)
                {
                    wavHeader[i] ^= _wzReader.WzKey[i];
                }

                wavFmt = BytesToStruct<WaveFormat>(wavHeader);

                if (Marshal.SizeOf<WaveFormat>() + wavFmt.ExtraSize != wavHeader.Length)
                {
                    Console.WriteLine("parse sound header failed");
                    return;
                }

                _headerEncrypted = true;
            }

            // parse to mp3 header
            if (wavFmt.Encoding == WaveFormatEncoding.MpegLayer3 && wavHeader.Length >= Marshal.SizeOf<Mp3WaveFormat>())
            {
                _wavFormat = BytesToStructConstructorless<Mp3WaveFormat>(wavHeader);
            }
            else if (wavFmt.Encoding == WaveFormatEncoding.Pcm)
            {
                _wavFormat = wavFmt;
            }
            else
            {
                Console.WriteLine($"Unknown wave encoding {wavFmt.Encoding.ToString()}");
            }
        }

        #endregion

        #region Parsing Methods

        private byte[] GetBytes(bool saveInMemory)
        {
            if (_mp3Bytes != null)
                return _mp3Bytes;
            if (_wzReader == null)
                return null;

            var currentPos = _wzReader.BaseStream.Position;
            _wzReader.BaseStream.Position = _offs;
            _mp3Bytes = _wzReader.ReadBytes(SoundLength);
            _wzReader.BaseStream.Position = currentPos;
            if (saveInMemory)
                return _mp3Bytes;
            var result = _mp3Bytes;
            _mp3Bytes = null;
            return result;
        }

        #endregion

        #region Cast Values

        public override byte[] GetBytes()
        {
            return GetBytes(false);
        }

        #endregion
    }
}