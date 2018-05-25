using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;

namespace DCT.ILR.ValidationService.TestWebClient.ServiceBus
{
    public class ServiceBusQueue : IServiceBusQueue
    {
        private string ServiceBusConnectionString;
        private string QueueName;
        public ServiceBusQueue(ConfigurationPackage configurationPackage)
        {
            QueueName = configurationPackage.Settings.Sections["ServiceBusQueue"].Parameters["QueueName"].Value; ;
            ServiceBusConnectionString = configurationPackage.Settings.Sections["ServiceBusQueue"].Parameters["ServiceBusConnectionString"].Value; ;
        }
        public async Task SendMessagesAsync(string messageToSend, string sessionId)
        {
            try
            {
                               

                var queueClient = new QueueClient(ServiceBusConnectionString, QueueName);

                                 
                    var message = new Message(Encoding.UTF8.GetBytes(messageToSend));
                message.SessionId = sessionId;
                

                    // Write the body of the message to the console.
                    Console.WriteLine($"Sending message: {messageToSend}");

                    // Send the message to the queue.
                    await queueClient.SendAsync(message);

                await queueClient.CloseAsync();
              
            }
            catch (Exception exception)
            {
                Console.WriteLine($"{DateTime.Now} :: Exception: {exception.Message}");
                throw;
            }
        }
    }
}
