using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/*
 *  This is modified code from the LegendaryExplorerCore codebase
 *  Taken from LEC on 2021/7/17
 */

namespace AutoTOC
{
    public enum MEGame
    {
        ME3,
        LE1,
        LE2,
        LE3
    }

    /// <summary>
    /// Class for generating TOC files
    /// </summary>
    public class TOCCreator
    {
        /// <summary>
        /// Returns the files in a given directory that match the pattern of a TOCable file
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static IEnumerable<string> GetTocableFiles(string path)
        {
            string[] Pattern = { "*.xxx", "*.pcc", "*.afc", "*.bik", "*.bin", "*.tlk", "*.cnd", "*.upk", "*.tfc", "*.isb", "*.usf", "*.txt", "*.ini", "*.dlc" };
            var res = new List<string>();
            foreach (string s in Pattern)
                res.AddRange(Directory.GetFiles(path, s));
            return res;
        }

        /// <summary>
        /// Recursively finds all TOCable files in a directory and it's subfolders
        /// </summary>
        /// <param name="basefolder"></param>
        /// <returns></returns>
        private static List<string> GetFiles(string basefolder, bool isLE2LE3)
        {
            var res = new List<string>();
            string directoryName = Path.GetFileName(basefolder);
            // Do not include the directory's existing OrbisTOC.bin
            res.AddRange(GetTocableFiles(basefolder).Except(new[] { Path.Combine(basefolder, "OrbisTOC.bin") }, StringComparer.InvariantCultureIgnoreCase));
            DirectoryInfo folder = new DirectoryInfo(basefolder);
            var folders = folder.GetDirectories();
            if (folders.Length != 0)
            {
                if (!directoryName.Equals("BioGame", StringComparison.InvariantCultureIgnoreCase))
                {
                    //treat as dlc and include all folders.
                    foreach (DirectoryInfo f in folders)
                        res.AddRange(GetFiles(Path.Combine(basefolder, f.Name), isLE2LE3));
                }
                else
                {
                    //BioGame, only do certain folders
                    foreach (DirectoryInfo f in folders)
                    {
                        if (f.Name == "CookedOrbis" || f.Name == "Movies")
                            res.AddRange(GetFiles(Path.Combine(basefolder, f.Name), isLE2LE3));
                        // LE2 and LE3 have the DLC folders included in TOC
                        else if (isLE2LE3 && f.Name == "DLC")
                            res.AddRange(GetFiles(Path.Combine(basefolder, f.Name), isLE2LE3));
                        // LE1 has the Content/Packages/ISACT folder included in TOC
                        else if (f.Name == "Content")
                            res.AddRange(GetFiles(Path.Combine(basefolder, f.Name, "Packages", "ISACT"), isLE2LE3));
                    }
                }
            }   

            return res;
        }


        /// <summary>
        /// Creates the binary for a TOC file for a specified directory root
        /// </summary>
        /// <param name="directory">The BIOGame directory of the game.</param>
        /// <param name="game"></param>
        /// <returns>Memorystream of TOC created, null if there are no entries or input was invalid</returns>
        public static MemoryStream CreateBasegameTOCForDirectory(string directory, MEGame game)
        {
            bool isLe2Le3 = game == MEGame.LE2 || game == MEGame.LE3;
            var files = GetFiles(directory, isLe2Le3);

            if (game == MEGame.LE1)
            {
                files = GetLE1Files(files, directory);
            }

            if (files.Count > 0)
            {
                // Basegame TOC
                string file0fullpath = files[0];
                int biogameStrPos = file0fullpath.IndexOf("BIOGame", StringComparison.InvariantCultureIgnoreCase);
                if (game != MEGame.ME3)
                {
                    files.AddRange(GetFiles(Path.Combine(directory.Substring(0, biogameStrPos), "Engine", "Shaders"), isLe2Le3));
                }

                var originalFilesList = files; // Original file paths, to use for getting filesizes after we shorten the paths

                if (biogameStrPos > 0)
                {
                    files = files.Select(x => x.Substring(biogameStrPos)).ToList();
                }

                var entries = originalFilesList.Select((t, i) => (files[i], (int)new FileInfo(t).Length)).ToList();

                return CreateTOCForEntries(entries);
            }
            throw new Exception("There are no TOCable files in the specified directory.");
        }

        /// <summary>
        /// Creates the binary for a TOC file for a specified DLC directory root
        /// </summary>
        /// <param name="directory">A DLC folder, such as DLC_CON_JAM</param>
        /// <param name="game"></param>
        /// <returns>Memorystream of TOC created, null if there are no entries or input was invalid</returns>
        public static MemoryStream CreateDLCTOCForDirectory(string directory, MEGame game)
        {
            bool isLe2Le3 = game == MEGame.LE2 || game == MEGame.LE3;
            var files = GetFiles(directory, isLe2Le3);
            var originalFilesList = files; // Original file paths, to use for getting filesizes after we shorten the paths

            if (files.Count > 0)
            {
                string file0fullpath = files[0];
                int dlcFolderStartSubStrPos = file0fullpath.IndexOf("DLC_", StringComparison.InvariantCultureIgnoreCase);
                if (dlcFolderStartSubStrPos == -1 || game == MEGame.LE1)
                {
                    throw new Exception("Not a TOCable DLC directory.");
                }

                files = files.Select(x => x.Substring(dlcFolderStartSubStrPos)).ToList();
                files = files.Select(x => x.Substring(x.IndexOf(Path.DirectorySeparatorChar) + 1)).ToList(); //remove first slash
                var entries = originalFilesList.Select((t, i) => (files[i], (int)new FileInfo(t).Length)).ToList();
                return CreateTOCForEntries(entries);

            }
            throw new Exception("There are no TOCable files in the specified directory.");
        }

        /// <summary>
        /// Parses LE1 DLC Autoload files and creates a list of files with correct DLC overrides.
        /// Only one instance of a specific filename will be output by this method
        /// </summary>
        /// <param name="basegameFiles">List of files for basegame toc</param>
        /// <param name="biogameDirectory">LE1 BioGame directory</param>
        /// <returns>List of files for toc, including DLC supercedances</returns>
        public static List<string> GetLE1Files(List<string> basegameFiles, string biogameDirectory)
        {
            if (!Directory.Exists(Path.Combine(biogameDirectory, "DLC"))) return basegameFiles;

            // Build dictionary of DLCs in mount priority
            Dictionary<int, string> dlcMounts = new Dictionary<int, string>();
            string[] dlcList = Directory.GetDirectories(Path.Combine(biogameDirectory, "DLC"), "*.*", SearchOption.TopDirectoryOnly);
            foreach (var dlcFolder in dlcList)
            {
                if (!(new DirectoryInfo(dlcFolder).Name).StartsWith("DLC_", StringComparison.OrdinalIgnoreCase))
                    continue;

                string autoLoadPath = Path.Combine(dlcFolder, "autoload.ini");  //CHECK IF FILE EXISTS?
                if (File.Exists(autoLoadPath))
                {
                    DuplicatingIni dlcAutoload = DuplicatingIni.LoadIni(autoLoadPath);
                    int mount = Convert.ToInt32(dlcAutoload["ME1DLCMOUNT"]["ModMount"].Value);
                    dlcMounts.Add(mount, dlcFolder);
                }
            }

            // filename, filepath
            Dictionary<string, string> outFiles = new Dictionary<string, string>();
            foreach (var dlc in dlcMounts.OrderByDescending(t => t.Key))
            {
                var files = GetFiles(dlc.Value, false);
                foreach (var file in files)
                {
                    var name = new FileInfo(file).Name.ToUpper();
                    if(!outFiles.ContainsKey(name)) outFiles.Add(name, file);
                }
            }

            // Add in basegame files
            foreach (var file in basegameFiles)
            {
                var name = new FileInfo(file).Name.ToUpper();
                if(!outFiles.ContainsKey(name)) outFiles.Add(name, file);
            }

            return outFiles.Values.ToList();
        }

        /// <summary>
        /// Creates the binary for a TOC file for a specified list of filenames and sizes. The filenames should already be in the format that will be used in the TOC itself.
        /// </summary>
        /// <param name="filesystemInfo">list of filenames and sizes for the TOC</param>
        /// <returns>memorystream of TOC, null if list is empty</returns>
        public static MemoryStream CreateTOCForEntries(List<(string relativeFilename, int size)> filesystemInfo)
        {
            if (filesystemInfo.Count != 0)
            {
                var tbf = new TOCBinFile();

                // Generate hashes for all names
                Dictionary<(string relativeFileName, int size), uint> fullHashMap =
                    new Dictionary<(string relativeFileName, int size), uint>(); // Unbounded hash table size

                foreach (var f in filesystemInfo)
                {
                    fullHashMap[f] = GetStringFullHash(Path.GetFileName(f.relativeFilename));
                }

                // Calculate optimal hash table size for performance
                var hashTableSize = filesystemInfo.Count; // Initial size is 100% 1:1
                List<TOCBinFile.TOCHashTableEntry> hashBuckets;

                while (true)
                {
                    hashBuckets = new List<TOCBinFile.TOCHashTableEntry>();
                    for(int i = 0; i < hashTableSize; i++) hashBuckets.Add(new TOCBinFile.TOCHashTableEntry());

                    //Populate the buckets with file entries
                    foreach (var hashPair in fullHashMap)
                    {
                        var bucketIdx = GetBoundedHashValue(hashPair.Value, hashTableSize);
                        hashBuckets[(int)bucketIdx].TOCEntries.Add(new TOCBinFile.Entry()
                        {
                            flags = 0,
                            name = hashPair.Key.relativeFileName,
                            size = hashPair.Key.size
                        });
                    }


                    // Check fill rate. We must have over 75% fill rate or we will lower the hash table size by 25% and try again (down to 50% of file table size)
                    var emptyHashBucketsCount = hashBuckets.Count(x => x.TOCEntries.Count == 0);

                    //Console.WriteLine($@"Hash fill rate: {100 - (emptyHashBucketsCount * 100.0f / hashTableSize)}%");

                    if (emptyHashBucketsCount > hashTableSize / 4)
                    {
                        int shrunkTableSize = Math.Max(filesystemInfo.Count / 2, hashTableSize - (hashTableSize / 4));
                        if (shrunkTableSize == hashTableSize)
                        {
                            // WARNING: This will be suboptimal hash table size but we are going to use it anyways to prevent crash
                            Console.WriteLine(@"WARNING: Hash table fill rate is low; even at 50% of the file size");
                            Console.WriteLine(@"This TOC file will work, but is suboptimal.");
                            break;
                        }

                        // Update size
                        hashTableSize = shrunkTableSize;
                        continue;
                    }
                    else
                    {
                        // This is a satisfactory TOC
                        break;
                    }
                }

                tbf.HashBuckets = hashBuckets;
                return tbf.Save();
            }

            return null;
        }

        /// <summary>
        /// Gets the hash value of a string without bounding (UE3-hash)
        /// </summary>
        /// <param name="strToHash"></param>
        /// <returns></returns>
        private static uint GetStringFullHash(string strToHash)
        {
            initCRCTable();

            uint hash = 0;
            var upperCaseStr = strToHash.ToUpper();
            for (var i = 0; i < upperCaseStr.Length; ++i)
            {
                char upperChar = upperCaseStr[i];
                hash = ((hash >> 8) & 0x00FFFFFF) ^ crcTable[(hash ^ ((byte)upperChar)) & 0x000000FF]; // ASCII
                hash = ((hash >> 8) & 0x00FFFFFF) ^ crcTable[(hash) & 0x000000FF]; // This is for unicode as each character is two bytes.
            }
            return hash;
        }

        /// <summary>
        /// Gets the hash value of a string with bounding (UE3-hash)
        /// </summary>
        /// <param name="inputString"></param>
        /// <param name="hashTableSize"></param>
        /// <returns></returns>
        private static uint GetStringHashBounded(string inputString, int hashTableSize)
        {
            return (uint)(GetStringFullHash(inputString) % hashTableSize);
        }

        /// <summary>
        /// Applies the specified bound to the listed hash
        /// </summary>
        /// <param name="hash"></param>
        /// <param name="bound"></param>
        /// <returns></returns>
        private static uint GetBoundedHashValue(uint hash, int bound)
        {
            return (uint)(hash % bound);
        }

        private static uint[] crcTable;

        /// <summary>
        /// Polynomial for our CRCs
        /// </summary>
        private const uint CRC_POLYNOMIAL = 0x04C11DB7;

        /// <summary>
        /// Initializes the CRC table which is used for calculating a hash
        /// </summary>
        private static void initCRCTable()
        {
            if (crcTable != null) return;
            crcTable = new uint[256];
            // Table has 256 entries.
            for (uint idx = 0; idx < 256; idx++)
            {
                // Generate CRCs based on the polynomial
                for (uint crc = idx << 24, bitIdx = 8; bitIdx != 0; bitIdx--)
                {
                    crc = ((crc & 0x80000000) == 0x80000000) ? (crc << 1) ^ CRC_POLYNOMIAL : crc << 1;
                    crcTable[idx] = crc;
                }
            }
        }
    }
}
