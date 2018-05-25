using System;
using System.Collections.Generic;
using System.Fabric;
using System.Fabric.Description;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DCT.ILR.ValidationService.TestWebClient.ServiceBus;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ServiceFabric.Services.Communication.AspNetCore;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;

namespace DCT.ILR.ValidationService.TestWebClient
{
    /// <summary>
    /// The FabricRuntime creates an instance of this class for each service type instance. 
    /// </summary>
    internal sealed class TestWebClient : StatelessService
    {
        public TestWebClient(StatelessServiceContext context)
            : base(context)
        { }

        /// <summary>
        /// Optional override to create listeners (like tcp, http) for this service instance.
        /// </summary>
        /// <returns>The collection of listeners.</returns>
        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            return new ServiceInstanceListener[]
            {
                new ServiceInstanceListener(serviceContext =>
                    new KestrelCommunicationListener(serviceContext, "ServiceEndpoint", (url, listener) =>
                    {
                        ServiceEventSource.Current.ServiceMessage(serviceContext, $"Starting Kestrel on {url}");
                        var config = FabricRuntime.GetActivationContext()?
                                    .GetConfigurationPackageObject("Config");
                                    //.Settings.Sections["ServiceBusQueue"];

                        return new WebHostBuilder()
                                    .UseKestrel()
                                    .ConfigureServices(
                                        services => {
                                            services
                                                .AddSingleton<StatelessServiceContext>(serviceContext)
                                                .AddSingleton<ConfigurationPackage>(config)
                                                .AddTransient<IServiceBusQueue, ServiceBusQueue>();
                                            services.Configure<FormOptions>(x =>
                                            {

                                               x.MultipartBodyLengthLimit = 524288000;
                                            });
                                        })
                                    .UseContentRoot(Directory.GetCurrentDirectory())                                    
                                    .UseStartup<Startup>()
                                    .UseApplicationInsights("##AppInsighKey##")
                                    .UseServiceFabricIntegration(listener, ServiceFabricIntegrationOptions.None)
                                    .UseUrls(url)                                    
                                    .Build();
                    }))
            };
        }
    }
}
