using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;

// ME3Tweaks AutoTOC
// Originally by SirCxyrtyx, updated for LE by HenBagle

namespace AutoTOC
{
    class Program
    {
        static void Main(string[] args)
        {
            string gameDir;
            MEGame game = MEGame.ME3;

            if (args.Length == 1)
            {
                // Path is passed in, hopefully is game .exe
                gameDir = args[0];
                if (gameDir.EndsWith(".exe"))
                {
                    (gameDir, game) = GetGamepathFromExe(gameDir);
                }
            }
            else if (args.Length == 2 && args[0] == "-r")
            {
                try {
                    game = (MEGame)Enum.Parse(typeof(MEGame), args[1], true);
                    gameDir = GetGamepathFromRegistry(game);
                    if(game != MEGame.ME3)
                    {
                        switch(game){
                            case MEGame.LE1:
                                gameDir = Path.Combine(gameDir, "Game", "ME1");
                                break;
                            case MEGame.LE2:
                                gameDir = Path.Combine(gameDir, "Game", "ME2");
                                break;
                            case MEGame.LE3:
                                gameDir = Path.Combine(gameDir, "Game", "ME3");
                                break;
                            default:
                                throw new ArgumentException();
                        }
                    }
                    Console.WriteLine("Game location detected in registry");
                }
                catch (ArgumentException e){
                    Console.WriteLine("Not a supported Mass Effect game");
                    return;
                }
                catch {
                    Console.WriteLine("Unable to detect gamepath from registry");
                    return;
                }
            }
            else
            {
                Console.WriteLine("Requires one argument: .exe of the game you're trying to TOC.");
                Console.WriteLine("(eg. \"D:\\Origin Games\\Mass Effect Legendary Edition\\ME3\\Binaries\\Win64\\MassEffect3.exe)");
                Console.WriteLine("Detect game from registry with -r {game}. Options: ME3, LE1, LE2, LE3");
                return;
            }

            Console.WriteLine($"Generating TOCs for {gameDir}");
            GenerateTocFromGamedir(gameDir, game);
            Console.WriteLine("Done!");
        }

        static void GenerateTocFromGamedir(string gameDir, MEGame game)
        {
            string baseDir = Path.Combine(gameDir, @"BIOGame\");
            string dlcDir = Path.Combine(baseDir, @"DLC\");
            List<string> folders = new List<string>();
            if (game != MEGame.LE1)
            {
                if(Directory.Exists(dlcDir))
                {
                    folders.AddRange((new DirectoryInfo(dlcDir)).GetDirectories().Select(d => d.FullName));
                }
                else
                {
                    Console.WriteLine("DLC folder not detected, TOCing basegame only...");
                }
            }
            Task.WhenAll(folders.Select(loc => TOCDLCAsync(loc, game)).Prepend(TOCBasegameAsync(baseDir, game))).Wait();
        }

        static Task TOCBasegameAsync(string tocLoc, MEGame game)
        {
            return Task.Run(() =>
            {
                var TOC = TOCCreator.CreateBasegameTOCForDirectory(tocLoc, game);
                TOC.WriteToFile(Path.Combine(tocLoc, "PCConsoleTOC.bin"));
            });
        }

        static Task TOCDLCAsync(string tocLoc, MEGame game)
        {
            return Task.Run(() => CreateDLCTOC(tocLoc, game));
        }

        static void CreateDLCTOC(string tocLoc, MEGame game)
        {
            try
            {
                var TOC = TOCCreator.CreateDLCTOCForDirectory(tocLoc, game);
                TOC.WriteToFile(Path.Combine(tocLoc, "PCConsoleTOC.bin"));
            }
            catch (Exception e)
            {
                Console.WriteLine($"No TOCable files in {tocLoc}, may just be packed DLC.");
            }
        }

        static string[] ValidExecutables = { "MassEffect1.exe", "MassEffect2.exe", "MassEffect3.exe" };

        static (string, MEGame) GetGamepathFromExe(string path)
        {
            if(File.Exists(path) && ValidExecutables.Any((exe) => path.EndsWith(exe)))
            {
                var dir = path.Substring(0, path.LastIndexOf("Binaries", StringComparison.OrdinalIgnoreCase));
                if (path.EndsWith(ValidExecutables[0])) return (dir, MEGame.LE1);
                if (path.EndsWith(ValidExecutables[1])) return (dir, MEGame.LE2);
                if (path.EndsWith(ValidExecutables[2]))
                {
                    var versionInfo = FileVersionInfo.GetVersionInfo(path);
                    if (versionInfo.FileVersion.StartsWith("1")) return (dir, MEGame.ME3);
                    else return (dir, MEGame.LE3);
                }
                // Should never get here
                throw new ArgumentException("Executable file is not a supported Mass Effect game.");
            }
            throw new ArgumentException("Executable file is not a supported Mass Effect game.");
        }

        static string GetGamepathFromRegistry(MEGame game)
        {
            if(game != MEGame.ME3)
            {
                string hkey64 = @"HKEY_LOCAL_MACHINE\SOFTWARE\BioWare\Mass Effectâ„¢ Legendary Edition"; // Yes all that weird garbage in this name is required... but not for everyone
                string test = (string)Registry.GetValue(hkey64, "Install Dir", null);
                if (test != null)
                {
                    return test;
                }
                hkey64 = @"HKEY_LOCAL_MACHINE\SOFTWARE\BioWare\Mass Effect Legendary Edition"; //For those without weird garbage
                test = (string)Registry.GetValue(hkey64, "Install Dir", null);
                return test;
            }
            else
            {
                // Get ME3 path from registry
                string hkey32 = @"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\BioWare\Mass Effect 3";

                return (string)Registry.GetValue(hkey32, "Install Dir", null);
            }

        }
    }
}
