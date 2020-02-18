using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Siemens.Sinumerik.Operate.Services;


namespace MqttBridge
{
    public class OperateNetService : IClient
    {

        public static event EventHandler<MonitoredItem> NewNotification;

        DataSvc DataSvcReadWrite;
        DataSvc DataSvcSubscribe;
        SortedList<string, MonitoredItem> MonitoredItems;


        public OperateNetService()
        {
            DataSvcReadWrite = new DataSvc();
            DataSvcSubscribe = new DataSvc();
            MonitoredItems = new SortedList<string, MonitoredItem>();
        }

        public async Task<string> Read(string Name)
        {
            string result = await Task.Run(() =>
            {
                Item item = new Item("/" + Name);
                DataSvcReadWrite.Read(item);
                return Functions.GetStringFromDataObject(item.Value);
            });
            return result;
        }
        public async Task<uint> Write(string Name, string Value)
        {
            uint result= await Task.Run(() =>
            {
                DataSvcReadWrite.Write(new Item("/"+Name, Value));
                return (uint)0;
            });
            return result;
        }


        public async Task<uint> Subscribe(string nodeId, int interval)
        {
            uint statuscode = await Task.Run(() =>
            {
                if (MonitoredItems.ContainsKey(nodeId))
                {
                    return (uint)0;
                }
                Item item = new Item("/" + nodeId);
                MonitoredItem monitoredItem = new MonitoredItem()
                {
                    DisplayName = nodeId,
                    NodeId = nodeId,
                    //Value = ReadVariable(nodeId)
                };
                monitoredItem.Guid = DataSvcSubscribe.Subscribe(OnDataChanged, item);
                MonitoredItems.Add(nodeId, monitoredItem);
                return (uint)0;
            });
            return statuscode;
        }

        void OnDataChanged(Guid guid, Item item, DataSvcStatus status)
        {
            foreach (var i in MonitoredItems)
            {
                if (i.Value.Guid == guid)
                    i.Value.Value =Functions.GetStringFromDataObject(item.Value);
                if (!String.IsNullOrEmpty(i.Value.Value))
                    if (NewNotification != null)
                        NewNotification(this, i.Value);
            }
        }
    }
}
