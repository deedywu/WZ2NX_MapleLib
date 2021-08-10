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
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices; 
using MapleLib.MapleCryptoLib; 
using Mh.MapleLib.WzLib;

namespace MapleLib.WzLib.Util
{
    public class WzTool
    {

        public static Hashtable StringCache = new Hashtable();

        public static UInt32 RotateLeft(UInt32 x, byte n)
        {
            return (UInt32)(((x) << (n)) | ((x) >> (32 - (n))));
        }

        public static UInt32 RotateRight(UInt32 x, byte n)
        {
            return (UInt32)(((x) >> (n)) | ((x) << (32 - (n))));
        }

        public static int GetCompressedIntLength(int i)
        {
            if (i > 127 || i < -127)
                return 5;
            return 1;
        }

        public static int GetEncodedStringLength(string s)
        {
            int len = 0;
            if (string.IsNullOrEmpty(s))
                return 1;
            bool unicode = false;
            foreach (char c in s)
                if (c > 255)
                    unicode = true;
            if (unicode)
            {
                if (s.Length > 126)
                    len += 5;
                else
                    len += 1;
                len += s.Length * 2;
            }
            else
            {
                if (s.Length > 127)
                    len += 5;
                else
                    len += 1;
                len += s.Length;
            }
            return len;
        }

        public static int GetWzObjectValueLength(string s, byte type)
        {
            string storeName = type + "_" + s;
            if (s.Length > 4 && StringCache.ContainsKey(storeName))
            {
                return 5;
            }
            else
            {
                StringCache[storeName] = 1;
                return 1 + GetEncodedStringLength(s);
            }
        }

        public static T StringToEnum<T>(string name)
        {
            try
            {
                return (T)Enum.Parse(typeof(T), name);
            }
            catch
            {
                return default(T);
            }
        }

        /// <summary>
        /// Get WZ encryption IV from maple version 
        /// </summary>
        /// <param name="ver"></param>
        /// <param name="fallbackCustomIv">The custom bytes to use as IV</param>
        /// <returns></returns>
        public static byte[] GetIvByMapleVersion(WzMapleVersion ver)
        {
            switch (ver)
            {
                case WzMapleVersion.Ems:
                    return MapleCryptoConstants.WZ_MSEAIV;//?
                case WzMapleVersion.Gms:
                    return MapleCryptoConstants.WZ_GMSIV; 
                case WzMapleVersion.Generate: // dont fill anything with GENERATE, it is not supposed to load anything
                    return new byte[4]; 

                case WzMapleVersion.Bms:
                case WzMapleVersion.Classic:
                default:
                    return new byte[4];
            }
        }

        private static int GetRecognizedCharacters(string source)
        {
            int result = 0;
            foreach (char c in source)
                if (0x20 <= c && c <= 0x7E)
                    result++;
            return result;
        }  
        public const int WzHeader = 0x31474B50; //PKG1

        public static bool IsListFile(string path)
        {
            bool result;
            using (BinaryReader reader = new BinaryReader(File.OpenRead(path)))
            {
                int header = reader.ReadInt32();
                result = header != WzHeader;
            }
            return result;
        }
 
        private static byte[] Combine(byte[] a, byte[] b)
        {
            byte[] result = new byte[a.Length + b.Length];
            Array.Copy(a, 0, result, 0, a.Length);
            Array.Copy(b, 0, result, a.Length, b.Length);
            return result;
        }
    }
}