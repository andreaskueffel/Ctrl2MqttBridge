using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ctrl2MqttBridge
{
    public class SubscriptionHelper
    {
        private object monitoredItemsLock = new object();
        private SortedList<string, MonitoredItem> MonitoredItems { get; set; }
        public event EventHandler<MonitoredItem> DataChanged;
        public void UpdateValue(string nodeId, string value)
        {
            lock (monitoredItemsLock)
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
        }
        public SubscriptionHelper()
        {
            MonitoredItems = new SortedList<string, MonitoredItem>();
        }

        internal void AddIfNotContained(string nodeId, MonitoredItem monitoredItem)
        {
            lock (monitoredItemsLock)
            {
                if (!MonitoredItems.ContainsKey(nodeId))
                    MonitoredItems.Add(nodeId, monitoredItem);
            }
        }
    }
}
