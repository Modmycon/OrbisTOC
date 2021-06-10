# AutoTOC
Standalone program to create the PCConsoleTOC.bin files for ME3 and Mass Effect Legendary Edition.

Run from the command line, pass it the game executable, and it will generate a TOC for the base game and all DLCs.

eg. ```.\AutoTOC.exe "C:\Program Files (x86)\Origin Games\Mass Effect 3\Binaries\Win32\MassEffect3.exe"```

You can also drag a game executable file directly onto AutoTOC in Windows Explorer to TOC.

You can use the registry detection feature with the -r flag and the game you're trying to TOC. Game options are ME3, LE1, LE2, and LE3.

eg: ```.\AutoTOC.exe -r LE2```
