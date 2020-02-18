using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using MQTTnet.Client.Receiving;
using MQTTnet.Server;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MqttOpcUaBridge
{
    class Program
    {
        const int MQTT_PORT = 51883;

        static void Main(string[] args)
        {
            Console.WriteLine("MQTT - OPC UA Bridge      PRÄWEMA (C) 2020");
           
            Task.Run(async () => await initMqttServer());
            Task.Run(async () => await initMqttClient());
            Task.Run(async () => await initOPCUAClient());
            //Timer t = new Timer(async (e) => {
            //    await mqttClient.PublishAsync(new MqttApplicationMessage() { Topic = "timer", Payload = Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("s")) });
            //    });
            //t.Change(1000, 1000);
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
                        string[] arTopic = eventArgs.ApplicationMessage.Topic.Split('/');
                        try
                        {
                            int ns = Convert.ToInt32(arTopic[2]);
                            string s = arTopic[3];
                            string payload = eventArgs.ApplicationMessage.ConvertPayloadToString();
                            uint resultCode=await opcUaConsoleClient.Write(String.Format("ns={0};s={1}", ns, s), payload);
                            await mqttClient.PublishAsync(new MqttApplicationMessage() { Topic = ("opcua/writeresult/" + arTopic[2] + "/" + arTopic[3]),Payload=Encoding.UTF8.GetBytes(resultCode.ToString()) });
                            if(resultCode==0)
                                await mqttClient.PublishAsync(new MqttApplicationMessage() { Topic = ("opcua/writevalue/" + arTopic[2] + "/" + arTopic[3]), Payload = Encoding.UTF8.GetBytes(payload) });
                        }
                        catch(Exception exc)
                        {
                            Console.WriteLine(exc.ToString());
                        }
                    }
                    if (eventArgs.ApplicationMessage.Topic.StartsWith("opcua/read/"))
                    {
                        string[] arTopic = eventArgs.ApplicationMessage.Topic.Split('/');
                        try
                        {
                            int ns = Convert.ToInt32(arTopic[2]);
                            string s = arTopic[3];
                            string payload = eventArgs.ApplicationMessage.ConvertPayloadToString();
                            string result = await opcUaConsoleClient.Read(String.Format("ns={0};s={1}", ns, s));
                            await mqttClient.PublishAsync(new MqttApplicationMessage() { Topic = ("opcua/readresult/" + arTopic[2] + "/" + arTopic[3]), Payload = Encoding.UTF8.GetBytes(result) });
                        }
                        catch (Exception exc)
                        {
                            Console.WriteLine(exc.ToString());
                        }
                    }
                    if (eventArgs.ApplicationMessage.Topic.StartsWith("opcua/subscribe/"))
                    {
                        string[] arTopic = eventArgs.ApplicationMessage.Topic.Split('/');
                        try
                        {
                            int ns = Convert.ToInt32(arTopic[2]);
                            string s = arTopic[3];
                            string payload = eventArgs.ApplicationMessage.ConvertPayloadToString();
                            int interval = Convert.ToInt32(payload);
                            uint resultCode = await opcUaConsoleClient.Subscribe(String.Format("ns={0};s={1}", ns, s), interval);
                        }
                        catch (Exception exc)
                        {
                            Console.WriteLine(exc.ToString());
                        }

                    }

                });
            }
        }
        static OpcUaConsoleClient opcUaConsoleClient;
        async static Task initOPCUAClient()
        {

            opcUaConsoleClient = new OpcUaConsoleClient("opc.tcp://192.168.142.250:4840", true, 5000);
            await opcUaConsoleClient.RunAsync();
            OpcUaConsoleClient.NewNotification += OpcUaConsoleClient_NewNotification;
        }

        private static void OpcUaConsoleClient_NewNotification(object sender, OpcUaConsoleClient.OpcItemNotificationEventArgs e)
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
