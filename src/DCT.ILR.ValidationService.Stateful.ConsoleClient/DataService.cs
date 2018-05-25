using DCT.ILR.ValidationService.Models.Interfaces;
using Hydra.Core.Sharding;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Wcf;
using Microsoft.ServiceFabric.Services.Communication.Wcf.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DCT.ILR.ValidationService.Stateful.ConsoleClient
{
    public class DataService
    {
        public async static Task Run()
        {
            Binding binding = WcfUtility.CreateTcpClientBinding();
            // Create a partition resolver
            IServicePartitionResolver partitionResolver = ServicePartitionResolver.GetDefault();
            //var partitionResolver = new ServicePartitionResolver("dctsfpoc.westeurope.cloudapp.azure.com:19080",
            //    "dctsfpoc.westeurope.cloudapp.azure.com:20188", "dctsfpoc.westeurope.cloudapp.azure.com:19000");
            // create a  WcfCommunicationClientFactory object.
            var wcfClientFactory = new WcfCommunicationClientFactory<IReferenceDataService>
                (clientBinding: binding, servicePartitionResolver: partitionResolver);

            //
            // Create a client for communicating with the ICalculator service that has been created with the
            // Singleton partition scheme.
            //
            var dataServiceCommunicationClient = new WcfCommunicationClient(
                            wcfClientFactory,
                            new Uri("fabric:/DCT.ILR.Processing.POC/DCT.ILR.Data"),
                            new ServicePartitionKey(1)
                            );


            var resolved = partitionResolver.ResolveAsync(new Uri("fabric:/DCT.ILR.Processing.POC/DCT.ILR.Data"),
                new ServicePartitionKey(1), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10), CancellationToken.None).Result;

            //foreach (var endpoint in resolved.Endpoints)
            //{
            //    Console.WriteLine($"Discovered Service Endpoint:'{endpoint.Address}");
            //}
            // ListEndpoints(partitionResolver);
            //
            // Call the service to perform the operation.
            //
            try
            {
                var correlationId = Guid.NewGuid();

                //var processTimes = validationServiceCommunicationClient.InvokeWithRetryAsync(
                //        client => client.Channel.GetResults(correlationId)).Result;

                Console.WriteLine("1. Insert ULns values, 2. Just get results ");
                var keyValue = Console.ReadKey();
                if (keyValue.KeyChar != 50)
                {
                    await dataServiceCommunicationClient.InvokeWithRetryAsync(
                               client => client.Channel.InsertULNs());
                }

                var ulns = new List<long>()
                {
                    1000000027,
1000000035,
1000000043,
1000000051,
1000000078,
1000000272,
1000000280,
1000000299,
1000000302,
1000000310,
1000000477,
1000000485,
1000000493,
1000000647,
1000000655,
1000000671,
1000000779,
1000000787,
1000000795,
1000000841
                };

                var value1 = new JumpSharding().GetShard("1000000795", 10);
                var value2 = new JumpSharding().GetShard("1000000795", 10);


                var validUlns= await dataServiceCommunicationClient.InvokeWithRetryAsync(
                          client => client.Channel.GetULNs( ulns ));

                



                Console.WriteLine("1. Insert LARS values, 2. Just get results ");
                keyValue = Console.ReadKey();
                if (keyValue.KeyChar != 50)
                {
                    await dataServiceCommunicationClient.InvokeWithRetryAsync(
                               client => client.Channel.InsertLARSData());
                }
                var learnAimRefs = new List<string>()
                    {
                        "00100309",
"00100325",
"00100432",
"00100525",
"00100533",
"00100567",
"00100572",
"00100573",
"00228740",
"00228761",
"00228762",
"00228763",
"00228764",
"00228787",
"00228789",
"00228790",
"00230643",
"00230644",
"00230645",
"00230646",
"00230648",
"00230680",
"00230684",
"00230698",
"00230699",
"00230703",
"00230704",
"00230712",
"00230713",
"00230718",
"00230722",
"00230761",
"00230764",
"00243034",
"00243035",
"00243042",
"00243043",
"00243045",
"00243046",
"00243047",
"00243054",
"00243057",
"00243060",
"00243064",
"00243066",
"00243067",
"00243068",
"00243071",
"00243072",
"00243073",
"00243075",
"00243076",
"00243077",
"00243078",
"00243114",
"J6018531",
"J6018545",
"J6018576",
"J6018593",
"J6018626",
"J6018643",
"J6018657",
"J6018707",
"J6018710",
"J6018724",
"J6018741",
"J6018755",
"J6018769",
"J6018772",
"J6018805",
"J6018836",
"J6018853",
"J6018867",
"L5068787",
"L5068904",
"L5070183",
"L5070197",
"L5070202",
"L5070233",
"L5070247",
"L5070250",
"L5070264",
"L5070281",
"L5070295",
"L5070300",
"Z0005494",
"Z0005495",
"Z0005496",
"Z0005497",
"Z0005498",
"Z0005499",
"Z0005500",
"Z0005501",
"Z0005502",
"Z0005503",
"Z0005504",
"Z0005505",
"Z0005506",
"Z0005507",
"Z0005508",
"Z0005509",
"Z0005510",
"Z0005511",
"Z0005512",


                    };
                var tasksList = new List<Task>();

                foreach (var item in learnAimRefs)
                {
                    tasksList.Add(dataServiceCommunicationClient.InvokeWithRetryAsync(
                          client => client.Channel.GetLARSLearningDeliveriesAsync(new List<string>() { item })));
                }

                try
                {


                    await Task.WhenAll(tasksList.ToArray());
                    Console.WriteLine("failed tasks: " + tasksList.Count(x => x.IsFaulted));
                    //foreach (var task in tasksList)
                    //{
                    //    Console.WriteLine(task.Result);
                    //}
                }
                catch (Exception iex)
                {

                    Console.WriteLine(iex.ToString());
                }

                Console.WriteLine("");
                



            

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                Console.ReadLine();
            }

        }

    }
}
