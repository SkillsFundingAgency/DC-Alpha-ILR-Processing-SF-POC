using Microsoft.ServiceFabric.Services.Remoting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace DCT.ILR.ValidationService.Models.Interfaces
{
    [ServiceContract]
    public interface IReferenceDataService : IService
    {
        [OperationContract]
        Task InsertLARSData();

        [OperationContract]
        Task<IDictionary<string, string>> GetLARSLearningDeliveriesAsync(IEnumerable<string> learnAimRefs);

        [OperationContract]
        Task<string> GetLARSFromDB(IEnumerable<string> learnAimRefs);

        [OperationContract]
        Task InsertULNs();

        [OperationContract]
        Task<IEnumerable<long>> GetULNs(IEnumerable<long> ulns);
    }
}
