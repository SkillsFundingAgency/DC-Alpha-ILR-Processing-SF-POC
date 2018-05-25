using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Fabric;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DCT.ILR.Data.ULNv2.Entities;
using DCT.ILR.ValidationService.Models.Interfaces;
using DCT.ILR.ValidationService.Models.JsonSerialization;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Communication.Wcf;
using Microsoft.ServiceFabric.Services.Communication.Wcf.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;

namespace DCT.ILR.Data.ULNv2
{
    /// <summary>
    /// An instance of this class is created for each service replica by the Service Fabric runtime.
    /// </summary>
    internal sealed class ULNv2 : StatefulService, IULNv2DataService
    {
        public ULNv2(StatefulServiceContext context)
            : base(context)
        { }


        public async Task InsertULNs(IEnumerable<long> ulns)
        {
            using (var logger = ESFA.DC.Logging.LoggerManager.CreateDefaultLogger())
            {
                var stopwatch = new Stopwatch();

                var ulnHelper = new ULNHelper();
                stopwatch.Start();
                //get data from DB.
            //    var ulnData = ulnHelper.GetAllULNs();
            //    var ulndataInMs = stopwatch.ElapsedMilliseconds;

           //     stopwatch.Restart();
                //add it into the reliabledictionary
                var ulnsDictionary = await StateManager.GetOrAddAsync<IReliableDictionary<long, long>>("ULNsv2");

                using (var tx = StateManager.CreateTransaction())
                {
                    try
                    {
                        //clear existing values
                        //await ulnsDictionary.ClearAsync();

                        foreach (var uln in ulns)
                        {
                            await ulnsDictionary.TryAddAsync(tx, uln, uln,TimeSpan.FromMinutes(2),CancellationToken.None);
                        }
                        await tx.CommitAsync();
                    }
                    catch (Exception ex)
                    {
                        tx.Abort();
                        logger.LogError("Error while saving ULN into dic", ex);
                    }
                }
                stopwatch.Stop();
                var dataInsertionInMs = stopwatch.ElapsedMilliseconds;              
                logger.LogInfo($"ULN Data records:{ulns.Count()} saved in reliabledic: {dataInsertionInMs}");
            }
        }

        /// <summary>
        /// Gets Ulns from in-memory dic
        /// </summary>
        /// <param name="ulns"></param>
        /// <returns></returns>
        public async Task<IEnumerable<long>> GetULNs(IEnumerable<long> ulns)
        {
            var stopwatch = new Stopwatch();
            var validUlns = new HashSet<long>();
            using (var logger = ESFA.DC.Logging.LoggerManager.CreateDefaultLogger())
            {
                stopwatch.Start();
                var ulnsDictionary = await StateManager.GetOrAddAsync<IReliableDictionary<long, long>>("ULNsv2");
                using (var tx = StateManager.CreateTransaction())
                {
                    try
                    {
                        stopwatch.Start();
                        IAsyncEnumerator<KeyValuePair<long, long>> enumerator =
                            (await ulnsDictionary.CreateEnumerableAsync(tx, (x) => ulns.Contains(x), EnumerationMode.Ordered)).GetAsyncEnumerator();

                        while (await enumerator.MoveNextAsync(CancellationToken.None))
                        {
                            validUlns.Add(enumerator.Current.Value);
                        }

                        logger.LogInfo($"Retrieved ULNs in: {stopwatch.ElapsedMilliseconds}");
                        await tx.CommitAsync();
                        return validUlns;

                    }
                    catch (Exception ex)
                    {
                        tx.Abort();
                        logger.LogError("Error while reading from Ulns dic", ex);
                        return null;
                    }
                }

            }

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
            return new[]
            {

                //new ServiceReplicaListener((context) =>
                //   new WcfCommunicationListener<IULNv2DataService>(
                //       wcfServiceObject:this,
                //       serviceContext:context,
                //       //
                //       // The name of the endpoint configured in the ServiceManifest under the Endpoints section
                //       // that identifies the endpoint that the WCF ServiceHost should listen on.
                //       //
                //       endpointResourceName: "WcfULNDataServiceEndpoint",

                //       //
                //       // Populate the binding information that you want the service to use.
                //       //
                //       listenerBinding: WcfUtility.CreateTcpListenerBinding()                //   ), "ULNDataServiceWCFListener"),


                new ServiceReplicaListener(
                    (c) => new FabricTransportServiceRemotingListener(c, this, null,
                        new ServiceRemotingJsonSerializationProvider()),
                    "ULNdataServiceRemotingListener", true) //enable secondaries to listen to requests.
            };
        }

        public async Task<bool> ClearULNs()
        {
            using (var logger = ESFA.DC.Logging.LoggerManager.CreateDefaultLogger())
            {
                var ulnsDictionary = await StateManager.GetOrAddAsync<IReliableDictionary<long, long>>("ULNsv2");

                using (var tx = StateManager.CreateTransaction())
                {
                    await ulnsDictionary.ClearAsync(TimeSpan.FromMinutes(2), CancellationToken.None);
                    await tx.CommitAsync();
                }

                logger.LogInfo($"ULN Data records cleared in reliabledic");
                return true;
            }


        }

        
    }
}
