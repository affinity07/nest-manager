#region Using declarations
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Indicators;
#endregion

// StockTrendTrader - NinjaTrader 8 automated stock strategy.
//
// Entry:  fast EMA crosses the slow EMA, in the direction of a long-term trend EMA,
//         with RSI confirming momentum but not overextended.
// Exit:   ATR-based stop loss and profit target, opposite crossover, or end-of-day flatten.
// Risk:   position size is derived from a fixed dollar risk per trade, capped by a max
//         share count; new entries stop for the day once a daily loss limit is reached.
//
// Always test in Strategy Analyzer and on a Sim101 account before trading live money.
namespace NinjaTrader.NinjaScript.Strategies
{
	public class StockTrendTrader : Strategy
	{
		private const string LongSignal  = "STT Long";
		private const string ShortSignal = "STT Short";

		private EMA fastEma;
		private EMA slowEma;
		private EMA trendEma;
		private RSI rsi;
		private ATR atr;

		private double sessionStartPnL;
		private bool   dailyLimitHit;

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description                   = "EMA crossover trend strategy for stocks with ATR stops, risk-based sizing and a daily loss limit.";
				Name                          = "StockTrendTrader";
				Calculate                     = Calculate.OnBarClose;
				EntriesPerDirection           = 1;
				EntryHandling                 = EntryHandling.AllEntries;
				IsExitOnSessionCloseStrategy  = true;
				ExitOnSessionCloseSeconds     = 30;
				IsFillLimitOnTouch            = false;
				MaximumBarsLookBack           = MaximumBarsLookBack.TwoHundredFiftySix;
				OrderFillResolution           = OrderFillResolution.Standard;
				Slippage                      = 0;
				StartBehavior                 = StartBehavior.WaitUntilFlat;
				TimeInForce                   = TimeInForce.Gtc;
				TraceOrders                   = false;
				RealtimeErrorHandling         = RealtimeErrorHandling.StopCancelClose;
				StopTargetHandling            = StopTargetHandling.PerEntryExecution;
				BarsRequiredToTrade           = 50;
				IsInstantiatedOnEachOptimizationIteration = true;

				FastPeriod        = 9;
				SlowPeriod        = 21;
				TrendPeriod       = 200;
				RsiPeriod         = 14;
				RsiLongMin        = 50;
				RsiLongMax        = 70;
				RsiShortMin       = 30;
				RsiShortMax       = 50;
				AtrPeriod         = 14;
				StopAtrMultiple   = 1.5;
				TargetAtrMultiple = 3.0;
				RiskPerTrade      = 100;
				MaxShares         = 500;
				MaxDailyLoss      = 300;
				AllowShorts       = false;
				ExitOnOppositeCross = true;
				StartTime         = 93500;
				EndTime           = 153000;
				FlattenTime       = 155500;
			}
			else if (State == State.DataLoaded)
			{
				fastEma  = EMA(FastPeriod);
				slowEma  = EMA(SlowPeriod);
				trendEma = EMA(TrendPeriod);
				rsi      = RSI(RsiPeriod, 3);
				atr      = ATR(AtrPeriod);

				fastEma.Plots[0].Brush  = System.Windows.Media.Brushes.DodgerBlue;
				slowEma.Plots[0].Brush  = System.Windows.Media.Brushes.Orange;
				trendEma.Plots[0].Brush = System.Windows.Media.Brushes.Gray;
				AddChartIndicator(fastEma);
				AddChartIndicator(slowEma);
				AddChartIndicator(trendEma);
			}
		}

		protected override void OnBarUpdate()
		{
			if (BarsInProgress != 0 || CurrentBar < Math.Max(BarsRequiredToTrade, TrendPeriod))
				return;

			if (Bars.IsFirstBarOfSession)
			{
				sessionStartPnL = SystemPerformance.AllTrades.TradesPerformance.Currency.CumProfit;
				dailyLimitHit   = false;
			}

			int now = ToTime(Time[0]);

			// End-of-day flatten
			if (now >= FlattenTime)
			{
				FlattenAll("EOD Flatten");
				return;
			}

			// Daily loss limit (realized + open P&L)
			double sessionPnL = SystemPerformance.AllTrades.TradesPerformance.Currency.CumProfit - sessionStartPnL
				+ Position.GetUnrealizedProfitLoss(PerformanceUnit.Currency, Close[0]);
			if (!dailyLimitHit && sessionPnL <= -MaxDailyLoss)
			{
				dailyLimitHit = true;
				FlattenAll("Daily Loss Limit");
				Print(string.Format("{0} {1}: daily loss limit hit ({2:C}), no more entries today.", Time[0], Instrument.FullName, sessionPnL));
			}

			bool crossUp   = CrossAbove(fastEma, slowEma, 1);
			bool crossDown = CrossBelow(fastEma, slowEma, 1);

			// Exit on opposite crossover
			if (ExitOnOppositeCross)
			{
				if (Position.MarketPosition == MarketPosition.Long && crossDown)
					ExitLong("Cross Exit", LongSignal);
				else if (Position.MarketPosition == MarketPosition.Short && crossUp)
					ExitShort("Cross Exit", ShortSignal);
			}

			if (Position.MarketPosition != MarketPosition.Flat || dailyLimitHit || now < StartTime || now > EndTime)
				return;

			double stopDistance = atr[0] * StopAtrMultiple;
			if (stopDistance <= 0)
				return;

			int quantity = CalculateQuantity(stopDistance);
			if (quantity < 1)
				return;

			int stopTicks   = Math.Max(1, (int)Math.Ceiling(stopDistance / TickSize));
			int targetTicks = Math.Max(1, (int)Math.Ceiling(atr[0] * TargetAtrMultiple / TickSize));

			if (crossUp && Close[0] > trendEma[0] && rsi[0] >= RsiLongMin && rsi[0] <= RsiLongMax)
			{
				SetStopLoss(LongSignal, CalculationMode.Ticks, stopTicks, false);
				SetProfitTarget(LongSignal, CalculationMode.Ticks, targetTicks);
				EnterLong(quantity, LongSignal);
			}
			else if (AllowShorts && crossDown && Close[0] < trendEma[0] && rsi[0] >= RsiShortMin && rsi[0] <= RsiShortMax)
			{
				SetStopLoss(ShortSignal, CalculationMode.Ticks, stopTicks, false);
				SetProfitTarget(ShortSignal, CalculationMode.Ticks, targetTicks);
				EnterShort(quantity, ShortSignal);
			}
		}

		// Shares = dollar risk / (stop distance * point value), capped at MaxShares.
		private int CalculateQuantity(double stopDistance)
		{
			double pointValue = Instrument.MasterInstrument.PointValue;
			if (pointValue <= 0)
				pointValue = 1;

			int shares = (int)Math.Floor(RiskPerTrade / (stopDistance * pointValue));
			return Math.Min(shares, MaxShares);
		}

		private void FlattenAll(string reason)
		{
			if (Position.MarketPosition == MarketPosition.Long)
				ExitLong(reason, LongSignal);
			else if (Position.MarketPosition == MarketPosition.Short)
				ExitShort(reason, ShortSignal);
		}

		#region Properties
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name = "Fast EMA", Order = 1, GroupName = "1. Signals")]
		public int FastPeriod { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name = "Slow EMA", Order = 2, GroupName = "1. Signals")]
		public int SlowPeriod { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name = "Trend EMA", Order = 3, GroupName = "1. Signals")]
		public int TrendPeriod { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name = "RSI period", Order = 4, GroupName = "1. Signals")]
		public int RsiPeriod { get; set; }

		[NinjaScriptProperty]
		[Range(0, 100)]
		[Display(Name = "RSI long min", Order = 5, GroupName = "1. Signals")]
		public double RsiLongMin { get; set; }

		[NinjaScriptProperty]
		[Range(0, 100)]
		[Display(Name = "RSI long max", Order = 6, GroupName = "1. Signals")]
		public double RsiLongMax { get; set; }

		[NinjaScriptProperty]
		[Range(0, 100)]
		[Display(Name = "RSI short min", Order = 7, GroupName = "1. Signals")]
		public double RsiShortMin { get; set; }

		[NinjaScriptProperty]
		[Range(0, 100)]
		[Display(Name = "RSI short max", Order = 8, GroupName = "1. Signals")]
		public double RsiShortMax { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Allow shorts", Order = 9, GroupName = "1. Signals")]
		public bool AllowShorts { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Exit on opposite cross", Order = 10, GroupName = "1. Signals")]
		public bool ExitOnOppositeCross { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name = "ATR period", Order = 1, GroupName = "2. Risk")]
		public int AtrPeriod { get; set; }

		[NinjaScriptProperty]
		[Range(0.1, double.MaxValue)]
		[Display(Name = "Stop (x ATR)", Order = 2, GroupName = "2. Risk")]
		public double StopAtrMultiple { get; set; }

		[NinjaScriptProperty]
		[Range(0.1, double.MaxValue)]
		[Display(Name = "Target (x ATR)", Order = 3, GroupName = "2. Risk")]
		public double TargetAtrMultiple { get; set; }

		[NinjaScriptProperty]
		[Range(1, double.MaxValue)]
		[Display(Name = "Risk per trade ($)", Order = 4, GroupName = "2. Risk")]
		public double RiskPerTrade { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name = "Max shares", Order = 5, GroupName = "2. Risk")]
		public int MaxShares { get; set; }

		[NinjaScriptProperty]
		[Range(1, double.MaxValue)]
		[Display(Name = "Max daily loss ($)", Order = 6, GroupName = "2. Risk")]
		public double MaxDailyLoss { get; set; }

		[NinjaScriptProperty]
		[Range(0, 235959)]
		[Display(Name = "Start time (HHmmss)", Order = 1, GroupName = "3. Session")]
		public int StartTime { get; set; }

		[NinjaScriptProperty]
		[Range(0, 235959)]
		[Display(Name = "Last entry time (HHmmss)", Order = 2, GroupName = "3. Session")]
		public int EndTime { get; set; }

		[NinjaScriptProperty]
		[Range(0, 235959)]
		[Display(Name = "Flatten time (HHmmss)", Order = 3, GroupName = "3. Session")]
		public int FlattenTime { get; set; }
		#endregion
	}
}
