using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Fabric;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DCT.ILR.Model;
using DCT.ILR.ValidationService.Models;
using DCT.ILR.ValidationService.Models.Models;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.Azure;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Xml;
using System.IO;
using System.Xml.Serialization;
using DCT.ILR.VadationServiceStateful.Extensions;
using DCT.ValidationService.Service.Interface;
using Microsoft.ServiceFabric.Services.Communication.Wcf.Runtime;
using Microsoft.ServiceFabric.Services.Communication.Wcf;
using DCT.ILR.ValidationService.LearnerActor.Interfaces;
using Microsoft.ServiceFabric.Actors.Client;
using Microsoft.ServiceFabric.Actors;
using BusinessRules.POC.Interfaces;
using Autofac;
using Newtonsoft.Json;
using DCT.ILR.VadationServiceStateful.Listeners;
using System.Text;

namespace DCT.ILR.VadationServiceStateful
{



    /// <summary>
    /// An instance of this class is created for each service replica by the Service Fabric runtime.
    /// </summary>
    public class VadationServiceStateful : StatefulService, IValidationServiceStateful, IDisposable
    {
       
        private ILifetimeScope _parentLifeTimeScope;
        private string _serviceBusConnectionString;
        private string _queueName;

        public VadationServiceStateful(StatefulServiceContext context, ILifetimeScope parentLifeTimeScope)
            : base(context)
        {
            _parentLifeTimeScope = parentLifeTimeScope;
            _serviceBusConnectionString = CloudConfigurationManager.GetSetting("ServiceBusConnectionString");
            _queueName = CloudConfigurationManager.GetSetting("QueueName");



        }

        public async Task<List<string>> GetResults(Guid correlationId)
        {
            var history = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, List<string>>>("history");

            using (var tx = StateManager.CreateTransaction())
            {
                var result = await history.TryGetValueAsync(tx, correlationId);

                await tx.CommitAsync();
                return  result.HasValue ? result.Value: null;
            }

        }

        public async Task<bool> Validate(IlrContext ilrContext)
        {
            using (var childLifeTimeScope = _parentLifeTimeScope.BeginLifetimeScope("childLifeTimeScope"))
            {


                var validationService = childLifeTimeScope.Resolve<IValidationService>();

                var startDateTime = DateTime.Now;


                Message message = new Message();
                //try
                //{
                var stopwatch = new Stopwatch();

                stopwatch.Start();

                string xml;

                CloudStorageAccount cloudStorageAccount = CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("StorageConnectionString"));



                var cloudStorageAccountElapsed = stopwatch.ElapsedMilliseconds;

                CloudBlobClient cloudBlobClient = cloudStorageAccount.CreateCloudBlobClient();

                var cloudBlobClientElapsed = stopwatch.ElapsedMilliseconds;

                CloudBlobContainer cloudBlobContainer = cloudBlobClient.GetContainerReference(ilrContext.ContainerReference);

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

                if (ilrContext.IsShredAndProcess)
                {
                    // create actors here.
                    results = DivideAndConquer(message);
                }
                else
                {
                    results = validationService.Validate(message);
                }

                var validate = stopwatch.ElapsedMilliseconds;

                var processTimes = new List<string>()
            {
                string.Format("Start Time : {0}", startDateTime),
                string.Format("Errors : {0}", results.Count()),
                string.Format("Blob Client : {0}", cloudBlobClientElapsed),
                string.Format("Blob Container : {0}", cloudBlobContainerElapsed),
                string.Format("Blob Block Blob : {0}", cloudBlockBlobElapsed),
                string.Format("Blob Download Text : {0}", blob),
                string.Format("Deserialize ms : {0}", deserialize),
                string.Format("Validation ms : {0}", validate)
            };

                ServiceEventSource.Current.ServiceMessage(this.Context, "result-{0}", processTimes);

                //store the results in reliable dictionary
                var history = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, List<string>>>("history");

                using (var tx = StateManager.CreateTransaction())
                {
                    await history.AddOrUpdateAsync(tx, ilrContext.CorrelationId, processTimes,(id,oldValue) => processTimes);

                    await tx.CommitAsync();
                }
                return true;
            }
        }

        private IEnumerable<LearnerValidationError> DivideAndConquer(Message message)
        {
            var actorTasks = new List<Task<string>>();
            //split the file
            var listOfShreddedLearners = message.Learner.ToList().SplitList(4000);
            message.Learner = null; //work around until actual split logic
            foreach (var learnerShard in listOfShreddedLearners)
            {
                //get actor ref
                var actor = GetLearnerActor(learnerShard.FirstOrDefault().LearnRefNumber);
               // actorTasks.Add(actor.Validate(message, learnerShard.ToArray()));
            }

            Task.WaitAll(actorTasks.ToArray());

            List<LearnerValidationError> results = new List<LearnerValidationError>();
                        
            foreach (var actorTask in actorTasks)
            {                
                results.AddRange(JsonConvert.DeserializeObject<IEnumerable<LearnerValidationError>>(actorTask.Result));
            }

            return results;




        }

        /// <summary>
        /// Optional override to create listeners (e.g., HTTP, Service Remoting, WCF, etc.) for this service replica to handle client or user requests.
        /// </summary>
        /// <remarks>
        /// For more information on service communication, see https://aka.ms/servicefabricservicecommunication
        /// </remarks>
        /// <returns>A collection of listeners.</returns>
        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
        {

            try
            {
                return this.CreateServiceRemotingReplicaListeners();
            }
            catch (Exception)
            {

                throw;
             }

                //yield return new ServiceReplicaListener(context => new ServiceBusQueueListeners(ProcessMessageHandler,
                //    _serviceBusConnectionString, _queueName), "StatelfulService-ServiceBusQueueListener");


                //return new[] {


                //    new ServiceReplicaListener((context) =>
                //        new WcfCommunicationListener<IValidationServiceStateful>(
                //            wcfServiceObject:this,
                //            serviceContext:context,
                //            //
                //            // The name of the endpoint configured in the ServiceManifest under the Endpoints section
                //            // that identifies the endpoint that the WCF ServiceHost should listen on.
                //            //
                //            endpointResourceName: "WcfServiceEndpoint",

                //            //
                //            // Populate the binding information that you want the service to use.
                //            //
                //            listenerBinding: WcfUtility.CreateTcpListenerBinding()
                //        )
                //)};



            }
     

        async Task ProcessMessageHandler(ServiceBusQueueListernerModel listernerModel)
        {
            Debug.WriteLine(listernerModel.Message.Body);
            var ilrContext = JsonConvert.DeserializeObject<IlrContext>(Encoding.UTF8.GetString(listernerModel.Message.Body));
            try
            {
                await Validate(ilrContext);
            }
            catch (Exception ex)
            {
                ServiceEventSource.Current.ServiceMessage(this.Context, "Exception-{0}", ex.ToString());
                throw;
            }
           

            //return Task<true>;
        } 

        protected override Task RunAsync(CancellationToken cancellationToken)
        {
            return base.RunAsync(cancellationToken);
        }

        private ILearnerActor GetLearnerActor(string userId)
        {
            return ActorProxy.Create<ILearnerActor>(new ActorId(userId), 
                new Uri("fabric:/DCT.ILR.Processing.POC/LearnerActorService"));
        }

        public void Dispose()
        {
            
        }

        protected override Task OnCloseAsync(CancellationToken cancellationToken)
        {
            _parentLifeTimeScope.Dispose();
            return base.OnCloseAsync(cancellationToken);    
        }
    }

    
}
