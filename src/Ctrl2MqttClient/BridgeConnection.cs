using Ctrl2MqttClient;
using MQTTnet;
using MQTTnet.Client.Options;
using MQTTnet.Extensions.ManagedClient;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Security;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ctrl2MqttBridge
{
    public class BridgeConnection : IDisposable
    {
        //Stub class to integrate common Code for client side

        public static string MqttPrefix { get; set; } = "ctrl2mqttbridge/";
        public static string MqttClientUsername { get; set; } = "Ctrl2MqttBridge";
        public static string MqttClientPassword { get; set; } = "Ctrl2MqttBridge";

        public static IManagedMqttClient mqttClient = null;
        static List<SubscriptionHelper> subscriptionHelpers = null;
        static object subscriptionHelpersLock = null;
        public static event EventHandler<bool> ConnectionHandler;
        static ManualResetEvent ReadItemResetEvent = null;
        static ManualResetEvent WriteItemResetEvent = null;
        static bool connectionRoundTrip = false;
        bool ConnectionRoundTrip
        {
            get => connectionRoundTrip;
            set
            {
                if (connectionRoundTrip != value)
                {
                    connectionRoundTrip = value;
                    OnConnectionHandler(connectionRoundTrip);
                }
            }
        }
        public static bool IsConnected
        {

            get
            {
                //if (_instance == null) return false;
                if (mqttClient == null) return false;
                else return mqttClient.IsConnected;
            }

        }

        

        public BridgeConnection()
        {
            if (subscriptionHelpers == null)
                subscriptionHelpers = new List<SubscriptionHelper>();
            if (ReadItemResetEvent == null)
                ReadItemResetEvent = new ManualResetEvent(false);
            if (WriteItemResetEvent == null)
                WriteItemResetEvent = new ManualResetEvent(false);
            if (subscriptionHelpersLock == null)
                subscriptionHelpersLock = new object();
        }
        /// <summary>
        /// Connects synchronous to the Bridge - this is not recommended!! Use ConnectAsync instead
        /// </summary>
        /// <param name="mqttIp"></param>
        /// <param name="mqttPort"></param>
        /// <param name="clientID"></param>
        public void ConnectSync(string mqttIp, int mqttPort, string clientID)
        {
            ConnectAsync(mqttIp, mqttPort, clientID).Wait();
        }
        public Task ConnectAsync(string mqttIp, int mqttPort, string clientID)
        {
            return Task.Run(async () => { 
                await InitializeMqttClient(mqttIp, mqttPort, clientID);
                _ = await CheckMqttBridgeRoundtrip();
                if (!ConnectionRoundTrip)
                {
                    try
                    {
                        Disconnect();
                        MqttPrefix = BridgeSettingsHelper.GetBridgeTopic();
                        MqttClientUsername = BridgeSettingsHelper.GetBridgeCredentials().Username;
                        MqttClientPassword = BridgeSettingsHelper.GetBridgeCredentials().Password;

                        await InitializeMqttClient(mqttIp, mqttPort, clientID);
                        _ = await CheckMqttBridgeRoundtrip(); //connectionRoundTrip = true;
                    }
                    catch(Exception e)
                    {
                        ConnectionRoundTrip = false;
                    }
                }
                if (!ConnectionRoundTrip)
                {
                    Disconnect();
                    MqttPrefix = "mqttbridge/";
                    MqttClientUsername = MqttClientPassword = "HoningHMI";

                    await InitializeMqttClient(mqttIp, mqttPort, clientID);
                    _ = await CheckMqttBridgeRoundtrip(); //connectionRoundTrip = true;
                }
            });
        }
        
        public void Disconnect()
        {
            if (mqttClient != null)
            {
                //mqttClient.StopAsync().Wait();
                mqttClient.Dispose();
                mqttClient = null;
            }
        }

        public void AddMonitoredOPCItem(string nodeId, SubscriptionHelper subsc)
        {
            if (!nodeId.StartsWith("/"))
                nodeId = "/" + nodeId;
            lock (subscriptionHelpersLock)
            {
                if (!subscriptionHelpers.Contains(subsc))
                {
                    subscriptionHelpers.Add(subsc);
                }
            }
            if (!subsc.MonitoredItems.ContainsKey(nodeId))
                subsc.MonitoredItems.Add(nodeId, new MonitoredItem() { NodeId = nodeId, DisplayName = nodeId.ToLower() });
            _ = SendToMQTT(MqttPrefix + "subscribe" + nodeId, "100", 1);
        }

        public string ReadNode(string NodeName)
        {
            if (!IsConnected)
                return null;
            if (!NodeName.StartsWith("/"))
                NodeName = "/" + NodeName;
            lock (readLock)
            {
                MonitoredItemRead = new MonitoredItem()
                {
                    NodeId = NodeName,
                    DisplayName = NodeName.ToLower(),
                    Value = null
                };
                ReadItemResetEvent.Reset();
                _ = SendToMQTT(MqttPrefix + "read" + NodeName, "");
                ReadItemResetEvent.WaitOne(1000);
                string ret_val = null;
                if (MonitoredItemRead.NodeId == NodeName)
                    ret_val = MonitoredItemRead.Value;


                MonitoredItemRead = null;
                return ret_val;

            }
        }
        public string ReadR(RList paramnumber, int channel)
        {
            return ReadR((uint)paramnumber, channel);
        }
        public string ReadR(uint paramnumber, int channel)
        {
            return ReadNode("/channel/parameter/r[u" + channel + "," + paramnumber + "]");
        }
        public string ReadGUD5(string GUDName)
        {
            return ReadNode("/NC/_N_NC_GD5_ACX/" + GUDName);
        }
        public string ReadGUD2(string GUDName)
        {
            return ReadNode("/NC/_N_NC_GD2_ACX/" + GUDName);
        }

        public string ReadAxis(Axis axis, string channel)
        {
            string ncChannel = "[u" + channel + ",";
            return ReadNode("=/Channel/GeometricAxis/actProgPos" + ncChannel + ((int)axis).ToString() + "]");
        }

        public void WriteR(RList paramnumber, int channel, string value)
        {
            WriteR((uint)paramnumber, channel, value);
        }
        private void WriteR(uint paramnumber,int channel, string value)
        {
            WriteNode("/channel/parameter/r[u" + channel + "," + paramnumber + "]", value);
            
        }
        public void WritePLC(string adress, string value)
        {
            WriteNode("/Plc/" + adress, value);
        }
        public void WriteNode(string nodeId, string value)
        {
            if (!nodeId.StartsWith("/"))
                nodeId = "/" + nodeId;
            _ = SendToMQTT(MqttPrefix + "write" + nodeId, value);
        }
        public bool WriteNodeSync(string nodeId, string value)
        {
            if (!nodeId.StartsWith("/"))
                nodeId = "/" + nodeId;

            lock (writeLock)
            {
                MonitoredItemWrite = new MonitoredItem()
                {
                    NodeId = nodeId,
                    DisplayName = nodeId.ToLower(),
                    Value = null
                };
                WriteItemResetEvent.Reset();
                _ = SendToMQTT(MqttPrefix + "write" + nodeId, value);
                WriteItemResetEvent.WaitOne(1000);

                bool success = false;
                if (MonitoredItemWrite.NodeId == nodeId)
                    if (MonitoredItemWrite.Value != null)
                        if (MonitoredItemWrite.Value == "0")
                            success = true;
                MonitoredItemWrite = null;
                return success;

            }
        }
        public static string RName(RList rparameter, string channel)
        {
            return "/channel/parameter/r[u" + channel + "," + ((uint)rparameter) + "]".ToString();
        }
        public string GetRName(RList rparameter, string channel)
        {
            return BridgeConnection.RName(rparameter, channel);
        }

        #region MQTT Client
        //private static Storage Storage = null;

        private async Task InitializeMqttClient(string server, int port, string clientID)
        {
            mqttClient = new MqttFactory().CreateManagedMqttClient();
            //Storage = new Storage(mqttClient);
            var tlsoptions = new MqttClientOptionsBuilderTlsParameters();
            //tlsoptions.CertificateValidationCallback = new Func<X509Certificate, X509Chain, SslPolicyErrors, IMqttClientOptions, bool>(ValidateServerCert);
            tlsoptions.CertificateValidationHandler = new Func<MqttClientCertificateValidationCallbackContext, bool>(ValidateServerCert);

            tlsoptions.UseTls = true;


            // Setup and start a managed MQTT client.
            var options = new ManagedMqttClientOptionsBuilder()
                .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
                //.WithStorage(Storage)
                .WithClientOptions(new MqttClientOptionsBuilder()
                    .WithClientId(clientID + new Random().Next(10000, 10000000).ToString())
                    .WithTcpServer(server, port)
                    //.WithTls(tlsoptions)
                    .WithCredentials(MqttClientUsername, MqttClientPassword)
                    .WithWillMessage(new MqttApplicationMessage()
                    {
                        Topic = MqttPrefix + clientID,
                        Retain = true,
                        Payload = Encoding.UTF8.GetBytes("DROPPED"),
                        QualityOfServiceLevel = MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce
                    })
                    .Build())
                .Build();

            mqttClient.ConnectingFailedHandler = new ConnectingFailedHandler();

            await mqttClient.SubscribeAsync(MqttPrefix + "subscriptionnotification/#");
            await mqttClient.SubscribeAsync(MqttPrefix + "readresult/#");
            await mqttClient.SubscribeAsync(MqttPrefix + "writeresult/#");

            mqttClient.UseApplicationMessageReceivedHandler(async e =>
            {
                await Task.Run(() => {
                    if (e.ApplicationMessage.Topic.StartsWith(MqttPrefix + "subscriptionnotification/"))
                    {
                        string nodeId = e.ApplicationMessage.Topic.Replace(MqttPrefix + "subscriptionnotification/", "/");
                        string payload = null;
                        if (e.ApplicationMessage.Payload != null)
                            payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                        foreach (var subsc in subscriptionHelpers)
                        {
                            Task.Run(() => subsc.UpdateValue(nodeId, payload));
                        }
                    }
                    if (e.ApplicationMessage.Topic.StartsWith(MqttPrefix + "readresult/"))
                    {
                        string nodeId = e.ApplicationMessage.Topic.Replace(MqttPrefix + "readresult/", "/");
                        string payload = null;
                        //System.Diagnostics.Trace.WriteLine(e.ApplicationMessage.Topic + "=" + e.ApplicationMessage.Payload.ToString());
                        if (e.ApplicationMessage.Payload != null)
                            payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                        Task.Run(() => OnReadResult(nodeId, payload));

                    }
                    if (e.ApplicationMessage.Topic.StartsWith(MqttPrefix + "writeresult/"))
                    {
                        string nodeId = e.ApplicationMessage.Topic.Replace(MqttPrefix + "writeresult/", "/");
                        string payload = null;
                        //System.Diagnostics.Trace.WriteLine(e.ApplicationMessage.Topic + "=" + e.ApplicationMessage.Payload.ToString());
                        if (e.ApplicationMessage.Payload != null)
                            payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                        Task.Run(() => OnWriteResult(nodeId, payload));

                    }
                });
            });
            mqttClient.UseConnectedHandler(async e =>
            {
                await SendConnectionState("CONNECTED", clientID);
                //await Task.Run(async () => await Task.Run(() => OnConnectionHandler(true)));
                //Moved to ConnectionRoundTrip
                _ = await CheckMqttBridgeRoundtrip();
                System.Diagnostics.Trace.WriteLine("MQTT Connected");
            });
            mqttClient.UseDisconnectedHandler(async e =>
            {
                await SendConnectionState("DISCONNECTED", clientID);
                //await Task.Run(async () => await Task.Run(() => OnConnectionHandler(false)));
                ConnectionRoundTrip = false;
                subscriptionHelpers.Clear();
                System.Diagnostics.Trace.WriteLine("MQTT Disconnected");
            });
            await mqttClient.StartAsync(options);
            
        }
        
        private async Task<bool> CheckMqttBridgeRoundtrip(int connectedTimeout=10000)
        {
            int timeoutcount = 0;
            //Check backwards compatibility with MqttBridge
            while (!IsConnected &&timeoutcount<=connectedTimeout)
            {
                await Task.Delay(200);
                timeoutcount += 200;
            }
            if (!IsConnected)
            {
                ConnectionRoundTrip = true; //When we do not get any connection do not try the fallbacks at all
                return false;
            }
            await SendToMQTT(MqttPrefix + "write" + "/nonsensetocheckbridgeconnectivity", "roundtriptest");
            int timeout = 10;
            while (!ConnectionRoundTrip && timeout>0)
            {
                await Task.Delay(100);
                timeout--;
                Trace.WriteLine("Checking roundtrip to Bridge..." + timeout);
            }
            return ConnectionRoundTrip;
        }
        private void OnConnectionHandler(bool v)
        {
            if (ConnectionHandler != null)
                ConnectionHandler(this, v);
        }

        static MonitoredItem MonitoredItemRead = null;
        static MonitoredItem MonitoredItemWrite = null;
        static object readLock = new object();
        static object writeLock = new object();
        private void OnReadResult(string nodeId, string payload)
        {
            if (MonitoredItemRead != null && MonitoredItemRead.NodeId.ToLower() == nodeId.ToLower())
            {
                MonitoredItemRead = new MonitoredItem()
                {
                    NodeId = nodeId,
                    Value = payload,
                    DisplayName = nodeId.ToLower()
                };
                //System.Diagnostics.Trace.WriteLine("ReadResult for NodeID " + nodeId);
                ReadItemResetEvent.Set();
            }
        }
        private void OnWriteResult(string nodeId, string payload)
        {
            ConnectionRoundTrip = true;
            if (MonitoredItemWrite != null && MonitoredItemWrite.NodeId.ToLower() == nodeId.ToLower())
            {
                MonitoredItemWrite = new MonitoredItem()
                {
                    NodeId = nodeId,
                    Value = payload,
                    DisplayName = nodeId.ToLower()
                };
                //System.Diagnostics.Trace.WriteLine("WriteResult for NodeID " + nodeId);
                WriteItemResetEvent.Set();
            }
        }

        private static async Task SendConnectionState(string payload, string clientID)
        {
            await mqttClient.PublishAsync(new ManagedMqttApplicationMessage()
            {
                ApplicationMessage = new MqttApplicationMessage()
                {
                    Topic = MqttPrefix + clientID,
                    Retain = true,
                    Payload = Encoding.UTF8.GetBytes(payload),
                    QualityOfServiceLevel = MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce
                }
            });
        }

        private class Creds : IMqttClientCredentials
        {
            public string Username => throw new NotImplementedException();

            public byte[] Password => throw new NotImplementedException();
        }

        private static async Task SendToMQTT(string topic, string value, int qos = 0)
        {
            if (mqttClient != null)
                await mqttClient.PublishAsync(
                        new ManagedMqttApplicationMessage()
                        {
                            ApplicationMessage = new MqttApplicationMessage()
                            {
                                Topic = topic,
                                Payload = Encoding.UTF8.GetBytes(value),
                                QualityOfServiceLevel = (MQTTnet.Protocol.MqttQualityOfServiceLevel)qos
                            }
                        });
        }

        private static bool ValidateServerCert(MqttClientCertificateValidationCallbackContext mqttClientCertificateValidationCallbackContext)
        {
                return true;
                //return mqttClientCertificateValidationCallbackContext.SslPolicyErrors == SslPolicyErrors.None;
        }

        private class ConnectingFailedHandler : IConnectingFailedHandler
        {
            public Task HandleConnectingFailedAsync(ManagedProcessFailedEventArgs eventArgs)
            {
                return Task.Run(new Action(() =>
                {
                    System.Diagnostics.Trace.WriteLine(eventArgs.Exception.ToString(), "DataInterface.ConnectingFailedHander");
                }));
            }
            public ConnectingFailedHandler()
            {

            }
        }



        #endregion
        #region IDisposable Support
        private bool disposedValue = false; // Dient zur Erkennung redundanter Aufrufe.

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: verwalteten Zustand (verwaltete Objekte) entsorgen.
                    //mqttClient.Dispose();
                }

                // TODO: nicht verwaltete Ressourcen (nicht verwaltete Objekte) freigeben und Finalizer weiter unten überschreiben.
                // TODO: große Felder auf Null setzen.

                disposedValue = true;
            }
        }

        // TODO: Finalizer nur überschreiben, wenn Dispose(bool disposing) weiter oben Code für die Freigabe nicht verwalteter Ressourcen enthält.
        // ~CtrlConnection()
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
