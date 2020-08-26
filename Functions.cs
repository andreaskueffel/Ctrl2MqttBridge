using MqttBridge.Classes;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MqttBridge
{
    static class Functions
    {
        public static bool IsSiemens { get { return Directory.Exists(@"C:\ProgramData\Siemens\MotionControl\"); } }
        public static bool IsRexroth { get { return Directory.Exists(@"C:\Program Files (x86)\Rexroth\IndraWorks"); } }


        public static MqttBridgeSettings ReadSettings(string SettingsFilename)
        {
            Console.WriteLine("Getting settings...");
            string settings = "";
            MqttBridgeSettings mqttBridgeSettings = new MqttBridgeSettings();
            if (File.Exists(SettingsFilename))
                settings = File.ReadAllText(SettingsFilename);
            if (!String.IsNullOrEmpty(settings))
                try
                {
                    mqttBridgeSettings = JsonConvert.DeserializeObject<MqttBridgeSettings>(settings);
                    Console.WriteLine("Settings read.");
                }
                catch { }
            return mqttBridgeSettings;

        }
        public static void SaveSettings(string SettingsFilename, MqttBridgeSettings mqttBridgeSettings)
        {
            Console.WriteLine("Getting settings...");
            string settings = "";
            if (File.Exists(SettingsFilename))
                settings = File.ReadAllText(SettingsFilename);

            string newSettings = JsonConvert.SerializeObject(mqttBridgeSettings, Formatting.Indented);
            if (settings != newSettings)
            {
                Console.WriteLine("Settings changed, save to " + SettingsFilename);
                File.WriteAllText(SettingsFilename, newSettings);
            }
        }

        public static string GetStringFromDataObject(object item)
        {
            try
            {
                //Welcher Typ kommt da wohl zurück??
                if (item.GetType() == typeof(string))
                    return (string)item;
                if (item.GetType() == typeof(int))
                    return ((int)item).ToString();
                if (item.GetType() == typeof(uint))
                    return ((uint)item).ToString();
                if (item.GetType() == typeof(double))
                    return ((double)item).ToString().Replace(",", ".");
                if (item.GetType() == typeof(float))
                    return ((float)item).ToString().Replace(",", ".");
                if (item.GetType() == typeof(object))
                    return "";

            }
            catch (Exception exc)
            {
                return exc.ToString();
            }
            return item.GetType().ToString();
        }
    }
}
