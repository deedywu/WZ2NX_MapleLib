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
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using NAudio.Wave;
using Wz2Nx_MapleLib.MapleLib.WzLib.Util;

namespace Wz2Nx_MapleLib.MapleLib.WzLib.WzProperties
{
    /// <summary>
    /// A property that contains data for an MP3 or binary file
    /// </summary>
    public sealed class WzBinaryProperty : WzExtended
    {
        #region Fields

        private string _name;
        private byte[] _mp3Bytes;

        private readonly byte[] _header;

        //internal WzImage imgParent;
        private readonly WzBinaryReader _wzReader;
        private readonly long _offs;
        private readonly int _soundDataLen;

        private static readonly byte[] SoundHeader =
        {
            0x02,
            0x83, 0xEB, 0x36, 0xE4, 0x4F, 0x52, 0xCE, 0x11, 0x9F, 0x53, 0x00, 0x20, 0xAF, 0x0B, 0xA7, 0x70,
            0x8B, 0xEB, 0x36, 0xE4, 0x4F, 0x52, 0xCE, 0x11, 0x9F, 0x53, 0x00, 0x20, 0xAF, 0x0B, 0xA7, 0x70,
            0x00,
            0x01,
            0x81, 0x9F, 0x58, 0x05, 0x56, 0xC3, 0xCE, 0x11, 0xBF, 0x01, 0x00, 0xAA, 0x00, 0x55, 0x59, 0x5A
        };

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
            set => _name = value;
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

        /// <summary>
        /// Length of the mp3 file in milliseconds
        /// </summary>
        public int Length { get; }

        // BPS of the mp3 file 
        //public byte BPS { get { return bps; } set { bps = value; } }
        /// <summary>
        /// Creates a WzSoundProperty with the specified name
        /// </summary>
        /// <param name="name">The name of the property</param>
        /// <param name="reader">The wz reader</param>
        /// <param name="parseNow">Indicating whether to parse the property now</param>
        public WzBinaryProperty(string name, WzBinaryReader reader, bool parseNow)
        {
            _name = name;
            _wzReader = reader;
            reader.BaseStream.Position++;

            //note - soundDataLen does NOT include the length of the header.
            _soundDataLen = reader.ReadCompressedInt();
            Length = reader.ReadCompressedInt();

            var headerOff = reader.BaseStream.Position;
            reader.BaseStream.Position += SoundHeader.Length; //skip GUIDs
            int wavFormatLen = reader.ReadByte();
            reader.BaseStream.Position = headerOff;

            _header = reader.ReadBytes(SoundHeader.Length + 1 + wavFormatLen);
            ParseWzSoundPropertyHeader();

            //sound file offs
            _offs = reader.BaseStream.Position;
            if (parseNow)
                _mp3Bytes = reader.ReadBytes(_soundDataLen);
            else
                reader.BaseStream.Position += _soundDataLen;
        }

        private static T BytesToStruct<T>(IEnumerable data) where T : new()
        {
            var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                return Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject());
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
                for (var i = 0; i < wavHeader.Length; i++)
                {
                    wavHeader[i] ^= _wzReader.WzKey[i];
                }

                wavFmt = BytesToStruct<WaveFormat>(wavHeader);

                if (Marshal.SizeOf<WaveFormat>() + wavFmt.ExtraSize != wavHeader.Length)
                {
                    Console.WriteLine("parse sound header failed");
                }
            }
        }

        #endregion

        #region Parsing Methods

        private byte[] GetBytes(bool saveInMemory)
        {
            if (_mp3Bytes != null)
                return _mp3Bytes;
            else
            {
                if (_wzReader == null)
                    return null;

                long currentPos = _wzReader.BaseStream.Position;
                _wzReader.BaseStream.Position = _offs;
                _mp3Bytes = _wzReader.ReadBytes(_soundDataLen);
                _wzReader.BaseStream.Position = currentPos;
                if (saveInMemory)
                    return _mp3Bytes;
                else
                {
                    byte[] result = _mp3Bytes;
                    _mp3Bytes = null;
                    return result;
                }
            }
        }

        public void SaveToFile(string file)
        {
            File.WriteAllBytes(file, GetBytes(false));
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