using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ctrl2MqttBridge.Classes
{
    public static class BridgeInstaller
    {
        static bool ProcWasRunning = false;
        public static int Install(bool silent)
        {
            Console.WriteLine("Begin install");
            const bool ScanDirectories = false;
            //const string sysconfig = @"\\192.168.214.241\c$\ProgramData\Siemens\MotionControl\oem\sinumerik\hmi\cfg\systemconfiguration.ini";
            const string sysconfig = @"C:\ProgramData\Siemens\MotionControl\oem\sinumerik\hmi\cfg\systemconfiguration.ini";
            const string sysconfigpath = @"C:\ProgramData\Siemens\MotionControl\oem\sinumerik\hmi\cfg";
            const string TargetPath = @"C:\Ctrl2MqttBridge";
            const string procName = "PROC610";

            #region Laufenden Prozess beenden

            var myProcess = System.Diagnostics.Process.GetCurrentProcess();
            var processes = System.Diagnostics.Process.GetProcessesByName(myProcess.ProcessName);
            foreach (var process in processes)
            {
                if (process.Id == myProcess.Id)
                    continue;
                ProcWasRunning = true;
                Console.Write("Exit running instance...");
                process.CloseMainWindow();
                process.WaitForExit(2000);
                process.Kill();
                Console.Write("...waiting...");
                Thread.Sleep(2000);
                Console.WriteLine("...OK");
            }
            #endregion

            #region Dateien kopieren (bei Bedarf)
            string myExe = myProcess.ProcessName;
            if (!myExe.EndsWith(".exe"))
                myExe += ".exe";
            string exe = Path.GetFullPath(myExe);
            string exedir = Path.GetDirectoryName(exe);
            
            //Wenn setup gestartet aus Temp dir kopieren, sonst nicht
            if (TargetPath.ToLower() != exedir.ToLower())
            {
                if (!Directory.Exists(TargetPath))
                {
                    Console.Write("Create directory " + TargetPath);
                    Directory.CreateDirectory(TargetPath);
                }

                //Copy Files
                Console.Write("Copy files to " + TargetPath);
                try
                {
                    FileInfo[] files = new DirectoryInfo(exedir).GetFiles("*.*");
                    foreach (var file in files)
                    {
                        Console.Write(".");
                        File.Copy(file.FullName, TargetPath + "\\" + file.Name, true);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Error - Copy files: \r\n" + e.ToString());
                    Console.ForegroundColor = ConsoleColor.White;
                    return Errors.CopyFiles;

                }
            }
            #endregion

            Console.WriteLine();
            
            if (Functions.IsSiemens && !Directory.Exists(sysconfigpath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error - file not found: " + sysconfig);
                Console.ForegroundColor = ConsoleColor.White;
                return Errors.SysconfigNotFound;
            }
            #region Alle sysconfig.inis suchen - inaktiv
            //Alle systemconfiguration.ini Dateien suchen und alle Prozesse auflisten?
            if (ScanDirectories)
            {
                //Alle systemconfiguration.ini Dateien suchen
                string startPath = @"\\192.168.214.241\c$\ProgramData\Siemens\MotionControl";
                var files = GetFilesRecursive(startPath);
                Console.WriteLine("Getting declared processes...");
                List<string> processList = new List<string>();
                foreach (var file in files)
                {
                    IniFile ini = new IniFile(file);
                    processList.AddRange(ini.GetValues("processes"));
                }
                Console.WriteLine("Found " + processList.Count + " processes.");
                Console.ForegroundColor = ConsoleColor.Yellow;
                bool ProcFound = false;
                foreach (var p in processList)
                {
                    string processName = p.Substring(0, p.IndexOf('=') + 1);
                    int indexPro = p.IndexOf("process:=");
                    int length = p.IndexOf(',', p.IndexOf("process:=")) - indexPro;
                    processName += p.Substring(indexPro, length);
                    if (processName.ToLower().StartsWith(procName.ToLower()))
                        ProcFound = true;
                    Console.WriteLine(processName);
                }
                Console.ForegroundColor = ConsoleColor.White;
                if (ProcFound)
                {
                    //Gucken ob in der eigentlichen Datei oder irgendeiner

                    Console.WriteLine(procName + " already found. Overwrite? [y/n])");
                    var input = Console.ReadKey();
                    if (input.Key == ConsoleKey.Y)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        throw new NotImplementedException("Diese Funktion gibt es leider noch nicht");
                        Console.ForegroundColor = ConsoleColor.White;
                        return -99;
                    }
                    else
                    {
                        Console.WriteLine("Done.");
                        return -99;

                    }
                }
            }
            #endregion

            //Alternativ Prozessnummer/Name festlegen und IMMER den nehmen. 610?
            #region Systemconfiguration.ini anpassen
            if (Functions.IsSiemens)
            {
                Console.Write("Look for " + procName + " in " + sysconfig + "...");
                IniFile iniFile = new IniFile(sysconfig);
                string procValue = iniFile.GetValue("processes." + procName);
                if (String.IsNullOrEmpty(procValue))
                    Console.WriteLine("NOT FOUND");
                else
                {
                    Console.WriteLine("FOUND:");
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine(procValue);
                    Console.ForegroundColor = ConsoleColor.White;
                }
                string newValue = "image:=\"" + TargetPath + "\\" + myExe + " -r\", process:=ctrl2mqttbridge, startupTime:=afterServices, workingdir:=\"" + TargetPath + "\", background:=true";
                if (newValue != procValue)
                {
                    Console.WriteLine("Setting new value for " + procName + "=");
                    iniFile.SetValue("processes." + procName, newValue);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(newValue);
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine("Saving.");
                    iniFile.Save();
                }
                else
                {
                    Console.WriteLine("Value for " + procName + " is OK");
                }
            }
            #endregion

            #region Bei Rexroth in den Autostart legen
            if(Functions.IsRexroth)
            {
                try
                {
                    //string linkFileName = Environment.GetFolderPath(Environment.SpecialFolder.Startup)
                    //                + Path.DirectorySeparatorChar
                    //                + myExe.Replace(".exe", "")
                    //                + ".lnk";

                    //var shell = new IWshRuntimeLibrary.WshShell();
                    //var shortcut = shell.CreateShortcut(linkFileName) as IWshRuntimeLibrary.IWshShortcut;
                    //shortcut.TargetPath = TargetPath + Path.DirectorySeparatorChar + myExe;
                    //shortcut.Arguments = "-r";
                    //shortcut.WorkingDirectory = TargetPath;
                    //shortcut.WindowStyle = 7;
                    //shortcut.Save();
                    //ToDo: Reimplement shortcut creation
                    throw new NotImplementedException("Implement shortcut creation");
                }
                catch 
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Error - unable to create shortcut in startup");
                    Console.ForegroundColor = ConsoleColor.White;
                    return Errors.ShortcutCreation;
                }

                var ctrl2MqttBridgeSettings = Functions.ReadSettings(TargetPath + Path.DirectorySeparatorChar + Program.SettingsFilename);
                ctrl2MqttBridgeSettings.OpcUaMode = true;
                ctrl2MqttBridgeSettings.ServerName = "192.168.142.250";
                Functions.SaveSettings(TargetPath + Path.DirectorySeparatorChar + Program.SettingsFilename, ctrl2MqttBridgeSettings);

            }
            #endregion
            
            Console.WriteLine();
            if (ProcWasRunning && !silent)
            {
                Console.WriteLine("Ctrl2MqttBridge was running before install.");
                Console.Write("Shall i try to restart the service? [y/n]");
                var key = Console.ReadKey();
                if(key.Key== ConsoleKey.Y)
                {
                    Console.WriteLine("This will not work with 3GL/OperateNet - please restart OPERATE");
                    //Console.WriteLine();
                    //Console.Write("Try to start " + exe+"...");
                    //string filename= TargetPath + "\\" + myExe + " -r";
                    //Process p = new Process();
                    //p.StartInfo = new ProcessStartInfo(filename);
                    //p.Start();
                    //Console.Write("OK");
                }
            }
            Console.WriteLine();
            Console.WriteLine("Done- over and out.");
            return Errors.None;
        }

        static int cursorStart = 0;
        static List<string> GetFilesRecursive(string startDir)
        {
            var FileList = new List<string>();
            Console.WriteLine("Scanning direcotry \"" + startDir + "\"...");
            cursorStart = Console.CursorTop;
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            GetDirs(startDir, ref FileList);
            Console.ForegroundColor = ConsoleColor.White;
            Console.SetCursorPosition(0, cursorStart);
            Console.Write(lastDir);
            Console.WriteLine("Done. Found " + FileList.Count + " systemconfiguration files.");
            return FileList;
            
        }
        static char[] lastDir=new char[] { ' ' };
        static void GetDirs(string directory, ref List<string> fileList)
        {
            Console.SetCursorPosition(0, cursorStart);
            Console.Write(lastDir);
            Console.SetCursorPosition(0, cursorStart);
            Console.Write(directory);
            lastDir = new char[directory.Length]; 
            for(int i = 0; i<lastDir.Length; i++)
            {
                lastDir[i] = ' ';
            }
            foreach(var di in new DirectoryInfo(directory).GetDirectories())
            {
                GetDirs(di.FullName, ref fileList);
                foreach(var fi in di.GetFiles("systemconfiguration.ini"))
                {
                    fileList.Add(fi.FullName);
                }
            }
        }
        public static class Errors
        {
            public const int None = 0;
            public const int SysconfigNotFound = -1;
            public const int CopyFiles = -2;
            public const int ShortcutCreation = -3;

        }
    }
}
