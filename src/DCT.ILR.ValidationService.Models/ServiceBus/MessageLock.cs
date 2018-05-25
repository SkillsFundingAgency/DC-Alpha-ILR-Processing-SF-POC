using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceBus.Messaging;

namespace DCT.ILR.ValidationService.Models.ServiceBus
{
    public class MessageLock : IDisposable
    {
        private BrokeredMessage _message;
        private Timer _timer;

        private readonly AsyncLock _asyncLock = new AsyncLock();

        public MessageLock(BrokeredMessage message)
        {
            this._message = message;
            InitializeTimer();
        }

        public async Task CompleteAsync()
        {
            using (await _asyncLock.LockAsync())
            {
                _timer.Dispose();

                if (_message == null)
                    return;

                try
                {
                    await _message.CompleteAsync();
                }
                catch /*(Exception e)*/
                {
                    // log it
                }

                _message = null;
            }
        }

        public async Task AbandonAsync()
        {
            using (await _asyncLock.LockAsync())
            {
                _timer.Dispose();

                if (_message == null)
                    return;

                try
                {
                    await _message.AbandonAsync();
                }
                catch /*(Exception e)*/
                {
                    // log it
                }

                _message = null;
            }
        }

        private void InitializeTimer()
        {
            var renewInterval =
                new TimeSpan((long)Math.Round(
                    _message.LockedUntilUtc.Subtract(DateTime.UtcNow)
                        .Ticks * 0.7, 0, MidpointRounding.AwayFromZero));

            _timer = new Timer(async state =>
            {
                using (await _asyncLock.LockAsync())
                {
                    if (_message == null)
                        return;

                    try
                    {
                        await _message.RenewLockAsync();
                        _timer.Change(renewInterval,
                            TimeSpan.FromMilliseconds(-1));
                    }
                    catch /*(Exception e)*/
                    {
                        // log it
                    }
                }
            }, null, renewInterval, TimeSpan.FromMilliseconds(-1));
        }

        public void Dispose()
        {
            AbandonAsync().Wait();
        }
    }
}
