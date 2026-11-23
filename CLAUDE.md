# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

All projects target .NET 10 (`net10.0`); the solution file is `TradeR.slnx`.

```bash
dotnet build TradeR.slnx
dotnet test TradeR.Tests
dotnet test TradeR.Tests --filter "FullyQualifiedName~SimpleMovingAverageTests"                              # one fixture
dotnet test TradeR.Tests --filter "FullyQualifiedName~SimpleMovingAverageTests.Add_WindowJustFilled_ReturnsAverage"  # one test
```

Importer (bulk-loads Kraken CSV dumps into PostgreSQL):

```bash
dotnet user-secrets set ConnectionStrings:KrakenMarketData "Host=...;Database=...;Username=...;Password=..." --project Importer
dotnet run --project Importer -c Release -- ohlcvt "<folder with CSVs>" [--parallel N]
dotnet run --project Importer -c Release -- trades "<folder with CSVs>" [--parallel N]
```

Backtester (interactive menu, writes an Excel workbook):

```bash
dotnet user-secrets set ConnectionStrings:KrakenMarketData "Host=...;Database=...;Username=...;Password=..." --project Backtester
dotnet build Backtester -c Release
cd Backtester/bin/Release/net10.0 && dotnet Backtester.dll
```

Run it from its output folder: `TradeWriter` opens `Templates/BacktestTemplate.xlsx` and saves the workbook relative to the current folder, so `dotnet run` from the repository root can't save results.

The database schema is a hand-written script, `TradeR.Infrastructure/Scripts/001_initial_kraken_market_data_db_structure.sql`, run manually against PostgreSQL. There is no migration tool.

## Architecture

Projects and dependencies (arrows point to what is referenced):

- **TradeR.SharedKernel**: no dependencies. Contains the platform-independent building blocks:
  - `Results/`: `Result`, `Result<T>`, `Error`.
  - `Indicators/`: SMA, EMA, `IndicatorErrors`.
  - `Common/PnLCalculator`: a generic P&L calculator, not tied to any exchange. The fee is passed to its constructor. A partial close pays only its share of the opening fee. Invalid input throws.
  - `Enums/PositionDirection`: `Long`, `Short`.
  - `Extensions/`: `ArgumentException.ThrowIfNotUtc`.
- **TradeR.Services** → SharedKernel. The application core:
  - the models: `OhlcvtCandle`, `MarketInterval` (its values are minutes), `Order`, `Position`, `EntrySignal`/`ExitSignal`;
  - the strategies: `IStrategy`, `StrategyState`, and `MockStrategy`, the only one so far;
  - `IMarketDataLoader`/`MarketDataLoader`, which validates the request and puts the preload candles in front of the stream;
  - `ICandleReader`, the candle source that Infrastructure implements.
- **TradeR.Infrastructure** → Services, SharedKernel. **Everything that talks to the outside world goes here** (HTTP APIs, database, Excel). Today it holds:
  - the Kraken REST client (`IKrakenRESTApiClient`, `GET /0/public/OHLC` only), which no app uses yet;
  - its DI registration, `services.AddKrakenRESTApi(configuration)`, which sets up a named `HttpClient` (`KrakenRESTApiClient.HttpClientName`) with `AddStandardResilienceHandler()` and an optional `Kraken:BaseUrl` setting;
  - `services.AddInfrastructureServices(configuration)`, which registers the data source, the repository, the candle reader and the trade writer. It throws if the `KrakenMarketData` connection string is missing;
  - `KrakenOhlcvtRepository` (Dapper) and `KrakenCandleReader`, which maps its rows to `OhlcvtCandle`;
  - `TradeWriter` (ClosedXML) and `Templates/BacktestTemplate.xlsx`;
  - the SQL schema script.
- **Importer** (console) → Infrastructure, Services. Loads Kraken's historical CSV dumps.
- **Backtester** (console) → Infrastructure, Services. The menu, the simulation engine and the DI registration. See the Backtester section below.
- **Layering rule:**
  - Services is the application core. It owns the models and the interfaces it consumes, e.g. `ICandleReader`.
  - Infrastructure implements those interfaces, so the references run Infrastructure → Services → SharedKernel. Services never references Infrastructure.
  - An interface lives with the code that consumes it. Exchange-specific clients such as `IKrakenRESTApiClient` stay in Infrastructure.
- **TradeR.Tests** (NUnit): one test project for the whole solution, with folders mirroring the area under test (e.g. `SharedKernel/Indicators/`, `Backtester/Engine/`). Test doubles are hand-written fakes; there is no mocking library. The Backtester's types are internal and exposed to it with `InternalsVisibleTo`.
- The `Screener/` folder is untracked and only contains leftover `bin`/`obj`. That project was removed.

### Conventions that span the codebase
- **Implicit usings are disabled everywhere.** Every file needs explicit `using`s. The style is block-scoped namespaces. Both `var` and explicit types are fine; match the file you are editing.
- **Result pattern instead of exceptions for expected failures.**
  - Methods return `Result`/`Result<T>`. Errors are `Error(Code, Message)`, built by static factory classes (`KrakenErrors`, `IndicatorErrors`) with codes prefixed by area (`Kraken.EQuery`, `Indicators.NotEnoughData`).
  - `Result<T>` converts implicitly from `T` and from `Error`, so methods can `return value;` or `return SomeErrors.X();`.
  - Exceptions stay for programmer errors (e.g. `ArgumentException` on an empty pair) and for cancellation from the caller's token, which is rethrown, not converted.
- **Data access uses Dapper + Npgsql. No EF Core.** Bulk loads use Npgsql binary `COPY`.
- **Prices and volumes are `double` everywhere**, matching the `DOUBLE PRECISION` columns.

### Indicators (`TradeR.SharedKernel/Indicators`)
- They take raw `double` values, never platform models like `ApiKrakenOhlcCandle`, so they work with any data source.
- Each indicator is a class with two ways to use it, which must give identical results (a test checks this):
  - `Create(period)`, then `Add(value)`, for candle-by-candle use in a backtest loop or live trading.
  - The static `Calculate(ReadOnlySpan<double>, period)` for a whole series.
- Output is `double?`, with `null` during warm-up (the first `period - 1` values). It's never `NaN`. Batch output has the same length as the input, so index `i` always matches candle `i`.
- Values must only depend on inputs up to their own index, to avoid look-ahead bias in backtests.

### Database (PostgreSQL, schema `kraken`)
- `asset_pairs (name PK)`.
- `order_types` and `trade_types`: seeded lookups (1 = market/limit, 1 = buy/2 = sell).
- `ohlcvt`: identity `id` PK plus `UNIQUE (asset_pair, interval_minutes, timestamp)`.
- `time_and_sales`: `PK (asset_pair, trade_id)`.
- Timestamps are Unix seconds: `BIGINT` for candles, `DOUBLE PRECISION` for trades (they have fractional seconds).
- The full OHLCVT import is about 100 GB. **Trades are deliberately not imported** (an estimated ~300 GB), so the Backtester is designed around OHLCVT candles only.

### Importer pipeline
1. **Discover files** in the folder (`Files/ImportFileDiscovery`). For OHLCVT, `<PAIR>_<INTERVAL>.csv` gives the pair and interval, and `<PAIR>_Daily_OHLC.csv` means interval 1440. For trades, `<PAIR>.csv` gives the pair.
2. **Parse** each file one line at a time (`Parsing/`). The CSVs have no header and may use scientific notation.
3. **Write** each file in one transaction (`Data/StagedCopy`): binary `COPY` into a temp staging table, then `INSERT ... SELECT ... ON CONFLICT DO NOTHING`. That makes re-runs and overlapping quarterly dumps safe; a direct `COPY` would abort on the first duplicate.
4. **Run in parallel** over files (`Importing/FileImportRunner`). A file that fails is logged and the rest continue.

The command-line parser accepts option flags anywhere and rebuilds unquoted folder paths that contain spaces.

### Backtester
The design is in `docs/backtester.md`, which covers more than what's built. Not built yet: real strategies, quote currencies (the Currency column stays empty and nothing is converted).

- **Flow:** `BacktestingMenu` returns a list of `EngineConfiguration`s. `HistoricalDataBacktester` runs each one in parallel, in its own DI scope, with its own `HistoricalDataEngine` and strategy instance, then saves all trades to one workbook.
- **A run that throws is logged and the others continue.** Only real cancellation ends everything.
- **Strategies** are matched by `Name`, so it must be unique. To add one, implement `IStrategy` and register it in `Backtester/Program.cs` with `AddScoped<IStrategy, ...>()`. Keep it scoped: strategies are stateful and must not be shared between runs.
- **Engine rules** (mostly covered by `HistoricalDataEngineTests`):
  - Candles before `From` only feed the strategy. No trades are taken on them.
  - Entry and exit signals fill at the next candle's open. An entry while a position is open, or an exit while flat, is ignored.
  - Stop loss and take profit fill at their own price when the candle's low or high reaches it, including on the entry candle. If one candle reaches both, the stop loss is used (not tested; an open question in the design doc).
  - An entry is skipped when its fill price is already at or past the stop, or (not tested) when the balance is zero or less.
  - Size = current balance × risk ÷ distance to the stop, capped so the value plus the opening fee fits in the current balance. The current balance includes realized P&L.
  - A position still open at the end closes at the last candle's close, at that candle's timestamp.
  - The engine writes the exit reasons `Stop Loss`, `Take Profit` and `End of backtest`.
  - `TradeRow.AccountSize` is the initial balance on every row.
- **`TradeRow` property order is the Excel column order**, because `TradeWriter` inserts the rows by reflection. Changing it means changing the template's `Trades` sheet and the table in `docs/backtester.md` too.
- **Edit the template in Excel**, not with openpyxl, which drops the Summary chart. ClosedXML keeps it when saving.
- **Fee:** `Backtester:Fee` is a fraction charged on every order's value, 0.008 by default. It's checked at startup through `IStartupValidator`, because the host is built but never started, so `ValidateOnStart` alone does nothing.
- **Settings order:** `appsettings.json` (copied to the output folder), then environment variables, then user secrets, which win.
- **Dates are UTC.** `EngineConfiguration`, `MarketDataLoadRequest` and the candle reader reject any other `DateTimeKind`. The period is `From` inclusive, `To` exclusive.

### Kraken REST API quirks
These were confirmed against the live API:
- **Candle format:** each OHLC row is `[time, "open", "high", "low", "close", "vwap", "volume", count]`, with prices as strings. Inside `result`, the pair key varies per request, next to a fixed `last` key. `KrakenOhlcResultConverter` handles this with System.Text.Json, and `KrakenResponse<T>` is the shared `{error, result}` envelope.
- **Only recent data:** it returns at most 720 closed candles **plus the current, still-changing candle as the last entry**. `since` can't reach older data, and passing `last` as `since` returns that candle again.
- **Asset version:** the parameter is `assetVersion` (camelCase); `asset_version` is silently ignored. The client defaults to `ApiKrakenAssetVersion.Version1`, which returns display names like `BTC/USD`. `Legacy` omits the parameter and returns internal names like `XXBTZUSD`. The candle data is identical either way.
- **Errors:** Kraken returns errors as HTTP 200 with `error: ["EQuery:Unknown asset pair"]`. Only `E…` entries fail the call; `W…` entries are warnings.
