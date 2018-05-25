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
using DCT.ValidationService.Service.Interface;

namespace DCT.ILR.ValidationServiceStateless.Modules
{
    public class ValidationServiceServiceModuleSF : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            //register local modules here.

            builder.RegisterType<ULNv2>().As<IULNv2Context>().InstancePerLifetimeScope();
            builder.RegisterType<LARSContext>().As<ILARSContext>().InstancePerLifetimeScope();
//            builder.RegisterType<ReferenceDataCache>().As<IReferenceDataCache>();
            builder.RegisterType<ReferenceDataCacheSF>().As<IReferenceDataCache>().InstancePerLifetimeScope();
            builder.RegisterType<FileData>().As<IFileData>().InstancePerLifetimeScope();
            builder.RegisterType<LearnerValidationErrorHandler>()
                .As<IValidationErrorHandler<MessageLearner>>().InstancePerLifetimeScope();
        }
    }

}
