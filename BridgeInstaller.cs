using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MqttBridge.Classes
{
    public static class BridgeInstaller
    {
        public static void Install()
        {
            Console.WriteLine("Begin install");
            const string sysconfig = @"C:\ProgramData\Siemens\...";
            //Wenn der Prozess läuft erstmal beenden?
            var processes = System.Diagnostics.Process.GetProcessesByName("mqttbridge");
            foreach(var process in processes)
            {
                process.CloseMainWindow();
                process.WaitForExit(2000);
                process.Kill();
            }
            if (File.Exists(sysconfig))
            {

            }
        }
    }
}
