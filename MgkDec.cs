using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;
using System.IO.Compression;
namespace MgkDec
{
    class Program
    {
        static string helptext = "Megakis Decoding Tool\n\tUsage : MgkDec.exe <assetBundlepath>\n\tOutFileName : <assetBundlepath>.unity3d";
        static byte[] StringToByteArray(string hex) => Enumerable.Range(0, hex.Length).Where(x => x % 2 == 0).Select(x => Convert.ToByte(hex.Substring(x, 2), 16)).ToArray();

        static ICryptoTransform transform = new RC2CryptoServiceProvider
        {
            Mode = CipherMode.CBC,
            Key = StringToByteArray(""), // Just Search
            IV = StringToByteArray("") // Just Search
        }.CreateDecryptor();

        static Stack<string> nameStack = new Stack<string>();

        static string InFile = string.Empty;

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                System.Console.WriteLine(helptext);
                return;
            }

            foreach (var arg in args)
            {
                InFile = Path.GetFullPath(arg);
                if (!File.Exists(InFile))
                {
                    Console.WriteLine($"Error: File not found. ({InFile})");
                    continue;
                }

                byte[] inBytes = File.ReadAllBytes(InFile);

                if (inBytes[0] == 0x50 && inBytes[1] == 0x4B) ReadZip(inBytes);
                else
                {
                    string path = $"{Path.GetDirectoryName(InFile)}\\{Path.GetFileNameWithoutExtension(InFile)}.unity3d";
                    
                    if (!Save(path, inBytes))
                    {
                        nameStack.Clear();
                        ReadWebGL(inBytes);
                    }
                }
                
            }
        }

        static bool Save(string path, byte[] bytes)
        {
            if (CheckRawFile(bytes))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, bytes);
                return true;
            }
            else if (TryConvertFromBase64String(bytes, out var bytes2))
            {
                bytes2 = transform.TransformFinalBlock(bytes2, 0, bytes2.Length);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, bytes2);
                return true;
            }
            return false;
        }


        static void ReadZip(byte[] inBytes)
        {
            using (MemoryStream inMS = new MemoryStream(inBytes))
            using (var archive = new ZipArchive(inMS))
            {
                foreach (var entry in archive.Entries)
                {
                    string outPath = $"{Path.GetDirectoryName(InFile)}\\{Path.GetFileNameWithoutExtension(InFile)}\\{entry.FullName.Replace("/", "\\").Replace(".assetbundle", ".unity3d")}";
                    using (var entryStream = entry.Open())
                    using (var outMS = new MemoryStream())
                    {
                        entryStream.CopyTo(outMS);
                        Save(outPath, outMS.ToArray());
                    }
                }
            }
        }

        static void ReadWebGL(byte[] inBytes)
        {
            using (MemoryStream inMS = new MemoryStream(inBytes))
            using (BinaryReader br = new BinaryReader(inMS))
                ReadWebGL(br);
        }

        static void ReadWebGL(BinaryReader br)
        {
            nameStack.Push(br.ReadString());
            int contents_count = br.ReadInt32();
            for (int i = 0; i < contents_count; i++)
            {
                string name = string.Join("\\", nameStack.Reverse().Concat(new string[] { br.ReadString().Replace(".assetbundle", ".unity3d") }).ToArray());
                long size = br.ReadInt64();
                byte[] bytes = br.ReadBytes((int)size);
                Save(Path.Combine(Path.GetDirectoryName(InFile), name), bytes);
            }
            int child_count = br.ReadInt32();
            for (int i = 0; i < child_count; i++)
            {
                ReadWebGL(br);
            }
            nameStack.Pop();
        }

        static bool CheckRawFile(byte[] bytes)
        {
            if (bytes.Length < 7) return false;
            try
            {
                return Encoding.UTF8.GetString(bytes.Take(7).ToArray()) == "UnityFS";
            }
            catch (Exception)
            {
                return false;
            }
        }
        static bool TryConvertFromBase64String(byte[] bytes, out byte[] result)
        {
            result = null;
            try
            {
                result = Convert.FromBase64String(Encoding.UTF8.GetString(bytes));
                return true;
            } catch (Exception)
            {
                return false;
            }
        }
    }
}
