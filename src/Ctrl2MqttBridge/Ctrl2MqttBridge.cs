﻿using Ctrl2MqttBridge.Classes;
using Ctrl2MqttBridge.Interfaces;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using MQTTnet.Client.Receiving;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Server;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ctrl2MqttBridge
{
    public class Ctrl2MqttBridge
    {
        static string mqttPrefix;
        IManagedMqttClient mqttClient;
        IMqttServer mqttServer;
        IManagedMqttClient mqttClientExternal;
        DateTime StartTime;
        IClient Client;
        OperateNetService operateNetService;
        OpcUaConsoleClient opcUaConsoleClient;
        DVSCtrlConnectorClient dvsCtrlConnectorClient;
        SinumerikSdkClient sinumerikClient;

        string MachineName;
        //Thread MqttServerThread;
        //Thread MqttClientThread;
        //Thread ClientThread;
        Dictionary<string, bool> ClientRights;
        string ClientId;

        public Ctrl2MqttBridge()
        {
            StartTime = DateTime.Now;
            MachineName = Environment.MachineName;
            ClientRights = new Dictionary<string, bool>();
            ClientId = "praekon_ctrl2mqttBridge_" + StartTime.ToString("yyyyMMddHHmmss");
            string bridgeTopic = !String.IsNullOrWhiteSpace(Program.Ctrl2MqttBridgeSettings.BridgeTopic) ? Program.Ctrl2MqttBridgeSettings.BridgeTopic : "ctrl2mqttbridge/";
            if (!bridgeTopic.EndsWith("/"))
                bridgeTopic += "/";
            mqttPrefix = bridgeTopic;

            //MqttServerThread = new Thread(new ThreadStart(()=>initMqttServer()));
        }

        string GetUsername()
        {
            try
            {
                return Program.Ctrl2MqttBridgeSettings.BridgeCredentials.Split(':')[0];
            }
            catch (Exception e) { }
            return "Ctrl2MqttBridge";
        }
        string GetPassword()
        {
            try
            {
                return Program.Ctrl2MqttBridgeSettings.BridgeCredentials.Split(':')[1];
            }
            catch (Exception e) { }
            return "Ctrl2MqttBridge";
        }
        volatile bool operateConnectInProgress = true;

        public async Task StartAsync()
        {
            if (!Program.Ctrl2MqttBridgeSettings.OpcUaMode && !Program.Ctrl2MqttBridgeSettings.DVSCtrlConnectorMode && !Program.Ctrl2MqttBridgeSettings.SinumerikSDKMode)
            {
                while (operateConnectInProgress)
                {
                    try
                    {
                        Console.WriteLine("Initialize OperateNET Service");
                        await initOperateNetService();
                        Client = (IClient)operateNetService;
                        operateConnectInProgress = false;
                        break;
                    }
                    catch (TypeInitializationException exc)
                    {
                        //We are not within Operate
                        Program.Ctrl2MqttBridgeSettings.SinumerikSDKMode = true;
                        Console.WriteLine("Initialize OperateNET Service failed, seems we do not run within Operate. Fallback to SinumerikSDK");
                        break;
                    }
                    catch (NotImplementedException exc)
                    {
                        //We do not have the Siemens binaries
                        Program.Ctrl2MqttBridgeSettings.SinumerikSDKMode = true;
                        Console.WriteLine("Initialize OperateNET Service failed, seems we do not have the Siemens OEM dlls. Fallback to SinumerikSDK");
                        break;
                    }
                    catch (Exception exc) 
                    { 
                        Console.WriteLine($"exception caught: {exc.Message}, {exc.ToString()}"); await Task.Delay(1000); 
                    }
                }
            }

            if(Program.Ctrl2MqttBridgeSettings.SinumerikSDKMode)
            {
                try {
                    Console.WriteLine("Initialize Sinumerik SDK Client");
                    await initSinumerikClient();
                    Client = (IClient)sinumerikClient;
                }
                catch(Exception e)
                {
                    Console.WriteLine("Exception caught when initializing SinumerikSDKClient");
                }
            }

            if (Program.Ctrl2MqttBridgeSettings.DVSCtrlConnectorMode)
            {
                try
                {
                    Console.WriteLine("Initialize DVSCtrlConnection Service");
                    await initDVSClient();
                    Client = (IClient)dvsCtrlConnectorClient;
                }
                catch (Exception e) { System.Diagnostics.Trace.WriteLine(e.ToString()); }
            }

            if (Program.Ctrl2MqttBridgeSettings.OpcUaMode)
            {
                try
                {
                    Console.WriteLine("Initialize OPC UA Service");
                    await initOPCUAClient();
                    Client = (IClient)opcUaConsoleClient;

                }
                catch (Exception e) { System.Diagnostics.Trace.WriteLine(e.ToString()); }
            }

            if (null == Client)
                Console.WriteLine("client is null -> ???");

            await Task.Run(async () => await initMqttServer());
            await Task.Run(async () => await initMqttClient());
            if (Program.Ctrl2MqttBridgeSettings.EnableExternalBroker)
            {
                await Task.Run(async () => await initMqttClientExternal());
            }
            if (Program.Ctrl2MqttBridgeSettings.EnableStatus)
            {
                Timer t = new Timer(async (e) =>
                {

                    string bridgeStatusJson = JsonConvert.SerializeObject(await GetBridgeStatus());
                    await mqttClient.PublishAsync(new MqttApplicationMessage() { Topic = mqttPrefix + "bridgeStatus", Payload = Encoding.UTF8.GetBytes(bridgeStatusJson) });
                    if (mqttClientExternal != null && mqttClientExternal.IsConnected)
                        await mqttClientExternal.PublishAsync(new MqttApplicationMessage() { Topic = mqttPrefix + "bridgeStatus", Payload = Encoding.UTF8.GetBytes(bridgeStatusJson) });
                });
                t.Change(1000, 10000);
            }

            System.Diagnostics.Trace.WriteLine("Started in " + (Program.Ctrl2MqttBridgeSettings.OpcUaMode ? "OPCUA" : "SIEMENSDLL") + "Mode", "MAIN");

        }

        static double lastCPUMillis = 0;
        static double lastUptimeMillis = 0;
        Task<BridgeStatus> GetBridgeStatus()
        {
            return Task.Run(async () =>
            {
                TimeSpan upTime = (DateTime.Now - StartTime);
                string upTimeString = "P" + Math.Floor(upTime.TotalDays).ToString("0") + "DT" + upTime.Hours + "H" + upTime.Minutes + "M" + upTime.Seconds + "S";
                string serverTimeString = DateTime.Now.ToString("o");
                double CPUMillis = System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime.TotalMilliseconds;

                double deltaCPUMillis = CPUMillis - lastCPUMillis;
                double deltaUptimeMillis = upTime.TotalMilliseconds - lastUptimeMillis;
                lastCPUMillis = CPUMillis;
                lastUptimeMillis = upTime.TotalMilliseconds;
                double cpu = Math.Round(100.0 * (deltaCPUMillis / deltaUptimeMillis), 1);
                string ram = (System.Diagnostics.Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024) + " MiB";

                return new BridgeStatus()
                {
                    Ctrl2MqttBridgeVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString(),
                    ClientCount = ((await mqttServer.GetClientStatusAsync()).Count) - 1,
                    OperationMode = Client != null ? (Program.Ctrl2MqttBridgeSettings.SinumerikSDKMode ? "SinumerikSDK" : Program.Ctrl2MqttBridgeSettings.DVSCtrlConnectorMode ? "DVS CtrlConnector" : Program.Ctrl2MqttBridgeSettings.OpcUaMode ? "OPCUA" : "OperateNetService") : "NO_CTRL_CONNECTION",
                    ServerName = MachineName,
                    SubcribedItemsCount = Client != null ? Client.SubscribedItemsCount : 0,
                    Uptime = upTimeString,
                    ServerTime = serverTimeString,
                    CPUUsage = cpu,
                    RAMUsage = ram,
                    ClientOK = Client != null && Client.IsConnected,
                    MqttServerOK = mqttServer != null && mqttClient != null && mqttClient.IsConnected
                };

            });


        }

        async Task initMqttServer()
        {
            // Configure MQTT server.
            var optionsBuilder = new MqttServerOptionsBuilder()
                .WithConnectionBacklog(100)
                .WithConnectionValidator(c =>
                {
                    //Wenn der Client schon mal da war raus nehmen:
                    if (ClientRights.ContainsKey(c.ClientId))
                    {
                        ClientRights.Remove(c.ClientId);
                    }
                    bool canPublish = false;

                    if (c.ClientId == ClientId)
                        canPublish = true;

                    if (c.Username == GetUsername() && c.Password == GetPassword())
                    {
                        canPublish = true;
                    }
                    ClientRights.Add(c.ClientId, canPublish);
                    c.ReasonCode = MQTTnet.Protocol.MqttConnectReasonCode.Success;

                })
                .WithApplicationMessageInterceptor(m =>
                {
                    if (m.ClientId != ClientId && BlacklistedTopics.ContainsBlacklisted(m.ApplicationMessage.Topic))
                        m.AcceptPublish = false;
                    else if (ClientRights.ContainsKey(m.ClientId)) //Sollte immer true sein...
                        m.AcceptPublish = ClientRights[m.ClientId];
                    else
                        m.AcceptPublish = false;
                })
                .WithSubscriptionInterceptor(s =>
                {
                    s.AcceptSubscription = true;
                })
                .WithDefaultEndpointPort(Program.Ctrl2MqttBridgeSettings.MqttPort);


            mqttServer = new MqttFactory().CreateMqttServer();
            await mqttServer.StartAsync(optionsBuilder.Build());
        }

        async Task initMqttClient()
        {
            // Configure MQTT server.
            var optionsBuilder = new ManagedMqttClientOptionsBuilder()
                .WithClientOptions(new MqttClientOptionsBuilder()
                .WithCleanSession(true)
                .WithClientId(ClientId)
                .WithCredentials("Ctrl2MqttBridge", "Ctrl2MqttBridge")
                .WithTcpServer("localhost", Program.Ctrl2MqttBridgeSettings.MqttPort));


            mqttClient = new MqttFactory().CreateManagedMqttClient();
            await mqttClient.StartAsync(optionsBuilder.Build());
            await mqttClient.SubscribeAsync(new System.Collections.Generic.List<MqttTopicFilter>() {
                    (new MqttTopicFilter() { Topic = "#" })

            });
            mqttClient.ApplicationMessageReceivedHandler = new MessageReceivedHandler()
            {
                mqttClient = mqttClient,
                Client = Client
            };
        }

        async Task initMqttClientExternal()
        {
            // Configure MQTT server.
            var optionsBuilder = new ManagedMqttClientOptionsBuilder()
                .WithClientOptions(new MqttClientOptionsBuilder()
                .WithCleanSession(true)
                .WithClientId(ClientId)
                .WithTls()
                .WithTcpServer(Program.Ctrl2MqttBridgeSettings.ExternalBrokerUrl.Split(':')[0], Convert.ToInt32(Program.Ctrl2MqttBridgeSettings.ExternalBrokerUrl.Split(':')[1])));


            mqttClientExternal = new MqttFactory().CreateManagedMqttClient();
            await mqttClientExternal.StartAsync(optionsBuilder.Build());
            await mqttClientExternal.SubscribeAsync(new System.Collections.Generic.List<MqttTopicFilter>() {
                    (new MqttTopicFilter() { Topic = mqttPrefix + "#" })

            });
            mqttClientExternal.ApplicationMessageReceivedHandler = new MessageReceivedHandler()
            {
                mqttClient = mqttClientExternal,
                Client = Client
            };
        }


        public class MessageReceivedHandler : IMqttApplicationMessageReceivedHandler
        {
            public IManagedMqttClient mqttClient { get; set; }
            public IClient Client { get; set; }



            public Task HandleApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs eventArgs)
            {
                return Task.Run(async () =>
                {
#if DEBUG
                    System.Diagnostics.Trace.WriteLine(eventArgs.ApplicationMessage.Topic + " = " + eventArgs.ApplicationMessage.ConvertPayloadToString(), "MQTT Message Received");
#endif
                    //WRITE
                    if (eventArgs.ApplicationMessage.Topic.StartsWith(mqttPrefix + "write/"))
                    {
                        string subTopic = eventArgs.ApplicationMessage.Topic.Substring((mqttPrefix + "write/").Length);
                        try
                        {
                            if (subTopic.Contains("nonsensetocheckbridgeconnectivity"))
                            {
                                await mqttClient.PublishAsync(new MqttApplicationMessage() { Topic = (mqttPrefix + "writeresult/" + subTopic), Payload = Encoding.UTF8.GetBytes("OK") });
                            }
                            else
                            {
                                uint resultCode = 1;
                                string payload = eventArgs.ApplicationMessage.ConvertPayloadToString();
                                if (String.IsNullOrEmpty(payload))
                                    resultCode = 2;
                                else
                                    resultCode = await Client.Write(subTopic, payload);
                                await mqttClient.PublishAsync(new MqttApplicationMessage() { Topic = (mqttPrefix + "writeresult/" + subTopic), Payload = Encoding.UTF8.GetBytes(resultCode.ToString()) });
                                if (resultCode == 0)
                                    await mqttClient.PublishAsync(new MqttApplicationMessage() { Topic = (mqttPrefix + "writevalue/" + subTopic), Payload = Encoding.UTF8.GetBytes(payload) });
                            }
                        }
                        catch (Exception exc)
                        {
                            Console.WriteLine(exc.ToString());
                        }
                    }
                    //READ
                    if (eventArgs.ApplicationMessage.Topic.StartsWith(mqttPrefix + "read/"))
                    {
                        string subTopic = eventArgs.ApplicationMessage.Topic.Substring((mqttPrefix + "read/").Length);
                        try
                        {
                            string payload = eventArgs.ApplicationMessage.ConvertPayloadToString();
                            string result = await Client.Read(subTopic);
                            await mqttClient.PublishAsync(new MqttApplicationMessage() { Topic = (mqttPrefix + "readresult/" + subTopic), Payload = Encoding.UTF8.GetBytes(result) });
                        }
                        catch (Exception exc)
                        {
                            Console.WriteLine(exc.ToString());
                        }
                    }
                    //SUBSCRIBE
                    if (eventArgs.ApplicationMessage.Topic.StartsWith(mqttPrefix + "subscribe/"))
                    {
                        string subTopic = eventArgs.ApplicationMessage.Topic.Substring((mqttPrefix + "subscribe/").Length);
                        try
                        {
                            string payload = eventArgs.ApplicationMessage.ConvertPayloadToString();
                            int interval = Convert.ToInt32(payload);
                            uint resultCode = await Client.Subscribe(subTopic, interval);
                        }
                        catch (Exception exc)
                        {
                            Console.WriteLine(exc.ToString());
                        }

                    }
                    //UNSUBSCRIBE
                    if (eventArgs.ApplicationMessage.Topic.StartsWith(mqttPrefix + "unsubscribe/"))
                    {
                        string subTopic = eventArgs.ApplicationMessage.Topic.Substring((mqttPrefix + "unsubscribe/").Length);
                        try
                        {
                            string payload = eventArgs.ApplicationMessage.ConvertPayloadToString();
                            int interval = Convert.ToInt32(payload);
                            uint resultCode = await Client.Unsubscribe(subTopic);
                        }
                        catch (Exception exc)
                        {
                            Console.WriteLine(exc.ToString());
                        }

                    }

                });
            }
        }
        async Task initOperateNetService()
        {
            await Task.Run(() =>
            {
                operateNetService = new OperateNetService();
                OperateNetService.NewNotification += Client_NewNotification;
                OperateNetService.NewAlarmNotification += Client_NewAlarmNotification;
            });
        }
        async Task initDVSClient()
        {
            await Task.Run(() =>
            {
                dvsCtrlConnectorClient = new DVSCtrlConnectorClient(Program.Ctrl2MqttBridgeSettings.ServerName, Program.Ctrl2MqttBridgeSettings.DVSCtrlConnectorPort);
                DVSCtrlConnectorClient.NewNotification += Client_NewNotification;
                DVSCtrlConnectorClient.NewAlarmNotification += Client_NewAlarmNotification;
            });
        }

        async Task initSinumerikClient()
        {
            await Task.Run(() =>
            {
                sinumerikClient = new SinumerikSdkClient(Program.Ctrl2MqttBridgeSettings.ServerName.Replace("192.168.214.241", "192.168.214.1"));
                SinumerikSdkClient.NewNotification += Client_NewNotification;
            });
        }



        async Task initOPCUAClient()
        {
            opcUaConsoleClient = new OpcUaConsoleClient("opc.tcp://" + Program.Ctrl2MqttBridgeSettings.ServerName + ":" + Program.Ctrl2MqttBridgeSettings.OpcUaPort, true, 5000);
            try
            {
                await opcUaConsoleClient.RunAsync();
            }
            catch(Exception exc) { Console.WriteLine("Exception caught: " + exc.ToString()); }
            OpcUaConsoleClient.NewNotification += Client_NewNotification;
            OpcUaConsoleClient.NewAlarmNotification += Client_NewAlarmNotification;
        }

        private void Client_NewAlarmNotification(object sender, IMonitoredItem e)
        {
            createAndPushMqttMessage("alarmnotification/", e, false);
        }
        private void Client_NewNotification(object sender, IMonitoredItem e)
        {
            createAndPushMqttMessage("subscriptionnotification/", e, false);
        }

        private void createAndPushMqttMessage(string extendedPrefix, IMonitoredItem e, bool createErrorMessage)
        {
            
            //if a payload is avaliable set it to the message
            if (e.Value != null)
            {
                var message = new MqttApplicationMessage()
                {
                    Topic = mqttPrefix + extendedPrefix + e.DisplayName,
                    Payload = Encoding.UTF8.GetBytes(e.Value)
                };
                sendMessageToBroker(message);
            }
            //if no payload was found check if an error message should be sent
            else if (createErrorMessage)
            {
                var message = new MqttApplicationMessage()
                {
                    Topic = mqttPrefix + extendedPrefix + e.DisplayName,
                    Payload = Encoding.UTF8.GetBytes("no value found on machine")
                };
                sendMessageToBroker(message);
            }
        }
        private void sendMessageToBroker(MqttApplicationMessage message)
        {
            if (mqttClient != null && mqttClient.IsConnected)
            {
                mqttClient.PublishAsync(message);
            }
            if (mqttClientExternal != null && mqttClientExternal.IsConnected)
            {
                mqttClientExternal.PublishAsync(message);
            }
        }
    }
}
