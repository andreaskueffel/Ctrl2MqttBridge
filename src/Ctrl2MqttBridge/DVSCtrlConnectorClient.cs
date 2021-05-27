using Ctrl2MqttBridge.Classes;
using Ctrl2MqttBridge.Interfaces;
using DVS.CtrlConnector.Communications;
using Opc.Ua;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ctrl2MqttBridge
{
    public class DVSCtrlConnectorClient : IClient, IDisposable
    {
        protected readonly static log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static event EventHandler<IMonitoredItem> NewNotification;
        public static event EventHandler<IMonitoredItem> NewAlarmNotification;
        private CommClientBridge myCommClientBridge;
        private CommEvent m_CommEvent;
        AutoResetEvent are_Read = new AutoResetEvent(false);
        string readResult = "";
        List<string> subscribedItems = new List<string>();
        public DVSCtrlConnectorClient(string serverName, int port)
        {
            CommClientBridge.TCPPort = port;
            CommClientBridge.TCPServer = System.Net.IPAddress.Parse(serverName);
            myCommClientBridge = CommClientBridge.GetInstance();
            m_CommEvent = myCommClientBridge.GetCommEventObject();
            m_CommEvent.CommEvent1 += OnCommEvent;
        }

        void OnCommEvent(object sender, CommEventArgs e)
        {
            if ("Ping" == e.KeyString)
                return;

            log.Debug($"CommEvent {e.KeyString}, {e.IntValue}, {e.StrValue}");
            if (e.IntValue == (int)CommItem.CommStates.svr_readDone)
            {
                readResult = e.StrValue;
                are_Read.Set();
            }
            
            if (e.IntValue == (int)CommItem.CommStates.svr_hotRun)
            {
                try
                {
                    if (!subscribedItems.Contains(e.KeyString))
                        subscribedItems.Add(e.KeyString);

                    if (NewNotification != null)
                        NewNotification(null, new MonitoredItemOpcUa()
                        {
                            NodeId = e.KeyString,
                            DisplayName = e.KeyString,
                            Value = e.StrValue
                        });
                }
                catch (Exception exception)
                {
                    log.Error("Exception in CommEvent", exception);
                }
            }
            else if (e.IntValue == (int)CommItem.CommStates.tcpip_connInterr)
            {
                connected = false;
            }
            else if (e.IntValue == (int)CommItem.CommStates.tcpip_connConnected)
            {
                connected = true;
            }
            else if (e.IntValue == 60 || e.IntValue == 61)
            {
                //Write command ack & succ
            }
            else
            {
                log.Info($"CommEvent {e.KeyString}, {e.IntValue}, {e.StrValue}");
            }
        }


        public int SubscribedItemsCount => subscribedItems.Count;
        private bool connected = false;
        public bool IsConnected => connected;
        object lockReading = new object();
        public async Task<string> Read(string nodeId)
        {
            await Task.Run(() =>
            {
                lock (lockReading)
                {
                    readResult = "";
                    myCommClientBridge.SendDataToServer($"{nodeId};1;0;");
                    are_Read.WaitOne(1000);
                }
            });
            return readResult;

        }

        public async Task<uint> Subscribe(string nodeId, int interval)
        {
            
            await Task.Run(()=>myCommClientBridge.SendDataToServer($"{nodeId};80;0;")); //Subscribe
            return 0;
        }

        public async Task<uint> Unsubscribe(string nodeId)
        {
            await Task.Run(() => myCommClientBridge.SendDataToServer($"{nodeId};81;0;")); //Unsubscribe
            await Task.Delay(500);
            if (subscribedItems.Contains(nodeId))
                subscribedItems.Remove(nodeId);
            return 0;
        }

        public async Task<uint> Write(string nodeId, string payload)
        {
            await Task.Run(() => myCommClientBridge.SendDataToServer($"{nodeId};40;{payload.Length};{payload}"));
            return 0;
        }

        public void Dispose()
        {
            myCommClientBridge.Dispose();
        }
    }
}
