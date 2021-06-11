using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/*
 *  This is modified code from the LegendaryExplorerCore codebase
 *  Taken from LEC on 2021/6/10, at https://github.com/ME3Tweaks/ME3Explorer/commit/0e01b9cfb85668a775afa22ba268e94b525ddc2e
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
            string[] Pattern = { "*.pcc", "*.afc", "*.bik", "*.bin", "*.tlk", "*.cnd", "*.upk", "*.tfc", "*.isb", "*.usf", "*.txt", "*.ini", "*.dlc" };
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
            string directoryName = Path.GetFileName(Path.GetDirectoryName(basefolder));
            // Do not include the directory's existing PCConsoleTOC.bin
            res.AddRange(GetTocableFiles(basefolder).Except(new[] { Path.Combine(basefolder, "PCConsoleTOC.bin") }, StringComparer.InvariantCultureIgnoreCase));
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
                    //biogame, only do cookedpcconsole and movies.
                    foreach (DirectoryInfo f in folders)
                    {
                        if (f.Name == "CookedPCConsole" || f.Name == "Movies")
                            res.AddRange(GetFiles(Path.Combine(basefolder, f.Name), isLE2LE3));
                        else if (isLE2LE3 && f.Name == "DLC")
                            res.AddRange(GetFiles(Path.Combine(basefolder, f.Name), isLE2LE3));  // may need updated when we get LE1 DLC system up
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
        /// <param name="directory">DLC_ directory, like DLC_CON_JAM, or the BIOGame directory of the game.</param>
        /// <param name="game"></param>
        /// <returns>Memorystream of TOC created, null if there are no entries or input was invalid</returns>
        public static MemoryStream CreateTOCForDirectory(string directory, MEGame game)
        {
            bool isLe2Le3 = game == MEGame.LE2 || game == MEGame.LE3;
            var files = GetFiles(directory, isLe2Le3);
            var originalFilesList = files;
            if (files.Count > 0)
            {
                //Strip the non-relative path information
                string file0fullpath = files[0];
                int dlcFolderStartSubStrPos = file0fullpath.IndexOf("DLC_", StringComparison.InvariantCultureIgnoreCase);
                if (dlcFolderStartSubStrPos > 0)
                {
                    // DLC TOC
                    files = files.Select(x => x.Substring(dlcFolderStartSubStrPos)).ToList();
                    files = files.Select(x => x.Substring(x.IndexOf(Path.DirectorySeparatorChar) + 1)).ToList(); //remove first slash
                }
                else
                {
                    // Basegame TOC
                    int biogameStrPos = file0fullpath.IndexOf("BIOGame", StringComparison.InvariantCultureIgnoreCase);
                    if (game != MEGame.ME3)
                    {
                        files.AddRange(GetFiles(Path.Combine(directory.Substring(0, biogameStrPos), "Engine", "Shaders"), isLe2Le3));
                        //TODO: is this required? It seems to include some DLC files in the TOC, but not all?
                        //if (game is MEGame.LE3)
                        //{

                        //    var dlcFolders = new DirectoryInfo(Path.Combine(directory, "DLC")).GetDirectories();
                        //    files.AddRange(dlcFolders.Where(f => f.Name.StartsWith("DLC_", StringComparison.OrdinalIgnoreCase)).Select(f => f.ToString())
                        //                             .SelectMany(dlcDir => GetFiles(dlcDir, isLe2Le3)));
                        //}
                    }
                    if (biogameStrPos > 0)
                    {
                        files = files.Select(x => x.Substring(biogameStrPos)).ToList();
                    }
                }

                var entries = originalFilesList.Select((t, i) => (files[i], (int)new FileInfo(t).Length)).ToList();

                return CreateTOCForEntries(entries);
            }
            throw new Exception("There are no TOCable files in the specified directory.");
        }

        /// <summary>
        /// Creates the binary for a TOC file for a specified list of filenames and sizes. The filenames should already be in the format that will be used in the TOC itself.
        /// </summary>
        /// <param name="filesystemInfo">list of filenames and sizes for the TOC</param>
        /// <returns>memorystream of TOC, null if list is empty</returns>
        public static MemoryStream CreateTOCForEntries(List<(string filename, int size)> filesystemInfo)
        {
            if (filesystemInfo.Count != 0)
            {
                var tbf = new TOCBinFile();

                // Todo: Update this someday so it lines up with the actual correct implementation
                var hashBucket = new TOCBinFile.TOCHashTableEntry();
                tbf.HashBuckets.Add(hashBucket);
                hashBucket.TOCEntries.AddRange(filesystemInfo.Select(x => new TOCBinFile.Entry
                {
                    flags = 0,
                    name = x.filename,
                    size = x.size
                }));

                return tbf.Save();
            }

            return null;
            /*
            MemoryStream fs = MemoryManager.GetMemoryStream();

                fs.WriteInt32(TOCBinFile.TOCMagicNumber); // Endian check
                fs.WriteInt32(0x0); // Media Data Count
                fs.WriteInt32(0x1); // Hash Table Count

                // TOCHashTableEntry (Only have 1 entry)
                fs.WriteInt32(0x8); // Offset of first entry from 
                fs.WriteInt32(filesystemInfo.Count); // Number of files in this table

                for (int i = 0; i < filesystemInfo.Count; i++)
                {

                    // TOCFileEntry - 4 Byte Aligned
                    (string file, int size) entry = filesystemInfo[i];

                    // Next Entry Offset
                    if (i == filesystemInfo.Count - 1) // Last entry has no next offset
                        fs.WriteAligned(BitConverter.GetBytes((ushort)0), 0, 2, 4);
                    else
                        fs.WriteAligned(BitConverter.GetBytes((ushort)entry.file.Length), 0, 2, 4);

                    //nextEntryOffsetPos = fs.Position;
                    fs.WriteUInt16((ushort)(0x1D + entry.file.Length)); // Next entry start offset

                    // Flags
                    fs.WriteUInt16(0);

                    // FileSize
                    if (!Path.GetFileName(entry.file).Equals("PCConsoleTOC.bin", StringComparison.InvariantCultureIgnoreCase))
                    {
                        fs.WriteInt32(entry.size);
                    }
                    else
                    {
                        selfSizePosition = fs.Position; // Save self-position so we can rewrite our own file size at the end
                        fs.WriteInt32(0);
                    }

                    // SHA1 - Not used by games...
                    fs.WriteZeros(20);
                    fs.WriteStringLatin1(entry.file); // Not present in hash table?

                    // Old method
                    //foreach (char c in file)
                    //    fs.WriteByte((byte)c);
                    fs.WriteByte(0);
                }

                if (selfSizePosition >= 0)
                {
                    // Write the size of our own TOC. This ensures TOC appears up to date when we try to update it later
                    // (important for DLC TOCs)
                    fs.Seek(selfSizePosition, SeekOrigin.Begin);
                    fs.WriteInt32((int)fs.Length);
                }

                return fs;
            }

            return null;*/
        }
    }
}