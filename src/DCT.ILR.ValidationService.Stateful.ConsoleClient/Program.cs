using DCT.ILR.ValidationService.Models;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Client;
using Microsoft.ServiceFabric.Services.Communication.Wcf;
using Microsoft.ServiceFabric.Services.Communication.Wcf.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;
using System.Fabric;
using System.Threading;
using DCT.ILR.ValidationService.Models.Interfaces;

namespace DCT.ILR.ValidationService.Stateful.ConsoleClient
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("1. Performance test or 2.Ref data test");
            var keyValue = Console.ReadKey();
                      
            if (keyValue.KeyChar != 50)
            {


                Task.Run(async () =>
                {
                    var app = new PerfTestSFWithQueues();
                    await app.RunAsync();
                }).GetAwaiter().GetResult();

                Console.ReadLine();

            }

            else
            {

                Task.Run(async () =>
                {                
                    await DataService.Run();
                }).GetAwaiter().GetResult();
                Console.ReadLine();

                //Console.WriteLine("1. Local or 2.Cloud");
                //var value = Console.ReadKey();

                //if (value.KeyChar == 50)
                //{
                //    try
                //    {
                //        CallCloud();
                //        return;
                //    }
                //    catch (Exception ex)
                //    {
                //        Console.WriteLine(ex.ToString());
                //        Console.ReadKey();
                //    }

                //}
                // Create binding

            }
        }

        private static long LongRandom()
        {
            byte[] buf = new byte[8];
            new Random().NextBytes(buf);
            long longRand = BitConverter.ToInt64(buf, 0);
            return longRand;
        }

        private static void CallCloud()
        {
            var binding = WcfUtility.CreateTcpClientBinding();
            var partitionResolver = new ServicePartitionResolver("ilrprocessingsfpoc.uksouth.cloudapp.azure.com:777", "ilrprocessingsfpoc.uksouth.cloudapp.azure.com:19000");
            var wcfClientFactory =
                new WcfCommunicationClientFactory<IValidationServiceStateful>(binding,
                    servicePartitionResolver: partitionResolver);
            var serviceUri = new Uri("fabric:/DCT.ILR.Processing.POC/DCT.ILR.VadationServiceStateful");
            var client = new ValidationServiceClient(wcfClientFactory, serviceUri, new ServicePartitionKey(LongRandom()));

            var resolved = partitionResolver.ResolveAsync( new Uri("fabric:/DCT.ILR.Processing.POC/DCT.ILR.VadationServiceStateful"), 
                new ServicePartitionKey(9223372036854775807), CancellationToken.None).Result;
            do
            {
                var correlationId = Guid.NewGuid();
                Console.WriteLine(client.Validation(new Models.Models.IlrContext()
                {
                    CorrelationId = correlationId,
                    ContainerReference = "ilr-files",
                    Filename = "ILR-10006341-1718-20171107-113456-01.xml",
                    //Filename = "ILR-10006148-1718-20170203-144336-03.xml",
                    IsShredAndProcess = false
                }).Result);
                Console.ReadKey();
            } while (true);
        }

        private static void ListEndpoints(ServicePartitionResolver resolver)
        {
            //var resolver = ServicePartitionResolver.GetDefault();
            var fabricClient = new FabricClient();
            var apps = fabricClient.QueryManager.GetApplicationListAsync().Result;
            foreach (var app in apps)
            {
                Console.WriteLine($"Discovered application:'{app.ApplicationName}");

                var services = fabricClient.QueryManager.GetServiceListAsync(app.ApplicationName).Result;
                foreach (var service in services)
                {
                    Console.WriteLine($"Discovered Service:'{service.ServiceName}");

                    var partitions = fabricClient.QueryManager.GetPartitionListAsync(service.ServiceName).Result;
                    foreach (var partition in partitions)
                    {
                        Console.WriteLine($"Discovered Service Partition:'{partition.PartitionInformation.Kind} {partition.PartitionInformation.Id}");


                        ServicePartitionKey key;
                        switch (partition.PartitionInformation.Kind)
                        {
                            case ServicePartitionKind.Singleton:
                                key = ServicePartitionKey.Singleton;
                                break;
                            case ServicePartitionKind.Int64Range:
                                var longKey = (Int64RangePartitionInformation)partition.PartitionInformation;
                                key = new ServicePartitionKey(longKey.LowKey);
                                break;
                            case ServicePartitionKind.Named:
                                var namedKey = (NamedPartitionInformation)partition.PartitionInformation;
                                key = new ServicePartitionKey(namedKey.Name);
                                break;
                            default:
                                throw new ArgumentOutOfRangeException("partition.PartitionInformation.Kind");
                        }
                        var resolved = resolver.ResolveAsync(service.ServiceName, key, CancellationToken.None).Result;
                        foreach (var endpoint in resolved.Endpoints)
                        {
                            Console.WriteLine($"Discovered Service Endpoint:'{endpoint.Address}");
                        }
                    }
                }
            }
        }
    }

    public class WcfCommunicationClient : ServicePartitionClient<WcfCommunicationClient<IReferenceDataService>>
    {
        public WcfCommunicationClient(ICommunicationClientFactory<WcfCommunicationClient<IReferenceDataService>> communicationClientFactory, Uri serviceUri, ServicePartitionKey partitionKey = null, TargetReplicaSelector targetReplicaSelector = TargetReplicaSelector.Default, string listenerName = null, OperationRetrySettings retrySettings = null)
            : base(communicationClientFactory, serviceUri, partitionKey, targetReplicaSelector, listenerName, retrySettings)
        {
        }

        
    }

    public class ValidationServiceClient : ServicePartitionClient<WcfCommunicationClient<IValidationServiceStateful>>
    {
        public ValidationServiceClient(
            ICommunicationClientFactory<WcfCommunicationClient<IValidationServiceStateful>> communicationClientFactory,
            Uri serviceUri, ServicePartitionKey partitionKey = null,
            TargetReplicaSelector targetReplicaSelector = TargetReplicaSelector.Default, string listenerName = null,
            OperationRetrySettings retrySettings = null) : base(communicationClientFactory, serviceUri, partitionKey,
            targetReplicaSelector, listenerName, retrySettings)
        {
        }

        public Task<bool> Validation(Models.Models.IlrContext ilrContext) => InvokeWithRetryAsync(client => client.Channel.Validate(ilrContext));
                
    }
}
