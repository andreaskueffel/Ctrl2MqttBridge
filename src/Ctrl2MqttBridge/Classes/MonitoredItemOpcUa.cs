using Ctrl2MqttBridge.Interfaces;
using Siemens.Sinumerik.Operate.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace Ctrl2MqttBridge.Classes
{
   public class MonitoredItemOpcUa: IMonitoredItem
    {
        public string DisplayName { get; set; }
        public string Value { get; set; }
        public string NodeId { get; set; }

        public int ClientHandle { get; set; }
        public MonitoredItemOpcUa()
        {

        }
    }
}
