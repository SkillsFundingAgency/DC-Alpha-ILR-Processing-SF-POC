using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Remoting;

namespace DCT.ILR.ValidationService.Models.Interfaces
{
    [ServiceContract]
    public interface IFundingCalcResults : IService
    {
        [OperationContract]
        Task SaveFCResultsAsync(string correlationId, IEnumerable<string> fundingCalcResults);

        [OperationContract]
        Task<IEnumerable<string>> GetFCResultsAsync(string correlationId);
    }
}
