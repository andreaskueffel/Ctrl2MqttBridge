using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MqttBridge
{
    public interface IClient
    {
        Task<uint> Write(string nodeId, string payload);
        Task<string> Read(string nodeId);
        Task<uint> Subscribe(string nodeId, int interval);




    }
}
