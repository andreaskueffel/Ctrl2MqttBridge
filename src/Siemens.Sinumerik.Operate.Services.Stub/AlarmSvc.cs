using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Siemens.Sinumerik.Operate.Services
{
    public class AlarmSvc
    {
        public AlarmSvc(string language)
        {
            Language = language;
        }

        public string Language { get; }

        public Guid Subscribe(Action<Guid, Alarm[]> alarmListCallback)
        {
            throw new NotImplementedException();
        }

        public Guid SubscribeEvents(Action<Guid,Alarm[]> alarmEventCallback)
        {
            throw new NotImplementedException();
        }

    }
}
