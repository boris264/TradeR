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

Backtester (runs the backtests listed in `Backtester/backtests.json`, or opens an interactive menu when that list is empty; writes an Excel workbook):

```bash
dotnet user-secrets set ConnectionStrings:KrakenMarketData "Host=...;Database=...;Username=...;Password=..." --project Backtester
dotnet build Backtester -c Release
cd Backtester/bin/Release/net10.0 && dotnet Backtester.dll
```

Run it from its output folder: `TradeWriter` opens `Templates/BacktestTemplate.xlsx` and saves the workbook relative to the current folder, so `dotnet run` from the repository root can't save results.

The database schema is two hand-written scripts in `TradeR.Infrastructure/Scripts/`, run manually against PostgreSQL. There is no migration tool.
- `001_initial_kraken_market_data_db_structure.sql` creates the schema and tables.
- `002_add_asset_pairs_time_range_materialized_view.sql` creates the `kraken.asset_pairs_time_range` materialized view. Run it after importing, and run `REFRESH MATERIALIZED VIEW kraken.asset_pairs_time_range;` after every later import. Nothing refreshes it automatically.

## Architecture

Projects and dependencies (arrows point to what is referenced):

- **TradeR.SharedKernel**: no dependencies. Contains the platform-independent building blocks:
  - `Results/`: `Result`, `Result<T>`, `Error`.
  - `Indicators/`: SMA, EMA, VWAP, `IndicatorErrors`.
  - `Common/PnLCalculator`: a generic P&L calculator, not tied to any exchange. The fee is passed to its constructor. A partial close pays only its share of the opening fee. Invalid input throws.
  - `Enums/`: `PositionDirection` (`Long`, `Short`) and `AnchorPoint` (`Session`, `Week`, `Month`, used by VWAP).
  - `Extensions/`: `ArgumentException.ThrowIfNotUtc`.
- **TradeR.Services** → SharedKernel, plus `Microsoft.Extensions.Logging` for the strategies' loggers. The application core:
  - the models: `OhlcvtCandle`, `MarketInterval` (its values are minutes), `Order`, `Position`, `EntrySignal`/`ExitSignal` (`Reason` is `required`);
  - the strategies:
    - `IStrategy`, and the abstract `Strategy` base class, which keeps the `state` field that `ChangeState` sets;
    - `StrategyState`: `Idle`, `LongPositionOpened`, `ShortPositionOpened`;
    - `MockStrategy`, a minimal one for tests, and `TwoHundredEmaFollowingStrategy`, a 200 EMA pullback strategy that preloads 1,200 candles;
  - `IMarketDataLoader`/`MarketDataLoader`, which validates the request and puts the preload candles in front of the stream;
  - `ICandleReader`, the candle source that Infrastructure implements.
- **TradeR.Infrastructure** → Services, SharedKernel. **Everything that talks to the outside world goes here** (HTTP APIs, database, Excel). Today it holds:
  - the Kraken REST client (`IKrakenRESTApiClient`, `GET /0/public/OHLC` only), which no app uses yet;
  - its DI registration, `services.AddKrakenRESTApi(configuration)`, which sets up a named `HttpClient` (`KrakenRESTApiClient.HttpClientName`) with `AddStandardResilienceHandler()` and an optional `Kraken:BaseUrl` setting;
  - `services.AddInfrastructureServices(configuration)`, which registers the data source, the repository, the candle reader and the trade writer. It throws if the `KrakenMarketData` connection string is missing;
  - `KrakenOhlcvtRepository` (Dapper) and `KrakenCandleReader`, which maps its rows to `OhlcvtCandle`;
  - `Models/Dtos/`: `KrakenAssetPairInterval` (the repository's input when it looks up time ranges) and `KrakenAssetPairTimeRange` (its output);
  - `TradeWriter` (ClosedXML) and `Templates/BacktestTemplate.xlsx`;
  - the SQL scripts.
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
- Values must only depend on inputs up to their own index, to avoid look-ahead bias in backtests.

**SMA and EMA:**
- Each is a class with two ways to use it, which must give identical results (a test checks this):
  - `Create(period)`, then `Add(value)`, for candle-by-candle use in a backtest loop or live trading.
  - The static `Calculate(ReadOnlySpan<double>, period)` for a whole series.
- Output is `double?`, with `null` during warm-up (the first `period - 1` values). It's never `NaN`. Batch output has the same length as the input, so index `i` always matches candle `i`.

**VWAP (`VolumeWeightedAveragePrice`):**
- It's streaming only: `Create(sessionOpen, sessionClose, anchorPoint)`, then `Add(close, high, low, volume, date)`. There's no batch `Calculate` yet.
- `Add` returns `double`, not `double?`, and `Vwap` holds the latest value.
- It uses the typical price, (high + low + close) / 3, weighted by volume.
- It only counts candles whose time of day is in the session range, open inclusive and close exclusive. `TimeOnly.MinValue`–`TimeOnly.MaxValue` means all day. A candle outside the range leaves the value unchanged.
- It restarts at each new `AnchorPoint` period (session, week or month).

### Database (PostgreSQL, schema `kraken`)
- `asset_pairs (name PK)`.
- `order_types` and `trade_types`: seeded lookups (1 = market/limit, 1 = buy/2 = sell).
- `ohlcvt`: identity `id` PK plus `UNIQUE (asset_pair, interval_minutes, timestamp)`.
- `time_and_sales`: `PK (asset_pair, trade_id)`.
- `asset_pairs_time_range`: a materialized view (script 002) with one row per `(asset_pair, interval_minutes)` and its `oldest_time` and `newest_time`. `ConsoleWorker` uses it to check `backtests.json`. It's only as current as its last `REFRESH`.
- Timestamps are Unix seconds: `BIGINT` for candles, `DOUBLE PRECISION` for trades (they have fractional seconds).
- The full OHLCVT import is about 100 GB. **Trades are deliberately not imported** (an estimated ~300 GB), so the Backtester is designed around OHLCVT candles only.

### Importer pipeline
1. **Discover files** in the folder (`Files/ImportFileDiscovery`). For OHLCVT, `<PAIR>_<INTERVAL>.csv` gives the pair and interval, and `<PAIR>_Daily_OHLC.csv` means interval 1440. For trades, `<PAIR>.csv` gives the pair.
2. **Parse** each file one line at a time (`Parsing/`). The CSVs have no header and may use scientific notation.
3. **Write** each file in one transaction (`Data/StagedCopy`): binary `COPY` into a temp staging table, then `INSERT ... SELECT ... ON CONFLICT DO NOTHING`. That makes re-runs and overlapping quarterly dumps safe; a direct `COPY` would abort on the first duplicate.
4. **Run in parallel** over files (`Importing/FileImportRunner`). A file that fails is logged and the rest continue.

The command-line parser accepts option flags anywhere and rebuilds unquoted folder paths that contain spaces.

### Backtester
The design is in `docs/backtester.md`, which covers more than what's built. Not built yet: more strategies beyond the basic 200 EMA one, quote currencies (the Currency column stays empty and nothing is converted).

- **Flow:**
  1. `Program` resolves `ConsoleWorker` and calls `StartAsync`.
  2. When `Backtests:Configurations` (from `backtests.json`) has entries, `ConsoleWorker` checks each one:
     - the strategy name exists;
     - the pair is in `kraken.asset_pairs`;
     - `From`/`To` are within the range in `kraken.asset_pairs_time_range`.

     It then builds `EngineConfiguration`s from them, with `From`/`To` marked as UTC. **One invalid entry logs an error and stops them all.** When the list is empty, it prompts `BacktestingMenu` instead.
  3. It passes the configurations to `IBacktester<EngineConfiguration>.RunAsync(configurations, token)`.
  4. `HistoricalDataBacktester` runs each one in parallel, in its own DI scope, with its own `HistoricalDataEngine` and strategy instance, then saves all trades to one workbook.
- **`backtests.json`** is copied to the output folder on every build (`Always`), so edit the one in `Backtester/`.
- **A run that throws is logged and the others continue.** Only real cancellation ends everything.
- **Strategies** are matched by `Name`, so it must be unique. To add one, derive from `Strategy` and register it in `Backtester/Program.cs` with `AddScoped<IStrategy, ...>()`. Keep it scoped: strategies are stateful and must not be shared between runs.
- **Preload:** `MarketDataLoader` streams the candles from `From − interval × CandlesToPreload` up to `From` ahead of the period. It's a time window, not a candle count, so intervals Kraken skipped (no trades) mean fewer candles.
- **Engine rules** (mostly covered by `HistoricalDataEngineTests`):
  - Candles before `From` only feed the strategy. No trades are taken on them.
  - After opening a position, the engine calls `strategy.ChangeState` with `LongPositionOpened` or `ShortPositionOpened`. After a signal exit, stop loss or take profit, it calls it with `Idle`.
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
- **Settings order:** `appsettings.json` (copied to the output folder), then environment variables, then user secrets, then `backtests.json` (optional), which is added last and wins.
- **Dates are UTC.** `EngineConfiguration`, `MarketDataLoadRequest` and the candle reader reject any other `DateTimeKind`. The period is `From` inclusive, `To` exclusive.

### Kraken REST API quirks
These were confirmed against the live API:
- **Candle format:** each OHLC row is `[time, "open", "high", "low", "close", "vwap", "volume", count]`, with prices as strings. Inside `result`, the pair key varies per request, next to a fixed `last` key. `KrakenOhlcResultConverter` handles this with System.Text.Json, and `KrakenResponse<T>` is the shared `{error, result}` envelope.
- **Only recent data:** it returns at most 720 closed candles **plus the current, still-changing candle as the last entry**. `since` can't reach older data, and passing `last` as `since` returns that candle again.
- **Asset version:** the parameter is `assetVersion` (camelCase); `asset_version` is silently ignored. The client defaults to `ApiKrakenAssetVersion.Version1`, which returns display names like `BTC/USD`. `Legacy` omits the parameter and returns internal names like `XXBTZUSD`. The candle data is identical either way.
- **Errors:** Kraken returns errors as HTTP 200 with `error: ["EQuery:Unknown asset pair"]`. Only `E…` entries fail the call; `W…` entries are warnings.
