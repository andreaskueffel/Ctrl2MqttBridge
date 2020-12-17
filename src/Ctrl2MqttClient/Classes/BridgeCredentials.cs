using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ctrl2MqttClient.Classes
{
    public class BridgeCredentials
    {
        public string Username { get; private set; }
        public string Password { get; private set; }
        public BridgeCredentials(string username, string password)
        {
            if (String.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentException("Username cannot be null, empty or whitespace", "username");
            }
            if (String.IsNullOrWhiteSpace(password))
            {
                throw new ArgumentException("Password cannot be null, empty or whitespace", "password");
            }

            Username = username;
            Password = password;
        }

    }
}
