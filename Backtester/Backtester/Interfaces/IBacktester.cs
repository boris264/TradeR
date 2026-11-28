using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Backtester.Backtester.Interfaces
{
    internal interface IBacktester<T>
    {
        Task RunAsync(IEnumerable<T> engineConfigurations, CancellationToken cancellationToken);
    }
}
