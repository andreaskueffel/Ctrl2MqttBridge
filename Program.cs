using MqttBridge.Interfaces;
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
        const bool TEST = false;
        static void Main(string[] args)
        {
            Console.WriteLine("MQTT - Bridge      PRÄWEMA (C) 2020");
            
            

            //Console.WriteLine("Press <ENTER> to INIT");
            //Console.ReadLine();
            
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
                Task.Run(async () => await initOPCUAClient()).Wait();
                Client = (IClient)opcUaConsoleClient;
            }
            else
                Client = (IClient)operateNetService;

            Task.Run(async () => await initMqttServer()).Wait();
            Task.Run(async () => await initMqttClient()).Wait();
            //Timer t = new Timer(async (e) => {
            //    await mqttClient.PublishAsync(new MqttApplicationMessage() { Topic = "timer", Payload = Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("s")) });
            //    });
            //t.Change(1000, 1000);
            if (TEST)
            {
                Task.Run(async () =>
                {
                    for (int i = 1; i < 1000; i++)
                    {
                        System.Diagnostics.Trace.WriteLine("Subscribe R" + i + "...");
                        await Client.Subscribe("channel/parameter/r[u1," + i + "]", 10);
                    }
                });
            }


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

                });
            }
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
