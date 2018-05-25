using System;
using System.Diagnostics;
using System.Fabric;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Autofac.Integration.ServiceFabric;
using BusinessRules.POC.Configuration;
using BusinessRules.POC.RuleManager;
using BusinessRules.POC.RuleManager.Interface;
using DCT.ILR.ValidationService.LearnerActor.Interfaces;
using DCT.ValidationService.Service;
using DCT.ValidationService.Service.Implementation;
using DCT.ValidationService.Service.Interface;
using Microsoft.Diagnostics.EventFlow.ServiceFabric;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Client;
using Microsoft.ServiceFabric.Actors.Runtime;
using static DCT.ILR.ValidationService.LearnerActor.Config.AutofacConfig;

namespace DCT.ILR.ValidationService.LearnerActor
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
                // **** Instantiate log collection via EventFlow
                using (var diagnosticsPipeline = ServiceFabricDiagnosticPipelineFactory.CreatePipeline("DCT.ILR.ValidationService.LearnerActor-pipeline"))
                {


                    // Start with the trusty old container builder.
                    var builder = new ContainerBuilder();

                    // Register any regular dependencies.
                    //builder.RegisterModule(new LoggerModule(ActorEventSource.Current.Message));
                    builder.RegisterModule<BusinessLogicAutofacModule>();
                    builder.RegisterModule<ValidationServiceServiceModuleSF>();

                    var config = new ESFA.DC.Logging.ApplicationLoggerSettings();
                    config.LoggerOutput = ESFA.DC.Logging.Enums.LogOutputDestination.SqlServer;
                    builder.RegisterType<ESFA.DC.Logging.SeriLogging.SeriLogger>().As<ESFA.DC.Logging.ILogger>()
                        .WithParameter(new TypedParameter(typeof(ESFA.DC.Logging.ApplicationLoggerSettings), config))
                        .InstancePerLifetimeScope();

                    builder.RegisterType<RuleManager>().As<IRuleManager>();
                    builder.RegisterType<RuleManagerValidationService>().As<IValidationService>().InstancePerLifetimeScope();


                    // Register the Autofac magic for Service Fabric support.
                    builder.RegisterServiceFabricSupport();

                    // Register the actor service.
                    builder.RegisterActor<LearnerActor>();

                    using (var container = builder.Build())
                    {
                        using (var childscope = container.BeginLifetimeScope())
                        {
                            var val = childscope.Resolve<IValidationService>();
                        }
                        Thread.Sleep(Timeout.Infinite);
                    }
                }

                // ActorRuntime.RegisterActorAsync<LearnerActor>(
                //(context, actorType) => new ActorService(context, actorType)).GetAwaiter().GetResult();

            }
            catch (Exception e)
            {
                ActorEventSource.Current.ActorHostInitializationFailed(e.ToString());
                throw;
            }
        }
    }
}
