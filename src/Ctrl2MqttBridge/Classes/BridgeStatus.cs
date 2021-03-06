﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ctrl2MqttBridge.Classes
{
    public class BridgeStatus
    {
        public string Ctrl2MqttBridgeVersion { get; set; }
        public string OperationMode { get; set; }
        public string ServerName { get; set; }
        public string ServerTime { get; set; }
        public string Uptime { get; set; }
        public int SubcribedItemsCount { get; set; }
        public int ClientCount { get; set; }
        public bool MqttServerOK { get; set; }
        public bool ClientOK { get; set; }

        public double CPUUsage { get; set; }
        public string RAMUsage { get; set; }

    }
}
