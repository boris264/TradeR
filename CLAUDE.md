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

The database schema is a hand-written script, `TradeR.Infrastructure/Scripts/001_initial_kraken_market_data_db_structure.sql`, run manually against PostgreSQL. There is no migration tool.

## Architecture

Projects and dependencies (arrows point to what is referenced):

- **TradeR.SharedKernel**: no dependencies. Contains the platform-independent building blocks:
  - `Results/`: `Result`, `Result<T>`, `Error`.
  - `Indicators/`: SMA, EMA, `IndicatorErrors`.
  - `Common/PnLCalculator`: a generic P&L calculator, not tied to any exchange. The fee is passed to its constructor. A partial close pays only its share of the opening fee. Invalid input throws.
  - `Enums/PositionDirection`: `Long`, `Short`.
- **TradeR.Infrastructure** → SharedKernel. **Everything that talks to the outside world goes here** (HTTP APIs, database scripts). Today it holds:
  - the Kraken REST client (`IKrakenRESTApiClient`, `GET /0/public/OHLC` only);
  - its DI registration, `services.AddKrakenRESTApi(configuration)`, which sets up a named `HttpClient` (`KrakenRESTApiClient.HttpClientName`) with `AddStandardResilienceHandler()` and an optional `Kraken:BaseUrl` setting;
  - the SQL schema script.
- **Importer** (console) → Infrastructure, Services. Loads Kraken's historical CSV dumps.
- **Backtester** (console) → Infrastructure, Services. Still a stub. Its design (streamed candles, layers, fills, Excel output) is in `docs/backtester.md`.
- **TradeR.Services**: an empty placeholder.
- **Layering rule** (the target design; the project references aren't wired up this way yet):
  - Services is the application core. It owns the models and the interfaces it consumes, e.g. the historical candle source.
  - Infrastructure implements those interfaces, so the references run Infrastructure → Services → SharedKernel. Services never references Infrastructure.
  - An interface lives with the code that consumes it. Exchange-specific clients such as `IKrakenRESTApiClient` stay in Infrastructure.
- **TradeR.Tests** (NUnit): one test project for the whole solution, with folders mirroring the area under test (e.g. `SharedKernel/Indicators/`).
- The `Screener/` folder only contains leftover `bin`/`obj`. That project was removed.

### Conventions that span the codebase
- **Implicit usings are disabled everywhere.** Every file needs explicit `using`s. The style is block-scoped namespaces and explicit types (`List<string> x = new List<string>();`).
- **Result pattern instead of exceptions for expected failures.**
  - Methods return `Result`/`Result<T>`. Errors are `Error(Code, Message)`, built by static factory classes (`KrakenErrors`, `IndicatorErrors`) with codes prefixed by area (`Kraken.EQuery`, `Indicators.NotEnoughData`).
  - `Result<T>` converts implicitly from `T` and from `Error`, so methods can `return value;` or `return SomeErrors.X();`.
  - Exceptions stay for programmer errors (e.g. `ArgumentException` on an empty pair) and for cancellation from the caller's token, which is rethrown, not converted.
- **Data access uses Dapper + Npgsql. No EF Core.** Bulk loads use Npgsql binary `COPY`.
- **Prices and volumes are `double` everywhere**, matching the `DOUBLE PRECISION` columns.

### Indicators (`TradeR.SharedKernel/Indicators`)
- They take raw `double` values, never platform models like `KrakenOhlcCandle`, so they work with any data source.
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

### Kraken REST API quirks
These were confirmed against the live API:
- **Candle format:** each OHLC row is `[time, "open", "high", "low", "close", "vwap", "volume", count]`, with prices as strings. Inside `result`, the pair key varies per request, next to a fixed `last` key. `KrakenOhlcResultConverter` handles this with System.Text.Json, and `KrakenResponse<T>` is the shared `{error, result}` envelope.
- **Only recent data:** it returns at most 720 closed candles **plus the current, still-changing candle as the last entry**. `since` can't reach older data, and passing `last` as `since` returns that candle again.
- **Asset version:** the parameter is `assetVersion` (camelCase); `asset_version` is silently ignored. The client defaults to `KrakenAssetVersion.Version1`, which returns display names like `BTC/USD`. `Legacy` omits the parameter and returns internal names like `XXBTZUSD`. The candle data is identical either way.
- **Errors:** Kraken returns errors as HTTP 200 with `error: ["EQuery:Unknown asset pair"]`. Only `E…` entries fail the call; `W…` entries are warnings.
