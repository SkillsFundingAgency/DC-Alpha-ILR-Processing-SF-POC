using System;
using System.Collections.Async;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Fabric;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Threading.Tasks;
using BusinessRules.POC.Interfaces;
using DCT.ILR.Data.Entities;
using DCT.ILR.Data.Listeners;
using DCT.ILR.ValidationService.Models.Interfaces;
using DCT.ILR.ValidationService.Models.JsonSerialization;
using DCT.ILR.ValidationService.Models.Models;
using DCT.LARS.Model;
using DCT.LARS.Model.Interface;
using DCT.ReferenceData.Model;
using Hydra.Core.Sharding;
using Microsoft.Extensions.Logging;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Client;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Communication.Wcf;
using Microsoft.ServiceFabric.Services.Communication.Wcf.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Client;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.V2.Wcf.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using Newtonsoft.Json;

namespace DCT.ILR.Data
{
    /// <summary>
    /// An instance of this class is created for each service replica by the Service Fabric runtime.
    /// </summary>    
    public class Data : StatefulService, IValidationServiceResults, IReferenceDataService, IFundingCalcResults
    {
        private ServiceProxyFactory _serviceProxyFactory;
        private long _maxMessageSize;

        public Data(StatefulServiceContext context)
            : base(context)
        {
            _serviceProxyFactory = new ServiceProxyFactory(
                (c) => new FabricTransportServiceRemotingClientFactory(
                    serializationProvider: new ServiceRemotingJsonSerializationProvider()));
           
        }

        public async Task<IEnumerable<string>> GetResultsAsync(string correlationId)
        {
            using (var logger = ESFA.DC.Logging.LoggerManager.CreateDefaultLogger())
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();

                var history =
                    await StateManager.GetOrAddAsync<IReliableDictionary<Guid, IEnumerable<string>>>("history");

                using (var tx = StateManager.CreateTransaction())
                {
                    var result = await history.TryGetValueAsync(tx, Guid.Parse(correlationId), LockMode.Update);

                    await tx.CommitAsync();
                    logger.LogInfo($"Completed GetResults from Partition:{this.Partition.PartitionInfo.Id} in : {stopwatch.ElapsedMilliseconds}");

                    return result.HasValue ? result.Value : null;
                }
            }
        }

        public async Task<IEnumerable<byte>> GetFullMessage(string correlationId)
        {
            using (var logger = ESFA.DC.Logging.LoggerManager.CreateDefaultLogger(correlationId))
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();

                var messageDictionary =
                    await StateManager.GetOrAddAsync<IReliableDictionary<Guid, IEnumerable<byte>>>("messageDictionary",
                        new TimeSpan(0, 0, 10));

                using (var tx = StateManager.CreateTransaction())
                {
                    var result =
                        await messageDictionary.TryGetValueAsync(tx, Guid.Parse(correlationId), LockMode.Update);

                    await tx.CommitAsync();
                    logger.LogInfo($"Completed GetResults from Partition:{this.Partition.PartitionInfo.Id} in : {stopwatch.ElapsedMilliseconds}");
                    return result.HasValue ? result.Value.AsEnumerable() : null;
                }


            }
        }

        public async Task SaveFullMessage(string correlationId, byte[] message)
        {
            //store the fullmessage in reliable dictionary
            using (var logger = ESFA.DC.Logging.LoggerManager.CreateDefaultLogger(correlationId))
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();

                var retryCount = 0;
                retry:
                try
                {
                    var messageDictionary =
                        await StateManager.GetOrAddAsync<IReliableDictionary<Guid, IEnumerable<byte>>>(
                            "messageDictionary",
                            new TimeSpan(0, 0, 10));

                    using (var tx = StateManager.CreateTransaction())
                    {
                        await messageDictionary.AddOrUpdateAsync(tx, Guid.Parse(correlationId), message,
                            (id, oldValue) => message);

                        await tx.CommitAsync();
                    }
                }
                catch (TimeoutException tex)
                {
                    logger.LogInfo($"Timeout exception while saving full message, retry count:{retryCount}");
                    await Task.Delay(100);
                    if (retryCount > 3) throw;
                    retryCount++;
                    goto retry;
                }

                logger.LogInfo($"Complted Save Full message from Partition:{this.Partition.PartitionInfo.Id} in: {stopwatch.ElapsedMilliseconds}");

            }
        }


        public async Task SaveResultsAsync(string correlationId, IEnumerable<string> learnerValidationErrors)
        {
            //store the results in reliable dictionary
            using (var logger = ESFA.DC.Logging.LoggerManager.CreateDefaultLogger(correlationId))
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();
                var history =
                    await StateManager.GetOrAddAsync<IReliableDictionary<Guid, IEnumerable<string>>>("history");
                var retryCount = 0;
                retry:
                try
                {
                    using (var tx = StateManager.CreateTransaction())
                    {
                        await history.AddOrUpdateAsync(tx, Guid.Parse(correlationId), learnerValidationErrors,
                            (id, oldValue) => learnerValidationErrors);

                        await tx.CommitAsync();
                    }
                }
                catch (TimeoutException tex)
                {
                    logger.LogInfo($"Timeout exception while saving results, retry count:{retryCount}");
                    await Task.Delay(100);
                    if (retryCount > 3) throw;
                    retryCount++;
                    goto retry;
                }

                logger.LogInfo($"Completed Save Results from Partition:{this.Partition.PartitionInfo.Id}  in : {stopwatch.ElapsedMilliseconds}");

            }
        }

        public async Task InsertLARSData()
        {
            using (var logger = ESFA.DC.Logging.LoggerManager.CreateDefaultLogger())
            {
                var stopwatch = new Stopwatch();

                var larsHelper = new LARSDataHelper();
                stopwatch.Start();
                //get data from DB.
                var larsDataDictionary = larsHelper.GetLearningDeliveries();
                var larsdataInMs = stopwatch.ElapsedMilliseconds;

                stopwatch.Restart();
                //add it into the reliabledictionary
                var larsLearningDeliveries = await StateManager.GetOrAddAsync<IReliableDictionary<string, string>>("LARS_LearningDelivery");

                using (var tx = StateManager.CreateTransaction())
                {
                    try
                    {
                        //clear existing values
                        await larsLearningDeliveries.ClearAsync();

                        foreach (var larsLd in larsDataDictionary)
                        {
                            await larsLearningDeliveries.TryAddAsync(tx, larsLd.Key, larsLd.Value);
                        }
                        await tx.CommitAsync();
                    }
                    catch (Exception ex)
                    {
                        tx.Abort();
                        logger.LogError("Error while saving into dic", ex);
                    }
                }
                stopwatch.Stop();
                var dataInsertionInMs = stopwatch.ElapsedMilliseconds;
                logger.LogInfo($"Lars retrieval: {larsdataInMs}");
                logger.LogInfo($"Data save in reliabledic: {dataInsertionInMs}");
            }
        }

        public async Task<IDictionary<string,string>> GetLARSLearningDeliveriesAsync(IEnumerable<string> learnAimRefs)
        {
            var stopwatch = new Stopwatch();
            var larsLearningDeliveries = await StateManager.GetOrAddAsync<IReliableDictionary<string, string>>("LARS_LearningDelivery");
            var list = new Dictionary<string, string>();
            using(var logger = ESFA.DC.Logging.LoggerManager.CreateDefaultLogger())
            using (var tx = StateManager.CreateTransaction())
            {
                try
                {
                    stopwatch.Start();
                    Microsoft.ServiceFabric.Data.IAsyncEnumerator<KeyValuePair<string, string>> enumerator =
                        (await larsLearningDeliveries.CreateEnumerableAsync(tx, (x)=> learnAimRefs.Contains(x), EnumerationMode.Ordered)).GetAsyncEnumerator();

                    while (await enumerator.MoveNextAsync(CancellationToken.None))
                    {
                        list.Add(enumerator.Current.Key, enumerator.Current.Value);
                    }

                    logger.LogInfo($"Retrieved LARS LDs in {stopwatch.ElapsedMilliseconds}");
                    await tx.CommitAsync();
                    return list;

                }
                catch (Exception ex)
                {
                    tx.Abort();
                    logger.LogError("Error while reading from dic", ex);
                    return null;
                }
            }
        }

        /// <summary>
        /// This method for testing the timigs for retrieving data from db
        /// </summary>
        /// <returns></returns>
        public Task<string> GetLARSFromDB(IEnumerable<string> learnAimRefs)
        {
            long larsdataInMs;
            string test = string.Empty;
            using (var logger = ESFA.DC.Logging.LoggerManager.CreateDefaultLogger())
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();
                var larsHelper = new LARSDataHelper();
                var larsDataDictionary = larsHelper.GetLearningDelivery(learnAimRefs);
                larsdataInMs = stopwatch.ElapsedMilliseconds;
                logger.LogInfo($"Lars retrieval from DB: {larsdataInMs}");
                logger.LogWarning($"Lars data: @larsDataDictionary.FirstOrDefault().Value", new object[] { larsDataDictionary.FirstOrDefault().Value });
            }
            return new Task<string>(() => test);

        }

        public async Task InsertULNs()
        {
            using (var logger = ESFA.DC.Logging.LoggerManager.CreateDefaultLogger())
            {
                var ulnv2Uri = new Uri("fabric:/DCT.ILR.Processing.POC/DCT.ILR.Data.ULNv2");

                //clear dictionary values first
                var clearULNTasks = new List<Task<bool>>();
                for (int i = 0; i < 10; i++)
                {
                    var ulnv2DataServiceProxy = _serviceProxyFactory.CreateServiceProxy<IULNv2DataService>(
                        ulnv2Uri,
                        new ServicePartitionKey(i), TargetReplicaSelector.PrimaryReplica,
                        "ULNdataServiceRemotingListener");

                    clearULNTasks.Add(ulnv2DataServiceProxy.ClearULNs());
                }

                await Task.WhenAll(clearULNTasks);
                logger.LogInfo($"Cleared all ULNs");


                var stopwatch = new Stopwatch();

                var ulnHelper = new ULNHelper();
                stopwatch.Start();
                //get data from DB.
                var ulnsData = ulnHelper.GetAllULNs();
                var ulndataInMs = stopwatch.ElapsedMilliseconds;

                stopwatch.Restart();

                //group by all the shards so that we can make single call for each shard
                var shardsWithUlnsDictionary = ulnsData.GroupBy(ulnKvp => ulnKvp.Value)
                    .ToDictionary(t => t.Key, t => t.Select(r => r.Key).ToList());

                await shardsWithUlnsDictionary.ParallelForEachAsync(async (ulnKvp) => 
                    //foreach (var ulnKvp in shardsWithUlnsDictionary)
                {

                    //ResolvedServicePartition partition = await ServicePartitionResolver.GetDefault()
                    //    .ResolveAsync(ulnv2Uri, new ServicePartitionKey(ulnKvp.Key), CancellationToken.None);
                    //ResolvedServiceEndpoint ep = partition.GetEndpoint();

                    //Insert to the correct shard based on the hash algorithm 
                    var ulnv2DataService = _serviceProxyFactory.CreateServiceProxy<IULNv2DataService>(
                        ulnv2Uri,
                        new ServicePartitionKey(ulnKvp.Key), TargetReplicaSelector.PrimaryReplica,
                        "ULNdataServiceRemotingListener");
                    //var size = GetObjectSize(ulnKvp.Value);
                    var ulnsShard = ulnKvp.Value;
                    //if the total records count is greater then 100000 then send it in chunks
                    if (ulnKvp.Value.Count > 1_000_000)
                    {
                        //var tasks = new List<Task>();
                        var totalCount = ulnsShard.Count;
                        var pageSize = 100000;
                        var page = 1;
                        var skip = 0;
                        while (skip < totalCount)
                        {
                            await ulnv2DataService.InsertULNs(ulnsShard.Skip(skip).Take(pageSize).ToList());
                            page++;
                            skip = pageSize * (page - 1);
                        }

                    }
                    else
                    {
                        await ulnv2DataService.InsertULNs(ulnsShard);
                    }

                });

                var ulnSavedInMs = stopwatch.ElapsedMilliseconds;
                logger.LogInfo($"Retrieved ULNs from DB in: {ulndataInMs}");
                logger.LogInfo($"Inserted ULNs into SF in: {ulnSavedInMs}");

            }

        }

        private int GetObjectSize(object TestObject)
        {
            BinaryFormatter bf = new BinaryFormatter();
            MemoryStream ms = new MemoryStream();
            byte[] Array;
            bf.Serialize(ms, TestObject);
            Array = ms.ToArray();
            return Array.Length;
        }

        public async Task<IEnumerable<long>> GetULNs(IEnumerable<long> ulns)
        {
            using (var logger = ESFA.DC.Logging.LoggerManager.CreateDefaultLogger())
            {
                var finalUlns = new ConcurrentBag<long>();
                var stopwatch = new Stopwatch();
                stopwatch.Start();
                var shardsWithUlnsDictionary = new ConcurrentDictionary<int, List<long>>();
                var shardsWithUlnsDicLock = new object();
                //find the correct shard for the Ulns and create the serviceproxy to retreive it
                Parallel.ForEach(ulns, (uln) =>
                {
                    var shardNumber = new JumpSharding().GetShard(uln.ToString(), 10);
                    shardsWithUlnsDictionary.TryGetValue(shardNumber, out List<long> ulnList);
                    lock (shardsWithUlnsDicLock)
                    {
                        ulnList = ulnList ?? new List<long>();
                        ulnList.Add(uln);
                        shardsWithUlnsDictionary[shardNumber] = ulnList;
                    }
                });

                //call the correct shard to retrive the data.
               //wait shardsWithUlnsDictionary.ParallelForEachAsync( async (ulnKvp) => {
                var getULNTasks = new List<Task<IEnumerable<long>>>();
                foreach (var ulnKvp in shardsWithUlnsDictionary)
                { 
                    var ulnv2Uri = new Uri("fabric:/DCT.ILR.Processing.POC/DCT.ILR.Data.ULNv2");                 
                    //Insert to the correct shard based on the hash algorithm 
                    var ulnv2DataService = _serviceProxyFactory.CreateServiceProxy<IULNv2DataService>(ulnv2Uri,
                        new ServicePartitionKey(ulnKvp.Key), TargetReplicaSelector.RandomReplica,
                        "ULNdataServiceRemotingListener");

                    getULNTasks.Add(ulnv2DataService.GetULNs(ulnKvp.Value));
                };

                await Task.WhenAll(getULNTasks);
                
                //loop through the results and add it to the final list
                foreach (var ulnTask in getULNTasks)
                {
                    foreach (var validUln in ulnTask.Result)
                    {
                        finalUlns.Add(validUln);
                    }
                }


                var ulnRetrievedInMs = stopwatch.ElapsedMilliseconds;
                logger.LogInfo(
                    $"Retrieved ULNs:Requested ULNs:{ulns.Count()}, retrieved ULNs: {finalUlns.Count} from SF in: {ulnRetrievedInMs}");
                return finalUlns;

            }
        }



        //private async Task<IList<KeyValuePair<Tkey, T>>> QueryReliableDictionary<Tkey, T>(IReliableStateManager stateManager, string collectionName, Func<T, bool> filter)
        //{
        //    var result = new List<KeyValuePair<Tkey, T>>();

        //    var reliableDictionary =
        //        await stateManager.GetOrAddAsync<IReliableDictionary<Tkey, T>>(collectionName);

        //    using (ITransaction tx = stateManager.CreateTransaction())
        //    {
        //        IAsyncEnumerable<KeyValuePair<Tkey, T>> asyncEnumerable = await reliableDictionary.CreateEnumerableAsync(tx);
        //        using (IAsyncEnumerator<KeyValuePair<Tkey, T>> asyncEnumerator = asyncEnumerable.GetAsyncEnumerator())
        //        {
        //            while (await asyncEnumerator.MoveNextAsync(CancellationToken.None))
        //            {
        //                if (filter(asyncEnumerator.Current.Value))
        //                    result.Add(asyncEnumerator.Current);
        //            }
        //        }
        //    }
        //    return result;
        //}

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

                var listenerSettings = new FabricTransportRemotingListenerSettings
                {
                    MaxMessageSize = _maxMessageSize // 1GB
                };

                return new[]
                {



                    new ServiceReplicaListener((context) =>
                        new WcfCommunicationListener<IReferenceDataService>(
                            wcfServiceObject: this,
                            serviceContext: context,
                            //
                            // The name of the endpoint configured in the ServiceManifest under the Endpoints section
                            // that identifies the endpoint that the WCF ServiceHost should listen on.
                            //
                            endpointResourceName: "WcfDataServiceEndpoint",

                            //
                            // Populate the binding information that you want the service to use.
                            //
                            listenerBinding: WcfUtility.CreateTcpListenerBinding()
                        ), "dataServiceWCFListener"),
                    new ServiceReplicaListener(
                        (c) => new FabricTransportServiceRemotingListener(c, this,
                            FabricTransportRemotingListenerSettings.LoadFrom("DataTransportSettings"),
                            new ServiceRemotingJsonSerializationProvider()),
                        "dataServiceRemotingListener", true)
//                     new ServiceReplicaListener(context =>
//                         new ServiceBusSubscriptionListener(ProcessLoadULNMessageHandler, "ULNLoadDataSubscriptionName"), "Stateless-ServiceBusFundingCalcSubsListener")
//                     new ServiceReplicaListener(context =>
//                         new ServiceBusSubscriptionListener(ProcessGetULNsTestMessageHandler, "ULNGetTestSubscriptionName"), "Stateless-ULNGetTestListener")
                };

            }
            catch (Exception)
            {

                throw;
            }
        }

        /// <summary>
        /// This gets called in the Listerner, whenever there is a new message in servicebus queue it will be triggered.
        /// </summary>
        /// <param name="listernerModel"></param>
        /// <returns></returns>
        async Task ProcessLoadULNMessageHandler(ServiceBusSubscriptionListenerModel listernerModel)
        {
            using (var logger = ESFA.DC.Logging.LoggerManager.CreateDefaultLogger(listernerModel.Message.CorrelationId))
            {
                try
                {
                    var body = listernerModel.Message.GetBody<Stream>();
                    dynamic messageBody = JsonConvert.DeserializeObject(new StreamReader(body, true).ReadToEnd());
                                       
                    logger.LogInfo($"Started ULN load data for jobId:{listernerModel.Message.CorrelationId} ");
                    //await InsertLARSData();
                    await InsertULNs();
                    logger.LogInfo($"Completed Uln load data for jobId:{listernerModel.Message.CorrelationId} ");
                    await Task.Run(() => Console.WriteLine("done"));

                }
                catch (Exception ex)
                {
                    ServiceEventSource.Current.ServiceMessage(this.Context, "Exception-{0}", ex.ToString());
                    logger.LogError("Error while processing loading ULN job", ex);
                    throw;
                }
                finally
                {
                    logger.Dispose();
                }

            }
        }

        async Task ProcessGetULNsTestMessageHandler(ServiceBusSubscriptionListenerModel listernerModel)
        {
            using (var logger = ESFA.DC.Logging.LoggerManager.CreateDefaultLogger(listernerModel.Message.CorrelationId))
            {
                try
                {
                    var body = listernerModel.Message.GetBody<Stream>();
                    dynamic messageBody = JsonConvert.DeserializeObject(new StreamReader(body, true).ReadToEnd());

                    logger.LogInfo($"Started ULN Read data for jobId:{listernerModel.Message.CorrelationId} ");
                    await GetULNs(messageBody.ULNs);
                    logger.LogInfo($"Completed Uln Read data for jobId:{listernerModel.Message.CorrelationId} ");
                    await Task.Run(() => Console.WriteLine("done"));

                }
                catch (Exception ex)
                {
                    ServiceEventSource.Current.ServiceMessage(this.Context, "Exception-{0}", ex.ToString());
                    logger.LogError("Error while processing loading ULN job", ex);
                    throw;
                }
                finally
                {
                    logger.Dispose();
                }

            }
        }


        public async Task SaveFCResultsAsync(string correlationId, IEnumerable<string> fundingCalcResults)
        {
            var fcResults = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, IEnumerable<string>>>("fcResults");
            var retryCount = 0;
            retry:
            try
            {
                using (var tx = StateManager.CreateTransaction())
                {
                    await fcResults.AddOrUpdateAsync(tx, Guid.Parse(correlationId), fundingCalcResults, (id, oldValue) => fundingCalcResults);

                    await tx.CommitAsync();
                }
            }
            catch (TimeoutException tex)
            {
                await Task.Delay(100);
                retryCount++;
                if (retryCount > 3) throw;
                goto retry;
            }
        }

        public async Task<IEnumerable<string>> GetFCResultsAsync(string correlationId)
        {
            var fcResults = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, IEnumerable<string>>>("fcResults");

            using (var tx = StateManager.CreateTransaction())
            {
                var result = await fcResults.TryGetValueAsync(tx, Guid.Parse(correlationId), LockMode.Update);

                await tx.CommitAsync();
                return result.HasValue ? result.Value : null;
            }
        }
    }
}
