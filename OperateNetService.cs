using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using MqttBridge.Classes;
using MqttBridge.Interfaces;
using Siemens.Sinumerik.Operate.Services;


namespace MqttBridge
{
    public class OperateNetService : IClient
    {

        public static event EventHandler<IMonitoredItem> NewNotification;

        DataSvc DataSvcReadWrite;
        DataSvc DataSvcSubscribe;
        SortedList<string, MonitoredItemSiemens> MonitoredItems;
        object LockSubscribe = new object();
        bool MonitoredItemsLocked = false;

        public OperateNetService()
        {
            DataSvcReadWrite = new DataSvc();
            DataSvcSubscribe = new DataSvc();
            MonitoredItems = new SortedList<string, MonitoredItemSiemens>();
        }

        public async Task<string> Read(string Name)
        {
            if (!Name.StartsWith("/"))
                Name = "/" + Name;
            if (Name.StartsWith("/Plc/"))
                    Name = Name.Replace("/Plc", "");


            string result = await Task.Run(() =>
            {
                Item item = new Item(Name);
                try
                {
                    DataSvcReadWrite.Read(item);
                    return Functions.GetStringFromDataObject(item.Value);
                }
                catch (DataSvcException)
                {
                    return "";
                }
            });
            return result;
        }
        public async Task<uint> Write(string Name, string Value)
        {
            if (!Name.StartsWith("/"))
                Name = "/" + Name;
            if (Name.StartsWith("/Plc/"))
                Name = Name.Replace("/Plc/", "");

            uint result = await Task.Run(() =>
            {
                try
                {
                    DataSvcReadWrite.Write(new Item(Name, Value));
                return (uint)0;
                }
                catch (DataSvcException exc)
                {
                    return (uint)exc.ErrorNumber;
                }
            });
            return result;
        }


        public async Task<uint> Subscribe(string rawNodeId, int interval)
        {
            string nodeId = rawNodeId;

            if (!nodeId.StartsWith("/"))
                nodeId = "/" + nodeId;
            if (nodeId.StartsWith("/Plc/"))
                nodeId = nodeId.Replace("/Plc", "");

            uint statuscode = await Task.Run(() =>
            {
               
                lock(LockSubscribe)
                {
                 
                    if (MonitoredItems.ContainsKey(nodeId))
                    {
                        return (uint)1;
                    }
                    try
                    {
                        DataSvc dataSvc = new DataSvc();
                        //Monitored Items müssen immer neu angelegt werden:
                        Item item = new Item(nodeId);
                        MonitoredItemSiemens monitoredItem = new MonitoredItemSiemens()
                        {
                            DisplayName = rawNodeId,
                            NodeId = rawNodeId,
                            DataSvc = dataSvc,
                            Guid = dataSvc.Subscribe(OnDataChanged, item)

                            //Value = ReadVariable(nodeId)
                        };
                        MonitoredItems.Add(nodeId, monitoredItem);
                    }
                    catch (DataSvcException exc)
                    {
                        return (uint)exc.ErrorNumber;
                    }
                }
               
                return (uint)0;
            });
            return statuscode;
        }
        object DataChangedLock = new object();
        void OnDataChanged(Guid guid, Item item, DataSvcStatus status)
        {
            lock (LockSubscribe)
            {
                foreach (var i in MonitoredItems)
                {
                    bool fireEvent = false;
                    if (i.Value.Guid == guid)
                    {
                        string newValue = Functions.GetStringFromDataObject(item.Value);
                        if (i.Value.Value != newValue)
                        {
                            i.Value.Value = newValue;
                            fireEvent = true;
                        }
                    }
                    if (!String.IsNullOrEmpty(i.Value.Value) && fireEvent)
                        if (NewNotification != null)
                            NewNotification(this, i.Value);
                }
            }
        }
    }
}
