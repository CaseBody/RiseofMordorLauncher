using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiseofMordorLauncher
{
    public class Logger
    {
        public static Logger Instance = new Logger();
        private string logFile = "launcher.log";

        private Logger()
        {
            if (File.Exists(logFile))
                File.Delete(logFile);
        }

        public static void Log(string message)
        {
            using (var stream = File.AppendText(Instance.logFile))
            {
                stream.WriteLine(message);
            }
        }
    }
}
