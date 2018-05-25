using DCT.ILR.ValidationService.Models.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using DCT.ValidationService.Service.Interface;

namespace DCT.ILR.ValidationService.Models.Interfaces
{
    [ServiceContract]
    public interface IValidationServiceStateless
    {
        [OperationContract]
        Task<bool> Validate(IlrContext ilrContext, IValidationService validationService);

        [OperationContract]
        Task<IEnumerable<string>> GetResults(Guid correlationId);
    }
}
