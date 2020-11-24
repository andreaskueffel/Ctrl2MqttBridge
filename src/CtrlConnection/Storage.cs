using MQTTnet.Extensions.ManagedClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.Serialization.Json;

namespace MqttBridge
{
    class Storage : IManagedMqttClientStorage
    {
        private IManagedMqttClient managedMqttClient = null;
        public Storage(IManagedMqttClient managedMqttClient)
        {
            this.managedMqttClient = managedMqttClient;
        }

        static string FileName = "mqtt.que";
        static object FileLock = new object();
        public Task<IList<ManagedMqttApplicationMessage>> LoadQueuedMessagesAsync()
        {
            IList<ManagedMqttApplicationMessage> _return = new List<ManagedMqttApplicationMessage>();
            lock (FileLock)
            {
                if (!File.Exists(FileName))
                    return Task.FromResult(_return);
                try
                {
                    using (FileStream stream = new FileStream(FileName, FileMode.Open))
                    {
                        DataContractJsonSerializer deserializer = new DataContractJsonSerializer(typeof(IList<ManagedMqttApplicationMessage>));
                        _return = (IList<ManagedMqttApplicationMessage>)deserializer.ReadObject(stream);
                        stream.Close();
                    }
                }
                catch { }
            }
            return Task.FromResult(_return);
        }

        public Task SaveQueuedMessagesAsync(IList<ManagedMqttApplicationMessage> messages)
        {
            return Task.Run(() =>
            {
                lock (FileLock)
                {
                    if (messages.Count > 0 && !managedMqttClient.IsConnected)
                    {
                        using (FileStream stream = new FileStream(FileName, FileMode.Create))
                        {
                            DataContractJsonSerializer js = new DataContractJsonSerializer(typeof(IList<ManagedMqttApplicationMessage>));
                            js.WriteObject(stream, messages);
                            stream.Close();
                        }
                    }
                    else
                    {
                        File.Delete(FileName);
                    }
                }
            });
        }
    }
}
