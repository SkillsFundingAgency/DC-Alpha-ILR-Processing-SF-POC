using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using BusinessRules.POC.Interfaces;
using DCT.ILR.Model;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Remoting.FabricTransport;
using Microsoft.ServiceFabric.Services.Remoting;


namespace DCT.ILR.ValidationService.LearnerActor.Interfaces
{
    /// <summary>
    /// This interface defines the methods exposed by an actor.
    /// Clients use this interface to interact with the actor that implements it.
    /// </summary>
    [ServiceContract]
    public interface ILearnerActor : IActor
    {
        [OperationContract]
        Task<string> Validate(string correlationId, Message message, MessageLearner[] shreddedLearners);

        [OperationContract]
        Task<int> GetCountAsync(CancellationToken cancellationToken);
    }
}
