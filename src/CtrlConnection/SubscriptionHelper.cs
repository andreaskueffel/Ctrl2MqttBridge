using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ctrl2MqttBridge
{
    public class SubscriptionHelper
    {
        public static event EventHandler<MonitoredItem> SubscribeEvent;
        public SortedList<string, MonitoredItem> MonitoredItems { get; set; }
        public event EventHandler<MonitoredItem> DataChanged;
        public void UpdateValue(string nodeId, string value)
        {
            if (MonitoredItems.ContainsKey(nodeId))
            {
                if (MonitoredItems[nodeId].Value != value)
                {
                    MonitoredItems[nodeId].Value = value;
                    if (DataChanged != null)
                        DataChanged(this, MonitoredItems[nodeId]);
                }
            }
        }
        public SubscriptionHelper()
        {
            MonitoredItems = new SortedList<string, MonitoredItem>();
        }
    }
}
