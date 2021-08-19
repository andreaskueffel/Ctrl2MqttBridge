using Ctrl2MqttBridge.Classes;
using Ctrl2MqttBridge.Interfaces;
using Sinumerik.Advanced;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace Ctrl2MqttBridge
{
    public class SinumerikSdkClient : IClient, IDisposable
    {
        public static event EventHandler<IMonitoredItem> NewNotification;

        public bool reconnect = true; //use this to enable automatic reconnection attempt

        private SinumerikDevice device;
        private NckDeviceConnection connection;
        private ConcurrentDictionary<string, string> subscribedItems;
        private System.Timers.Timer subscriptionTimer;
        private String hostIp;
        public SinumerikSdkClient(string _hostIp)
        {
            Licenser.LicenseKey = TraegerLicense.LicenseKey;
            hostIp = _hostIp;

            subscribedItems = new ConcurrentDictionary<string, string>();
            subscriptionTimer = new System.Timers.Timer(200);
            subscriptionTimer.Elapsed += SubscriptionTimer_Elapsed;
            subscriptionTimer.AutoReset = true;
            subscriptionTimer.Start();

            establishConnection(); //by Lukas Czycholl
            
            
        }
        //created by Lukas Czycholl
        private void establishConnection()
        {
            device = new SinumerikDevice(hostIp, SinumerikDeviceType.SolutionLine);
            connection = device.CreateConnection();
            //set event handlers
            connection.Opened += new EventHandler(delegate (Object o, EventArgs e)
            {
                Console.WriteLine("sdk Client Opened");
            });
            connection.Connected += new EventHandler(delegate (Object o, EventArgs e)
            {
                Console.WriteLine("sdk Client successfully connected");
            });
            connection.Closed += new EventHandler(delegate (Object o, EventArgs e)
            {
                Console.WriteLine("sdk Client Closed");
            });
            connection.Disconnected += new EventHandler(delegate (Object o, EventArgs e)
            {
                Console.WriteLine("sdk Client disconnected. Address was: " + hostIp);
                if (reconnect)
                {
                    connect();
                    Console.WriteLine("trying to reconnect.");
                }
            });
            connect();
        }

        private async Task checkConnection()
        {
            while (true)
            {
                Thread.Sleep(5000);
                if (!IsConnected)
                {
                    Console.WriteLine("sdk client was not able to connect. Address was: " + hostIp);
                    Console.WriteLine("is the NCU running?");
                }
                else
                {
                    break;
                }
            }
        }

        private void connect()
        {
            //opening and connecting connection.
            Task.Run(() => checkConnection().ConfigureAwait(false));
            connection.Open();
            connection.Connect();
        }
        private void disconnect()
        {
            connection.Close();
            connection.Dispose();
        }
        //end created by Lukas Czycholl
        private void SubscriptionTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            foreach (var item in subscribedItems.Keys)
            {
                var readResult = ReadSync(item);
                if (subscribedItems[item] != readResult)
                {
                    subscribedItems[item] = readResult;
                    Task.Run(() =>
                    {
                        NewNotification?.Invoke(this, new MonitoredItemOpcUa()
                        {
                            NodeId = item,
                            DisplayName = item,
                            Value = readResult
                        });
                    }).ConfigureAwait(false);
                }
            }
        }

        public int SubscribedItemsCount => subscribedItems.Count;

        public bool IsConnected { get { lock (connectionLock) { return connection.IsConnected; } } }

        public void Dispose()
        {
            connection.Dispose();
        }

        private object connectionLock = new object();
        private string ReadSync(string nodeId)
        {
            try
            {
                IDictionary<string, object> result;
                lock (connectionLock)
                {
                    result = connection.Read(nodeId);
                }
                return Functions.GetStringFromDataObject(result.First().Value);
            }
            catch { return null; }
        }
        public async Task<string> Read(string nodeId)
        {
            string retval = "";
            await Task.Run(() =>
            {
                retval = ReadSync(nodeId);
            });
            return retval;
        }

        public async Task<uint> Subscribe(string nodeId, int interval)
        {
            await Task.Run(() =>
            {
                subscribedItems.AddOrUpdate(nodeId, "", (key, newVal)=>"");
            });
            return 0;
        }

        public async Task<uint> Unsubscribe(string nodeId)
        {
            await Task.Run(() =>
            {
                if (subscribedItems.ContainsKey(nodeId))
                    subscribedItems.TryRemove(nodeId, out _);
            });
            return 0;
        }

        public async Task<uint> Write(string nodeId, string payload)
        {
            await Task.Run(() =>
            {
                var data = Functions.GetObjectFromString(payload);
                if (nodeId.ToLower().Contains("Channel/Parameter/R[".ToLower())&& data is int)
                    data = 1.0*(int)data; //R Parameter immer als double
                lock (connectionLock)
                {
                    if (data is string)
                        connection.WriteString(nodeId, (string)data);
                    if (data is bool)
                        connection.WriteBoolean(nodeId, (bool)data);
                    if (data is double)
                        connection.WriteDouble(nodeId, (double)data);
                    if (data is int)
                        connection.WriteInt32(nodeId, (int)data);
                }   
            });
            return 0;
        }
    }
}
