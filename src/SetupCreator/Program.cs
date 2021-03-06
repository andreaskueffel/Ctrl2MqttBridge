﻿using Ionic.Zip;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SetupCreator
{
    class Program
    {
        static void Main(string[] args)
        {
            //Usage:
            //createsetup path_to_files

            //Look for Ctrl2MqttBridge Process and stop it

            Console.WriteLine("Start to create sfx file");
            ZipFile myZip = new ZipFile("Ctrl2MqttBridgeSetup");
            if (args == null || args.Length < 1 || String.IsNullOrEmpty(args[0]))
            {
                Console.WriteLine("Es wurde kein Pfad angegeben");
                Environment.Exit(-1);
                return;
            }
            string directory = args[0];
            if (directory.EndsWith("\\"))
                directory = directory.Remove(directory.Length - 1);
            if (!Directory.Exists(directory))
            {
                Console.WriteLine("Verzeichnis "+directory+" existiert nicht");
                Environment.Exit(-2);
                return;
            }

            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(directory + "\\Ctrl2MqttBridge.exe");

            myZip.AddDirectory(directory, "");
            SelfExtractorSaveOptions saveOptions = new SelfExtractorSaveOptions();
            saveOptions.ExtractExistingFile = ExtractExistingFileAction.OverwriteSilently;
            saveOptions.Flavor = SelfExtractorFlavor.WinFormsApplication;
            saveOptions.SfxExeWindowTitle = "Präwema MQTT Bridge Setup";
            saveOptions.ProductName = "Präwema MQTT Bridge Setup Launcher";
            saveOptions.Copyright = "Copyright(C) 2020 Präwema Antriebstechnik GmbH";
            saveOptions.DefaultExtractDirectory = "%TEMP%\\Ctrl2MqttBridge"+DateTime.Now.ToString("yyyymmddHHMMss");
            saveOptions.Description = "Präwema MQTT Bridge Setup Launcher";
            saveOptions.ProductVersion = fvi.FileVersion;
            saveOptions.FileVersion = new Version(saveOptions.ProductVersion);
            saveOptions.Quiet = true;
            saveOptions.RemoveUnpackedFilesAfterExecute = true;
            saveOptions.PostExtractCommandLine = "Ctrl2MqttBridge.exe -i";
            
            myZip.SaveSelfExtractor(String.Format("Ctrl2MqttBridgeSetup-{0}.exe",fvi.FileVersion.ToString()), saveOptions);
        }
    }
}
