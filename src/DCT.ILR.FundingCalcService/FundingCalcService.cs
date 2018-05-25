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
using DCT.Funding.Model.Outputs;
using DCT.ILR.FundingCalcService.ALBActor.Interfaces;
using DCT.ILR.FundingCalcService.Listeners;
using DCT.ILR.FundingCalcService.Models;
using DCT.ILR.Model;
using DCT.ILR.ValidationService.Models.Interfaces;
using ESFA.DC.Logging;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Client;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Client;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.ServiceFabric.Services.Runtime;
using Newtonsoft.Json;
using DCT.ILR.ValidationService.Models;
using DCT.ILR.ValidationService.Models.Models;
using Hydra.Core.Sharding;
using ServiceBusSubscriptionListenerModel = DCT.ILR.FundingCalcService.Models.ServiceBusSubscriptionListenerModel;

namespace DCT.ILR.FundingCalcService
{
    /// <summary>
    /// An instance of this class is created for each service instance by the Service Fabric runtime.
    /// </summary>
    public class FundingCalcService : StatelessService
    {
        private ILifetimeScope _parentLifeTimeScope;
//        private ILogger _logger;
        private Uri _albActorUri;
        private ServiceProxyFactory _serviceProxyFactory;
        private Uri _validationResultsServiceUri;
        private IActorsHelper _actorsHelper;

        public FundingCalcService(StatelessServiceContext context, ILogger logger, ILifetimeScope parentLifeTimeScope)
            : base(context)
        {
//            _logger = logger;
            _parentLifeTimeScope = parentLifeTimeScope;

        }

        /// <summary>
        /// Optional override to create listeners (e.g., TCP, HTTP) for this service replica to handle client or user requests.
        /// </summary>
        /// <returns>A collection of listeners.</returns>
        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            yield return new ServiceInstanceListener(context =>
                new ServiceBusSubscriptionListener(ProcessMessageHandler), "Stateless-ServiceBusFundingCalcSubsListener");
        }

        /// <summary>
        /// This gets called in the Listerner, whenever there is a new message in servicebus queue it will be triggered.
        /// </summary>
        /// <param name="listernerModel"></param>
        /// <returns></returns>
        async Task ProcessMessageHandler(ServiceBusSubscriptionListenerModel listernerModel)
        {
            using (var childLifeTimeScope = _parentLifeTimeScope.BeginLifetimeScope())
            {
                var logger = childLifeTimeScope.Resolve<ILogger>();
                try
                {
                    var body = listernerModel.Message.GetBody<Stream>();
                    dynamic messageBody = JsonConvert.DeserializeObject(new StreamReader(body, true).ReadToEnd());

                    logger.StartContext(listernerModel.Message.CorrelationId);

                    logger.LogInfo($"Started Funding calc for jobId:{listernerModel.Message.CorrelationId} ");
                    var startDateTime = DateTime.Now;

                    var fundingCalcManager = childLifeTimeScope.Resolve<IFundingCalcManager>();
                    await fundingCalcManager.ProcessJobs(listernerModel.Message.CorrelationId);



                    logger.LogInfo(
                        $"Completed Funding calc for jobId:{listernerModel.Message.CorrelationId} in {(DateTime.Now - startDateTime).TotalMilliseconds} ");


                }
                catch (Exception ex)
                {
                    ServiceEventSource.Current.ServiceMessage(this.Context, "Exception-{0}", ex.ToString());
                    logger.LogError("Error while processing funding calc job", ex);
                    throw;
                }
            }
        }

       


      




    }

  
}
