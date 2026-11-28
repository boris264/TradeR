# TradeR

A .NET toolkit for backtesting crypto trading strategies on historical [Kraken](https://www.kraken.com) market data.

> **Status: work in progress.** Data import, the Kraken API client, the first technical indicators and the Backtester are in place. There's one basic strategy so far, a 200 EMA pullback, plus a mock one used for testing.

## Features

- **Kraken historical data importer:** bulk-loads Kraken's downloadable OHLCVT (candle) and trade CSV files into PostgreSQL.
  - It loads several files at once and uses Npgsql's binary `COPY`.
  - Re-running an import, or loading Kraken's quarterly update files on top, skips rows that are already stored instead of duplicating them.
- **Kraken REST API client:** fetches recent candles from the public OHLC endpoint.
  - Retries, a circuit breaker and timeouts are built in.
  - Failures come back as `Result` values instead of exceptions.
- **Technical indicators:** Simple and Exponential Moving Average (SMA, EMA), and Volume Weighted Average Price (VWAP).
  - They take plain numbers, so they work with any exchange's data, not just Kraken's.
  - SMA and EMA can be fed one candle at a time or run over a whole series, with identical results.
  - VWAP is fed one candle at a time. It restarts every session, week or month, and only counts candles inside a session time range you choose (all day by default).
- **Backtester:** runs strategies against the imported candles and writes every trade to an Excel workbook.
  - Any number of backtests, each with its own asset pair, strategy, interval, period, starting balance and risk per trade. They're read from `backtests.json`, or built in a console menu when the file lists none.
  - Backtests run in parallel, independently of each other. Candles are streamed from PostgreSQL, so a long period doesn't have to fit in memory.
  - Long and short positions, with a stop loss and an optional take profit. Shorts are simulated, and there's no leverage.
  - Signals are filled at the next candle's open, so a trade never uses a price that wasn't known when the decision was made. Stop losses and take profits fill at their own price when a candle's low or high reaches it.
  - Positions are sized from the current balance and the risk per trade, and fees are included.
  - The workbook is created from a template whose formulas and chart summarize the results.
- **Tests:** an NUnit test suite for the indicators, the P&L calculator and the backtesting engine.

The Backtester's design is in [docs/backtester.md](docs/backtester.md). It covers more than what's built so far.

### Not implemented yet
- More trading strategies. The 200 EMA strategy is a first, basic one.
- A batch `Calculate` for VWAP, like the one SMA and EMA have.
- Quote currencies: the starting balance is taken to be in the pair's quote currency. Nothing is converted, and the workbook's Currency column stays empty.
- Using the Kraken REST client in the console apps. They work from the imported data only.

## Project structure

| Project | Description |
|---|---|
| `TradeR.SharedKernel` | Building blocks with no external dependencies: the `Result` pattern, technical indicators and the P&L calculator |
| `TradeR.Infrastructure` | Everything that talks to the outside world: the Kraken REST client, the candle repository, the Excel writer with its template, and the database schema script |
| `Importer` | Console app that imports Kraken's historical CSV data into PostgreSQL |
| `Backtester` | Console app that runs trading strategies against the imported data: the menu and the simulation engine |
| `TradeR.Services` | Application core: the candle, signal, order and position models, the strategies and the market data loader |
| `TradeR.Tests` | NUnit tests |

## Tech stack

.NET 10 · C# · PostgreSQL · Dapper · Npgsql · ClosedXML · Microsoft.Extensions.Hosting · Microsoft.Extensions.Http.Resilience · NUnit

## Getting started

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [PostgreSQL](https://www.postgresql.org/download/), needed for importing and backtesting
- Excel 2019 or Microsoft 365, to open the backtest results

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

The second script creates `kraken.asset_pairs_time_range`, a materialized view with the oldest and newest candle of every pair and interval. The Backtester uses it to check `backtests.json`. Run it after your first import, because a materialized view stores its rows when it's created:

```bash
psql -d <database> -f TradeR.Infrastructure/Scripts/002_add_asset_pairs_time_range_materialized_view.sql
```

Nothing refreshes it automatically. After every later import, run:

```sql
REFRESH MATERIALIZED VIEW kraken.asset_pairs_time_range;
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

### Run a backtest
1. **Import the candles** for the pairs and intervals you want to test. The menu offers 1, 5, 15 and 30 minutes, 1 and 4 hours, and 1 day.

2. **Set the connection string.** The Backtester has its own user secrets, separate from the Importer's:
   ```bash
   dotnet user-secrets set ConnectionStrings:KrakenMarketData "Host=localhost;Database=<database>;Username=<user>;Password=<password>" --project Backtester
   ```

3. **Build it, then start it from its output folder.** It looks for the Excel template relative to the current folder, so `dotnet run` from the repository root can't save the results:
   ```bash
   dotnet build Backtester -c Release
   cd Backtester/bin/Release/net10.0
   dotnet Backtester.dll
   ```

4. **Choose the backtests,** in one of two ways:
   - **From `backtests.json`:** list them in `Backtester/backtests.json`. The file is copied to the output folder on every build, so edit the one in `Backtester/`, not the copy.
     ```json
     {
       "Backtests": {
         "Configurations": [
           {
             "AssetPair": "XBTUSD",
             "StrategyName": "200 EMA Following",
             "Interval": 15,
             "From": "2024-01-01",
             "To": "2025-01-01",
             "InitialBalance": 10000,
             "RiskSize": 0.01
           }
         ]
       }
     }
     ```
     - `Interval` is in minutes: 1, 5, 15, 30, 60, 240 or 1440.
     - `From` and `To` are read as UTC. `From` is included and `To` isn't.
     - `RiskSize` is the share of the balance risked per trade, between 0 and 1.
     - `StrategyName` must match a strategy's `Name`, and the pair must be imported. `From` and `To` must be inside the stored candles, according to `kraken.asset_pairs_time_range`.
     - If any entry fails these checks, the error is logged and no backtest runs.
     - When the list has entries, the menu is skipped.
   - **From the menu:** leave the `Configurations` list empty. Add backtests in the menu, then choose `0. Finish` at the asset pair step to run them. Asset pairs can be picked by number or by name.

The trades are saved in the same folder as `Backtest-<HH_mm_dd-MM-yyyy>.xlsx`. If there were no trades, no workbook is created.

### Add a strategy
1. Derive from `Strategy` in `TradeR.Services/Strategies`.
   - It receives one candle at a time and returns an `EntrySignal`, an `ExitSignal` or `null`.
   - Its `state` field says whether a long or a short is open, because the engine updates it whenever a position opens or closes.
   - `TwoHundredEmaFollowingStrategy` is a real example and `MockStrategy` a minimal one.
2. Set `CandlesToPreload` to the warm-up your indicators need. These candles before `From` only feed the strategy. For an EMA, about 5 × the period gives values close to TradingView's; the 200 EMA strategy preloads 1,200.
3. Register it in `Backtester/Program.cs` with `AddScoped<IStrategy, YourStrategy>()`.

Give it a unique `Name`, because the Backtester finds strategies by name.

### Configuration

| Setting | Used by | Description |
|---|---|---|
| `ConnectionStrings:KrakenMarketData` | Importer, Backtester | PostgreSQL connection string. Set it with user secrets (each app has its own) or the `ConnectionStrings__KrakenMarketData` environment variable. |
| `Import:MaxParallelFiles` | Importer | Number of files imported at once (default 4). The `--parallel` argument overrides it. |
| `Backtester:Fee` | Backtester | Fee charged on the value of every order, as a fraction. Defaults to 0.008 (0.8%), Kraken Pro's spot taker fee at the lowest volume tier. Change it in `Backtester/appsettings.json`, with user secrets or with the `Backtester__Fee` environment variable to match your tier. |
| `Backtests:Configurations` | Backtester | The backtests to run without the menu. Set them in `Backtester/backtests.json` (see [Run a backtest](#run-a-backtest)). |
| `Kraken:BaseUrl` | Kraken REST client | Optional. Defaults to `https://api.kraken.com/`. |

## Disclaimer

This project is for educational and research purposes only and is not financial advice. Backtest results don't guarantee future performance. Use it at your own risk.

## License

[MIT](LICENSE)
