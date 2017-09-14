using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OPCServiceClient;

namespace OPCClientDemo
{
    class Program
    {
        static void Main (string[] args)
        {

            OPCServiceClient.OPCServiceClient client = new OPCServiceClient.OPCServiceClient("127.0.0.1", 9100);

            client.BadBlockDetected +=Client_BadBlockDetected;
            if (client.Connect())
            {
                client.SetValue("tag1", "1");
                string tagValue1 = client.GetValue("tag1");

                Dictionary<string, string> dic = new Dictionary<string, string>();
                dic.Add("tag2", "20");
                dic.Add("tag3", "0");
                client.SetValues(dic);

                dic = client.GetValues(new List<string>() { "tag2", "tag3" });
                client.Disconnect();
            }
        }

        private static void Client_BadBlockDetected (string blockName, OPCQualities status)
        {
            Console.Write(blockName + " quality is " + status.ToString());
        }
    }
}
