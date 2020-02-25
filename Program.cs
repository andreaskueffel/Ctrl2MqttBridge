using MqttBridge.Classes;
using MqttBridge.Interfaces;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using MQTTnet.Client.Receiving;
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
    class Program
    {
        const int MQTT_PORT = 51883;
        static bool siemensdll = false;

        static IMqttClient mqttClient;
        static IMqttServer mqttServer;
        static DateTime StartTime;
        static IClient Client;
        static OperateNetService operateNetService;
        static OpcUaConsoleClient opcUaConsoleClient;

        static void Main(string[] args)
        {
            StartTime = DateTime.Now;
            Console.WriteLine("-----------------------------------------------------------");
            Console.WriteLine("MQTT - Bridge                              PRÄWEMA (c) 2020");
            Console.WriteLine("Version: " + typeof(Program).Assembly.GetName().Version.ToString());
            Console.WriteLine("");
            Console.WriteLine("-----------------------------------------------------------");

            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;
            
            siemensdll = true;
            try
            {
                Task.Run(async () => await initOperateNetService()).Wait(); 
                Client = (IClient)operateNetService;
            }
            catch            {            }
            if (Client==null)
            {
                try
                {
                    Task.Run(async () => await initOPCUAClient()).Wait();
                    Client = (IClient)opcUaConsoleClient;
                }
                catch { }
            }

            Task.Run(async () => await initMqttServer()).Wait();
            Task.Run(async () => await initMqttClient()).Wait();

            Timer t = new Timer(async (e) =>
            {

                string bridgeStatusJson = JsonConvert.SerializeObject(await GetBridgeStatus());
                await mqttClient.PublishAsync(new MqttApplicationMessage() { Topic = "mqttBridge/"+"bridgeStatus", Payload = Encoding.UTF8.GetBytes(bridgeStatusJson) });
            });
            t.Change(1000, 1000);

            System.Diagnostics.Trace.WriteLine("Started in " + (siemensdll ? "SIEMENS DLL" : "OPC UA") + "Mode", "MAIN");
            Console.WriteLine("Type 'q' to exit");
            string readLine = "";
            while(readLine.ToLower()!="q")
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
                    ClientCount = (await mqttServer.GetClientStatusAsync()).Count,
                    OperationMode = Client!=null ? (siemensdll ? "OperateNetService" : "OPC UA"):"NO CTRL CONNECTION",
                    ServerName = Environment.MachineName,
                    SubcribedItemsCount = Client!=null?Client.SubscribedItemsCount:0,
                    Uptime = upTimeString,
                    CPUUsage = cpu,
                    RAMUsage = ram
                };

            });


        }

        async static Task initMqttServer()
        {
            // Configure MQTT server.
            var optionsBuilder = new MqttServerOptionsBuilder()
                .WithConnectionBacklog(100)
                .WithDefaultEndpointPort(MQTT_PORT);

            mqttServer = new MqttFactory().CreateMqttServer();
            await mqttServer.StartAsync(optionsBuilder.Build());
        }
        
        async static Task initMqttClient()
        {
            // Configure MQTT server.
            var optionsBuilder = new MqttClientOptionsBuilder()
                .WithCleanSession(true)
                .WithTcpServer("localhost", MQTT_PORT)
                ;

            mqttClient = new MqttFactory().CreateMqttClient();
            await mqttClient.ConnectAsync(optionsBuilder.Build(), CancellationToken.None);
            await mqttClient.SubscribeAsync(new MQTTnet.Client.Subscribing.MqttClientSubscribeOptions()
            {
                TopicFilters = new System.Collections.Generic.List<TopicFilter>() {
                    (new TopicFilter() { Topic = "#" })
                }
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
                    siemensdll = true;
                }
                catch
                { siemensdll = false; }
            });
        }
        async static Task initOPCUAClient()
        {

            opcUaConsoleClient = new OpcUaConsoleClient("opc.tcp://192.168.214.241:4840", true, 5000);
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
