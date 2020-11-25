using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Siemens.Sinumerik.Operate.Services
{
    public class DataSvc
    {
        public DataSvc()
        {
            throw new NotImplementedException();
        }

        public void Read(Item item)
        {
            throw new NotImplementedException();
        }

        public void Write(Item item)
        {
            throw new NotImplementedException();
        }

        public Guid Subscribe(Action<Guid, Item, DataSvcStatus> onDataChanged, Item item)
        {
            throw new NotImplementedException();
        }

        public void UnSubscribe(Action<Guid, Item, DataSvcStatus> onDataChanged)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
