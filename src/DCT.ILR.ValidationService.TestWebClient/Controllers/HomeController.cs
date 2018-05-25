using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using DCT.ILR.ValidationService.TestWebClient.Models;
using DCT.ILR.ValidationService.Models;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.ServiceFabric.Services.Client;
using DCT.ILR.ValidationService.Models.Models;
using Microsoft.Extensions.Configuration;
using DCT.ILR.ValidationService.TestWebClient.ServiceBus;
using Newtonsoft.Json;
using DCT.ILR.ValidationService.Models.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.WindowsAzure.Storage;
using Microsoft.Azure;
using Microsoft.WindowsAzure.Storage.Blob;
using System.IO;
using System.Fabric;
using DCT.ILR.ValidationService.Models.JsonSerialization;
using Hydra.Core.Sharding;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Client;
using Microsoft.ServiceFabric.Services.Communication.Client;

namespace DCT.ILR.ValidationService.TestWebClient.Controllers
{
    public class HomeController : Controller
    {

        private readonly IValidationServiceStateful _validationService;
        private IConfiguration _configuration;
        private Random _rnd = new Random();
        private IServiceBusQueue _serviceBusQueueHelper;
        private IFundingCalcResults _fundingCalcResultsService;
        private ServiceProxyFactory _serviceProxyFactory;

        public HomeController(IConfiguration configuration, IServiceBusQueue serviceBusQueueHelper)
        {
            _validationService = ServiceProxy.Create<IValidationServiceStateful>(
               new Uri("fabric:/DCT.ILR.Processing.POC/DCT.ILR.VadationServiceStateful"),
               new ServicePartitionKey(0));

            _configuration = configuration;

            _serviceProxyFactory = new ServiceProxyFactory(
                (c) => new FabricTransportServiceRemotingClientFactory(
                    serializationProvider: new ServiceRemotingJsonSerializationProvider()));

         

            _fundingCalcResultsService = _serviceProxyFactory.CreateServiceProxy<IFundingCalcResults>(
               new Uri("fabric:/DCT.ILR.Processing.POC/DCT.ILR.Data"),
               new ServicePartitionKey(0), TargetReplicaSelector.RandomReplica, "dataServiceRemotingListener");

            _serviceBusQueueHelper = serviceBusQueueHelper;

        }

        public IActionResult Index()
        {           
            return View();
        }

        [HttpPost]
        [RequestSizeLimit(500_000_000)]
        public async Task<IActionResult> SubmitILR(IFormFile file, bool IsShredAndProcess)
        {
            ViewData["Message"] = "Your application description page.";
            CloudStorageAccount cloudStorageAccount = CloudStorageAccount.Parse("DefaultEndpointsProtocol=##StorageAccountEndpoint##;AccountName=##StorageAccountName##;AccountKey=##StorageAccountKey##;EndpointSuffix=core.windows.net");
            CloudBlobClient cloudBlobClient = cloudStorageAccount.CreateCloudBlobClient();
            CloudBlobContainer cloudBlobContainer = cloudBlobClient.GetContainerReference("ilr-files");

            var newFileName = $"{Path.GetFileNameWithoutExtension(file.FileName)}-{GetRandomCharacters(5)}.xml";
            CloudBlockBlob cloudBlockBlob = cloudBlobContainer.GetBlockBlobReference(newFileName);
                      
            using (var outputStream = await cloudBlockBlob.OpenWriteAsync())
            {
                await file.CopyToAsync(outputStream);
            }




            //write it into a queue
            
             var correlationId = Guid.NewGuid();
                var model = new IlrContext()
                {
                    CorrelationId = correlationId,
                    ContainerReference = "ilr-files",
                    Filename = newFileName,
                    //Filename = $"ILR-10006341-1718-20171107-113456-04.xml",
                    IsShredAndProcess = IsShredAndProcess
                };
                

               await _serviceBusQueueHelper.SendMessagesAsync(JsonConvert.SerializeObject(model), GetRandomCharacters(8));
           

           


            return RedirectToAction("Confirmation", new { correlationId });
        }

        public IActionResult Confirmation(Guid correlationId)
        {
            ViewData["CorrelationId"] = correlationId;

            return View();
        }

        public async Task<IActionResult> Status(Guid correlationId)
        {
            var valResultsProxy = GetValResultsProxy(correlationId.ToString(), true);
            var resultsModel = await valResultsProxy.GetResultsAsync(correlationId.ToString());
            var fundingCalcModel = await _fundingCalcResultsService.GetFCResultsAsync(correlationId.ToString());
            return View(new ResultsModel() {
               ValidationResults = resultsModel?.ToList(), FundingCalcResults = fundingCalcModel?.ToList()
            });
        }

        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        private string GetRandomCharacters(int length)
        {
            string Characters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

           
            var result = new string(
                Enumerable.Repeat(Characters, length)
                          .Select(s => s[_rnd.Next(s.Length)])
                          .ToArray());

            return result;
        }

        private IValidationServiceResults GetValResultsProxy(string correlationId, bool isRead)
        {
            //get the partition 
            var shardNumber = new JumpSharding().GetShard(correlationId, 10);

            return _serviceProxyFactory.CreateServiceProxy<IValidationServiceResults>(
                new Uri("fabric:/DCT.ILR.Processing.POC/DCT.ILR.Data"),
                new ServicePartitionKey(shardNumber),
                isRead ? TargetReplicaSelector.RandomReplica : TargetReplicaSelector.PrimaryReplica,
                "dataServiceRemotingListener");
        }
    }
}
