using System;
using System.Collections.Generic;
using System.Text;

namespace MqttBridge
{
    static class Functions
    {

        public static string GetStringFromDataObject(object item)
        {
            try
            {
                //Welcher Typ kommt da wohl zurück??
                if (item.GetType() == typeof(string))
                    return (string)item;
                if (item.GetType() == typeof(int))
                    return ((int)item).ToString();
                if (item.GetType() == typeof(double))
                    return ((double)item).ToString().Replace(",", ".");
                if (item.GetType() == typeof(float))
                    return ((float)item).ToString().Replace(",", ".");

            }
            catch (Exception exc)
            {
                return exc.ToString();
            }
            return item.ToString();
        }
    }
}
