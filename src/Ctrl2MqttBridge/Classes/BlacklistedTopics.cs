using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ctrl2MqttBridge.Classes
{
    static class BlacklistedTopics
    {
        private static readonly string[] ReservedTopics = { 
            "/bridgeStatus",
            "/writeresult",
            "/readresult",
            "/subscriptionnotification",
            "/alarmnotification",
            "/writevalue"
        };

        public static bool ContainsBlacklisted(string topic)
        {
            bool result = false;
            foreach(var blacklistedTopic in ReservedTopics)
            {
                if (topic.ToLower().Contains(blacklistedTopic.ToLower()))
                    result = true;
            }
            return result;
        }

    }
}
