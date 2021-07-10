using System;
using System.Collections.Generic;
using System.IO;

namespace Celeste_Launcher_Gui.Win32
{
    public class LibraryChecker
    {
        public static bool X86LibraryExists(string libraryName)
        {
            // Search order as defined by: https://docs.microsoft.com/en-us/windows/win32/dlls/dynamic-link-library-search-order#search-order-for-desktop-applications
            var dllSearchDirectories = new List<string>
            {
                AppDomain.CurrentDomain.BaseDirectory,
                Environment.GetFolderPath(Environment.SpecialFolder.SystemX86),
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.CurrentDirectory
            };

            var pathEnvironmentVariable = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrWhiteSpace(pathEnvironmentVariable))
                dllSearchDirectories.AddRange(pathEnvironmentVariable.Split(';'));

            foreach (var dllBaseDir in dllSearchDirectories)
            {
                var dllFilePath = Path.Combine(dllBaseDir, libraryName);
                if (File.Exists(dllFilePath))
                    return true;
            }

            return false;
        }
    }
}
