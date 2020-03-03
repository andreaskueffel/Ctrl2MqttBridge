using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MqttBridge.Classes
{
    public static class BridgeInstaller
    {
        public static void Install()
        {
            Console.WriteLine("Begin install");
            const bool ScanDirectories = false;
            //const string sysconfig = @"\\192.168.214.241\c$\ProgramData\Siemens\MotionControl\oem\sinumerik\hmi\cfg\systemconfiguration.ini";
            const string sysconfig = @"C:\ProgramData\Siemens\MotionControl\oem\sinumerik\hmi\cfg\systemconfiguration.ini";
            const string TargetPath = @"C:\MqttBridge";
            const string procName = "PROC610";
            //Wenn der Prozess läuft erstmal beenden?
            var processes = System.Diagnostics.Process.GetProcessesByName("mqttbridge");
            var myProcess = System.Diagnostics.Process.GetCurrentProcess();
            foreach (var process in processes)
            {
                if (process.Id == myProcess.Id)
                    continue;

                process.CloseMainWindow();
                process.WaitForExit(2000);
                process.Kill();
            }
            string myExe = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
            if (!myExe.EndsWith(".exe"))
                myExe += ".exe";
            string exe = Path.GetFullPath(myExe);//"MqttBridge.exe");
            string exedir = Path.GetDirectoryName(exe);

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
            catch(Exception e)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error - Copy files: \r\n" + e.ToString());
                Console.ForegroundColor = ConsoleColor.White;
                return;

            }
            Console.WriteLine();
            
            if (!File.Exists(sysconfig))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error - file not found: " + sysconfig);
                Console.ForegroundColor = ConsoleColor.White;
                return;
            }
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
                        return;
                    }
                    else
                    {
                        Console.WriteLine("Done.");
                        return;

                    }
                }
            }
            
            //Alternativ Prozessnummer/Name festlegen und IMMER den nehmen. 610?
            Console.Write("Look for " + procName + " in "+sysconfig+"...");
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
            //string newValue = "process:=MQTTBRIDGE, cmdline:=\"" + exe + "\", deferred:=false, startupTime:=afterServices, oemframe:=true, processaffinitymask:=0xFFFFFFFF"; //windowname:=\"" + exe + "\",
            string newValue = "image:=\""+exe+" -r\", process:=mqttbridge, startupTime:=afterServices, workingdir:=\"" + exedir + "\", background:=true";
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
                Console.WriteLine("Value for "+procName+" is OK");
            }
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("Done- over and out.");
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
    }
}
