using Ctrl2MqttBridge.Interfaces;
using Siemens.Sinumerik.Operate.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace Ctrl2MqttBridge.Classes
{
   public class MonitoredItemSiemens: IMonitoredItem
    {
        public string DisplayName { get; set; }
        public string Value { get; set; }
        public string NodeId { get; set; }
        public Guid Guid { get; set; }
        public DataSvc DataSvc { get; set; }
        public MonitoredItemSiemens()
        {

        }
    }
}
