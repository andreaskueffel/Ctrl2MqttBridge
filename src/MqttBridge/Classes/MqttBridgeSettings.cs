using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MqttBridge.Classes
{
    public class MqttBridgeSettings
    {
        public bool OpcUaMode { get; set; } = false;
        public string ServerName { get; set; } = "192.168.214.241";
        public int MqttPort { get; set; } = 51883;
        public int OpcUaPort { get; set; } = 4840;
        public string OpcUaUsername { get; set; } = "HoningHMI";
        public string OpcUaPassword { get; set; } = "HoningHMI";
        public string ExternalBrokerUrl { get; set; } = "ssl://ekon.praewema.de:8883";
        public bool EnableExternalBroker { get; set; } = false;
        public bool EnableStatus { get; set; } = true;



        public MqttBridgeSettings() { }

    }
}
