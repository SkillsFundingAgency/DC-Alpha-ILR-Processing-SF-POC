using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Autofac.Integration.ServiceFabric;
using BusinessRules.POC.Configuration;
using BusinessRules.POC.RuleManager;
using BusinessRules.POC.RuleManager.Interface;
using DCT.ValidationService.Service;
using DCT.ValidationService.Service.Implementation;
using DCT.ValidationService.Service.Interface;
using Microsoft.ServiceFabric.Services.Runtime;
using static DCT.ILR.VadationServiceStateful.Config.AutofacConfig;

namespace DCT.ILR.VadationServiceStateful
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
                builder.RegisterModule<BusinessLogicAutofacModule>();
                builder.RegisterModule<ValidationServiceServiceModuleSF>();

                builder.RegisterType<RuleManagerValidationService>().As<IValidationService>().InstancePerMatchingLifetimeScope("childLifeTimeScope");
                builder.RegisterType<RuleManager>().As<IRuleManager>().InstancePerMatchingLifetimeScope("childLifeTimeScope");

                // Register the Autofac magic for Service Fabric support.
                builder.RegisterServiceFabricSupport();

                // Register the stateful service.
                builder.RegisterStatefulService<VadationServiceStateful>("DCT.ILR.VadationServiceStatefulType");

                //ServiceRuntime.RegisterServiceAsync("DCT.ILR.VadationServiceStatefulType",
                //    context => new VadationServiceStateful(context)).GetAwaiter().GetResult();

                //ServiceEventSource.Current.ServiceTypeRegistered(Process.GetCurrentProcess().Id, typeof(VadationServiceStateful).Name);

                using (var container= builder.Build())
                {
                    
                    ServiceEventSource.Current.ServiceTypeRegistered(Process.GetCurrentProcess().Id, typeof(VadationServiceStateful).Name);

                    // Prevents this host process from terminating so services keep running.
                    Thread.Sleep(Timeout.Infinite);
                }


                // Prevents this host process from terminating so services keep running.
               // Thread.Sleep(Timeout.Infinite);
            }
            catch (Exception e)
            {
                ServiceEventSource.Current.ServiceHostInitializationFailed(e.ToString());
                throw;
            }
        }
    }
}
