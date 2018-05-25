using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Fabric;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using Autofac.Features.AttributeFilters;
using DCT.Funding.Model.Outputs;
using DCT.ILR.FundingCalcService.ALBActor.Interfaces;
using DCT.ILR.FundingCalcService.Models;
using DCT.ILR.Model;
using DCT.ILR.ValidationService.Models;
using DCT.ILR.ValidationService.Models.Interfaces;
using DCT.ILR.ValidationService.Models.Models;
using ESFA.DC.Logging;
using Hydra.Core.Sharding;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Client;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Client;
using Microsoft.ServiceFabric.Services.Remoting.Client;

namespace DCT.ILR.FundingCalcService
{
    public class FundingCalcManager : IFundingCalcManager
    {
        private ILogger _logger;
        private IActorsHelper _actorsHelper;
        private Uri _albActorUri;
        private Uri _validationResultsServiceUri;
        private ServiceProxyFactory _serviceProxyFactory;

        public FundingCalcManager(ILogger logger, IActorsHelper actorsHelper, ActorOptions actorOptions,
            ServiceProxyFactory serviceProxyFactory, DataServiceOptions dataServiceOptions )
        {
             var context = FabricRuntime.GetActivationContext();
            _logger = logger;
            _actorsHelper = actorsHelper;
            _albActorUri =
                new Uri($"{context.ApplicationName}/{actorOptions.ALBActorName}");

            _validationResultsServiceUri =
                new Uri(
                    $"{context.ApplicationName}/{dataServiceOptions.ValidationResultsServiceName}");

            _serviceProxyFactory = serviceProxyFactory;
        }

        public async Task ProcessJobs(string correlationId)
        {
            var stopWatch = new Stopwatch();

            stopWatch.Start();
            //read the message from data service
            var message = await GetValidLearnersFromDataService(correlationId);
            var getFromDSMs = stopWatch.ElapsedMilliseconds;
            _logger.LogInfo($"Get data from DS:{getFromDSMs}");
            stopWatch.Restart();

            var ruleBaseTasks = new List<Task>();
            for (int i = 0; i < 8; i++)
            {
                ruleBaseTasks.Add(ProcessALBRuleBase(message.DeepClone(), correlationId));
            }

            await Task.WhenAll(ruleBaseTasks);
        }

        private async Task ProcessALBRuleBase(Message message, string correlationId)
        {
            var stopWatch = new Stopwatch();
            var startDateTime = DateTime.Now;
            stopWatch.Start();

            var learnerShards = message.Learner.ToList()
                .SplitList(_actorsHelper.GetLearnersPerActor(message.Learner.Count()));
            message.Learner = null;

            var actorTasks = new List<Task<FundingServiceOutputs>>();
            foreach (var learnerShard in learnerShards)
            {
                var albActorProxy = GetALBActor();
                actorTasks.Add(albActorProxy.ProcessFunding(correlationId,
                    message, learnerShard.ToArray()));
            }
            _logger.LogInfo($"Created {actorTasks.Count} actors.");
            await Task.WhenAll(actorTasks);

            var finalResult = new FundingServiceOutputs()
            {
                LearnerPeriodAttributeOutputses = new GlobalOutputs.LearnerPeriodAttributeOutputs[] {},
                LearnerPeriodisedValuesOutputs = new GlobalOutputs.LearnerPeriodisedValuesOutput[]{},
                LearnerPeriodOutputs = new GlobalOutputs.LearnerPeriodOutput[]{},
                LearningDeliveryOutputs = new GlobalOutputs.LearningDeliveryOutput[]{},
                LearningDeliveryPeriodAttributeOutputses = new GlobalOutputs.LearningDeliveryPeriodAttributeOutputs[]{},
                LearningDeliveryPeriodOutputs = new GlobalOutputs.LearningDeliveryPeriodOutput[]{},
                LearningDeliveryPeriodisedValuesOutputs = new GlobalOutputs.LearningDeliveryPeriodisedValuesOutput[] {}
            };
            foreach (var actorTask in actorTasks)
            {
                finalResult.GlobalOutputs = actorTask.Result.GlobalOutputs;
                finalResult.LearnerPeriodAttributeOutputses?.ToList().AddRange(actorTask.Result.LearnerPeriodAttributeOutputses);
                finalResult.LearnerPeriodOutputs.ToList().AddRange(actorTask.Result.LearnerPeriodOutputs);
                finalResult.LearnerPeriodisedValuesOutputs.ToList().AddRange(actorTask.Result.LearnerPeriodisedValuesOutputs);
                finalResult.LearningDeliveryOutputs.ToList().AddRange(actorTask.Result.LearningDeliveryOutputs);
                finalResult.LearningDeliveryPeriodAttributeOutputses.ToList().AddRange(actorTask.Result.LearningDeliveryPeriodAttributeOutputses);
                finalResult.LearningDeliveryPeriodOutputs.ToList().AddRange(actorTask.Result.LearningDeliveryPeriodOutputs);
                finalResult.LearningDeliveryPeriodisedValuesOutputs.ToList()
                    .AddRange(actorTask.Result.LearningDeliveryPeriodisedValuesOutputs);
            }

            var opaJobMs = stopWatch.ElapsedMilliseconds;

            var endTime = DateTime.Now;

            var processTimes = new List<string>()
            {
                $"Start Time : {startDateTime}",
                $"OPA job : {opaJobMs}",
                $"End Time : {endTime}",
                $"Total Time : {(endTime - startDateTime).TotalMilliseconds}",
            };

            stopWatch.Restart();

            //save results in DS.
            await SaveResultsToDataService(correlationId, processTimes);
            var dataSavedtime = stopWatch.ElapsedMilliseconds;
            
            _logger.LogInfo(
                $"Results saved in DS in {dataSavedtime} ");

            _logger.LogInfo(
                $"Completed ALB Rulebase for jobId:{correlationId} in {(DateTime.Now - startDateTime).TotalMilliseconds} ");

        }


        private async Task SaveResultsToDataService(string correlationId, IEnumerable<string> results)
        {
            var fundingCalcResultsService = _serviceProxyFactory.CreateServiceProxy<IFundingCalcResults>(
                _validationResultsServiceUri,
                new ServicePartitionKey(0), TargetReplicaSelector.PrimaryReplica, "dataServiceRemotingListener");

            await fundingCalcResultsService.SaveFCResultsAsync(correlationId, results);

        }


        private async Task<Message> GetValidLearnersFromDataService(string correlationId)
        {
            var validationResultsService = GetValResultsProxy(correlationId, true);
            //            var validationResultsService = _serviceProxyFactory.CreateServiceProxy<IValidationServiceResults>(
            //                _validationResultsServiceUri,
            //                new ServicePartitionKey(0), TargetReplicaSelector.RandomReplica, "dataServiceRemotingListener");

            //read the xml string from DS
            var messageBytes = await validationResultsService.GetFullMessage(correlationId);
            //unzip the string
            var xmlString = await messageBytes.ToArray().Unzip<string>();

            //deserialize again (this is a work around as the validation service ILR model is diff from fundingcal model.
            using (var reader = XmlReader.Create(new StringReader(xmlString)))
            {
                var serializer = new XmlSerializer(typeof(Message));
                return serializer.Deserialize(reader) as Message;
            }


        }

        private IValidationServiceResults GetValResultsProxy(string correlationId, bool isRead)
        {
            //get the partition 
            var shardNumber = new JumpSharding().GetShard(correlationId, 10);

            return _serviceProxyFactory.CreateServiceProxy<IValidationServiceResults>(
                _validationResultsServiceUri,
                new ServicePartitionKey(shardNumber),
                isRead ? TargetReplicaSelector.RandomReplica : TargetReplicaSelector.PrimaryReplica,
                "dataServiceRemotingListener");
        }
        private IALBActor GetALBActor()
        {
            return ActorProxy.Create<IALBActor>(ActorId.CreateRandom(), _albActorUri);
        }
    }

    public static class Extensions
    {
        public static IEnumerable<List<T>> SplitList<T>(this List<T> locations, int nSize = 30)
        {
            for (int i = 0; i < locations.Count; i += nSize)
            {
                yield return locations.GetRange(i, Math.Min(nSize, locations.Count - i));
            }
        }

        // Deep clone
        public static T DeepClone<T>(this T a)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(stream, a);
                stream.Position = 0;
                return (T)formatter.Deserialize(stream);
            }
        }

    }
}
