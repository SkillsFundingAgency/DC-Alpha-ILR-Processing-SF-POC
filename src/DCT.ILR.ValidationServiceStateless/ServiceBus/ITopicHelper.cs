using System.Threading.Tasks;
using Microsoft.ServiceBus.Messaging;

namespace DCT.ILR.ValidationServiceStateless.ServiceBus
{
    public interface ITopicHelper
    {
        Task SendMessage(BrokeredMessage message);
    }
}