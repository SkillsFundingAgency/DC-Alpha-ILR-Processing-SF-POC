
using Microsoft.ServiceBus.Messaging;
using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DCT.ILR.ValidationService.Models.Models;

namespace DCT.ILR.ValidationServiceStateless.ServiceBus
{
    public class TopicHelper : ITopicHelper
    {
        private readonly string _topicName;
        private readonly string _serviceBusConnectionString;

        public TopicHelper(ServiceBusOptions serviceBusOptions)
        {
            _serviceBusConnectionString = serviceBusOptions.ServiceBusConnectionString;
            _topicName = serviceBusOptions.TopicName;
        }

        public async Task SendMessage(BrokeredMessage message)
        {
            var topicClient = TopicClient.CreateFromConnectionString(
                   _serviceBusConnectionString,
                  _topicName);

            await topicClient.SendAsync(message);
            await topicClient.CloseAsync();
        }


    }

//    public class ConfigHelper
//    {
//        public static string Get(string configSectionName, string key)
//        {
//            return FabricRuntime.GetActivationContext()
//                            .GetConfigurationPackageObject("Config")
//                            .Settings
//                            .Sections[configSectionName]
//                            .Parameters[key]
//                            .Value;
//        }
//
//    }
}
