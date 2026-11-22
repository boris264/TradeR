using ClosedXML.Excel;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TradeR.Infrastructure.Interfaces;
using TradeR.Infrastructure.Models.Excel;
using TradeR.SharedKernel.Results;

namespace TradeR.Infrastructure.Services
{
    public class TradeWriter : ITradeWriter
    {
        private static readonly string tradesTemplateName = Path.Join("Templates", "BacktestTemplate.xlsx");
        private static readonly string tradesWorksheetName = "Trades";
        private const int cellBelowHeaderIndex = 2;
        private const int cellFirstColumn = 1;

        public async Task<Result> SaveTrades(string newWorkbookName, IEnumerable<TradeRow> trades)
        {
            using var workbook = new XLWorkbook(tradesTemplateName);

            if (workbook == null)
            {
                return Result.Failure(new Error("TradeWriter.Workbook", "Failed to open Excel template for trades!"));
            }

            var tradesSheetExists = workbook.TryGetWorksheet(tradesWorksheetName, out var tradesSheet);

            if (!tradesSheetExists)
            {
                return Result.Failure(new Error("TradeWriter.Sheet", "Sheet Trades doesn't exist in the template!"));
            }

            tradesSheet.Cell(cellBelowHeaderIndex, cellFirstColumn).InsertData(trades);
            workbook.SaveAs(newWorkbookName);

            return Result.Success();
        }
    }
}
