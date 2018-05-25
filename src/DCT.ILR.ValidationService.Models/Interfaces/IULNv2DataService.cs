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
    public interface IULNv2DataService : IService
    {
        [OperationContract]
        Task InsertULNs(IEnumerable<long> ulns);

        [OperationContract]
        Task<IEnumerable<long>> GetULNs(IEnumerable<long> ulns);

        [OperationContract]
        Task<bool> ClearULNs();
    }
}
