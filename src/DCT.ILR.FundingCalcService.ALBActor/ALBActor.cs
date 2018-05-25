using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Autofac.Features.OwnedInstances;
using DCT.Funding.Model.Outputs;
using DCT.FundingService.Service.Interface;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Runtime;
using Microsoft.ServiceFabric.Actors.Client;
using DCT.ILR.FundingCalcService.ALBActor.Interfaces;
using DCT.ILR.Model;
using ESFA.DC.Logging;

namespace DCT.ILR.FundingCalcService.ALBActor
{
    /// <remarks>
    /// This class represents an actor.
    /// Every ActorID maps to an instance of this class.
    /// The StatePersistence attribute determines persistence and replication of actor state:
    ///  - Persisted: State is written to disk and replicated.
    ///  - Volatile: State is kept in memory only and replicated.
    ///  - None: State is kept in memory only and not replicated.
    /// </remarks>
    [StatePersistence(StatePersistence.Volatile)]
    public class ALBActor : Actor, IALBActor
    {
        private Func<IFundingService> _fundingServiceFactory;
        private ILogger _logger;
        private ILifetimeScope _parentLifeTimeScope;
        private ActorId _actorId;

        /// <summary>
        /// Initializes a new instance of ALBActor
        /// </summary>
        /// <param name="actorService">The Microsoft.ServiceFabric.Actors.Runtime.ActorService that will host this actor instance.</param>
        /// <param name="actorId">The Microsoft.ServiceFabric.Actors.ActorId for this actor instance.</param>
        public ALBActor(ActorService actorService, ActorId actorId, Func<IFundingService> fundingServiceFactory,
            ILogger logger, ILifetimeScope parentLifeTimeScope)
            : base(actorService, actorId)
        {
            _fundingServiceFactory = fundingServiceFactory;
            _logger = logger;
            _parentLifeTimeScope = parentLifeTimeScope;
            _actorId = actorId;


        }

        /// <summary>
        /// This method is called whenever an actor is activated.
        /// An actor is activated the first time any of its methods are invoked.
        /// </summary>
        protected override Task OnActivateAsync()
        {
            ActorEventSource.Current.ActorMessage(this, "Actor activated.");

            // The StateManager is this actor's private state store.
            // Data stored in the StateManager will be replicated for high-availability for actors that use volatile or persisted state storage.
            // Any serializable object can be saved in the StateManager.
            // For more information, see https://aka.ms/servicefabricactorsstateserialization

            return this.StateManager.TryAddStateAsync("count", 0);
        }


        public async Task<FundingServiceOutputs> ProcessFunding(string correlationId, Message message, MessageLearner[] learners)
        {
            var stopwatch = new Stopwatch();

            message.Learner = learners;
            using (var childLifeTimeScope = _parentLifeTimeScope.BeginLifetimeScope("childLifeTimeScope"))
            {
                var logger = childLifeTimeScope.Resolve<ILogger>();

                try
                {
                    logger.StartContext(correlationId, _actorId.ToString());
                    var fundingService = childLifeTimeScope.Resolve<IFundingService>();
                    logger.LogInfo($"Start ALB Actor processing :{DateTime.Now} ");
                    stopwatch.Start();
                    var results = fundingService.ProcessFunding(message);
                    //                    var fundingService = childLifeTimeScope.Resolve<IFundingService>();
                    //                    var results = fundingService.ProcessFunding(message);
                    stopwatch.Stop();
                    var completedTime = stopwatch.Elapsed;
                    logger.LogInfo($"Completed ALB Actor processing :{completedTime} ");
                    return results;
                }
                catch (Exception ex)
                {
                    logger.LogError("Error in ALB Actor processFunding", ex);
                    //throw;
                    return null;
                }
            }
        }



    }
}
