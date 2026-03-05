import { useState, useCallback } from 'react'
import CandlestickChart from './CandlestickChart'
import ChartInfoOverlay from './ChartInfoOverlay'

const SYMBOLS = [
  'EURUSD', 'GBPUSD', 'USDJPY', 'USDCHF', 'AUDUSD', 'USDCAD', 'NZDUSD',
  'EURGBP', 'EURJPY', 'GBPJPY', 'XAUUSD', 'XAGUSD',
  'US30', 'NAS100', 'SPX500', 'BTCUSD', 'ETHUSD',
]

const TIMEFRAMES = [
  { label: '1M', value: '1m' },
  { label: '5M', value: '5m' },
  { label: '15M', value: '15m' },
  { label: '1H', value: '1h' },
  { label: '4H', value: '4h' },
  { label: '1D', value: '1d' },
]

const styles = {
  wrapper: {
    display: 'flex',
    flexDirection: 'column',
    height: '100%',
    width: '100%',
    background: '#0a1118',
  },
  controlsBar: {
    display: 'flex',
    alignItems: 'center',
    gap: 12,
    padding: '8px 12px',
    background: 'linear-gradient(135deg, #111d28, #162230)',
    borderBottom: '1px solid #2a3a4a',
    flexShrink: 0,
  },
  select: {
    background: '#0a1118',
    color: '#e1e8ed',
    border: '1px solid #2a3a4a',
    borderRadius: 4,
    padding: '6px 10px',
    fontFamily: "'JetBrains Mono', monospace",
    fontSize: 13,
    fontWeight: 600,
    cursor: 'pointer',
    outline: 'none',
  },
  timeframeGroup: {
    display: 'flex',
    gap: 4,
  },
  timeframeBtn: {
    background: 'transparent',
    color: '#5a7a8a',
    border: '1px solid #2a3a4a',
    borderRadius: 4,
    padding: '5px 10px',
    fontFamily: "'JetBrains Mono', monospace",
    fontSize: 12,
    fontWeight: 600,
    cursor: 'pointer',
    transition: 'all 0.15s ease',
  },
  timeframeBtnActive: {
    background: '#4a9eff',
    color: '#ffffff',
    border: '1px solid #4a9eff',
    borderRadius: 4,
    padding: '5px 10px',
    fontFamily: "'JetBrains Mono', monospace",
    fontSize: 12,
    fontWeight: 600,
    cursor: 'pointer',
    transition: 'all 0.15s ease',
  },
  chartArea: {
    flex: 1,
    position: 'relative',
    minHeight: 0,
  },
  loadingOverlay: {
    position: 'absolute',
    top: 0,
    left: 0,
    right: 0,
    bottom: 0,
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    background: 'rgba(10, 17, 24, 0.75)',
    zIndex: 20,
  },
  loadingSpinner: {
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    gap: 12,
  },
  spinnerDot: {
    width: 32,
    height: 32,
    borderRadius: '50%',
    border: '3px solid #2a3a4a',
    borderTopColor: '#4a9eff',
    animation: 'chartSpin 0.8s linear infinite',
  },
  loadingText: {
    color: '#5a7a8a',
    fontFamily: 'Inter, sans-serif',
    fontSize: 13,
  },
}

// Inject keyframe animation for spinner
if (typeof document !== 'undefined') {
  const styleId = 'chart-tab-keyframes'
  if (!document.getElementById(styleId)) {
    const styleTag = document.createElement('style')
    styleTag.id = styleId
    styleTag.textContent = `
      @keyframes chartSpin {
        to { transform: rotate(360deg); }
      }
    `
    document.head.appendChild(styleTag)
  }
}

export default function ChartTab({ chartData }) {
  const { candles, symbol, interval, setSymbol, setInterval, loading } = chartData
  const [crosshairOhlc, setCrosshairOhlc] = useState(null)

  const handleCrosshairData = useCallback((data) => {
    setCrosshairOhlc(data)
  }, [])

  const handleSymbolChange = useCallback((e) => {
    setSymbol(e.target.value)
  }, [setSymbol])

  const handleTimeframeClick = useCallback((value) => {
    setInterval(value)
  }, [setInterval])

  const lastCandle = candles.length > 0 ? candles[candles.length - 1] : null
  const currentPrice = lastCandle ? lastCandle.close : null

  return (
    <div style={styles.wrapper}>
      {/* Controls bar */}
      <div style={styles.controlsBar}>
        <select
          style={styles.select}
          value={symbol}
          onChange={handleSymbolChange}
        >
          {SYMBOLS.map(s => (
            <option key={s} value={s}>{s}</option>
          ))}
        </select>

        <div style={styles.timeframeGroup}>
          {TIMEFRAMES.map(tf => (
            <button
              key={tf.value}
              style={interval === tf.value ? styles.timeframeBtnActive : styles.timeframeBtn}
              onClick={() => handleTimeframeClick(tf.value)}
            >
              {tf.label}
            </button>
          ))}
        </div>
      </div>

      {/* Chart area */}
      <div style={styles.chartArea}>
        <CandlestickChart
          candles={candles}
          symbol={symbol}
          interval={interval}
          onCrosshairData={handleCrosshairData}
        />

        <ChartInfoOverlay
          symbol={symbol}
          interval={interval}
          price={currentPrice}
          ohlc={crosshairOhlc}
        />

        {/* Loading overlay */}
        {loading && (
          <div style={styles.loadingOverlay}>
            <div style={styles.loadingSpinner}>
              <div style={styles.spinnerDot} />
              <span style={styles.loadingText}>Loading {symbol} data...</span>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}
