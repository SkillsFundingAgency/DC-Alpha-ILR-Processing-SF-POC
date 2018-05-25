using DCT.ILR.ValidationService.Models.Models;
using Microsoft.ServiceBus.Messaging;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DCT.ILR.ValidationService.Models.ServiceBus;

namespace DCT.ILR.Data.Listeners
{
    public class ServiceBusSubscriptionListener : ICommunicationListener, IDisposable
    {

        private SubscriptionClient _subscriptionClient;
        private Func<ServiceBusSubscriptionListenerModel, Task> _callback;
        private string _configSectionName = "ServiceBusSettings";
        private string _serviceBusConnectionStringName = "ServiceBusConnectionString";
        private readonly string _serviceBusConnectionString;
        private string _topicParameterName = "TopicName";
        private string _subscriptionParameterName = "ULNLoadDataSubscriptionName";

        public ServiceBusSubscriptionListener(Func<ServiceBusSubscriptionListenerModel, Task> callback, string subscriptionParameterNameInConfig)
        {
            _callback = callback;
            _serviceBusConnectionString = ConfigHelper.Get(_configSectionName, _serviceBusConnectionStringName);
            _subscriptionClient = SubscriptionClient.CreateFromConnectionString(
                    _serviceBusConnectionString,
                    ConfigHelper.Get(_configSectionName, _topicParameterName),
                    ConfigHelper.Get(_configSectionName, subscriptionParameterNameInConfig));
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
            // register the OnMessageAsync callback            
            _subscriptionClient.OnMessageAsync(
                async message =>
                {
                    using (var messageLock = new MessageLock(message))
                    {
                        //make sure that the message is correct
                        if (message.Label != null &&
                            message.ContentType != null &&
                            message.ContentType.Equals("application/json", StringComparison.InvariantCultureIgnoreCase))
                        {
                            //var body = message.GetBody<Stream>();

                            //dynamic messageBody = JsonConvert.DeserializeObject(new StreamReader(body, true).ReadToEnd());

                            try
                            {
                                //callback for processing the message
                                await _callback(new ServiceBusSubscriptionListenerModel()
                                {
                                    Message = message,
                                    token = cancellationToken
                                });
                            }
                            catch (Exception)
                            {
                                //TODO:log it here and throw and move message into dead-letter queue?                            
                                throw;
                            }

                        }

                        //complete the message after processing
                        await messageLock.CompleteAsync();
                    }
                },
                new OnMessageOptions { AutoComplete = false, MaxConcurrentCalls = 1 });

            // Return the uri - in this case, that's just our connection string
            return Task.FromResult(_serviceBusConnectionString);
        }


        private void Stop()
        {
            if (_subscriptionClient != null && !_subscriptionClient.IsClosed)
            {
                _subscriptionClient.CloseAsync();
                _subscriptionClient = null;
            }
        }

        public void Dispose()
        {

        }
    }

    public class ConfigHelper
    {
        public static string Get(string configSectionName, string key)
        {
            return FabricRuntime.GetActivationContext()
                            .GetConfigurationPackageObject("Config")
                            .Settings
                            .Sections[configSectionName]
                            .Parameters[key]
                            .Value;
        }

    }
}
