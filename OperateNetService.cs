using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using MqttBridge.Classes;
using MqttBridge.Interfaces;
using Siemens.Sinumerik.Operate.Services;


namespace MqttBridge
{
    public class OperateNetService : IClient, IDisposable
    {

        public static event EventHandler<IMonitoredItem> NewNotification;

        DataSvc DataSvcReadWrite;

        SortedList<string, MonitoredItemSiemens> MonitoredItems;
        object LockSubscribe = new object();

        public int SubscribedItemsCount
        {
            get { return MonitoredItems.Count; }
        }

        public OperateNetService()
        {
            DataSvcReadWrite = new DataSvc();
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
        public async Task<uint> Unsubscribe(string rawNodeId)
        {
            string nodeId = rawNodeId;

            if (!nodeId.StartsWith("/"))
                nodeId = "/" + nodeId;
            if (nodeId.StartsWith("/Plc/"))
                nodeId = nodeId.Replace("/Plc", "");

            uint statuscode = await Task.Run(() =>
            {

                lock (LockSubscribe)
                {

                    if (MonitoredItems.ContainsKey(nodeId))
                    {
                        MonitoredItems[nodeId].DataSvc.UnSubscribe(OnDataChanged);
                        MonitoredItems[nodeId].DataSvc.Dispose();
                        MonitoredItems.Remove(nodeId);
                        return (uint)0;
                    }
                }

                return (uint)1;
            });
            return statuscode;
        }
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

        #region IDisposable Support
        private bool disposedValue = false; // Dient zur Erkennung redundanter Aufrufe.

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: verwalteten Zustand (verwaltete Objekte) entsorgen.
                    DataSvcReadWrite.Dispose();
                    if (MonitoredItems != null)
                        foreach (var item in MonitoredItems)
                            item.Value.DataSvc.Dispose();
                }

                // TODO: nicht verwaltete Ressourcen (nicht verwaltete Objekte) freigeben und Finalizer weiter unten überschreiben.
                // TODO: große Felder auf Null setzen.

                disposedValue = true;
            }
        }

        // TODO: Finalizer nur überschreiben, wenn Dispose(bool disposing) weiter oben Code für die Freigabe nicht verwalteter Ressourcen enthält.
        // ~OperateNetService()
        // {
        //   // Ändern Sie diesen Code nicht. Fügen Sie Bereinigungscode in Dispose(bool disposing) weiter oben ein.
        //   Dispose(false);
        // }

        // Dieser Code wird hinzugefügt, um das Dispose-Muster richtig zu implementieren.
        public void Dispose()
        {
            // Ändern Sie diesen Code nicht. Fügen Sie Bereinigungscode in Dispose(bool disposing) weiter oben ein.
            Dispose(true);
            // TODO: Auskommentierung der folgenden Zeile aufheben, wenn der Finalizer weiter oben überschrieben wird.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}
