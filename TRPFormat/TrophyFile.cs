using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Drawing;
using System.Xml;
using System.Linq;
using System.Windows.Media.Imaging;

namespace PS3TRPViewer.TRPFormat
{

    // Basic parsing function used from: https://github.com/mhvuze/TRPUnpack/blob/master/TRPUnpack/Program.cs
    public class TrophyFile
    {
        private static readonly int magicNumber = 0x004DA2DC;

        private readonly BinaryReader reader;
        private string filePath;

        public int EntryCount { get; set; }
        public int Start { get; set; }
        private Dictionary<string, byte[]> fileNameToData;
        public List<SFMFile> ConfigFiles { get; set; }
        public List<PNGFile> ImageFiles { get; set; }

        public TrophyFile(string filePath)
        {
            reader = new BinaryReader(File.Open(filePath, FileMode.Open));

            this.filePath = filePath;
            ConfigFiles = new List<SFMFile>();
            ImageFiles = new List<PNGFile>();
            ParseFile();

            reader.Close();
        }

        /// <summary>
        /// Parses the raw bytes of the .TRP file
        /// </summary>
        private void ParseFile()
        {
            // TODO: Add a warning here
            if(reader.ReadInt32() != magicNumber) { return;  }

            // Read out the starting entry count
            reader.BaseStream.Seek(0x10, SeekOrigin.Begin);
            EntryCount = IPAddress.NetworkToHostOrder(reader.ReadInt32());
            Start = IPAddress.NetworkToHostOrder(reader.ReadInt32());
            reader.BaseStream.Seek(Start, SeekOrigin.Begin);

            long readerOffset = 0;
            fileNameToData = new Dictionary<string, byte[]>();

            for(int i = 0; i < EntryCount; i++)
            {
                // Get our entry info
                string fileName = ReadNullTerminatedString(reader).ToLowerInvariant();
                reader.BaseStream.Seek(Start + (i * 0x40) + 0x20, SeekOrigin.Begin);
                long offset = IPAddress.NetworkToHostOrder(reader.ReadInt64());
                long size = IPAddress.NetworkToHostOrder(reader.ReadInt64());

                readerOffset = reader.BaseStream.Position + 0x10;

                Console.WriteLine("Processing file [{0}]: {1}", (i + 1), fileName);

                reader.BaseStream.Seek(offset, SeekOrigin.Begin);

                byte[] fileData = reader.ReadBytes(Convert.ToInt32(size));

                fileNameToData.Add(fileName, fileData);
                
                reader.BaseStream.Seek(readerOffset, SeekOrigin.Begin);
            }

            foreach(KeyValuePair<string, byte[]> z in fileNameToData.Where(x => x.Key.EndsWith(".png"))) { ImageFiles.Add(new PNGFile(z.Value, z.Key)); }
            foreach (KeyValuePair<string, byte[]> z in fileNameToData.Where(x => x.Key.EndsWith(".sfm") && !x.Key.Contains("tropconf.sfm"))) { ConfigFiles.Add(new SFMFile(z.Value, z.Key, ImageFiles)); }

            Console.WriteLine("Complete with parsing TRP file...");
        }

        /// <summary>
        /// Read a null-terminated string from the associated binary reader
        /// </summary>
        public static string ReadNullTerminatedString(BinaryReader reader)
        {
            StringBuilder builder = new StringBuilder();
            char c;
            while( (c = (char)reader.ReadByte()) != '\0') builder.Append(c);
            return builder.ToString();
        }

        public bool ExportExtractedFiles(string filePath)
        {
            foreach(KeyValuePair<string, byte[]> kvp in fileNameToData)
            {
                File.WriteAllBytes(Path.Combine(filePath, kvp.Key.ToUpper()), kvp.Value);
            }
            return true;
        }
    }

    public struct Trophy
    {
        public int ID { get; set; }
        public bool Hidden { get; set; }
        public TrophyType Type { get; set; }
        public int PID { get; set; }

        public string Name { get; set; }
        public string Detail { get; set; }
        public BitmapImage TrophyIcon {
            get {
                int z = ID;
                var tst = PNGFiles.Where(x => x.fileID.Equals(z)).Select(x => x.imageIcon).First();
                BitmapImage img = new BitmapImage();
                using (MemoryStream mem = new MemoryStream())
                {
                    tst.Save(mem, System.Drawing.Imaging.ImageFormat.Png);
                    mem.Position = 0;
                    img.BeginInit();
                    img.StreamSource = mem;
                    img.CacheOption = BitmapCacheOption.OnLoad;
                    img.EndInit();
                }
                return img;
            }
        }

        public List<PNGFile> PNGFiles { get; set; }

        /// <summary>
        /// An enum representing the type of this trophy (Platinum/Gold/Silver/Bronze)
        /// The proper value is just the UTF8 version of the first letter (as described in the .SFM files)
        /// </summary>
        public enum TrophyType
        {
            Platinum = 0x50,
            Gold = 0x47,
            Silver = 0x53,
            Bronze = 0x42
        }
    }

    #region File Types
    /// <summary>
    /// A file type used to describe all of the trophy data, there's usually multiple per .TRP file, one per language
    /// </summary>
    public class SFMFile
    {
        private XmlDocument document;
        private string filePath;

        public int SFMID { get; set; }
        public string NPCommID { get; set; }
        public string TrophySetVersion { get; set;}
        public string ParentalLevel { get; set; }
        public string TitleName { get; set; }
        public string TitleDetail { get; set; }

        public List<Trophy> Trophies { get; set; }

        public List<PNGFile> PNGFiles { get; set; }

        public SFMFile(byte[] data, string filePath, List<PNGFile> files)
        {
            document = new XmlDocument();
            document.LoadXml(Encoding.UTF8.GetString(data));

            Trophies = new List<Trophy>();

            this.filePath = filePath;
            if(filePath.Any(c => Char.IsDigit(c)))
            {
                SFMID = Int32.Parse(filePath.Substring(5, 2));
            } else
            {
                SFMID = 0;
            }
            PNGFiles = files;
            ParseXMLDocument();
        }

        private void ParseXMLDocument()
        {
            var trophyConfig = document["trophyconf"];
            NPCommID = trophyConfig.ChildNodes[0].InnerText;
            TrophySetVersion = trophyConfig.ChildNodes[1].InnerText;
            ParentalLevel = trophyConfig.ChildNodes[2].InnerText;
            TitleName = trophyConfig.ChildNodes[3].InnerText;
            TitleDetail = trophyConfig.ChildNodes[4].InnerText;

            // Iterate through all of our trophies as stored in the file.
            for(int i = 5; i < trophyConfig.ChildNodes.Count; i++)
            {
                var trophyNode = trophyConfig.ChildNodes[i];
                Trophies.Add(new Trophy
                {
                    ID = int.Parse(trophyNode.Attributes["id"].Value),
                    Hidden = trophyNode.Attributes["hidden"].Value.Equals("yes") ? true : false,
                    Type = (Trophy.TrophyType)(Encoding.UTF8.GetBytes(trophyNode.Attributes["ttype"].Value)[0]),
                    PID = int.Parse(trophyNode.Attributes["pid"].Value),
                    Name = (trophyNode.ChildNodes[0]).InnerText,
                    Detail = (trophyNode.ChildNodes[1]).InnerText,
                    PNGFiles = PNGFiles
                });
            }

        }
    }


    /// <summary>
    /// A file type (just a PNG lol) used to show all of the trophy images, the ID corresponds to the ID as stored in the SFM file(s).
    /// </summary>
    public class PNGFile
    {
        public int fileID { get; set; }
        public Bitmap imageIcon { get; set; }
        public string fileName { get; set; }
        private MemoryStream fileStream { get; set; }

        public PNGFile(byte[] data, string filePath)
        {
            if (filePath.StartsWith("icon")) fileID = -1;
            else fileID = Int32.Parse(filePath.Substring(4, 3));

            fileStream = new MemoryStream(data);
            imageIcon = new Bitmap(fileStream);
        }

        /// <summary>
        /// Save out the trophy/icon out to a specific path
        /// </summary>
        /// <param name="outputPath">The folder in which to save the file</param>
        public void SavePNGFile(string outputPath)
        {
            Console.WriteLine("Saving PNG {0} to {1}", fileName, outputPath);
            imageIcon.Save(Path.Combine(outputPath, fileName));
        }
    }
    #endregion
}
