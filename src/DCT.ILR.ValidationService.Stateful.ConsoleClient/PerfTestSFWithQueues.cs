using DCT.ILR.ValidationService.Models.Models;
using Microsoft.Azure.ServiceBus;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DCT.ILR.ValidationService.Stateful.ConsoleClient
{
    public class PerfTestSFWithQueues
    {
        private readonly string StorageConnectionString = ConfigurationManager.AppSettings["StorageConnectionString"];
        private readonly string ServiceBusConnectionString = ConfigurationManager.AppSettings["serviceBusConnectionString"];
        private string _queueName = "dct-ilrsubmissions-queue-poc";


        public async Task RunAsync()
        {
            try
            {




                CloudStorageAccount cloudStorageAccount = CloudStorageAccount.Parse(StorageConnectionString);
                CloudBlobClient cloudBlobClient = cloudStorageAccount.CreateCloudBlobClient();
                var container = cloudBlobClient.GetContainerReference("ilr-files");
                var blobs = container.ListBlobs(useFlatBlobListing: true);
                var blobNames = blobs.OfType<CloudBlockBlob>().Select(b => b.Name).ToList();

                var queueClient = new QueueClient(ServiceBusConnectionString, _queueName);
                var isShredAndProcess = true;
                int counter = 1;
                foreach (var blobName in blobNames.Where(x=> x.Contains("ILR-10006341-1718-20180118-023456-1600")))
                {
                    var correlationId = Guid.NewGuid();
                    var model = new IlrContext()
                    {
                        CorrelationId = correlationId,
                        ContainerReference = "ilr-files",
                        Filename = blobName,
                        //Filename = $"ILR-10006341-1718-20171107-113456-04.xml",
                        IsShredAndProcess = true
                    };
                    isShredAndProcess = !isShredAndProcess;

                    var message = new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(model)));
                    message.SessionId = Guid.NewGuid().ToString();

                    // Write the body of the message to the console.
                    Console.WriteLine($"Url: http://dcsflarge.westeurope.cloudapp.azure.com/Home/Status?correlationId={correlationId.ToString()}");

                    // Send the message to the queue.
                    await queueClient.ScheduleMessageAsync(message, DateTimeOffset.Parse("12/03/2018 15:42:00 PM"));
                    //await queueClient.SendAsync(message);
                    counter++;
                    if (counter == 41) break;

                }

                await queueClient.CloseAsync();




            }
            catch (Exception exception)
            {
                Console.WriteLine($"{DateTime.Now} :: Exception: {exception.Message}");
                throw;
            }

        }
    }
}
