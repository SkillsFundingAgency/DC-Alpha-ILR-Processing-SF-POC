using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DCT.LARS.Model.Interface;
using DCT.PostcodeFactors.Model.Interface;
using DCT.ReferenceData.Interface;
using DCT.ReferenceData.Model.LARS;
using DCT.ReferenceData.Model.PostcodeFactors;
using ESFA.DC.Logging;

namespace DCT.ILR.FundingCalcService.ALBActor.RulebaseOverrides
{
    public class ReferenceDataCacheSF : IReferenceDataCache
    {
        private bool _initialized;

        private readonly ILarsContext _larsContext;

        private readonly IPostcodeFactorsReferenceDataContext _postcodeFactorsReferenceDataContext;
        private ILogger _logger;

        public string LarsVersion { get; set; }

        public string PostcodeFactorsVersion { get; set; }

        public Dictionary<string, LarsLearningDeliveryRD> LarsLearningDeliveries { get; private set; }

        public Dictionary<string, List<LarsFundingRD>> LarsFunding { get; private set; }

        public Dictionary<string, List<SfaAreaCostRD>> SfaAreaCost { get; private set; }

        public ReferenceDataCacheSF(ILarsContext larsContext, 
            IPostcodeFactorsReferenceDataContext postcodeFactorsReferenceDataContext,
            ILogger logger)
        {
            _larsContext = larsContext;
            _postcodeFactorsReferenceDataContext = postcodeFactorsReferenceDataContext;
            _logger = logger;
        }

        public void Populate(IEnumerable<string> learnAimRefs, IEnumerable<string> postcodes)
        {
            if (!_initialized)
            {
                var stopWatch = new Stopwatch();
                stopWatch.Start();

                Task larsLearningDeliveriesTask = new Task(() =>
                {
                    LarsLearningDeliveries = _larsContext.LarsLearningDelivery
                                             .Where(ld => learnAimRefs.Contains(ld.LearnAimRef))
                                             .Select(ld => new LarsLearningDeliveryRD()
                                             {
                                                 LearnAimRef = ld.LearnAimRef,
                                                 LearnAimRefType = ld.LearnAimRefType,
                                                 NotionalNVQLevelv2 = ld.NotionalNvqlevelv2,
                                                 RegulatedCreditValue = ld.RegulatedCreditValue
                                             }).ToDictionary(ld => ld.LearnAimRef, ld => ld);
                });

                Task larsFundingTask = new Task(() =>
                {
                    LarsFunding = _larsContext.LarsFunding
                                .Where(lf => learnAimRefs.Contains(lf.LearnAimRef))
                                .Select(lf => new LarsFundingRD()
                                {
                                    LearnAimRef = lf.LearnAimRef,
                                    FundingCategory = lf.FundingCategory,
                                    EffectiveFrom = lf.EffectiveFrom,
                                    EffectiveTo = lf.EffectiveTo,
                                    RateWeighted = lf.RateWeighted,
                                    WeightingFactor = lf.WeightingFactor
                                }).GroupBy(a => a.LearnAimRef)
                                .ToDictionary(a => a.Key, a => a.ToList());
                });

                Task sfaAreaCostTask = new Task(() =>
                {
                    SfaAreaCost = _postcodeFactorsReferenceDataContext.SfaPostcodeAreaCost
                                  .Where(p => postcodes.Contains(p.Postcode))
                                  .Select(p => new SfaAreaCostRD()
                                  {
                                      Postcode = p.Postcode,
                                      AreaCostFactor = p.AreaCostFactor,
                                      EffectiveFrom = p.EffectiveFrom,
                                      EffectiveTo = p.EffectiveTo
                                  }).GroupBy(a => a.Postcode)
                                .ToDictionary(a => a.Key, a => a.ToList());
                });

                Task larsVersionTask = new Task(() =>
                {
                    LarsVersion = _larsContext.LarsVersion.Select(lv => lv.MainDataSchemaName).Max().ToString();
                });

                Task postCodeFactorsVersionTask = new Task(() =>
                {
                    PostcodeFactorsVersion = _postcodeFactorsReferenceDataContext.PostCodeVersion.Select(lv => lv.MainDataSchemaName).Max().ToString();
                });

                var versionTasks = new List<Task>()
                {
                    larsVersionTask,
                    postCodeFactorsVersionTask,
                };

                versionTasks.ForEach(t => t.Start());

                Task.WaitAll(versionTasks.ToArray());

                var dataTasksOne = new List<Task>()
                {
                    larsFundingTask,
                    sfaAreaCostTask,
                };

                dataTasksOne.ForEach(t => t.Start());

                Task.WaitAll(dataTasksOne.ToArray());

                var dataTasksTwo = new List<Task>()
                {
                    larsLearningDeliveriesTask,
                };

                dataTasksTwo.ForEach(t => t.Start());

                Task.WaitAll(dataTasksTwo.ToArray());

                _initialized = true;

                _logger.LogInfo($"Get fundingcal ref data:{stopWatch.ElapsedMilliseconds} ");
            }
        }
    }
}
