# StockTrendTrader: a NinjaTrader 8 stock strategy

`StockTrendTrader` is an automated NinjaScript strategy for trading stocks on intraday charts in NinjaTrader 8.

## How it trades

| | Rule |
|---|---|
| **Long entry** | Fast EMA (9) crosses above the slow EMA (21), price is above the trend EMA (200), and RSI (14) is between 50 and 70 |
| **Short entry** | The mirror image of the long entry. Off by default (`Allow shorts`) |
| **Stop loss** | 1.5 × ATR(14) from entry |
| **Profit target** | 3.0 × ATR(14) from entry (2:1 reward to risk) |
| **Other exits** | Opposite EMA crossover, the daily loss limit, or the end-of-day flatten time |
| **Position size** | `Risk per trade ($)` ÷ stop distance, capped at `Max shares` |
| **Daily loss limit** | When realized plus open P&L for the session reaches `-Max daily loss`, it flattens and stops entering new trades until the next session |
| **Trading window** | New entries only between 9:35 and 15:30. Flattens at 15:55. Times use your PC's time zone |

It calculates on bar close, so signals come only from completed bars. Every input is on the strategy's settings screen and can be optimized in Strategy Analyzer.

## Install

1. Copy `Strategies/StockTrendTrader.cs` into
   `Documents\NinjaTrader 8\bin\Custom\Strategies\`
2. In NinjaTrader, open **New → NinjaScript Editor**, open the file, and press **F5** to compile.
   (Or use **Tools → Import → NinjaScript Add-On** if you package it as a zip.)
3. The strategy then appears as **StockTrendTrader** in the strategy lists.

## Use it

1. **Backtest:** open **New → Strategy Analyzer**, pick a stock (for example AAPL) on a 5-minute chart, choose StockTrendTrader, and run it over several months of data.
2. **Paper trade:** open a chart, right-click it, choose **Strategies**, add StockTrendTrader, set **Account = Sim101**, and enable it.
3. **Go live** only after it behaves as you expect in simulation. Keep `Risk per trade`, `Max shares`, and `Max daily loss` small at first.

> ⚠️ This is sample code, not financial advice. Backtest results do not guarantee future performance, and automated strategies can lose money quickly. You are responsible for any trades it places.
