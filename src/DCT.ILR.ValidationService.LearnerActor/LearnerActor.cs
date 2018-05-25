using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Runtime;
using Microsoft.ServiceFabric.Actors.Client;
using DCT.ILR.ValidationService.LearnerActor.Interfaces;
using DCT.ILR.Model;
using DCT.ValidationService.Service.Interface;
using System.Diagnostics;
using BusinessRules.POC.Interfaces;
using Autofac;
using ESFA.DC.Logging;
using Newtonsoft.Json;

namespace DCT.ILR.ValidationService.LearnerActor
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
    public class LearnerActor : Actor, ILearnerActor, IDisposable
    {
        private ILifetimeScope _parentLifeTimeScope;
        private ActorId _actorId;


        /// <summary>
        /// Initializes a new instance of LearnerActor
        /// </summary>
        /// <param name="actorService">The Microsoft.ServiceFabric.Actors.Runtime.ActorService that will host this actor instance.</param>
        /// <param name="actorId">The Microsoft.ServiceFabric.Actors.ActorId for this actor instance.</param>
        public LearnerActor(ActorService actorService, ActorId actorId, ILifetimeScope parentLifeTimeScope)
            : base(actorService, actorId)
        {
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

            return this.StateManager.TryAddStateAsync("count", 5);
        }

        public async Task<string> Validate(string correlationId, Message message, MessageLearner[] shreddedLearners)
        {
            using (var childLifeTimeScope = _parentLifeTimeScope.BeginLifetimeScope())
            {
                var logger = childLifeTimeScope.Resolve<ILogger>();
                logger.StartContext(correlationId, _actorId.ToString());
                logger.LogInfo("Started Validation learnerActor");
                var validationService = childLifeTimeScope.Resolve<IValidationService>();

                message.Learner = shreddedLearners;

                var stopwatch = new Stopwatch();
                stopwatch.Start();

                var results = await validationService.Validate(message);
                
                //IEnumerable<string> productIDs = await StateManager.GetStateNamesAsync();
                var jsonResult = JsonConvert.SerializeObject(results);
                logger.LogInfo($"Completed Validation LearnerActor in :{stopwatch.ElapsedMilliseconds} ");

                return jsonResult;
            }

        }
        

        Task<int> ILearnerActor.GetCountAsync(CancellationToken cancellationToken)
        {
            return this.StateManager.GetStateAsync<int>("count", cancellationToken);
        }

        protected override Task OnDeactivateAsync()
        {
            return base.OnDeactivateAsync();
        }

        public void Dispose()
        {
               
        }
    }
}
