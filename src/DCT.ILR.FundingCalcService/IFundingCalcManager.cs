using System.Threading.Tasks;

namespace DCT.ILR.FundingCalcService
{
    public interface IFundingCalcManager
    {
        Task ProcessJobs(string correlationId);
    }
}