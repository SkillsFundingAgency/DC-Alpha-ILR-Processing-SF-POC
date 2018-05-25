using Microsoft.ServiceFabric.Services.Communication.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DCT.ILR.ValidationService.Models.ServiceBus;
using Microsoft.Azure.ServiceBus;
    using ESFA.DC.Logging;

namespace DCT.ILR.ValidationServiceStateless.Listeners
{
    public class ServiceBusQueueListeners : ICommunicationListener
    {        
        private string _serviceBusConnectionString;
        private IQueueClient _queueClient;
        private Func<ServiceBusQueueListernerModel,Task> _callback;
        private ESFA.DC.Logging.ILogger _logger;
        public ServiceBusQueueListeners(Func<ServiceBusQueueListernerModel, Task> callback, string serviceBusConnectionString, string queueName, ILogger logger)
        {
            _callback = callback;
            _serviceBusConnectionString = serviceBusConnectionString;
            var retryPolicy = new RetryExponential(TimeSpan.Zero, TimeSpan.FromMinutes(5), 3);

            _queueClient = new QueueClient(serviceBusConnectionString, queueName, ReceiveMode.PeekLock, retryPolicy);
            _logger = logger;

        }

        public void Abort()
        {
            Stop();
        }

        public Task CloseAsync(CancellationToken cancellationToken)
        {
            Stop();
            return Task.FromResult(true);
        }

        public Task<string> OpenAsync(CancellationToken cancellationToken)
        {
            // Configure the message handler options in terms of exception handling, number of concurrent messages to deliver, etc.
            var sessionHandlerOptions = new SessionHandlerOptions(ExceptionReceivedHandler)
            {
//                MaxConcurrentSessions  = 8,                
                // Maximum number of concurrent calls to the callback ProcessMessagesAsync(), set to 1 for simplicity.
                // Set it according to how many messages the application wants to process in parallel.
               // MaxConcurrentCalls = 1,

                // Indicates whether the message pump should automatically complete the messages after returning from user callback.
                // False below indicates the complete operation is handled by the user callback as in ProcessMessagesAsync().
                AutoComplete = false
            };

            // Register the function that processes messages.
            _queueClient.RegisterSessionHandler(ProcessMessagesAsync, sessionHandlerOptions);

            // Return the uri - in this case, that's just our connection string
            return Task.FromResult(_serviceBusConnectionString);
        }


        async Task ProcessMessagesAsync(IMessageSession session, Message message, CancellationToken token)
        {

            _logger.LogInfo($"Received message: SequenceNumber:{message.SystemProperties.SequenceNumber} Body:{Encoding.UTF8.GetString(message.Body)}");

            // Process the message.
            Console.WriteLine(
                $"Received message: SequenceNumber:{message.SystemProperties.SequenceNumber} Body:{Encoding.UTF8.GetString(message.Body)}");

            //call handler
            await _callback(new ServiceBusQueueListernerModel()
            {
                Message = message,
                MessageSession = session,
                token = token
            });


            // Complete the message so that it is not received again.
            // This can be done only if the queue Client is created in ReceiveMode.PeekLock mode (which is the default).
            await session.CompleteAsync(message.SystemProperties.LockToken);

            // Note: Use the cancellationToken passed as necessary to determine if the queueClient has already been closed.
            // If queueClient has already been closed, you can choose to not call CompleteAsync() or AbandonAsync() etc.
            // to avoid unnecessary exceptions.

        }

        // Use this handler to examine the exceptions received on the message pump.
        private Task ExceptionReceivedHandler(ExceptionReceivedEventArgs exceptionReceivedEventArgs)
        {
            Console.WriteLine($"Message handler encountered an exception {exceptionReceivedEventArgs.Exception}.");
            var context = exceptionReceivedEventArgs.ExceptionReceivedContext;
            Console.WriteLine("Exception context for troubleshooting:");
            Console.WriteLine($"- Endpoint: {context.Endpoint}");
            Console.WriteLine($"- Entity Path: {context.EntityPath}");
            Console.WriteLine($"- Executing Action: {context.Action}");
            return Task.CompletedTask;
        }

        private void Stop()
        {
            if (_queueClient != null && !_queueClient.IsClosedOrClosing )
            {
                _queueClient.CloseAsync();
                _queueClient = null;
            }
        }
    }

    public class ServiceBusQueueListernerModel
    {
        public Microsoft.Azure.ServiceBus.IMessageSession MessageSession { get; set; }
        public Microsoft.Azure.ServiceBus.Message Message { get; set; }
        public CancellationToken token { get; set; }

    }
}
