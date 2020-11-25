using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ctrl2MqttBridge.Classes
{
    public class Ctrl2MqttBridgeSettings
    {
        public bool OpcUaMode { get; set; } = false;
        public string ServerName { get; set; } = "192.168.214.241";
        public int MqttPort { get; set; } = 51883;
        public int OpcUaPort { get; set; } = 4840;
        public string OpcUaUsername { get; set; } = "Username";
        public string OpcUaPassword { get; set; } = "Password";
        public string ExternalBrokerUrl { get; set; } = "ssl://some.broker.org:8883";
        public bool EnableExternalBroker { get; set; } = false;
        public bool EnableStatus { get; set; } = true;



        public Ctrl2MqttBridgeSettings() { }

    }
}
