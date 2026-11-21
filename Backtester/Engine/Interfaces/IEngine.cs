using System.Threading;
using System.Threading.Tasks;
using TradeR.SharedKernel.Results;

namespace Backtester.Engine.Interfaces
{
    internal interface IEngine
    {
        Task<Result> StartAsync(CancellationToken cancellationToken);
    }
}
