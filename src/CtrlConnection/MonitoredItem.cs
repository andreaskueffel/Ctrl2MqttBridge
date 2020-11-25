using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ctrl2MqttBridge
{
    public class MonitoredItem
    {
        public string DisplayName { get; set; }
        public string NodeId { get; set; }
        public string Value { get; set; }

    }
}
