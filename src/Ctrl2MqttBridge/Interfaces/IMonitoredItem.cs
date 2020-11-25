using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ctrl2MqttBridge.Interfaces
{
    public interface IMonitoredItem
    {
        string DisplayName { get; set; }
        string Value { get; set; }
        string NodeId { get; set; }

    }
}
