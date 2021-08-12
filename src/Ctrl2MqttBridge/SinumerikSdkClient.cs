using Ctrl2MqttBridge.Classes;
using Ctrl2MqttBridge.Interfaces;
using Sinumerik.Advanced;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace Ctrl2MqttBridge
{
    public class SinumerikSdkClient : IClient, IDisposable
    {
        public static event EventHandler<IMonitoredItem> NewNotification;


        private readonly SinumerikDevice device;
        private NckDeviceConnection connection;
        private ConcurrentDictionary<string, string> subscribedItems;
        private Timer subscriptionTimer;
        public SinumerikSdkClient(string hostIp)
        {
            subscribedItems = new ConcurrentDictionary<string, string>();
            subscriptionTimer = new Timer(200);
            subscriptionTimer.Elapsed += SubscriptionTimer_Elapsed;
            subscriptionTimer.AutoReset = true;
            subscriptionTimer.Start();

            device = new SinumerikDevice(hostIp, SinumerikDeviceType.SolutionLine);
            connection = device.CreateConnection();
            connection.Open();
            
            //var items = device.Values;
            //foreach(var item in items)
            //{
            //    System.IO.File.AppendAllText("items.txt", item.ToString() + Environment.NewLine);
            //}
        }

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
