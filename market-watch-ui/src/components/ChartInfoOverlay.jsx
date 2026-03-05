import { useMemo } from 'react'

const styles = {
  container: {
    position: 'absolute',
    top: 8,
    left: 8,
    zIndex: 10,
    background: 'rgba(10, 17, 24, 0.85)',
    borderRadius: 6,
    padding: '8px 12px',
    pointerEvents: 'none',
    minWidth: 180,
    border: '1px solid #2a3a4a',
  },
  symbolRow: {
    display: 'flex',
    alignItems: 'center',
    gap: 8,
    marginBottom: 4,
  },
  symbolText: {
    fontFamily: 'Inter, sans-serif',
    fontSize: 15,
    fontWeight: 600,
    color: '#e1e8ed',
  },
  intervalBadge: {
    fontFamily: 'Inter, sans-serif',
    fontSize: 10,
    fontWeight: 600,
    color: '#4a9eff',
    background: 'rgba(74, 158, 255, 0.15)',
    borderRadius: 3,
    padding: '2px 6px',
    textTransform: 'uppercase',
  },
  priceText: {
    fontFamily: "'JetBrains Mono', monospace",
    fontSize: 18,
    fontWeight: 700,
    marginBottom: 4,
  },
  ohlcRow: {
    display: 'flex',
    flexWrap: 'wrap',
    gap: '4px 12px',
    marginTop: 4,
  },
  ohlcItem: {
    fontFamily: "'JetBrains Mono', monospace",
    fontSize: 11,
    display: 'flex',
    gap: 4,
  },
  ohlcLabel: {
    color: '#5a7a8a',
    fontWeight: 500,
  },
}

function formatNumber(value, symbol) {
  if (value == null || isNaN(value)) return '---'
  // Determine decimal places based on symbol
  let decimals = 5
  if (symbol) {
    const s = symbol.toUpperCase()
    if (s.includes('JPY')) decimals = 3
    else if (s.startsWith('XAU')) decimals = 2
    else if (s.startsWith('XAG')) decimals = 3
    else if (['US30', 'NAS100', 'SPX500'].some(idx => s.includes(idx))) decimals = 2
    else if (s.includes('BTC')) decimals = 2
    else if (s.includes('ETH')) decimals = 2
  }
  return Number(value).toFixed(decimals)
}

function formatVolume(value) {
  if (value == null || isNaN(value)) return '---'
  const v = Number(value)
  if (v >= 1e6) return (v / 1e6).toFixed(2) + 'M'
  if (v >= 1e3) return (v / 1e3).toFixed(1) + 'K'
  return v.toFixed(0)
}

export default function ChartInfoOverlay({ symbol, interval, price, ohlc }) {
  const isUp = ohlc ? ohlc.close >= ohlc.open : (price != null ? true : true)
  const trendColor = isUp ? '#00c853' : '#ff5252'

  const displayPrice = ohlc ? ohlc.close : price

  return (
    <div style={styles.container}>
      <div style={styles.symbolRow}>
        <span style={styles.symbolText}>{symbol || '---'}</span>
        {interval && <span style={styles.intervalBadge}>{interval}</span>}
      </div>

      {displayPrice != null && (
        <div style={{ ...styles.priceText, color: trendColor }}>
          {formatNumber(displayPrice, symbol)}
        </div>
      )}

      {ohlc && (
        <div style={styles.ohlcRow}>
          <div style={styles.ohlcItem}>
            <span style={styles.ohlcLabel}>O:</span>
            <span style={{ color: trendColor }}>{formatNumber(ohlc.open, symbol)}</span>
          </div>
          <div style={styles.ohlcItem}>
            <span style={styles.ohlcLabel}>H:</span>
            <span style={{ color: trendColor }}>{formatNumber(ohlc.high, symbol)}</span>
          </div>
          <div style={styles.ohlcItem}>
            <span style={styles.ohlcLabel}>L:</span>
            <span style={{ color: trendColor }}>{formatNumber(ohlc.low, symbol)}</span>
          </div>
          <div style={styles.ohlcItem}>
            <span style={styles.ohlcLabel}>C:</span>
            <span style={{ color: trendColor }}>{formatNumber(ohlc.close, symbol)}</span>
          </div>
          <div style={styles.ohlcItem}>
            <span style={styles.ohlcLabel}>V:</span>
            <span style={{ color: '#5a7a8a' }}>{formatVolume(ohlc.volume)}</span>
          </div>
        </div>
      )}
    </div>
  )
}
