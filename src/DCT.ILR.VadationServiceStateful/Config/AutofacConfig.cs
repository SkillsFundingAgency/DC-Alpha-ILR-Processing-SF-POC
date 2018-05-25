using Autofac;
using BusinessRules.POC.FileData;
using BusinessRules.POC.FileData.Interface;
using BusinessRules.POC.Interfaces;
using DCT.ILR.Model;
using DCT.LARS.Model;
using DCT.LARS.Model.Interface;
using DCT.ULN.Model;
using DCT.ULN.Model.Interface;
using DCT.ValidationService.Service.Implementation;
using DCT.ValidationService.Service.ReferenceData.Implementation;
using DCT.ValidationService.Service.ReferenceData.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DCT.ILR.VadationServiceStateful.Config
{
    public class AutofacConfig
    {
        public class ValidationServiceServiceModuleSF : Module
        {
            protected override void Load(ContainerBuilder builder)
            {
                //register local modules here.

                builder.RegisterType<ULNv2>().As<IULNv2Context>().InstancePerMatchingLifetimeScope("childLifeTimeScope"); 
                builder.RegisterType<LARSContext>().As<ILARSContext>().InstancePerMatchingLifetimeScope("childLifeTimeScope");
                builder.RegisterType<ReferenceDataCache>().As<IReferenceDataCache>().InstancePerMatchingLifetimeScope("childLifeTimeScope");
                builder.RegisterType<FileData>().As<IFileData>().InstancePerMatchingLifetimeScope("childLifeTimeScope");
                builder.RegisterType<LearnerValidationErrorHandler>()
                    .As<IValidationErrorHandler<MessageLearner>>().InstancePerMatchingLifetimeScope("childLifeTimeScope");
            }
        }

    }
}
