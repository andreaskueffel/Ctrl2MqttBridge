using Ctrl2MqttBridge;
using Ctrl2MqttClient.Classes;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ctrl2MqttClient
{
    public class BridgeSettingsHelper
    {
        public static string GetBridgeTopic()
        {
            const string BridgeTopicProperty = "BridgeTopic";
            const string DefaultValue = "mqttbridge/";
            return GetJsonProperty(BridgeTopicProperty, DefaultValue);
        }
        public static BridgeCredentials GetBridgeCredentials()
        {
            BridgeCredentials result=null;
            const string BridgeTopicProperty = "BridgeCredentials";
            const string DefaultValue = "Ctrl2MqttBridge:Ctrl2MqttBridge";
            string credentialsString = GetJsonProperty(BridgeTopicProperty, DefaultValue);
            if (null != credentialsString)
            {
                if (credentialsString.Contains(":"))
                {
                    result = new BridgeCredentials(
                        username: credentialsString.Split(':')[0],
                        password: credentialsString.Split(':')[1]);
                }
            }
            if (null == result)
            {
                result = new BridgeCredentials(
                    username: DefaultValue.Split(':')[0],
                    password: DefaultValue.Split(':')[1]);
            }
            return result;
        }


        static string GetJsonProperty(string propertyName, string defaultValue)
        {
            string result = null;
            JObject jsonObject = JObject.Parse(ReadSettingsFile());
            JToken propertyToken;
            if (jsonObject.TryGetValue(propertyName, out propertyToken))
                result = propertyToken.Value<string>();

            if (String.IsNullOrWhiteSpace(result))
                return defaultValue;
            else
                return result;

        }

        static string ReadSettingsFile()
        {
            if (File.Exists(GetSettingsFilename()))
                return File.ReadAllText(GetSettingsFilename());
            else
                return "";

        }

        static string GetSettingsFilename()
        {
            const string defaultFileName= @"C:\Ctrl2MqttBridge\MqttBridgeSettings.json";
            if (File.Exists(defaultFileName))
                return defaultFileName;
            string defaultLocation = @"C:\Ctrl2MqttBridge";
            string legacyLocation = @"C:\MqttBridge";
            if (Directory.Exists(defaultLocation))
                return new DirectoryInfo(defaultLocation).GetFiles("*BridgeSettings.json").FirstOrDefault().FullName;
            else if (Directory.Exists(legacyLocation))
                return new DirectoryInfo(legacyLocation).GetFiles("*BridgeSettings.json").FirstOrDefault().FullName;
            return "";
        }
    }
}
