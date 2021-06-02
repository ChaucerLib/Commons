using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Commons
{
    public class LoopService
    {
        private readonly IWorker _worker;
        private readonly TimeSpan _loopDelay;
        private readonly CancellationTokenSource _cts;
        private readonly ILogger<LoopService> _logger;

        /// <param name="worker"></param>
        /// <param name="loopDelay">Duration to wait before running the loop again. If the loop is meant to run every 3 seconds, and the previous iteration took
        /// 2 seconds, the wait will be 1 second. If the previous iteration took 4 seconds, the wait will be zero.</param>
        /// <param name="cts"></param>
        /// <param name="logger"></param>
        public LoopService(IWorker worker, TimeSpan loopDelay, CancellationTokenSource cts, ILogger<LoopService> logger)
        {
            _worker = worker ?? throw new ArgumentNullException(nameof(worker));
            _loopDelay = loopDelay < TimeSpan.FromSeconds(1)
                ? throw new ArgumentOutOfRangeException($"Loop delay must be at least 1 second")
                : loopDelay;
            _cts = cts ?? throw new ArgumentNullException(nameof(cts));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task LoopAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                _logger.LogInformation("Starting work loop");
                var timer = Stopwatch.StartNew();

                try
                {
                    await _worker.DoWorkAsync(_cts.Token);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Loop threw an exception");
                }

                timer.Stop();
                _logger.LogInformation($"Finished work loop in {timer.ElapsedMilliseconds:N0}ms");

                if (_loopDelay > TimeSpan.Zero)
                {
                    var toWait = GetSleepDelay(_loopDelay, timer.Elapsed);
                    var sb = new StringBuilder();
                    sb.Append("Sleeping for ");

                    var prior = false;
                    if (toWait.Hours > 0)
                    {
                        prior = true;
                        sb.Append($"{toWait.Hours:N0} hours ");
                    }

                    if (toWait.Minutes > 0 || prior)
                    {
                        prior = true;
                        sb.Append($"{toWait.Minutes:N0} minutes ");
                    }

                    if (prior || toWait.Seconds > 0)
                    {
                        prior = true;
                        sb.Append($"{toWait.Seconds:N0} seconds ");
                    }

                    if (prior || toWait.Milliseconds > 0)
                    {
                        sb.Append($"{toWait.Milliseconds:N0}ms");
                    }
                    
                    _logger.LogInformation(sb.ToString());
                    await Task.Delay(toWait, _cts.Token);
                }
            }
        }

        internal static TimeSpan GetSleepDelay(TimeSpan delay, TimeSpan elapsed)
        {
            var toWait = delay - elapsed;
            return toWait > TimeSpan.Zero
                ? toWait
                : TimeSpan.Zero;
        }
    }
}