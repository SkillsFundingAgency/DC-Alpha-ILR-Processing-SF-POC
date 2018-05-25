using DCT.LARS.Model;
using DCT.ReferenceData.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DCT.ILR.Data.Entities
{
    public class LARSDataHelper
    {
        public Dictionary<string,string> GetLearningDeliveries()
        {
            //get data from db
            using (var larsContext = new LARSContext())
            {
                var larsLearningDeliveries = larsContext.LARS_LearningDelivery
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
                                        }).ToDictionary(ld => ld.LearnAimRef, ld => JsonConvert.SerializeObject(ld));

                return larsLearningDeliveries;
            }
        }
        public Dictionary<string, LearningDelivery> GetLearningDelivery(IEnumerable<string> learnAimRefs)
        {
            //get data from db
            using (var larsContext = new LARSContext())
            {
                var larsLearningDeliveries = larsContext.LARS_LearningDelivery
                                        .Where(ld=> learnAimRefs.Contains(ld.LearnAimRef))
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

                return larsLearningDeliveries;
            }
        }
    }
}
