using System;
using System.Collections.Generic;
using System.Text;

namespace MqttBridge
{
   public class MonitoredItem
    {
        public string DisplayName { get; set; }
        public string Value { get; set; }
        public string NodeId { get; set; }
        public Guid Guid { get; set; }
        public MonitoredItem()
        {

        }
    }
}
