using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using MQTTnet.Client.Receiving;
using MQTTnet.Server;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MqttBridge
{
    class Program
    {
        const int MQTT_PORT = 51883;
        static bool siemensdll = false;
        static IClient Client;
        static OperateNetService operateNetService;
        static OpcUaConsoleClient opcUaConsoleClient;

        static void Main(string[] args)
        {
            Console.WriteLine("MQTT - OPC UA Bridge      PRÄWEMA (C) 2020");
            
            Task.Run(async () => await initMqttServer());
            Task.Run(async () => await initMqttClient());

            Console.WriteLine("Press <ENTER> to INIT");
            Console.ReadLine();
            
            siemensdll = true;
            try
            {
                operateNetService = new OperateNetService();
                OperateNetService.NewNotification += Client_NewNotification;
            }
            catch(Exception exc)
            {
                siemensdll = false;
            }
            if (!siemensdll)
            {
                Task.Run(async () => await initOPCUAClient());
                Client = (IClient)opcUaConsoleClient;
            }
            else
                Client = (IClient)operateNetService;
            
            //Timer t = new Timer(async (e) => {
            //    await mqttClient.PublishAsync(new MqttApplicationMessage() { Topic = "timer", Payload = Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("s")) });
            //    });
            //t.Change(1000, 1000);
            
            System.Diagnostics.Trace.WriteLine("Started in " + (siemensdll ? "SIEMENS DLL" : "OPC UA") + "Mode", "MAIN");
            Console.WriteLine("Press <ENTER> to exit");
            Console.ReadLine();
        }

        async static Task initMqttServer()
        {
            // Configure MQTT server.
            var optionsBuilder = new MqttServerOptionsBuilder()
                .WithConnectionBacklog(100)
                .WithDefaultEndpointPort(MQTT_PORT);

            var mqttServer = new MqttFactory().CreateMqttServer();
            await mqttServer.StartAsync(optionsBuilder.Build());
        }
        static IMqttClient mqttClient;
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
                    System.Diagnostics.Trace.WriteLine(eventArgs.ApplicationMessage.Topic + " = " + eventArgs.ApplicationMessage.ConvertPayloadToString(), "MQTT Message Received");
                    
                    if(eventArgs.ApplicationMessage.Topic.StartsWith("opcua/write/"))
                    {
                        string subTopic = eventArgs.ApplicationMessage.Topic.Substring("opcua/write/".Length);
                        try
                        {
                            string payload = eventArgs.ApplicationMessage.ConvertPayloadToString();
                            uint resultCode=await Client.Write(subTopic, payload);
                            await mqttClient.PublishAsync(new MqttApplicationMessage() { Topic = ("opcua/writeresult/" +subTopic),Payload=Encoding.UTF8.GetBytes(resultCode.ToString()) });
                            if(resultCode==0)
                                await mqttClient.PublishAsync(new MqttApplicationMessage() { Topic = ("opcua/writevalue/" + subTopic), Payload = Encoding.UTF8.GetBytes(payload) });
                        }
                        catch(Exception exc)
                        {
                            Console.WriteLine(exc.ToString());
                        }
                    }
                    if (eventArgs.ApplicationMessage.Topic.StartsWith("opcua/read/"))
                    {
                        string subTopic = eventArgs.ApplicationMessage.Topic.Substring("opcua/read/".Length);
                        try
                        {
                            string payload = eventArgs.ApplicationMessage.ConvertPayloadToString();
                            string result = await Client.Read(subTopic);
                            await mqttClient.PublishAsync(new MqttApplicationMessage() { Topic = ("opcua/readresult/" + subTopic), Payload = Encoding.UTF8.GetBytes(result) });
                        }
                        catch (Exception exc)
                        {
                            Console.WriteLine(exc.ToString());
                        }
                    }
                    if (eventArgs.ApplicationMessage.Topic.StartsWith("opcua/subscribe/"))
                    {
                        string subTopic = eventArgs.ApplicationMessage.Topic.Substring("opcua/subscribe/".Length);
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

                });
            }
        }
        
        async static Task initOPCUAClient()
        {

            opcUaConsoleClient = new OpcUaConsoleClient("opc.tcp://192.168.142.250:4840", true, 5000);
            await opcUaConsoleClient.RunAsync();
            OpcUaConsoleClient.NewNotification += Client_NewNotification;
        }

        private static void Client_NewNotification(object sender, MonitoredItem e)
        {
            if(mqttClient!=null && mqttClient.IsConnected)
            {
                mqttClient.PublishAsync(new MqttApplicationMessage()
                {
                    Topic = "opcua/subscriptionnotification/" + e.NodeId,
                    Payload = Encoding.UTF8.GetBytes(e.Value)
                });
            }
        }
    }
}
