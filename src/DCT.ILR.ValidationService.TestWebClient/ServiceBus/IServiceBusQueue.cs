using System.Threading.Tasks;

namespace DCT.ILR.ValidationService.TestWebClient.ServiceBus
{
    public interface IServiceBusQueue
    {
        Task SendMessagesAsync(string messageToSend, string sessionId);
    }
}