using DCT.ReferenceData.Model;
using DCT.ValidationService.Service.ReferenceData.Interface;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Client;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DCT.LARS.Model.Interface;
using DCT.ULN.Model.Interface;
using ESFA.DC.Logging;

namespace DCT.ILR.ValidationService.LearnerActor.Config
{
    public class ReferenceDataCacheSF : IReferenceDataCache
    {
        private IEnumerable<long> _ulns = new HashSet<long>();
//        private IReferenceDataService _referenceDataService;
        private IDictionary<string, LearningDelivery> _learningDeliveries = new Dictionary<string, LearningDelivery>();
        private readonly Func<IULNv2Context> _ulnv2ContextFactory;
        private readonly Func<ILARSContext> _larsContextFactory;
        private ILogger _logger;


        public ReferenceDataCacheSF(Func<IULNv2Context> ulnv2ContextFactory, Func<ILARSContext> larsContextFactory, ILogger logger)
        {
            //ServiceProxyFactory serviceProxyFactory = new ServiceProxyFactory(
            //    (c) => new FabricTransportServiceRemotingClientFactory(serializationProvider: new ServiceRemotingJsonSerializationProvider()));

            //_referenceDataService = serviceProxyFactory.CreateServiceProxy<IReferenceDataService>(
            //   new Uri("fabric:/DCT.ILR.Processing.POC/DCT.ILR.Data"),
            //   new ServicePartitionKey(0), TargetReplicaSelector.RandomReplica, "dataServiceRemotingListener");

            _ulnv2ContextFactory = ulnv2ContextFactory;
            _larsContextFactory = larsContextFactory;
            _logger = logger;

        }

        public IEnumerable<long> ULNs
        {
            get { return _ulns; }
            private set { _ulns = value; }
        }

        public IDictionary<string, LearningDelivery> LearningDeliveries
        {
            get { return _learningDeliveries; }
            private set { _learningDeliveries = value; }
        }

        public async Task Populate(IEnumerable<long> ulns, IEnumerable<string> learnAimRefs)
        {
                var stopWatch = new Stopwatch();
                stopWatch.Start();
//                var getULNsTask = _referenceDataService.GetULNs(ulns);
                var getULNsTask = Task.Run(() =>
                {
                    ULNs = new HashSet<long>(_ulnv2ContextFactory().UniqueLearnerNumbers2
                        .Where(u => ulns.Contains(u.ULN))
                        .Select(uln => uln.ULN));
                });

//                var getLARSLdsTask = _referenceDataService.GetLARSLearningDeliveriesAsync(learnAimRefs);
                var getLARSLdsTask = Task.Run(() =>
                {
                    LearningDeliveries = _larsContextFactory().LARS_LearningDelivery
                                        .Where(ld => learnAimRefs.Contains(ld.LearnAimRef))
                                        .Select(ld => new LearningDelivery()
                                        {
                                            LearnAimRef = ld.LearnAimRef,
                                            NotionalNVQLevelv2 = ld.NotionalNVQLevelv2,
                                            LearningDeliveryCategories = ld.LARS_LearningDeliveryCategory.Select
                                            (
                                                ldc => new LearningDeliveryCategory()
                                                {
                                                    CategoryRef = ldc.CategoryRef,
                                                    EffectiveFrom = ldc.EffectiveFrom,
                                                    EffectiveTo = ldc.EffectiveTo,
                                                    LearnAimRef = ldc.LearnAimRef
                                                }
                                            ),
                                            FrameworkAims = ld.LARS_FrameworkAims.Select
                                            (
                                                fa => new FrameworkAim()
                                                {
                                                    FworkCode = fa.FworkCode,
                                                    ProgType = fa.ProgType,
                                                    PwayCode = fa.PwayCode,
                                                    LearnAimRef = fa.LearnAimRef,
                                                    EffectiveFrom = fa.EffectiveFrom,
                                                    EffectiveTo = fa.EffectiveTo,
                                                    FrameworkComponentType = fa.FrameworkComponentType
                                                }
                                            ),
                                            AnnualValues = ld.LARS_AnnualValue.Select
                                            (
                                                av => new AnnualValue()
                                                {
                                                    LearnAimRef = av.LearnAimRef,
                                                    EffectiveFrom = av.EffectiveFrom,
                                                    EffectiveTo = av.EffectiveTo,
                                                    BasicSkills = av.BasicSkills
                                                }
                                            )
                                        }).ToDictionary(ld => ld.LearnAimRef, ld => ld);
                });

                await Task.WhenAll(getULNsTask, getLARSLdsTask);
//                await getULNsTask;
//                var ulnsfromDbinMs = stopWatch.ElapsedMilliseconds;
//                stopWatch.Restart();
//                await getLARSLdsTask;
//               // await getLARSLdsTask;
//               // await Task.WhenAll(getULNsTask, getLARSLdsTask);
//                logger.LogInfo($"completed get ULN from SF in:{ulnsfromDbinMs}");
//      
                stopWatch.Stop();
                _logger.LogInfo($"Got ref data from DB in : {stopWatch.ElapsedMilliseconds}");
//                logger.LogInfo($"ULns:{ULNs.Count()}");
                // _ulns = getULNsTask.Result;
                //_learningDeliveries = getLARSLdsTask.Result.ToDictionary(e => e.Key,
                //    e => JsonConvert.DeserializeObject<LearningDelivery>(e.Value));
        }

       
    }
}
