namespace MT5Connector
{
    public static class PnLEngine
    {
        /// <summary>
        /// Calculate the profit for a single position given current bid/ask and symbol info.
        /// Buy (action=0): profit = (bid - priceOpen) * contractSize * lots [* conversionRate for forex]
        /// Sell (action=1): profit = (priceOpen - ask) * contractSize * lots [* conversionRate for forex]
        /// Volume is stored as lots * 10000, so lots = volume / 10000.0
        /// </summary>
        public static double CalculatePositionProfit(PositionData position, double bid, double ask, SymbolInfo symbol)
        {
            try
            {
                double lots = position.Volume / 10000.0;
                double contractSize = symbol.ContractSize;
                double profit = 0.0;

                if (position.Action == 0) // Buy
                {
                    profit = (bid - position.PriceOpen) * contractSize * lots;
                }
                else // Sell
                {
                    profit = (position.PriceOpen - ask) * contractSize * lots;
                }

                // For Forex (calcMode=0), we would apply currency conversion if profitCurrency != USD
                // In a full implementation, we'd need the ticks dictionary here, but since this method
                // receives only a single symbol's tick, the conversion is handled at the caller level
                // or left as 1.0 (most forex pairs already quote in USD or are USD-based).

                // Add storage (swap) to the profit
                profit += position.Storage;

                return Math.Round(profit, 2);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PnL] Error calculating profit for ticket {position.Ticket}: {ex.Message}");
                return position.Profit; // Return existing profit on error
            }
        }

        /// <summary>
        /// Get the conversion rate between two currencies using available tick data.
        /// If same currency, returns 1.0.
        /// Tries direct pair (e.g. EURUSD) then reverse pair (e.g. USDEUR inverted).
        /// Falls back to 1.0 if no rate found.
        /// </summary>
        public static double GetConversionRate(string fromCurrency, string toCurrency, Dictionary<string, TickData> ticks)
        {
            try
            {
                if (string.IsNullOrEmpty(fromCurrency) || string.IsNullOrEmpty(toCurrency))
                    return 1.0;

                if (string.Equals(fromCurrency, toCurrency, StringComparison.OrdinalIgnoreCase))
                    return 1.0;

                // Try direct pair: fromCurrency + toCurrency (e.g., EUR->USD = EURUSD)
                string directPair = fromCurrency + toCurrency;
                if (ticks.TryGetValue(directPair, out var directTick) && directTick.Bid > 0)
                {
                    return directTick.Bid;
                }

                // Try reverse pair: toCurrency + fromCurrency (e.g., USD->EUR = look up EURUSD and invert)
                string reversePair = toCurrency + fromCurrency;
                if (ticks.TryGetValue(reversePair, out var reverseTick) && reverseTick.Bid > 0)
                {
                    return 1.0 / reverseTick.Bid;
                }

                // Fallback: no rate found
                return 1.0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PnL] Error getting conversion rate {fromCurrency}->{toCurrency}: {ex.Message}");
                return 1.0;
            }
        }

        /// <summary>
        /// Recalculate profits for all positions using current tick data and symbol info.
        /// Updates PriceCurrent and Profit on each position in place.
        /// </summary>
        public static void RecalculateAllProfits(List<PositionData> positions, Dictionary<string, TickData> ticks, Dictionary<string, SymbolInfo> symbols)
        {
            try
            {
                foreach (var position in positions)
                {
                    if (!ticks.TryGetValue(position.Symbol, out var tick))
                        continue; // No tick data for this symbol, skip

                    if (!symbols.TryGetValue(position.Symbol, out var symbolInfo))
                        continue; // No symbol info, skip

                    // Update current price based on direction
                    if (position.Action == 0) // Buy - current price is bid (what we'd close at)
                    {
                        position.PriceCurrent = tick.Bid;
                    }
                    else // Sell - current price is ask (what we'd close at)
                    {
                        position.PriceCurrent = tick.Ask;
                    }

                    // Calculate base profit
                    double lots = position.Volume / 10000.0;
                    double contractSize = symbolInfo.ContractSize;
                    double profit;

                    if (position.Action == 0) // Buy
                    {
                        profit = (tick.Bid - position.PriceOpen) * contractSize * lots;
                    }
                    else // Sell
                    {
                        profit = (position.PriceOpen - tick.Ask) * contractSize * lots;
                    }

                    // Apply currency conversion for Forex (calcMode == 0) when profit currency is not USD
                    if (symbolInfo.CalcMode == 0 && !string.IsNullOrEmpty(symbolInfo.ProfitCurrency)
                        && !string.Equals(symbolInfo.ProfitCurrency, "USD", StringComparison.OrdinalIgnoreCase))
                    {
                        double convRate = GetConversionRate(symbolInfo.ProfitCurrency, "USD", ticks);
                        profit *= convRate;
                    }

                    // Add storage (swap)
                    profit += position.Storage;

                    position.Profit = Math.Round(profit, 2);
                    position.UpdatedAt = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PnL] Error in RecalculateAllProfits: {ex.Message}");
            }
        }
    }
}
