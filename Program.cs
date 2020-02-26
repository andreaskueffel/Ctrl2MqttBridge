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
        const string SettingsFilename = "MqttBridgeSettings.json";

        public static MqttBridgeSettings MqttBridgeSettings=new MqttBridgeSettings();
        static MqttBridge MqttBridge;

        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;

            Console.WriteLine("-----------------------------------------------------------");
            Console.WriteLine("MQTT - Bridge                              PRÄWEMA (c) 2020");
            Console.WriteLine("Version: " + typeof(Program).Assembly.GetName().Version.ToString());
            Console.WriteLine("");
            Console.WriteLine("-----------------------------------------------------------");


            if (args != null)
            {
                foreach (string arg in args)
                {
                    if (arg.ToLower() == "install")
                    {
                        Console.WriteLine("Installation Mode");
                        Console.WriteLine("-----------------");
                        BridgeInstaller.Install();
                        return;
                    }
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
            

            Console.WriteLine("Getting settings...");
            string settings = "";
            if (File.Exists(SettingsFilename))
                settings = File.ReadAllText(SettingsFilename);
            if (!String.IsNullOrEmpty(settings)) try
                {
                    MqttBridgeSettings = JsonConvert.DeserializeObject<MqttBridgeSettings>(settings);
                    Console.WriteLine("Settings read.");
                }
                catch { }

            string newSettings = JsonConvert.SerializeObject(MqttBridgeSettings, Formatting.Indented);
            if (settings != newSettings)
            {
                Console.WriteLine("Settings changed, save to " + SettingsFilename);
                File.WriteAllText(SettingsFilename, newSettings);
            }
            MqttBridge = new MqttBridge();
            await MqttBridge.StartAsync();
        }

        private static void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            Console.WriteLine("Unhandled Exception occured:\r\n" + e.ExceptionObject);
        }

        
    }
}
