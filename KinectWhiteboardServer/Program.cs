using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using System.Configuration;

namespace KinectWhiteboardServer
{
    class Program
    {
        static void Main(string[] args)
        {
            // Address
            Uri uri = new Uri(ConfigurationManager.AppSettings["addr"]);
            // Contract-> Setting
            // Binding -> App.Config
            ServiceHost host = new ServiceHost(typeof(KinectWhiteboardServer.ChatService), uri);

            // Open the host
            host.Open();

            Console.WriteLine("Starting the server {0}", uri.ToString());
            Console.WriteLine("Please enter 'return key' if you want to stop the server");
            Console.ReadLine();
            
            // Terminate the server
            host.Abort();
            host.Close();
        }
    }
}
