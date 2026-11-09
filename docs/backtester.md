# Backtester

## Overview
The Backtester runs trading strategies against historical candle data and records every trade each strategy takes, so the results can be analysed in Excel.

## Starting a backtest
A backtest is started from a menu.

## Backtest configuration
Each backtest has:

- **Start period**
- **End period**
- **Strategies** to run

Each strategy in the backtest has its own settings:

- **Currency pair(s):** one or more. Different strategies can use different pairs.
- **Interval:** the candle size. Different strategies can use different intervals.
- **Account size:** the amount in US dollars the account can buy with.
  - **Every pair gets the full account size.** A strategy with $10,000 trading BTC/USD and ETH/USD has $10,000 for each pair.
  - **Pairs quoted in another currency** (e.g. ETH/EUR) use the account size converted into that currency. The conversion happens **once, at the start of the backtest**, using the **historical exchange rate at the backtest's start period**, taken from the exchange's imported candles (e.g. EUR/USD).
- **Stop loss:** every strategy has a fixed stop loss. Its size is defined by each strategy in code, not chosen in the menu.

Leverage is not supported for now. It may be added in the future, but a strategy can only buy with the account size it has.

**Short positions are simulated.** No coins are borrowed: a short profits when the price falls and loses when it rises, sized from the same account size as a long.

## Execution flow
1. Start the Backtester and open the menu.
2. Configure the backtest: start and end period, which strategies to run, and each strategy's pairs, interval and account size.
3. Start the backtest. The Excel template is copied to create this backtest's workbook, and every selected strategy begins running.
4. Each strategy runs on each of its pairs separately, in parallel with the other strategies.
5. When a strategy finishes, its trades are appended as rows to the workbook's shared data sheet.
6. The template's formulas and graphs update automatically from the written trades.

## Architecture
C4 component diagram of the Backtester:

![Backtester components (C4)](images/tradeR_c4_components_backtester.png)

The editable source, which also has the System Context and Containers pages, is [images/tradeR_c4.drawio](images/tradeR_c4.drawio).

## Parallel strategy execution
- Multiple strategies run in parallel.
- Strategies are independent of each other. One strategy's execution does not affect another's.
- A strategy with several pairs runs on each pair separately. Each pair is traded on its own, not as one combined portfolio.

## Results
- **One Excel workbook per backtest**, created by copying an Excel template, which will be provided.
- **The template has one shared data sheet**, and it's the only sheet the Backtester writes to. Every strategy's trades go into it.
- **Each trade is one row**, with these columns:

| Column | Description |
|---|---|
| Asset pair | The pair traded, e.g. BTC/USD |
| Interval | The strategy's candle interval |
| Strategy name | Which strategy took the trade |
| Position | Long or Short |
| Coin amount | How many coins were bought or sold |
| Bought amount | The cost in the quote currency |
| Currency | The quote currency of the pair (EUR, CHF, USD), so results in different currencies aren't added together |
| Buy price | Price per coin of the buy order |
| Sell price | Price per coin of the sell order |
| Fees | Total fees of all executed orders of the trade (open + close) |
| P&L | Profit or loss, including the fees, in the pair's currency |
| Open time | When the position was opened (UTC) |
| Close time | When the position was closed (UTC) |

Hold time is calculated in the template from the open and close times.

- The template's formulas operate on the shared data sheet and produce the aggregations and graphs automatically. The Backtester writes the trades and nothing else; all analysis lives in the template.
- Writing to the workbook must be thread-safe: strategies run in parallel and may finish at the same time, and they all write to the same sheet.

## Fills, fees and slippage
- **Signal fills:** entries and exits triggered by a strategy's signal are filled at the **next candle's open**. A signal produced on a candle's close is executed at the open of the following candle, so a trade never uses a price that wasn't known when the decision was made.
- **Stop loss and take profit fills:** they fill **at their own price** (the stop price or the take profit price) as soon as a candle's low or high reaches it:
  - Long: the stop loss fills when the candle's low reaches the stop; the take profit when the candle's high reaches the target.
  - Short: the stop loss fills when the candle's high reaches the stop; the take profit when the candle's low reaches the target.
- **Fees:** hard-coded constants based on Kraken's fees.
- **Slippage:** ignored.

## Data
- Candles come from the imported Kraken OHLCVT data (`kraken.ohlcvt`). Trade (time & sales) data is not used.

## Open questions
- **Both levels in one candle (deferred):** if a single candle's range reaches both the stop loss and the take profit, the candle doesn't say which came first. Not decided yet. Options: count the stop loss (conservative), or look at smaller-interval candles to decide.
