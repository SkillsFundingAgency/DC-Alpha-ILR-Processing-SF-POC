using System;
using System.Diagnostics;
using System.Fabric;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Autofac.Integration.ServiceFabric;
using DCT.FundingService.Config.Implementation;
using DCT.FundingService.Config.Interface;
using DCT.FundingService.Service.Implementation;
using DCT.FundingService.Service.Interface;
using DCT.FundingService.Service.OpaTasks.OpaInputMap;
using DCT.FundingService.Service.ReferenceData;
using DCT.ILR.FundingCalcService.ALBActor.RulebaseOverrides;
using DCT.LARS.Model.Interface;
using DCT.LARS.Model.Models;
using DCT.OPA.Model.Models;
using DCT.OPA.Service.Implementation;
using DCT.OPA.Service.Interface;
using DCT.PostcodeFactors.Model.Interface;
using DCT.PostcodeFactors.Model.Models;
using DCT.ReferenceData.Interface;
using Microsoft.ServiceFabric.Actors.Runtime;

namespace DCT.ILR.FundingCalcService.ALBActor
{
    internal static class Program
    {
        /// <summary>
        /// This is the entry point of the service host process.
        /// </summary>
        private static void Main()
        {
            try
            {
                // This line registers an Actor Service to host your actor class with the Service Fabric runtime.
                // The contents of your ServiceManifest.xml and ApplicationManifest.xml files
                // are automatically populated when you build this project.
                // For more information, see https://aka.ms/servicefabricactorsplatform

                // Start with the trusty old container builder.
                var builder = ConfigureBuilderDependencies();

                // Register the Autofac magic for Service Fabric support.
                builder.RegisterServiceFabricSupport();

                // Register the actor service.
                builder.RegisterActor<ALBActor>();

                using (var container = builder.Build())
                {
//                    var config = container.Resolve<IFundingServiceConfig>();

                    Thread.Sleep(Timeout.Infinite);
                }



//                ActorRuntime.RegisterActorAsync<ALBActor>(
//                   (context, actorType) => new ActorService(context, actorType)).GetAwaiter().GetResult();
//
//                Thread.Sleep(Timeout.Infinite);
            }
            catch (Exception e)
            {
                ActorEventSource.Current.ActorHostInitializationFailed(e.ToString());
                throw;
            }
        }

        private static ContainerBuilder ConfigureBuilderDependencies()
        {
            var builder = new ContainerBuilder();

//            builder.RegisterType<PostcodeFactorsReferenceDataContext>().As<IPostcodeFactorsReferenceDataContext>().InstancePerMatchingLifetimeScope("childLifeTimeScope"); 
//            builder.RegisterType<LarsContext>().As<ILarsContext>().InstancePerMatchingLifetimeScope("childLifeTimeScope");
//            builder.RegisterType<ReferenceDataCache>().As<IReferenceDataCache>().InstancePerMatchingLifetimeScope("childLifeTimeScope");
//            builder.RegisterType<OpaService>().As<IOpaService>().InstancePerMatchingLifetimeScope("childLifeTimeScope");
//            builder.RegisterType<DataPersisterConfig>().As<IDataPersisterConfig>().InstancePerMatchingLifetimeScope("childLifeTimeScope");
//            builder.RegisterType<DataPersister>().As<IDataPersister>().InstancePerMatchingLifetimeScope("childLifeTimeScope");
//            builder.RegisterType<FundingServiceConfigSF>().As<IFundingServiceConfig>().InstancePerMatchingLifetimeScope("childLifeTimeScope");
//            builder.RegisterType<AttributeListBuilder>().As<IAttributeListBuilder<AttributeData>>().InstancePerMatchingLifetimeScope("childLifeTimeScope");
//            builder.RegisterType<FundingService.Service.Implementation.FundingService>().As<IFundingService>().InstancePerMatchingLifetimeScope("childLifeTimeScope");

            builder.RegisterType<PostcodeFactorsReferenceDataContext>().As<IPostcodeFactorsReferenceDataContext>().InstancePerLifetimeScope();
            builder.RegisterType<LarsContext>().As<ILarsContext>().InstancePerLifetimeScope();
            builder.RegisterType<ReferenceDataCacheSF>().As<IReferenceDataCache>().InstancePerLifetimeScope();
            builder.RegisterType<OpaService>().As<IOpaService>().InstancePerLifetimeScope();
            builder.RegisterType<DataPersisterConfig>().As<IDataPersisterConfig>().InstancePerLifetimeScope();
            builder.RegisterType<DataPersister>().As<IDataPersister>().InstancePerLifetimeScope();
            builder.RegisterType<FundingServiceConfigSF>().As<IFundingServiceConfig>().InstancePerLifetimeScope();
            builder.RegisterType<AttributeListBuilder>().As<IAttributeListBuilder<AttributeData>>().InstancePerLifetimeScope();
            builder.RegisterType<FundingService.Service.Implementation.FundingService>().As<IFundingService>()
                .InstancePerLifetimeScope();


            var config = new ESFA.DC.Logging.ApplicationLoggerSettings();
            config.LoggerOutput = ESFA.DC.Logging.Enums.LogOutputDestination.SqlServer;
            builder.RegisterType<ESFA.DC.Logging.SeriLogging.SeriLogger>().As<ESFA.DC.Logging.ILogger>()
                .WithParameter(new TypedParameter(typeof(ESFA.DC.Logging.ApplicationLoggerSettings), config))
                .InstancePerLifetimeScope();

            return builder;
        }
    }
}
