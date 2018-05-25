using System;
using System.Diagnostics;
using System.Fabric;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Autofac.Integration.ServiceFabric;
using BusinessRules.POC.Configuration;
using BusinessRules.POC.FileData.Interface;
using BusinessRules.POC.RuleManager;
using BusinessRules.POC.RuleManager.Interface;
using DCT.ILR.ValidationService.Models;
using DCT.ILR.ValidationService.Models.Models;
using DCT.ILR.ValidationServiceStateless.Modules;
using DCT.ILR.ValidationServiceStateless.ServiceBus;
using DCT.ValidationService.Service.Implementation;
using DCT.ValidationService.Service.Interface;
using Microsoft.Diagnostics.EventFlow.ServiceFabric;
using Microsoft.ServiceFabric.Services.Runtime;
//using ServiceFabric.Logging;

namespace DCT.ILR.ValidationServiceStateless
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

                using (var diagnosticsPipeline = ServiceFabricDiagnosticPipelineFactory.CreatePipeline("DCT.ILR.ValidationServiceStateless-pipeline"))
                {
                    // Start with the trusty old container builder.
                    var builder = new ContainerBuilder();

                    // Register any regular dependencies.
                    builder.RegisterModule<BusinessLogicAutofacModule>();
                    builder.RegisterModule<ValidationServiceServiceModuleSF>();

                    var config = new ESFA.DC.Logging.ApplicationLoggerSettings();
                    config.LoggerOutput = ESFA.DC.Logging.Enums.LogOutputDestination.SqlServer;
                    builder.RegisterType<ESFA.DC.Logging.SeriLogging.SeriLogger>().As<ESFA.DC.Logging.ILogger>()
                        .WithParameter(new TypedParameter(typeof(ESFA.DC.Logging.ApplicationLoggerSettings), config))
                        .InstancePerLifetimeScope();
                        

                    var applicationInsightsKey = FabricRuntime.GetActivationContext()
                            .GetConfigurationPackageObject("Config")
                            .Settings
                            .Sections["ConfigurationSection"]
                            .Parameters["ApplicationInsightsKey"]
                            .Value;

                    var configurationOptions =
                        ConfigurationHelper.GetSectionValues<ConfigurationOptions>("ConfigurationSection");

                    var seviceBusOptions =
                        ConfigurationHelper.GetSectionValues<ServiceBusOptions>("ServiceBusSettings");

                    
                    builder.RegisterInstance(configurationOptions).As<ConfigurationOptions>().SingleInstance();
                    builder.RegisterInstance(seviceBusOptions).As<ServiceBusOptions>().SingleInstance();


                 



                    //var loggerFactory = new LoggerFactoryBuilder().CreateLoggerFactory(applicationInsightsKey);
                    //logger = loggerFactory.CreateLogger<MyStateless>();

                    builder.RegisterType<TopicHelper>().As<ITopicHelper>().InstancePerDependency();
                    builder.RegisterType<RuleManager>().As<IRuleManager>();
                    //builder.Register(c =>
                    //        new RuleManagerValidationService(c.Resolve<IRuleManager>(), c.Resolve<IFileData>()))
                    //    .As<IValidationService>().InstancePerLifetimeScope();
                    builder.RegisterType<RuleManagerValidationService>().As<IValidationService>().InstancePerLifetimeScope();

                    builder.RegisterInstance(new ActorsHelper()).As<IActorsHelper>();

                    // Register the Autofac magic for Service Fabric support.
                    builder.RegisterServiceFabricSupport();
                    // Register the stateless service.
                    builder.RegisterStatelessService<ValidationServiceStateless>("DCT.ILR.ValidationServiceStatelessType");
                  
                    //ServiceRuntime.RegisterServiceAsync("DCT.ILR.VadationServiceStatefulType",
                    //    context => new VadationServiceStateful(context)).GetAwaiter().GetResult();

                    //ServiceEventSource.Current.ServiceTypeRegistered(Process.GetCurrentProcess().Id, typeof(VadationServiceStateful).Name);

                    using (var container = builder.Build())
                    {
                        using (var newScope = container.BeginLifetimeScope())
                        {
                            var val = newScope.Resolve<IValidationService>();
                        }
                        ServiceEventSource.Current.ServiceTypeRegistered(Process.GetCurrentProcess().Id, typeof(ValidationServiceStateless).Name);

                        // Prevents this host process from terminating so services keep running.
                        Thread.Sleep(Timeout.Infinite);
                    }
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
