using System;
using System.Collections.Generic;
using System.Fabric;
using System.Fabric.Description;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using DCT.ILR.ValidationService.Models;
using DCT.ILR.ValidationService.Models.Models;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Communication.Wcf;
using Microsoft.ServiceFabric.Services.Communication.Wcf.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.ServiceFabric.Services.Runtime;

namespace DCT.ILR.ValidationService.Client
{
    /// <summary>
    /// An instance of this class is created for each service instance by the Service Fabric runtime.
    /// </summary>
    internal sealed class Client : StatelessService, ITestValidationClient
    {
        public Client(StatelessServiceContext context)
            : base(context)
        { }

        public Task<int> Execute()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Optional override to create listeners (e.g., TCP, HTTP) for this service replica to handle client or user requests.
        /// </summary>
        /// <returns>A collection of listeners.</returns>
        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            return null;
            //return new[] { new ServiceReplicaListener((context) =>
            //    new WcfCommunicationListener<ITestValidationClient>(
            //        wcfServiceObject:this,
            //        serviceContext:context,
            //        //
            //        // The name of the endpoint configured in the ServiceManifest under the Endpoints section
            //        // that identifies the endpoint that the WCF ServiceHost should listen on.
            //        //
            //        endpointResourceName: "WcfServiceEndpoint",

            //        //
            //        // Populate the binding information that you want the service to use.
            //        //
            //        listenerBinding: WcfUtility.CreateTcpListenerBinding()
            //    )
            //)};
        }

        /// <summary>
        /// This is the main entry point for your service instance.
        /// </summary>
        /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service instance.</param>
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            // TODO: Replace the following sample code with your own logic 
            //       or remove this RunAsync override if it's not needed in your service.

            long iterations = 0;

         //   while (true)
          //  {
                cancellationToken.ThrowIfCancellationRequested();


                //var clientProxy = ServiceProxy.Create<IValidationServiceStateful>(
                //    new Uri("fabric:/DCT.ILR.Processing.POC/DCT.ILR.VadationServiceStateful"),
                //    new ServicePartitionKey(LongRandom()));

                //var result = clientProxy.Validate(new IlrContext()
                //{
                //    Filename = "ILR-10006341-1718-20171107-113456-02.xml",
                //    ContainerReference = "ilr"
                //});


                ServiceEventSource.Current.ServiceMessage(this.Context, "Working-{0}", ++iterations);



                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            //}
        }

        private long LongRandom()
        {
            byte[] buf = new byte[8];
            new Random().NextBytes(buf);
            long longRand = BitConverter.ToInt64(buf, 0);
            return longRand;
        }
    }

    [ServiceContract]
    public interface ITestValidationClient
    {
        [OperationContract]
        Task<int> Execute();
    }


}
