using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ctrl2MqttBridge
{
    public enum Axis
    {
        None = -1,
        //Prozess rechts
        C = 1,
        Z1 = 2,
        X1 = 3,

        B = 5,
        Y = 6,

        //Ladeshuttle
        X2 = 7,
        Z2 = 8,

    }
}
