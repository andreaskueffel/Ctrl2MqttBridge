using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Siemens.Sinumerik.Operate.Services
{
    public class Item
    {
        public Item(string name)
        {
            Name = name;
        }

        public Item(string name, string value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; }
        public object Value { get; set; }
    }
}
