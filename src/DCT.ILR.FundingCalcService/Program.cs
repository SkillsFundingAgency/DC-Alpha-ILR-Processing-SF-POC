using System;
using System.Diagnostics;
using System.Fabric;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Autofac.Integration.ServiceFabric;
using DCT.ILR.FundingCalcService.Models;
using DCT.ILR.ValidationService.Models;
using DCT.ILR.ValidationService.Models.JsonSerialization;
using DCT.ILR.ValidationService.Models.Models;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Client;
using Microsoft.ServiceFabric.Services.Runtime;

namespace DCT.ILR.FundingCalcService
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
                // The ServiceManifest.XML file defines one or more service type names.
                // Registering a service maps a service type name to a .NET type.
                // When Service Fabric creates an instance of this service type,
                // an instance of the class is created in this host process.

                // Start with the trusty old container builder.
                var builder = new ContainerBuilder();

                // Register any regular dependencies.


                var config = new ESFA.DC.Logging.ApplicationLoggerSettings();
                config.LoggerOutput = ESFA.DC.Logging.Enums.LogOutputDestination.SqlServer;
                builder.RegisterType<ESFA.DC.Logging.SeriLogging.SeriLogger>().As<ESFA.DC.Logging.ILogger>()
                    .WithParameter(new TypedParameter(typeof(ESFA.DC.Logging.ApplicationLoggerSettings), config))
                    .InstancePerLifetimeScope();

                //get the config values and register in container
                var fundingActorOptions =
                    ConfigurationHelper.GetSectionValues<ActorOptions>("FundingActorsSection");
                builder.RegisterInstance(fundingActorOptions).As<ActorOptions>().SingleInstance();

                var dataServiceOptions =
                    ConfigurationHelper.GetSectionValues<DataServiceOptions>("DataServiceSection");
                builder.RegisterInstance(dataServiceOptions).As<DataServiceOptions>().SingleInstance();

                builder.RegisterInstance(new ActorsHelper()).As<IActorsHelper>();


                //store proxy factory in container
                var serviceProxyFactory = new ServiceProxyFactory(
                    (c) => new FabricTransportServiceRemotingClientFactory(
                        remotingSettings: FabricTransportRemotingSettings.LoadFrom("DataTransportSettings"),
                        remotingCallbackMessageHandler: null, servicePartitionResolver: null, exceptionHandlers: null,
                        traceId: null,
                        serializationProvider: new ServiceRemotingJsonSerializationProvider()));

                builder.RegisterInstance(serviceProxyFactory).As<ServiceProxyFactory>().SingleInstance();
                
                builder.RegisterType<FundingCalcManager>().As<IFundingCalcManager>().InstancePerLifetimeScope();


                // Register the Autofac magic for Service Fabric support.
                builder.RegisterServiceFabricSupport();
                // Register the stateless service.
                builder.RegisterStatelessService<FundingCalcService>("DCT.ILR.FundingCalcServiceType");

                builder.Register(c =>
                {
                    var ctx = c.Resolve<StatelessServiceContext>();
                    return ctx.CodePackageActivationContext.ApplicationName;
                }).Named<string>("ApplicationName");

                //ServiceRuntime.RegisterServiceAsync("DCT.ILR.VadationServiceStatefulType",
                //    context => new VadationServiceStateful(context)).GetAwaiter().GetResult();

                //ServiceEventSource.Current.ServiceTypeRegistered(Process.GetCurrentProcess().Id, typeof(VadationServiceStateful).Name);

                using (var container = builder.Build())
                {
                    using (var childlifetime = container.BeginLifetimeScope())
                    {
                        var s = childlifetime.Resolve<IFundingCalcManager>();
                    }

                    //var logger = container.Resolve<ESFA.DC.Logging.ILogger>();
                    ServiceEventSource.Current.ServiceTypeRegistered(Process.GetCurrentProcess().Id, typeof(FundingCalcService).Name);

                    // Prevents this host process from terminating so services keep running.
                    Thread.Sleep(Timeout.Infinite);
                }
               
            }
            catch (Exception e)
            {
                ServiceEventSource.Current.ServiceHostInitializationFailed(e.ToString());
                throw;
            }
        }
    }
}
