using Microsoft.ServiceBus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using Microsoft.ServiceBus.Messaging;
using System.IO;
using Newtonsoft.Json;

namespace DCT.Messaging.Topics.ConsoleClient
{
    class Program
    {
       


        static void Main(string[] args)
        {
            Task.Run(async () =>
            {
                var app = new MessagesTestProgram();
                await app.Run();
            }).GetAwaiter().GetResult();

            Console.ReadLine();            
        }


        


        

    }
}
