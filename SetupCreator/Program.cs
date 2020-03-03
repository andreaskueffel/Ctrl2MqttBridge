using Ionic.Zip;
using System;
using System.Collections.Generic;
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

            //Look for MqttBridge Process and stop it

            Console.WriteLine("Start to create sfx file");
            ZipFile myZip = new ZipFile("MqttBridgeSetup");
            if (args == null || args.Length < 1 || String.IsNullOrEmpty(args[0]))
            {
                Console.WriteLine("Es wurde kein Pfad angegeben");
                Environment.Exit(-1);
                return;
            }
            string directory = args[0];
            if (!Directory.Exists(directory))
            {
                Console.WriteLine("Verzeichnis "+directory+" existiert nicht");
                Environment.Exit(-2);
                return;
            }
            myZip.AddDirectory(directory, "");
            SelfExtractorSaveOptions saveOptions = new SelfExtractorSaveOptions();
            saveOptions.ExtractExistingFile = ExtractExistingFileAction.OverwriteSilently;
            saveOptions.Flavor = SelfExtractorFlavor.WinFormsApplication;
            saveOptions.SfxExeWindowTitle = "Präwema MQTT Bridge Setup";
            saveOptions.ProductName = "Präwema MQTT Bridge Setup Launcher";
            saveOptions.Copyright = "Copyright(C) 2020 Präwema Antriebstechnik GmbH";
            saveOptions.DefaultExtractDirectory = "%TEMP%\\MqttBridge"+DateTime.Now.ToString("yyyymmddHHMMss");
            saveOptions.Description = "Präwema MQTT Bridge Setup Launcher";
            saveOptions.ProductVersion = "1.0.2.0";
            saveOptions.FileVersion = new Version(saveOptions.ProductVersion);
            saveOptions.Quiet = false;
            saveOptions.RemoveUnpackedFilesAfterExecute = true;
            saveOptions.PostExtractCommandLine = "MqttBridge.exe -i";

            myZip.SaveSelfExtractor("MqttBridgeSetup.exe", saveOptions);
        }
    }
}
