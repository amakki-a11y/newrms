export function groupByCategory(symbols, symbolInfo) {
  const grouped = {}
  for (const symbol of symbols) {
    const info = symbolInfo?.[symbol]
    // Use the MT5 Path as category, fallback to 'Other'
    let cat = info?.category || 'Other'
    // MT5 paths can be like "Forex\Majors" - use the top-level folder
    const slashIdx = cat.indexOf('\\')
    if (slashIdx > 0) cat = cat.substring(0, slashIdx)
    if (!cat || cat.trim() === '') cat = 'Other'

    if (!grouped[cat]) grouped[cat] = []
    grouped[cat].push(symbol)
  }
  // Sort categories alphabetically, but put 'Other' last
  const sorted = {}
  const keys = Object.keys(grouped).sort((a, b) => {
    if (a === 'Other') return 1
    if (b === 'Other') return -1
    return a.localeCompare(b)
  })
  for (const key of keys) {
    sorted[key] = grouped[key].sort()
  }
  return sorted
}

export default groupByCategory
