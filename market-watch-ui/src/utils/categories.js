const SYMBOL_CATEGORIES = {
  'Forex Majors': ['EURUSD', 'GBPUSD', 'USDJPY', 'USDCHF', 'AUDUSD', 'USDCAD', 'NZDUSD'],
  'Forex Crosses': ['EURGBP', 'EURJPY', 'GBPJPY', 'EURCHF', 'AUDNZD', 'EURAUD', 'GBPAUD', 'AUDCAD', 'CADJPY', 'CHFJPY'],
  'Metals': ['XAUUSD', 'XAGUSD'],
  'Indices': ['US30', 'NAS100', 'SPX500', 'UK100', 'GER40'],
  'Crypto': ['BTCUSD', 'ETHUSD']
}

export function getCategory(symbol) {
  for (const [category, symbols] of Object.entries(SYMBOL_CATEGORIES)) {
    if (symbols.includes(symbol)) return category
  }
  return 'Other'
}

export function groupByCategory(symbols) {
  const grouped = {}
  for (const symbol of symbols) {
    const cat = getCategory(symbol)
    if (!grouped[cat]) grouped[cat] = []
    grouped[cat].push(symbol)
  }
  return grouped
}

export default SYMBOL_CATEGORIES
