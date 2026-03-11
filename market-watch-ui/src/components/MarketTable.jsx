import { useState, useMemo } from 'react'
import PriceCell from '../components/PriceCell'
import { groupByCategory } from '../utils/categories'

const styles = {
  wrapper: {
    position: 'relative',
    width: '100%',
    height: '100%',
  },
  scrollContainer: {
    overflowX: 'auto',
    overflowY: 'auto',
    width: '100%',
    height: '100%',
  },
  overlay: {
    position: 'absolute',
    top: 0,
    left: 0,
    right: 0,
    bottom: 0,
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    background: 'rgba(10, 17, 24, 0.85)',
    zIndex: 10,
    backdropFilter: 'blur(4px)',
  },
  overlayText: {
    color: '#4a9eff',
    fontSize: '14px',
    fontFamily: "'Inter', sans-serif",
    display: 'flex',
    alignItems: 'center',
    gap: '10px',
  },
  overlayDot: {
    width: '8px',
    height: '8px',
    borderRadius: '50%',
    background: '#4a9eff',
    animation: 'pulse 1.5s ease-in-out infinite',
  },
  emptyState: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    padding: '60px 20px',
    color: '#5a7a8a',
    fontSize: '14px',
    fontFamily: "'Inter', sans-serif",
  },
  table: {
    width: '100%',
    borderCollapse: 'collapse',
    minWidth: '700px',
  },
  categoryRow: {
    cursor: 'pointer',
    userSelect: 'none',
  },
  categoryCell: {
    padding: '10px 14px',
    background: 'linear-gradient(135deg, #111d28, #162230)',
    borderBottom: '1px solid #2a3a4a',
    fontFamily: "'Inter', sans-serif",
    fontSize: '12px',
    fontWeight: 600,
    color: '#4a9eff',
    letterSpacing: '0.5px',
  },
  categoryContent: {
    display: 'flex',
    alignItems: 'center',
    gap: '8px',
  },
  chevron: {
    display: 'inline-block',
    fontSize: '10px',
    color: '#5a7a8a',
    transition: 'transform 0.2s ease',
    width: '14px',
    textAlign: 'center',
  },
  categoryCount: {
    color: '#5a7a8a',
    fontWeight: 400,
    fontSize: '11px',
  },
  headerCell: {
    padding: '8px 14px',
    background: '#0d1520',
    borderBottom: '1px solid #1a2a3a',
    color: '#5a7a8a',
    fontFamily: "'Inter', sans-serif",
    fontSize: '10px',
    fontWeight: 600,
    textTransform: 'uppercase',
    letterSpacing: '0.8px',
    textAlign: 'right',
    whiteSpace: 'nowrap',
  },
  headerCellSymbol: {
    padding: '8px 14px',
    background: '#0d1520',
    borderBottom: '1px solid #1a2a3a',
    color: '#5a7a8a',
    fontFamily: "'Inter', sans-serif",
    fontSize: '10px',
    fontWeight: 600,
    textTransform: 'uppercase',
    letterSpacing: '0.8px',
    textAlign: 'left',
    whiteSpace: 'nowrap',
  },
  dataCell: {
    padding: '8px 14px',
    borderBottom: '1px solid #1a2a3a',
    fontFamily: "'JetBrains Mono', monospace",
    fontSize: '13px',
    color: '#e1e8ed',
    textAlign: 'right',
    whiteSpace: 'nowrap',
  },
  symbolCell: {
    padding: '8px 14px',
    borderBottom: '1px solid #1a2a3a',
    fontFamily: "'Inter', sans-serif",
    fontSize: '13px',
    fontWeight: 600,
    color: '#e1e8ed',
    textAlign: 'left',
    whiteSpace: 'nowrap',
  },
  directionDot: {
    display: 'inline-block',
    width: '6px',
    height: '6px',
    borderRadius: '50%',
    marginRight: '8px',
    verticalAlign: 'middle',
  },
  spreadText: {
    color: '#5a7a8a',
    fontFamily: "'JetBrains Mono', monospace",
    fontSize: '12px',
  },
  volumeText: {
    color: '#8a9aaa',
    fontFamily: "'JetBrains Mono', monospace",
    fontSize: '12px',
  },
}

function formatNumber(value, digits) {
  if (value == null || isNaN(value)) return '--'
  return Number(value).toFixed(digits != null ? digits : 2)
}

function formatVolume(volume) {
  if (volume == null || isNaN(volume)) return '--'
  const v = Number(volume)
  if (v >= 1000000) return (v / 1000000).toFixed(2) + 'M'
  if (v >= 1000) return (v / 1000).toFixed(1) + 'K'
  return v.toString()
}

function DirectionDot({ direction }) {
  let color = 'transparent'
  if (direction === 'up') color = '#00c853'
  else if (direction === 'down') color = '#ff5252'
  else color = '#3a4a5a'

  return (
    <span
      style={{
        ...styles.directionDot,
        background: color,
        boxShadow: direction === 'up'
          ? '0 0 4px rgba(0, 200, 83, 0.5)'
          : direction === 'down'
            ? '0 0 4px rgba(255, 82, 82, 0.5)'
            : 'none',
      }}
    />
  )
}

function SymbolRow({ symbol, tick, index, symbolInfo }) {
  const [hovered, setHovered] = useState(false)

  const info = symbolInfo?.[symbol]
  const digits = tick?.digits != null ? tick.digits : (info?.digits != null ? info.digits : 2)
  const direction = tick?.direction || 'none'
  const isEvenRow = index % 2 === 0

  const rowBg = hovered
    ? 'rgba(74, 158, 255, 0.06)'
    : isEvenRow
      ? 'transparent'
      : 'rgba(255, 255, 255, 0.015)'

  const flashStyle = direction === 'up'
    ? { boxShadow: 'inset 2px 0 0 #00c853' }
    : direction === 'down'
      ? { boxShadow: 'inset 2px 0 0 #ff5252' }
      : {}

  return (
    <tr
      style={{ background: rowBg, transition: 'background 0.15s ease', ...flashStyle }}
      onMouseEnter={() => setHovered(true)}
      onMouseLeave={() => setHovered(false)}
    >
      <td style={styles.symbolCell}>
        <DirectionDot direction={direction} />
        {symbol}
      </td>
      <td style={styles.dataCell}>
        {tick ? <PriceCell value={tick.bid} previousValue={tick.prevBid} digits={digits} /> : <span style={{ color: '#3a4a5a' }}>--</span>}
      </td>
      <td style={styles.dataCell}>
        {tick ? <PriceCell value={tick.ask} previousValue={tick.prevAsk} digits={digits} /> : <span style={{ color: '#3a4a5a' }}>--</span>}
      </td>
      <td style={{ ...styles.dataCell }}>
        <span style={styles.spreadText}>
          {tick?.spread != null ? formatNumber(tick.spread, 1) : '--'}
        </span>
      </td>
      <td style={styles.dataCell}>
        {tick ? formatNumber(tick.high, digits) : '--'}
      </td>
      <td style={styles.dataCell}>
        {tick ? formatNumber(tick.low, digits) : '--'}
      </td>
      <td style={styles.dataCell}>
        <span style={styles.volumeText}>
          {tick ? formatVolume(tick.volume) : '--'}
        </span>
      </td>
    </tr>
  )
}

function CategoryGroup({ category, categorySymbols, ticks, symbolInfo }) {
  const [expanded, setExpanded] = useState(false)

  const symbolCount = categorySymbols.length

  return (
    <>
      <tr style={styles.categoryRow} onClick={() => setExpanded((e) => !e)}>
        <td colSpan={7} style={styles.categoryCell}>
          <div style={styles.categoryContent}>
            <span
              style={{
                ...styles.chevron,
                transform: expanded ? 'rotate(90deg)' : 'rotate(0deg)',
              }}
            >
              &#9654;
            </span>
            {category}
            <span style={styles.categoryCount}>({symbolCount})</span>
          </div>
        </td>
      </tr>
      {expanded && (
        <>
          <tr>
            <th style={styles.headerCellSymbol}>Symbol</th>
            <th style={styles.headerCell}>Bid</th>
            <th style={styles.headerCell}>Ask</th>
            <th style={styles.headerCell}>Spread</th>
            <th style={styles.headerCell}>High</th>
            <th style={styles.headerCell}>Low</th>
            <th style={styles.headerCell}>Volume</th>
          </tr>
          {categorySymbols.map((symbol, idx) => (
            <SymbolRow
              key={symbol}
              symbol={symbol}
              tick={ticks[symbol] || null}
              index={idx}
              symbolInfo={symbolInfo}
            />
          ))}
        </>
      )}
    </>
  )
}

export default function MarketTable({ ticks, connected, symbols, symbolInfo }) {
  const symbolList = useMemo(() => symbols && symbols.length > 0 ? symbols : Object.keys(ticks || {}), [symbols, ticks])
  const grouped = useMemo(() => groupByCategory(symbolList, symbolInfo), [symbolList, symbolInfo])
  const categoryEntries = useMemo(() => Object.entries(grouped), [grouped])
  const hasData = symbolList.length > 0

  return (
    <div style={styles.wrapper}>
      {!connected && (
        <div style={styles.overlay}>
          <div style={styles.overlayText}>
            <span style={styles.overlayDot} />
            Connecting to market data...
          </div>
        </div>
      )}
      {!hasData && connected && (
        <div style={styles.emptyState}>
          Waiting for market data...
        </div>
      )}
      <div style={styles.scrollContainer}>
        <table style={styles.table}>
          <tbody>
            {categoryEntries.map(([category, categorySymbols]) => (
              <CategoryGroup
                key={category}
                category={category}
                categorySymbols={categorySymbols}
                ticks={ticks}
                symbolInfo={symbolInfo}
              />
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}
