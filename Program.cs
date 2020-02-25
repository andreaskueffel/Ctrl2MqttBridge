using MqttBridge.Classes;
using MqttBridge.Interfaces;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using MQTTnet.Client.Receiving;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Server;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MqttBridge
{
    public class Program
    {
        const string SettingsFilename = "MqttBridgeSettings.json";

        public static MqttBridgeSettings MqttBridgeSettings=new MqttBridgeSettings();

        static IManagedMqttClient mqttClient;
        static IMqttServer mqttServer;
        static DateTime StartTime;
        static IClient Client;
        static OperateNetService operateNetService;
        static OpcUaConsoleClient opcUaConsoleClient;
        static string MachineName;
        static void Main(string[] args)
        {
            StartTime = DateTime.Now;
            MachineName = Environment.MachineName;
            Console.WriteLine("-----------------------------------------------------------");
            Console.WriteLine("MQTT - Bridge                              PRÄWEMA (c) 2020");
            Console.WriteLine("Version: " + typeof(Program).Assembly.GetName().Version.ToString());
            Console.WriteLine("");
            Console.WriteLine("-----------------------------------------------------------");

            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;
            
            Console.WriteLine("Getting settings...");
            string settings = "";
            if (File.Exists(SettingsFilename))
                settings = File.ReadAllText(SettingsFilename);
            if (!String.IsNullOrEmpty(settings)) try
                {
                    MqttBridgeSettings = JsonConvert.DeserializeObject<MqttBridgeSettings>(settings);
                    Console.WriteLine("Settings read.");
                }
                catch { }

            string newSettings = JsonConvert.SerializeObject(MqttBridgeSettings, Formatting.Indented);
            if (settings != newSettings)
            {
                Console.WriteLine("Settings changed, save to "+SettingsFilename);
                File.WriteAllText(SettingsFilename, newSettings);
            }

            
            if (MqttBridgeSettings.OpcUaMode)
            {
                try
                {
                    Task.Run(async () => await initOPCUAClient()).Wait();
                    Client = (IClient)opcUaConsoleClient;
                }
                catch { }
            }
            if (!MqttBridgeSettings.OpcUaMode)
            {
                try
                {
                    Task.Run(async () => await initOperateNetService()).Wait();
                    Client = (IClient)operateNetService;
                }
                catch { }
            }


            Task.Run(async () => await initMqttServer()).Wait();
            Task.Run(async () => await initMqttClient()).Wait();
            if (MqttBridgeSettings.EnableStatus)
            {
                Timer t = new Timer(async (e) =>
                {

                    string bridgeStatusJson = JsonConvert.SerializeObject(await GetBridgeStatus());
                    await mqttClient.PublishAsync(new MqttApplicationMessage() { Topic = "mqttBridge/" + "bridgeStatus", Payload = Encoding.UTF8.GetBytes(bridgeStatusJson) });
                });
                t.Change(1000, 10000);
            }

            System.Diagnostics.Trace.WriteLine("Started in " + (MqttBridgeSettings.OpcUaMode ? "OPCUA" : "SIEMENSDLL") + "Mode", "MAIN");
            Console.WriteLine("Type 'q' to exit");
            string readLine = "";
            while (readLine.ToLower() != "q")
                readLine = Console.ReadLine();
        }

        private static void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            Console.WriteLine("Unhandled Exception occured:\r\n" + e.ExceptionObject);
        }

        static double lastCPUMillis = 0;
        static double lastUptimeMillis = 0;
        static Task<BridgeStatus> GetBridgeStatus()
        {
            return Task.Run(async () =>
            {
                TimeSpan upTime = (DateTime.Now - StartTime);
                string upTimeString = "P" + Math.Floor(upTime.TotalDays).ToString("0") + "DT" + upTime.Hours + "H" + upTime.Minutes + "M" + upTime.Seconds + "S";
                double CPUMillis = System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime.TotalMilliseconds;
                
                double deltaCPUMillis =  CPUMillis - lastCPUMillis;
                double deltaUptimeMillis = upTime.TotalMilliseconds - lastUptimeMillis;
                lastCPUMillis = CPUMillis;
                lastUptimeMillis = upTime.TotalMilliseconds;
                double cpu = Math.Round(100.0*(deltaCPUMillis/deltaUptimeMillis),1);
                string ram = (System.Diagnostics.Process.GetCurrentProcess().WorkingSet64/1024/1024)+" MiB";
                

                return new BridgeStatus()
                {
                    ClientCount = ((await mqttServer.GetClientStatusAsync()).Count)-1,
                    OperationMode = Client!=null ? (MqttBridgeSettings.OpcUaMode ? "OPCUA":"OperateNetService") : "NO_CTRL_CONNECTION",
                    ServerName = MachineName,
                    SubcribedItemsCount = Client!=null?Client.SubscribedItemsCount:0,
                    Uptime = upTimeString,
                    CPUUsage = cpu,
                    RAMUsage = ram,
                    ClientOK=Client!=null && Client.IsConnected,
                    MqttServerOK=mqttServer!=null && mqttClient!=null && mqttClient.IsConnected
                };

            });


        }

        async static Task initMqttServer()
        {
            // Configure MQTT server.
            var optionsBuilder = new MqttServerOptionsBuilder()
                .WithConnectionBacklog(100)
                .WithDefaultEndpointPort(MqttBridgeSettings.MqttPort);

            mqttServer = new MqttFactory().CreateMqttServer();
            await mqttServer.StartAsync(optionsBuilder.Build());
        }
        
        async static Task initMqttClient()
        {
            // Configure MQTT server.
            var optionsBuilder = new ManagedMqttClientOptionsBuilder()
                .WithClientOptions( new MqttClientOptionsBuilder()
                .WithCleanSession(true)
                .WithTcpServer("localhost", MqttBridgeSettings.MqttPort))
                ;

            mqttClient = new MqttFactory().CreateManagedMqttClient();
            await mqttClient.StartAsync(optionsBuilder.Build());
            await mqttClient.SubscribeAsync(new System.Collections.Generic.List<TopicFilter>() {
                    (new TopicFilter() { Topic = "#" })
                
            });
            mqttClient.ApplicationMessageReceivedHandler=new MessageReceivedHandler();
        }

        public class MessageReceivedHandler : IMqttApplicationMessageReceivedHandler
        {
            public Task HandleApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs eventArgs)
            {
                return Task.Run(async () =>
                {
#if DEBUG
                    System.Diagnostics.Trace.WriteLine(eventArgs.ApplicationMessage.Topic + " = " + eventArgs.ApplicationMessage.ConvertPayloadToString(), "MQTT Message Received");
#endif
                    //WRITE
                    if(eventArgs.ApplicationMessage.Topic.StartsWith("mqttbridge/write/"))
                    {
                        string subTopic = eventArgs.ApplicationMessage.Topic.Substring("mqttbridge/write/".Length);
                        try
                        {
                            uint resultCode = 1;
                            string payload = eventArgs.ApplicationMessage.ConvertPayloadToString();
                            if (String.IsNullOrEmpty(payload))
                                resultCode = 2;
                            else
                                resultCode = await Client.Write(subTopic, payload);
                            await mqttClient.PublishAsync(new MqttApplicationMessage() { Topic = ("mqttbridge/writeresult/" +subTopic),Payload=Encoding.UTF8.GetBytes(resultCode.ToString()) });
                            if(resultCode==0)
                                await mqttClient.PublishAsync(new MqttApplicationMessage() { Topic = ("mqttbridge/writevalue/" + subTopic), Payload = Encoding.UTF8.GetBytes(payload) });
                        }
                        catch(Exception exc)
                        {
                            Console.WriteLine(exc.ToString());
                        }
                    }
                    //READ
                    if (eventArgs.ApplicationMessage.Topic.StartsWith("mqttbridge/read/"))
                    {
                        string subTopic = eventArgs.ApplicationMessage.Topic.Substring("mqttbridge/read/".Length);
                        try
                        {
                            string payload = eventArgs.ApplicationMessage.ConvertPayloadToString();
                            string result = await Client.Read(subTopic);
                            await mqttClient.PublishAsync(new MqttApplicationMessage() { Topic = ("mqttbridge/readresult/" + subTopic), Payload = Encoding.UTF8.GetBytes(result) });
                        }
                        catch (Exception exc)
                        {
                            Console.WriteLine(exc.ToString());
                        }
                    }
                    //SUBSCRIBE
                    if (eventArgs.ApplicationMessage.Topic.StartsWith("mqttbridge/subscribe/"))
                    {
                        string subTopic = eventArgs.ApplicationMessage.Topic.Substring("mqttbridge/subscribe/".Length);
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
                    if (eventArgs.ApplicationMessage.Topic.StartsWith("mqttbridge/unsubscribe/"))
                    {
                        string subTopic = eventArgs.ApplicationMessage.Topic.Substring("mqttbridge/unsubscribe/".Length);
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

        async static Task initOperateNetService()
        {
            await Task.Run(() =>
            {
                try
                {
                    operateNetService = new OperateNetService();
                    OperateNetService.NewNotification += Client_NewNotification;
                }
                catch { }
            });
        }
        async static Task initOPCUAClient()
        {

            opcUaConsoleClient = new OpcUaConsoleClient("opc.tcp://"+MqttBridgeSettings.ServerName+":"+MqttBridgeSettings.OpcUaPort, true, 5000);
            await opcUaConsoleClient.RunAsync();
            OpcUaConsoleClient.NewNotification += Client_NewNotification;
        }

        private static void Client_NewNotification(object sender, IMonitoredItem e)
        {
            if(mqttClient!=null && mqttClient.IsConnected)
            {
              mqttClient.PublishAsync(new MqttApplicationMessage()
                {
                    Topic = "mqttbridge/subscriptionnotification/" + e.DisplayName,
                    Payload = Encoding.UTF8.GetBytes(e.Value)
                });
                
            }
        }
    }
}
