using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Fabric;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using Autofac;
using Autofac.Features.OwnedInstances;
using BusinessRules.POC.Interfaces;
using DCT.ILR.Model;
using DCT.ILR.ValidationService.LearnerActor.Interfaces;
using DCT.ILR.ValidationService.Models;
using DCT.ILR.ValidationService.Models.Interfaces;
using DCT.ILR.ValidationService.Models.JsonSerialization;
using DCT.ILR.ValidationService.Models.Models;
using DCT.ILR.ValidationServiceStateless.Extensions;
using DCT.ILR.ValidationServiceStateless.Listeners;
using DCT.ILR.ValidationServiceStateless.ServiceBus;
using DCT.ValidationService.Service.Interface;
using ESFA.DC.Logging;
using Hydra.Core.Sharding;
using Microsoft.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.ServiceBus.Messaging;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Client;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Client;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Client;
using Microsoft.ServiceFabric.Services.Runtime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;

namespace DCT.ILR.ValidationServiceStateless
{
    /// <summary>
    /// An instance of this class is created for each service instance by the Service Fabric runtime.
    /// </summary>
    public class ValidationServiceStateless : StatelessService, IValidationServiceStateless, IDisposable
    {
        private ILifetimeScope _parentLifeTimeScope;
        private string _serviceBusConnectionString;
        private string _queueName;
//        private IValidationServiceResults _validationResultsService;
//        private IValidationService _validationService;
//        private ILogger _logger;
        private ITopicHelper _topicHelper;
        private string _fundingCalcSqlFilterValue;
        private Func<IValidationService> _validationServiceFactory;
        private ServiceProxyFactory _valResultsServiceProxyFactory;
        private IActorsHelper _actorsHelper;


        public ValidationServiceStateless(StatelessServiceContext context, ILifetimeScope parentLifeTimeScope,
            ILogger logger, ITopicHelper topicHelper, ServiceBusOptions seviceBusOptions, 
            Func<IValidationService> validationServiceFactory, IActorsHelper actorsHelper)
            : base(context)
        {
            _parentLifeTimeScope = parentLifeTimeScope;

            //get config values
            _queueName = seviceBusOptions.QueueName;
            _serviceBusConnectionString = seviceBusOptions.ServiceBusConnectionString;
            _fundingCalcSqlFilterValue = seviceBusOptions.FundingCalcSqlFilterValue;

            _actorsHelper = actorsHelper;
            //using serviceremoting v2
            _valResultsServiceProxyFactory = new ServiceProxyFactory(
                (c) => new FabricTransportServiceRemotingClientFactory(
                    remotingSettings: FabricTransportRemotingSettings.LoadFrom("DataTransportSettings"),
                    remotingCallbackMessageHandler: null, servicePartitionResolver: null, exceptionHandlers: null,
                    traceId: null,
                    serializationProvider: new ServiceRemotingJsonSerializationProvider()));
            
            
        

//            _logger = logger;
            _topicHelper = topicHelper;

            _validationServiceFactory = validationServiceFactory;
        }




        /// <summary>
        /// Optional override to create listeners (e.g., TCP, HTTP) for this service replica to handle client or user requests.
        /// </summary>
        /// <returns>A collection of listeners.</returns>
        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            yield return new ServiceInstanceListener(context => new ServiceBusQueueListeners(ProcessMessageHandler,
              _serviceBusConnectionString, _queueName, LoggerManager.CreateDefaultLogger()), "StatelessService-ServiceBusQueueListener");

            //return new ServiceInstanceListener[0];
        }

        /// <summary>
        /// This is the main entry point for your service instance.
        /// </summary>
        /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service instance.</param>
        //protected override async Task RunAsync(CancellationToken cancellationToken)
        //{
        //    // TODO: Replace the following sample code with your own logic 
        //    //       or remove this RunAsync override if it's not needed in your service.

        //    long iterations = 0;

        //    while (true)
        //    {
        //        cancellationToken.ThrowIfCancellationRequested();

        //        ServiceEventSource.Current.ServiceMessage(this.Context, "Working-{0}", ++iterations);

        //        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        //    }
        //}



        public async Task<bool> Validate(IlrContext ilrContext, IValidationService validationService)
        {
            using (var logger = ESFA.DC.Logging.LoggerManager.CreateDefaultLogger(ilrContext.CorrelationId.ToString()))
            {



                var startDateTime = DateTime.Now;
                logger.LogInfo($"Validation started for:{ilrContext.Filename} at :{startDateTime}");
                Message message = new Message();
                //try
                //{
                var stopwatch = new Stopwatch();

                stopwatch.Start();

                string xml;

                CloudStorageAccount cloudStorageAccount =
                    CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("StorageConnectionString"));



                var cloudStorageAccountElapsed = stopwatch.ElapsedMilliseconds;

                CloudBlobClient cloudBlobClient = cloudStorageAccount.CreateCloudBlobClient();

                var cloudBlobClientElapsed = stopwatch.ElapsedMilliseconds;

                CloudBlobContainer cloudBlobContainer =
                    cloudBlobClient.GetContainerReference(ilrContext.ContainerReference);

                var cloudBlobContainerElapsed = stopwatch.ElapsedMilliseconds;

                CloudBlockBlob cloudBlockBlob = cloudBlobContainer.GetBlockBlobReference(ilrContext.Filename);

                var cloudBlockBlobElapsed = stopwatch.ElapsedMilliseconds;

                xml = cloudBlockBlob.DownloadText();

                var blob = stopwatch.ElapsedMilliseconds;
                stopwatch.Restart();

                using (var reader = XmlReader.Create(new StringReader(xml)))
                {
                    var serializer = new XmlSerializer(typeof(Message));
                    message = serializer.Deserialize(reader) as Message;
                }

                var deserialize = stopwatch.ElapsedMilliseconds;
                stopwatch.Restart();
                IEnumerable<LearnerValidationError> results;
                var totalLearners = message.Learner.Count();

                if (ilrContext.IsShredAndProcess)
                {
                    // create actors here.
                    results = DivideAndConquer(ilrContext.CorrelationId, message, logger);
                }
                else
                {
                    results = await validationService.Validate(message);
                }

                var validate = stopwatch.ElapsedMilliseconds;
                var endTime = DateTime.Now;

                var processTimes = new List<string>()
                {
                    string.Format("Start Time : {0}", startDateTime),
                    string.Format("Learners : {0}", totalLearners),
                    string.Format("Errors : {0}", results.Count()),
                    string.Format("Blob Client : {0}", cloudBlobClientElapsed),
                    string.Format("Blob Container : {0}", cloudBlobContainerElapsed),
                    string.Format("Blob Block Blob : {0}", cloudBlockBlobElapsed),
                    string.Format("Blob Download Text : {0}", blob),
                    string.Format("Deserialize ms : {0}", deserialize),
                    string.Format("Validation ms : {0}", validate),
                    string.Format("End Time : {0}", endTime),
                    string.Format("Total Time : {0}", (endTime - startDateTime).TotalMilliseconds),

                };

                stopwatch.Restart();

                //store the results in reliable dictionary
                await SaveResultsInDataService(ilrContext.CorrelationId, processTimes, xml);

                logger.LogInfo("Stateless Validation Results:{@processTimes}", processTimes.ToArray(), "",
                    ilrContext.Filename);

                var saveResultsTime = stopwatch.ElapsedMilliseconds;
                logger.LogInfo($"saved Results to Db in: {saveResultsTime}");

                stopwatch.Restart();

                //send message topic to be picked up by next service

                dynamic data = new {To = _fundingCalcSqlFilterValue};

                var pubMessage =
                    new BrokeredMessage(new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(data))))
                    {
                        ContentType = "application/json",
                        Label = data.To,
                        CorrelationId = ilrContext.CorrelationId.ToString(),
                        MessageId = Guid.NewGuid().ToString(),
                        Properties =
                        {
                            {"To", data.To},
                            {"fileName", ilrContext.Filename}
                        },
                        TimeToLive = TimeSpan.FromMinutes(2)
                    };

                await _topicHelper.SendMessage(pubMessage);

                var pushedToTopicTime = stopwatch.ElapsedMilliseconds;
                logger.LogInfo($"pushed message into Topic in: {pushedToTopicTime} , {data}");

                return true;
            }
        }

        private IEnumerable<LearnerValidationError> DivideAndConquer(Guid correlationId, Message message, ILogger logger)
        {
            var actorTasks = new List<Task<string>>();
            //split the file

            var listOfShreddedLearners = message.Learner.ToList()
                .SplitList(_actorsHelper.GetLearnersPerActor(message.Learner.Count()));
            message.Learner = null; //work around until actual split logic

            foreach (var learnerShard in listOfShreddedLearners)
            {
                //get actor ref
                var actor = GetLearnerActor();
                actorTasks.Add(actor.Validate(correlationId.ToString(),message, learnerShard.ToArray()));
            }

            logger.LogInfo($"created {actorTasks.Count()} validation Actors");


            Task.WaitAll(actorTasks.ToArray());

            List<LearnerValidationError> results = new List<LearnerValidationError>();

            foreach (var actorTask in actorTasks)
            {
                results.AddRange(JsonConvert.DeserializeObject<IEnumerable<LearnerValidationError>>(actorTask.Result));
            }



            return results;




        }
        

        private async Task SaveResultsInDataService(Guid correlationId, IEnumerable<string> learnerValidationErrors, string xml)
        {
            using (var logger = ESFA.DC.Logging.LoggerManager.CreateDefaultLogger(correlationId.ToString()))
            {


                var stopWatch = new Stopwatch();
                stopWatch.Start();
                var xmlZipped = await xml.Zip();


                var validationResultsServiceProxy = GetValResultsProxy(correlationId.ToString(), false);

                var saveFullMessagetask = validationResultsServiceProxy.SaveFullMessage(correlationId.ToString(),
                    xmlZipped);

                logger.LogInfo($"Zipped full xml in : {stopWatch.ElapsedMilliseconds}");
                var saveResultsTask =
                    validationResultsServiceProxy.SaveResultsAsync(correlationId.ToString(), learnerValidationErrors);

                await Task.WhenAll(saveFullMessagetask, saveResultsTask);
            }
        }


        public async Task<IEnumerable<string>> GetResults(Guid correlationId)
        {
            var validationResultsServiceProxy = GetValResultsProxy(correlationId.ToString(), true);

            return await validationResultsServiceProxy.GetResultsAsync(correlationId.ToString());


        }

        /// <summary>
        /// This gets called in the Listerner, whenever there is a new message in servicebus queue it will be triggered.
        /// </summary>
        /// <param name="listernerModel"></param>
        /// <returns></returns>
        async Task ProcessMessageHandler(ServiceBusQueueListernerModel listernerModel)
        {
            //Debug.WriteLine(listernerModel.Message.Body);
            //create a childlifescope so that new objects are created per request.
//            using (var logger = ESFA.DC.Logging.LoggerManager.CreateDefaultLogger())
            using (var childLifeTimeScope = _parentLifeTimeScope.BeginLifetimeScope())
            {
                var logger = childLifeTimeScope.Resolve<ILogger>();

                try
                {
                    var ilrContext = JsonConvert.DeserializeObject<IlrContext>(Encoding.UTF8.GetString(listernerModel.Message.Body));

                    var validationService = childLifeTimeScope.Resolve<IValidationService>(); // this is workaround, 
                                                                                           //_validationService = _validationServiceFactory();
                    logger.StartContext(ilrContext.CorrelationId.ToString());
                    //_logger = LoggerManager.CreateDefaultLogger(ilrContext.CorrelationId.ToString());
                    await Validate(ilrContext, validationService);
                }
                catch (Exception ex)
                {
                    ServiceEventSource.Current.ServiceMessage(this.Context, "Exception-{0}", ex.ToString());
                    logger.LogError("Error while processing job", ex);
                    throw;
                }
                finally
                {
//                    logger.Dispose();
                }

            }
            //return Task<true>;
        }

        private IValidationServiceResults GetValResultsProxy(string correlationId, bool isRead)
        {
            //get the partition 
            var shardNumber = new JumpSharding().GetShard(correlationId, 10);

            return _valResultsServiceProxyFactory.CreateServiceProxy<IValidationServiceResults>(
                new Uri("fabric:/DCT.ILR.Processing.POC/DCT.ILR.Data"),
                new ServicePartitionKey(shardNumber),
                isRead ? TargetReplicaSelector.RandomReplica : TargetReplicaSelector.PrimaryReplica,
                "dataServiceRemotingListener");
        }

        private ILearnerActor GetLearnerActor()
        {
            return ActorProxy.Create<ILearnerActor>(ActorId.CreateRandom(),
                new Uri("fabric:/DCT.ILR.Processing.POC/LearnerActorService"));

        }




        protected override Task OnCloseAsync(CancellationToken cancellationToken)
        {
            _parentLifeTimeScope.Dispose();
            return base.OnCloseAsync(cancellationToken);
        }

        public void Dispose()
        {

        }


    }


}
