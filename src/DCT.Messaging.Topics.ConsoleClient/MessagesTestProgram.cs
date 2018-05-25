using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DCT.Messaging.Topics.ConsoleClient
{
    public class MessagesTestProgram
    {
        private  NamespaceManager _namespaceClient;
        private  TopicDescription _dctMessagingTopic;
        private string _namespace = "dct-ilrprocessing-sb-poc"; //dc-ilrproc-dev
//        private string _namespace = "dc-ilrproc-dev"; //
        private string _topicName = "dct-messaging-topic";
        private string _validationSubscriptionName = "Validation-dev";
        private string _fundingCalcSubscriptionName = "FundingCalc-dev";
        private string _ULNDataLoadSubscriptionName = "ULNDataLoad";
        private string _validationSqlFilterValue = "validation";
        private string _fundingCalcSqlFilterValue = "fundingcalc";
        private string _ulnDataSqlFilterValue = "ulndataload";

        public async Task Run()
        {
            Console.WriteLine("Press Enter to continue...");
            var value = Console.ReadKey();
            try
            {
                ServiceBusManagementCredentials();
                CreateTopic();
                CreateSubscriptions();
                

                ReceiveMessages(SubscriptionClient.CreateFromConnectionString(
                    ConfigurationManager.AppSettings["serviceBusConnectionString"], _topicName, _fundingCalcSubscriptionName), ConsoleColor.DarkGreen);

                await PublishMessages();

               // ReceiveMessages(SubscriptionClient.CreateFromConnectionString(
               //   ConfigurationManager.AppSettings["serviceBusConnectionString"], _topicName, _ULNDataLoadSubscriptionName), ConsoleColor.Red);

               // ReceiveMessages(SubscriptionClient.CreateFromConnectionString(
              //   ConfigurationManager.AppSettings["serviceBusConnectionString"], _topicName, _ULNDataLoadSubscriptionName), ConsoleColor.Blue);


                await Task.WhenAny(
                      Task.Run(() => Console.ReadKey()),
                      Task.Delay(TimeSpan.FromSeconds(10))
                  );

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                Console.ReadKey();
            }
        }


        void ReceiveMessages(SubscriptionClient receiver, ConsoleColor color)
        {
            // register the OnMessageAsync callback
            receiver.OnMessageAsync(
                async message =>
                {
                    if (message.Label != null &&
                        message.ContentType != null &&
                                               message.ContentType.Equals("application/json", StringComparison.InvariantCultureIgnoreCase))
                    {
                        var body = message.GetBody<Stream>();

                        dynamic messageBody = JsonConvert.DeserializeObject(new StreamReader(body, true).ReadToEnd());

                        lock (Console.Out)
                        {
                            Console.ForegroundColor = color;
                            Console.WriteLine(
                                "\t\t\t\tMessage received: \n\t\t\t\t\t\tMessageId = {0}, \n\t\t\t\t\t\tSequenceNumber = {1}, \n\t\t\t\t\t\tEnqueuedTimeUtc = {2}," +
                                "\n\t\t\t\t\t\tExpiresAtUtc = {5}, \n\t\t\t\t\t\tContentType = \"{3}\", \n\t\t\t\t\t\tSize = {4},  \n\t\t\t\t\t\tContent: [ firstName = {6}, name = {7} ]",
                                message.MessageId,
                                message.SequenceNumber,
                                message.EnqueuedTimeUtc,
                                message.ContentType,
                                message.Size,
                                message.ExpiresAtUtc,
                                messageBody.To,
                                messageBody.firstName);
                            Console.ResetColor();
                        }
                    }
                    await message.CompleteAsync();
                },
                new OnMessageOptions { AutoComplete = false, MaxConcurrentCalls = 1 });
        }

        private async Task PublishMessages()
        {
            
            var sendClient = TopicClient.CreateFromConnectionString(ConfigurationManager.AppSettings["serviceBusConnectionString"], _topicName);
            dynamic data = new[]
            {
                new {To = _fundingCalcSqlFilterValue, firstName = "Albert" },
                new {To = _ulnDataSqlFilterValue, firstName = "Werner"},
                //new {To = _fundingCalcSqlFilterValue, firstName = "Marie"},
                //new {To = _ulnDataSqlFilterValue, firstName = "Niels"},
                //new {To = _fundingCalcSqlFilterValue, firstName = "Michael"},
                //new {To = _ulnDataSqlFilterValue, firstName = "Galileo"},
                //new {To = _fundingCalcSqlFilterValue, firstName = "Johannes"},
                //new {To = _ulnDataSqlFilterValue, firstName = "Nikolaus"}
            };


            for (int i = 0; i < data.Length; i++)
            {

                var message = new BrokeredMessage(new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(data[i]))))
                {
                    ContentType = "application/json",
                    Label = data[i].To,
                    CorrelationId = Guid.NewGuid().ToString(),
                    MessageId = i.ToString(),
                    Properties =
                    {
                        { "To",  data[i].To },
                        { "firstName",  data[i].firstName }
                    },
                    TimeToLive = TimeSpan.FromMinutes(2)
                };

                await sendClient.SendAsync(message);

                lock (Console.Out)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Message sent: Id = {0}", message.MessageId);
                    Console.ResetColor();
                }
            }

        }

        private void CreateSubscriptions()
        {
            if (!_namespaceClient.SubscriptionExists(_dctMessagingTopic.Path, _fundingCalcSubscriptionName))
            {
                SubscriptionDescription validationSubscription =
                    _namespaceClient.CreateSubscription(_dctMessagingTopic.Path, _fundingCalcSubscriptionName, new SqlFilter($"To = '{_fundingCalcSqlFilterValue}'"));
            }

            if (!_namespaceClient.SubscriptionExists(_dctMessagingTopic.Path, _ULNDataLoadSubscriptionName)) 
            {
                SubscriptionDescription frontendSubscription =
                    _namespaceClient.CreateSubscription(_dctMessagingTopic.Path, _ULNDataLoadSubscriptionName, new SqlFilter($"To = '{_ulnDataSqlFilterValue}'"));
            }
        }

        private void CreateTopic()
        {
            if (_namespaceClient.TopicExists(_topicName))
            {
                _dctMessagingTopic = _namespaceClient.GetTopic(_topicName);
            }
            else
            {
                var td = new TopicDescription(_topicName);
                td.MaxSizeInMegabytes = 100;
                td.DefaultMessageTimeToLive = new TimeSpan(1, 0, 0);
                _dctMessagingTopic = _namespaceClient.CreateTopic(_topicName);
            }
        }

        void ServiceBusManagementCredentials()
        {
            // Create management credentials
            TokenProvider credentials = TokenProvider
                .CreateSharedAccessSignatureTokenProvider("RootManageSharedAccessKey", ConfigurationManager.AppSettings["sasKeyValue"]);
            // Create namespace client
            _namespaceClient = new NamespaceManager(
                ServiceBusEnvironment.CreateServiceUri("sb", _namespace, string.Empty), credentials);
        }
    }
}
