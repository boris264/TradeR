using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TradeR.Infrastructure.Models.Excel;
using TradeR.SharedKernel.Results;

namespace Backtester.Engine.Interfaces
{
    internal interface IEngine
    {
        Task<Result<IEnumerable<TradeRow>>> StartAsync(CancellationToken cancellationToken);
    }
}
