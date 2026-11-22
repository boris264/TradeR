using System.Collections.Generic;
using System.Threading.Tasks;
using TradeR.Infrastructure.Models.Excel;
using TradeR.SharedKernel.Results;

namespace TradeR.Infrastructure.Interfaces
{
    public interface ITradeWriter
    {
        Task<Result> SaveTrades(string newWorkbookName, IEnumerable<TradeRow> trades);
    }
}
