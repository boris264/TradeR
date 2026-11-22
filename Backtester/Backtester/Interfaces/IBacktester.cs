using Backtester.Engine;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Backtester.Backtester.Interfaces
{
    internal interface IBacktester
    {
        Task RunAsync(
            IEnumerable<EngineConfiguration> engineConfigurations,
            CancellationToken cancellationToken);
    }
}
