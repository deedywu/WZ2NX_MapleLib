using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Wz2Nx_MapleLib.MapleLib.WzLib;
using Wz2Nx_MapleLib.MapleLib.WzLib.WzProperties;

namespace Wz2Nx_MapleLib
{
    // override from WZ2NX (angles)
    internal static class Program
    {
        private static readonly byte[] Pkg4 = { 0x50, 0x4B, 0x47, 0x35 }; // PKG5
        private static readonly bool Is64Bit = IntPtr.Size == 8;

        private static bool _oldUol;

        public static void Main(string[] args)
        {
            // todo write uol with string or position
            _oldUol = false;
            // todo write your to convert wz files name
            string[] names =
            { 
                // "Effect", "Etc", "Item",
                // "Map", "Mob", "Morph", "Npc", "Quest",
                // "Reactor", "Skill", "Sound", "String",
                // "UI"
                "Our"
            };
            foreach (var name in names)
            {
                // todo write input wz path and output nx path,game version,Maple version here
                Run($"D:/deedy/{name}.wz",
                    $"D:/deedy/new/{name}.wz",
                    WzMapleVersion.Bms, 171);
            }
        }

        private static void Run(string inWz, string outPath, WzMapleVersion wzVar, short gameVersion)
        {
            Console.WriteLine("Input .wz: {0}{1}Output .nx: {2}", Path.GetFullPath(inWz),
                Environment.NewLine, Path.GetFullPath(outPath));

            var swOperation = new Stopwatch();
            var fullTimer = new Stopwatch();

            void ReportDone(string str)
            {
                Console.WriteLine("done. E{0} T{1}", swOperation.Elapsed, fullTimer.Elapsed);
                swOperation.Restart();
                Console.Write(str);
            }

            fullTimer.Start();
            swOperation.Start();
            Console.Write("Parsing input WZ... ".PadRight(31));

            using var wzf = new WzFile(inWz, gameVersion, wzVar);
            wzf.ParseWzFile();
            using var outFs = new FileStream(outPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            using var bw = new BinaryWriter(outFs);
            var state = new DumpState();

            ReportDone("Writing header... ".PadRight(31));
            bw.Write(Pkg4);
            bw.Write(new byte[(4 + 8) * 4]);

            ReportDone("Writing nodes... ".PadRight(31));
            outFs.EnsureMultiple(4);
            var nodeOffset = (ulong)bw.BaseStream.Position;

            var list = new List<WzObject> { wzf.WzDirectory };

            while (list.Count > 0)
                WriteNodeLevel(ref list, state, bw);

            ulong stringOffset;
            var stringCount = (uint)state.Strings.Count;
            {
                ReportDone("Writing string data...".PadRight(31));
                var strings = state.Strings.ToDictionary(kvp => kvp.Value,
                    kvp => kvp.Key);
                var offsets = new ulong[stringCount];
                for (uint idx = 0; idx < stringCount; ++idx)
                {
                    outFs.EnsureMultiple(2);
                    offsets[idx] = (ulong)bw.BaseStream.Position;
                    WriteString(strings[idx], bw);
                }

                outFs.EnsureMultiple(8);
                stringOffset = (ulong)bw.BaseStream.Position;
                for (uint idx = 0; idx < stringCount; ++idx)
                    bw.Write(offsets[idx]);
            }

            var bitmapOffset = 0UL;
            var bitmapCount = 0U;
            {
                ReportDone("Writing canvas data...".PadRight(31));
                bitmapCount = (uint)state.Canvases.Count;
                var offsets = new ulong[bitmapCount];
                long cId = 0;
                foreach (var cNode in state.Canvases)
                {
                    outFs.EnsureMultiple(8);
                    offsets[cId++] = (ulong)bw.BaseStream.Position;
                    WriteBitmap(cNode, bw);
                }

                outFs.EnsureMultiple(8);
                bitmapOffset = (ulong)bw.BaseStream.Position;
                for (uint idx = 0; idx < bitmapCount; ++idx)
                    bw.Write(offsets[idx]);
            }

            var soundOffset = 0UL;
            var soundCount = 0U;
            {
                ReportDone("Writing MP3 data... ".PadRight(31));
                soundCount = (uint)state.MP3s.Count;
                var offsets = new ulong[soundCount];
                long cId = 0;
                foreach (var mNode in state.MP3s)
                {
                    outFs.EnsureMultiple(8);
                    offsets[cId++] = (ulong)bw.BaseStream.Position;
                    WriteMP3(mNode, bw);
                }

                outFs.EnsureMultiple(8);
                soundOffset = (ulong)bw.BaseStream.Position;
                for (uint idx = 0; idx < soundCount; ++idx)
                    bw.Write(offsets[idx]);
            }

            ReportDone("Writing linked node data... ".PadRight(31));
            var uolReplace = new byte[16];
            foreach (var pair in state.UOLs)
            {
                var keyWzValue = pair.Key.WzValue;
                while (keyWzValue != null && keyWzValue is not WzNullProperty &&
                       keyWzValue is WzUolProperty uolProperty)
                {
                    keyWzValue = uolProperty.WzValue;
                }

                if (keyWzValue is WzNullProperty) continue;
                var result = (WzObject)keyWzValue;
                if (result == null) continue;
                bw.BaseStream.Position = (long)(nodeOffset + state.GetNodeID(result) * 20 + 4);
                bw.BaseStream.Read(uolReplace, 0, 16);
                pair.Value(bw, uolReplace);
            }

            ReportDone("Finalising... ".PadRight(31));

            bw.Seek(4, SeekOrigin.Begin);
            bw.Write((uint)state.Nodes.Count);
            bw.Write(nodeOffset);
            bw.Write(stringCount);
            bw.Write(stringOffset);
            bw.Write(bitmapCount);
            bw.Write(bitmapOffset);
            bw.Write(soundCount);
            bw.Write(soundOffset);

            ReportDone("Completed!");
        }

        private static void WriteNodeLevel(ref List<WzObject> nodeLevel, DumpState ds, BinaryWriter bw)
        {
            var nextChildId = (uint)(ds.GetNextNodeID() + nodeLevel.Count);
            foreach (var levelNode in nodeLevel)
            {
                if (_oldUol)
                {
                    WriteNode(levelNode, ds, bw, nextChildId);
                    nextChildId += (uint)levelNode.ChildCount();
                }
                else
                {
                    if (levelNode is WzUolProperty uolProperty)
                        WriteUOL(uolProperty, ds, bw);
                    else
                        WriteNode(levelNode, ds, bw, nextChildId);
                    nextChildId += (uint)levelNode.ChildCount();
                }
            }

            var @out = new List<WzObject>();
            foreach (var wzObject in nodeLevel.Where(wzObject => wzObject.ChildCount() > 0))
            {
                var childArray = wzObject.ChildArray();
                childArray.Sort((x, y) =>
                    string.Compare(x.Name, y.Name, StringComparison.Ordinal));
                @out.AddRange(childArray);
            }

            nodeLevel.Clear();
            nodeLevel = @out;
        }

        private static void WriteUOL(WzUolProperty node, DumpState ds, BinaryWriter bw)
        {
            ds.AddNode(node);
            bw.Write(ds.AddString(node.Name));
            ds.AddUOL(node, bw.BaseStream.Position);
            bw.Write(0L);
            bw.Write(0L);
        }

        private static void WriteNode(WzObject node, DumpState ds, BinaryWriter bw, uint nextChildID)
        {
            ds.AddNode(node);
            // format wz name ...
            if (node.Name.EndsWith(".wz"))
                node.Name = node.Name.Replace(".wz", "");
            bw.Write(ds.AddString(node.Name));
            bw.Write(nextChildID);
            bw.Write((ushort)node.ChildCount());
            ushort type;

            if (node is WzDirectory or WzImage or WzSubProperty or WzConvexProperty or WzNullProperty)
                type = 0; // no data; children only (8)
            else if (node is WzIntProperty or WzShortProperty or WzLongProperty)
                type = 1; // int32 (4)
            else if (node is WzDoubleProperty or WzFloatProperty)
                type = 2; // Double (0)
            else if (node is WzStringProperty || (_oldUol && node is WzUolProperty))
                type = 3; // String(if oldUol and uolString) (4)
            else if (node is WzVectorProperty)
                type = 4; // (0)
            else if (node is WzCanvasProperty)
                type = 5; // (4)
            else if (node is WzSoundProperty)
                type = 6; // (4)
            else
                throw new InvalidOperationException("Unhandled WZ node type [1]");

            bw.Write(type);
            if (_oldUol && node is WzUolProperty uolProperty)
                bw.Write(ds.AddString($"uol_{uolProperty.Value}"));
            else if (node is WzIntProperty intProperty)
                bw.Write((long)intProperty.Value);
            else if (node is WzShortProperty shortProperty)
                bw.Write((long)shortProperty.Value);
            else if (node is WzLongProperty longProperty)
                bw.Write(longProperty.Value);
            else if (node is WzFloatProperty floatProperty)
                bw.Write((double)floatProperty.Value);
            else if (node is WzDoubleProperty doubleProperty)
                bw.Write(doubleProperty.Value);
            else if (node is WzStringProperty property)
                bw.Write(ds.AddString(property.Value));
            else if (node is WzVectorProperty nodeV)
            {
                bw.Write(nodeV.X);
                bw.Write(nodeV.Y);
            }
            else if (node is WzCanvasProperty wzcp)
            {
                bw.Write(ds.AddCanvas(wzcp));
                bw.Write((ushort)wzcp.PngProperty.Width);
                bw.Write((ushort)wzcp.PngProperty.Height);
                wzcp.Dispose();
            }
            else if (node is WzSoundProperty wzmp)
            {
                bw.Write(ds.AddMP3(wzmp));
                bw.Write((uint)wzmp.SoundLength);
                wzmp.Dispose();
            }

            switch (type)
            {
                case 0:
                    bw.Write(0L);
                    break;
                case 3:
                    bw.Write(0);
                    break;
            }
        }

        private static void WriteString(string s, BinaryWriter bw)
        {
            var toWrite = Encoding.UTF8.GetBytes(s);
            bw.Write((ushort)toWrite.Length);
            bw.Write(toWrite);
        }

        private static void WriteBitmap(WzCanvasProperty node, BinaryWriter bw)
        {
            Bitmap b;
            try
            {
                b = node.PngProperty.GetBitmap();
            }
            catch (Exception e)
            {
                b = null;
            }

            if (b == null)
            {
                bw.Write((uint)0);
            }
            else
            {
                var compressed = GetCompressedBitmap(b);
                node.PngProperty.Dispose();
                // b = null;
                bw.Write((uint)compressed.Length);
                bw.Write(compressed);
            }
        }

        private static void WriteMP3(WzSoundProperty node, BinaryWriter bw)
        {
            var m = node.GetBytes();
            bw.Write(m);
            // node.Dispose();
            // m = null;
        }

        private static byte[] GetCompressedBitmap(Bitmap b)
        {
            BitmapData bd = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);
            int inLen = bd.Stride * bd.Height;
            int outLen = Is64Bit ? EMaxOutputLen64(inLen) : EMaxOutputLen32(inLen);
            byte[] outBuf = new byte[outLen];
            outLen = Is64Bit
                ? ECompressLZ464(bd.Scan0, outBuf, inLen, outLen, 0)
                : ECompressLZ432(bd.Scan0, outBuf, inLen, outLen, 0);
            b.UnlockBits(bd);
            Array.Resize(ref outBuf, outLen);
            return outBuf;
        }

        [DllImport("lz4hc_32.dll",
            CallingConvention = CallingConvention.Cdecl, EntryPoint = "LZ4_compress_HC")]
        private static extern int ECompressLZ432(IntPtr source, byte[] dest, int inputLen, int maxSize, int level);

        [DllImport("lz4hc_64.dll",
            CallingConvention = CallingConvention.Cdecl, EntryPoint = "LZ4_compress_HC")]
        private static extern int ECompressLZ464(IntPtr source, byte[] dest, int inputLen, int maxSize, int level);

        [DllImport("lz4hc_32.dll",
            CallingConvention = CallingConvention.Cdecl, EntryPoint = "LZ4_compressBound")]
        private static extern int EMaxOutputLen32(int inputLen);

        [DllImport("lz4hc_64.dll",
            CallingConvention = CallingConvention.Cdecl, EntryPoint = "LZ4_compressBound")]
        private static extern int EMaxOutputLen64(int inputLen);

        private static void EnsureMultiple(this Stream s, int multiple)
        {
            var skip = (int)(multiple - s.Position % multiple);
            if (skip == multiple)
                return;
            s.Write(new byte[skip], 0, skip);
        }

        private sealed class DumpState
        {
            public DumpState()
            {
                Canvases = new List<WzCanvasProperty>();
                Strings = new Dictionary<string, uint>(StringComparer.Ordinal) { { "", 0 } };
                MP3s = new List<WzSoundProperty>();
                UOLs = new Dictionary<WzUolProperty, Action<BinaryWriter, byte[]>>();
                Nodes = new Dictionary<WzObject, uint>();
            }

            public List<WzCanvasProperty> Canvases { get; private set; }

            public Dictionary<string, uint> Strings { get; private set; }

            public List<WzSoundProperty> MP3s { get; private set; }

            public Dictionary<WzUolProperty, Action<BinaryWriter, byte[]>> UOLs { get; private set; }

            public Dictionary<WzObject, uint> Nodes { get; private set; }

            public uint AddCanvas(WzCanvasProperty node)
            {
                uint ret = (uint)Canvases.Count;
                Canvases.Add(node);
                return ret;
            }

            public uint AddMP3(WzSoundProperty node)
            {
                uint ret = (uint)MP3s.Count;
                MP3s.Add(node);
                return ret;
            }

            public uint AddString(string str)
            {
                if (Strings.ContainsKey(str))
                    return Strings[str];
                uint ret = (uint)Strings.Count;
                Strings.Add(str, ret);
                return ret;
            }

            public void AddNode(WzObject node)
            {
                uint ret = (uint)Nodes.Count;
                Nodes.Add(node, ret);
            }

            public uint GetNodeID(WzObject node)
            {
                return Nodes[node];
            }

            public uint GetNextNodeID()
            {
                return (uint)Nodes.Count;
            }

            public void AddUOL(WzUolProperty node, long currentPosition)
            {
                UOLs.Add(node, (bw, data) =>
                {
                    bw.BaseStream.Position = currentPosition;
                    bw.Write(data);
                });
            }
        }
    }
}