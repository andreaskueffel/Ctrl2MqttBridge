using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Siemens.Sinumerik.Operate.Services
{
    public class Alarm
    {
        public Alarm(DateTime timeStamp, string v)
        {
            TimeStamp = timeStamp;
            V = v;
        }

        public string V { get; }
        public int Id { get; set; }
        public DateTime TimeStamp { get; set; }
        public string Message { get; set; }
    }
}
