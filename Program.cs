using MqttBridge.Classes;
using MqttBridge.Interfaces;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using MQTTnet.Client.Receiving;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Server;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MqttBridge
{
    public class Program
    {
        public const string SettingsFilename = "MqttBridgeSettings.json";

        public static MqttBridgeSettings MqttBridgeSettings=new MqttBridgeSettings();
        static MqttBridge MqttBridge;

        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;
            bool InstallOnly = false;
            bool RunOnly = false;

            Console.WriteLine("-----------------------------------------------------------");
            Console.WriteLine("MQTT - Bridge                              PRÄWEMA (c) 2020");
            Console.WriteLine("Version: " + typeof(Program).Assembly.GetName().Version.ToString());
            Console.WriteLine("");
            Console.WriteLine("-----------------------------------------------------------");


            if (args != null)
            {
                foreach (string arg in args)
                {
                    if (arg.ToLower() == "install" || arg.ToLower()=="-i")
                    {
                        InstallOnly = true;
                    }
                    if (arg.ToLower() == "run" || arg.ToLower() == "-r")
                    {
                        RunOnly = true;
                    }
                }
            }
            if (!RunOnly)
            {
                Console.WriteLine("Installation Mode");
                Console.WriteLine("-----------------");
                int result = BridgeInstaller.Install(InstallOnly);
                if (InstallOnly)
                {
                    //Console.WriteLine("Press any key to exit");
                    //Console.ReadKey();
                    if(result!=BridgeInstaller.Errors.None)
                    {
                        Console.WriteLine("Errors occured, Code "+result+" Press any key to exit");
                        Console.ReadKey();
                    }
                    return;
                }
                else
                {
                    Console.WriteLine();
                    Console.WriteLine("Installation finished. Do you want to run production mode NOW [Not Supported!]? [y/n]");
                    if (Console.ReadKey().Key != ConsoleKey.Y)
                        return;
                }
            }

            Console.WriteLine("Normal operation Mode");
            Console.WriteLine("---------------------");
            Task.Run(async () => await StartBridge()).Wait();
            Console.WriteLine("Type 'q' to exit");
            string readLine = "";
            while (readLine.ToLower() != "q")
                readLine = Console.ReadLine();
        }

        static async Task StartBridge()
        {

            MqttBridgeSettings = Functions.ReadSettings(SettingsFilename);
            Functions.SaveSettings(SettingsFilename, MqttBridgeSettings);
            MqttBridge = new MqttBridge();
            await MqttBridge.StartAsync();
        }

        private static void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            Console.WriteLine("Unhandled Exception occured:\r\n" + e.ExceptionObject);
        }

        
    }
}
