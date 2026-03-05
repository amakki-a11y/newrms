export const SYMBOL_CATEGORIES = {
  'Forex Majors': ['EURUSD', 'GBPUSD', 'USDJPY', 'USDCHF', 'AUDUSD', 'USDCAD', 'NZDUSD'],
  'Forex Crosses': ['EURGBP', 'EURJPY', 'GBPJPY', 'EURCHF', 'AUDNZD', 'EURAUD', 'GBPAUD', 'AUDCAD', 'CADJPY', 'CHFJPY'],
  'Metals': ['XAUUSD', 'XAGUSD'],
  'Indices': ['US30', 'NAS100', 'SPX500', 'UK100', 'GER40'],
  'Crypto': ['BTCUSD', 'ETHUSD'],
}

const CATEGORY_ORDER = [
  'Forex Majors',
  'Forex Crosses',
  'Metals',
  'Indices',
  'Crypto',
]

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
  // Return in defined category order
  const ordered = {}
  for (const cat of CATEGORY_ORDER) {
    if (grouped[cat]) {
      ordered[cat] = grouped[cat]
    }
  }
  // Append any 'Other' category at the end
  if (grouped['Other']) {
    ordered['Other'] = grouped['Other']
  }
  return ordered
}

export default SYMBOL_CATEGORIES
