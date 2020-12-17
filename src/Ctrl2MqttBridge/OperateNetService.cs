using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ctrl2MqttBridge.Classes;
using Ctrl2MqttBridge.Interfaces;
using Newtonsoft.Json;
using Siemens.Sinumerik.Operate.Services;


namespace Ctrl2MqttBridge
{
    public class OperateNetService : IClient, IDisposable
    {

        public static event EventHandler<IMonitoredItem> NewNotification;
        public static event EventHandler<IMonitoredItem> NewAlarmNotification;

        DataSvc DataSvcReadWrite;
        AlarmSvc AlarmService;
        Guid AlarmServiceListGuid;
        Guid AlarmServiceEventsGuid;


        Alarm[] Alarms;
        SortedList<string, MonitoredItemSiemens> MonitoredItems;
        object LockSubscribe = new object();

        public int SubscribedItemsCount
        {
            get { return MonitoredItems.Count; }
        }
        public bool IsConnected
        {
            get
            {
                return DataSvcReadWrite != null;
            }
        }


        public OperateNetService()
        {
            MonitoredItems = new SortedList<string, MonitoredItemSiemens>();
            DataSvcReadWrite = new DataSvc();
            AlarmService = new AlarmSvc("deu"); //Wir abonnieren erstmal Deutsch
            AlarmServiceListGuid = AlarmService.Subscribe(AlarmListCallback);
            AlarmServiceEventsGuid = AlarmService.SubscribeEvents(AlarmEventsCallback);
            
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
                        OnNewNotification(new KeyValuePair<string, MonitoredItemSiemens>(nodeId, MonitoredItems[nodeId]));
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
                    if(fireEvent)
                        OnNewNotification(i);
                }
            }
        }

        private void AlarmEventsCallback(Guid guid, Alarm[] events)
        {
            if (AlarmServiceEventsGuid.Equals(guid))
            {
                foreach(var alarmevent in events)
                {
                    OnNewAlarmNotification("alarmEvents", JsonConvert.SerializeObject(alarmevent));
                }
            }
        }

        private void AlarmListCallback(Guid guid, Alarm[] alarms)
        {
            if (AlarmServiceListGuid.Equals(guid))
            {
                Alarms = alarms;
                Alarm newestAlarm = new Alarm(new DateTime(1, 1, 1), "none") { Id = 0 };
                Alarm oldestAlarm = new Alarm(new DateTime(2100, 1, 1), "none") { Id = 0 };
                foreach (var alarm in alarms)
                {
                    if (alarm.TimeStamp > (new DateTime(2000, 1, 1)) && alarm.TimeStamp < oldestAlarm.TimeStamp)
                        oldestAlarm = alarm;
                    if(alarm.TimeStamp> newestAlarm.TimeStamp)
                        newestAlarm = alarm;
                }
                if (oldestAlarm.Message == "none") //If we did not get one with valid timestamp use any of the ones without
                {
                    foreach (var alarm in alarms)
                        if (alarm.TimeStamp < oldestAlarm.TimeStamp)
                            oldestAlarm = alarm;
                }
                OnNewAlarmNotification("activeAlarmList", JsonConvert.SerializeObject(alarms));
                OnNewAlarmNotification("activeAlarmId", newestAlarm.Id.ToString());
                OnNewAlarmNotification("activeAlarmDetails", JsonConvert.SerializeObject(newestAlarm));
                OnNewAlarmNotification("catchedAlarmId", oldestAlarm.Id.ToString());
                OnNewAlarmNotification("catchedAlarmDetails", JsonConvert.SerializeObject(oldestAlarm));
            }
        }

        private void OnNewAlarmNotification(string topic, string message)
        {
            if (NewAlarmNotification != null)
                NewAlarmNotification(this, new MonitoredItemSiemens()
                {
                    DisplayName = topic,
                    Value=message
                });
        }

        private void OnNewNotification(KeyValuePair<string, MonitoredItemSiemens> i)
        {
            if (!String.IsNullOrEmpty(i.Value.Value))
                if (NewNotification != null)
                    NewNotification(this, i.Value);
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
