# TradeR

A .NET toolkit for backtesting crypto trading strategies on historical [Kraken](https://www.kraken.com) market data.

> **Status: work in progress.** Data import, the Kraken API client and the first technical indicators are done. The Backtester is designed (see [docs/backtester.md](docs/backtester.md)) but not implemented yet.

## Features

- **Kraken historical data importer:** bulk-loads Kraken's downloadable OHLCVT (candle) and trade CSV files into PostgreSQL.
  - It loads several files at once and uses Npgsql's binary `COPY`.
  - Re-running an import, or loading Kraken's quarterly update files on top, skips rows that are already stored instead of duplicating them.
- **Kraken REST API client:** fetches recent candles from the public OHLC endpoint.
  - Retries, a circuit breaker and timeouts are built in.
  - Failures come back as `Result` values instead of exceptions.
- **Technical indicators:** Simple and Exponential Moving Average (SMA, EMA).
  - They take plain numbers, so they work with any exchange's data, not just Kraken's.
  - Each can be fed one candle at a time or run over a whole series, with identical results.
- **Tests:** an NUnit test suite for the indicators.

### Planned: Backtester
- Runs multiple strategies in parallel. Each strategy has its own pairs, interval, account size and stop loss.
- Fills are simulated from candles, with Kraken's fees.
- Every trade is written to an Excel workbook created from a template, whose formulas and charts summarize the results.

The full design is in [docs/backtester.md](docs/backtester.md).

## Project structure

| Project | Description |
|---|---|
| `TradeR.SharedKernel` | Building blocks with no external dependencies: the `Result` pattern and technical indicators |
| `TradeR.Infrastructure` | Everything that talks to the outside world: the Kraken REST client and the database schema script |
| `Importer` | Console app that imports Kraken's historical CSV data into PostgreSQL |
| `Backtester` | Console app that will run trading strategies against historical data (in progress) |
| `TradeR.Services` | Application services (placeholder) |
| `TradeR.Tests` | NUnit tests |

## Tech stack

.NET 10 · C# · PostgreSQL · Dapper · Npgsql · Microsoft.Extensions.Http.Resilience · NUnit

## Getting started

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [PostgreSQL](https://www.postgresql.org/download/), needed for importing and backtesting

### Build and test
```bash
git clone <repository-url>
cd TradeR
dotnet build TradeR.slnx
dotnet test TradeR.Tests
```

### Set up the database
Create a database, then run the schema script. It creates a `kraken` schema with the tables for asset pairs, candles and trades:

```bash
psql -d <database> -f TradeR.Infrastructure/Scripts/001_initial_kraken_market_data_db_structure.sql
```

### Import Kraken historical data
1. **Download the data.** Kraken publishes its full history as downloadable CSV files:
   - **Candles (OHLCVT):** [Downloadable historical OHLCVT data](https://support.kraken.com/articles/360047124832-downloadable-historical-ohlcvt-open-high-low-close-volume-trades-data)
   - **Trades (time and sales):** [Downloadable historical market data](https://support.kraken.com/articles/360047543791-downloadable-historical-market-data-time-and-sales-)

   Follow the article's instructions to download the parts, join them and extract the zip into a folder.

2. **Set the connection string.** It's stored as a user secret, outside the repository:
   ```bash
   dotnet user-secrets set ConnectionStrings:KrakenMarketData "Host=localhost;Database=<database>;Username=<user>;Password=<password>" --project Importer
   ```

3. **Run the import:**
   ```bash
   dotnet run --project Importer -c Release -- ohlcvt "<folder with the extracted CSVs>"
   dotnet run --project Importer -c Release -- trades "<folder with the extracted CSVs>"
   ```
   Add `--parallel N` to change how many files are imported at once (default 4).

> **Disk space:** the full OHLCVT history takes about **100 GB** in PostgreSQL. The full trades history would take roughly **300 GB**; the Backtester is designed to work from candles only, so importing trades is optional.

### Configuration

| Setting | Used by | Description |
|---|---|---|
| `ConnectionStrings:KrakenMarketData` | Importer | PostgreSQL connection string. Set it with user secrets or the `ConnectionStrings__KrakenMarketData` environment variable. |
| `Import:MaxParallelFiles` | Importer | Number of files imported at once (default 4). The `--parallel` argument overrides it. |
| `Kraken:BaseUrl` | Kraken REST client | Optional. Defaults to `https://api.kraken.com/`. |

## Disclaimer

This project is for educational and research purposes only and is not financial advice. Backtest results don't guarantee future performance. Use it at your own risk.

## License

[MIT](LICENSE)
