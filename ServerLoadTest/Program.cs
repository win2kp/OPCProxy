using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using OPCServiceClient;
using System.Collections;


namespace ServerLoadTest
{
    class Program
    {
        private static List<Thread> _threads = new List<Thread>();
        private static string _server = "10.97.141.250";
        private static int _port = 9100;
        private static int _gap = 5;
        private static Hashtable _tags = Hashtable.Synchronized(new Hashtable());
        static void Main (string[] args)
        {
            _tags.Add("EmergencyPass", "");
            _tags.Add("FixtruePass", "");

            int threadCount = 10;
            Console.Write("Please enter threads to run(10):");
            string input = Console.ReadLine();
            if (input !="") int.TryParse(input, out threadCount);

            Console.Write("Please enter the server(10.97.141.250):");
            input = Console.ReadLine();
            if (input != "") _server = input;


            Console.Write("Please enter the port(9100):");
            input = Console.ReadLine();
            if (input != "") int.TryParse(input, out _port);

            for (int n = 0; n < threadCount; n++)
            {
                Thread td = new Thread(new ThreadStart(LoadTestHandler));
                td.IsBackground = true;
                _threads.Add(td);
                td.Start();
                Thread.Sleep(2000);
                Console.WriteLine("Test thread started");
            }

            DateTime ts = DateTime.Now;
            while (true)
            {

                TimeSpan span = DateTime.Now.Subtract(ts);
                Console.WriteLine("Running " + span.Hours + ":" +  span.Minutes + ":" + span.Seconds);
                Thread.Sleep(10000);
                
            }
        }

        private static void LoadTestHandler()
        {

            string[] tags = new string[_tags.Count];
            _tags.Keys.CopyTo(tags, 0);

            OPCServiceClient.OPCServiceClient client = new OPCServiceClient.OPCServiceClient(_server, _port);
            if (client.Connect())
            {
                while (true)
                {
                    Random rand = new Random();
                    int n = rand.Next(0, tags.Length);
                    string tag = tags[n];

                    if (DateTime.Now.Second % 2 == 0)
                    {
                        string value = client.GetValue(tag);
                        Console.WriteLine("read " + tag  +"=" + value);
                    } else
                    {
                        string value = (DateTime.Now.Millisecond).ToString();
                        client.SetValue(tag, value);
                        Console.WriteLine("write " + tag  + "=" + value);
                    }

                    Thread.Sleep(_gap);
                }
            } else
            {
                Console.WriteLine("无法连接服务器");
            }

        }
    }
}
