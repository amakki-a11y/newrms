import { useEffect, useRef, useMemo } from 'react'
import { createChart, AreaSeries } from 'lightweight-charts'

/* ───── helpers ───── */
function fmt$(v) {
  if (v == null) return '$0.00'
  const abs = Math.abs(v)
  if (abs >= 1e9) return (v < 0 ? '-' : '') + '$' + (abs / 1e9).toFixed(2) + 'B'
  if (abs >= 1e6) return (v < 0 ? '-' : '') + '$' + (abs / 1e6).toFixed(2) + 'M'
  if (abs >= 1e3) return (v < 0 ? '-' : '') + '$' + (abs / 1e3).toFixed(2) + 'K'
  return (v < 0 ? '-$' : '$') + abs.toFixed(2)
}

function fmtNum(v) {
  if (v == null) return '0'
  return Number(v).toLocaleString('en-US')
}

function fmtVol(v) {
  if (v == null) return '0.00'
  return Number(v).toFixed(2)
}

function profitColor(v) {
  if (v == null || v === 0) return '#e1e8ed'
  return v > 0 ? '#00c853' : '#ff5252'
}

/* ───── inline style objects ───── */
const styles = {
  root: {
    padding: '16px 20px',
    fontFamily: "'Inter', sans-serif",
    minHeight: '100%',
  },
  grid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(340px, 1fr))',
    gap: '16px',
  },
  card: {
    background: 'linear-gradient(135deg, #111d28, #162230)',
    border: '1px solid #2a3a4a',
    borderRadius: '10px',
    padding: '18px 20px',
  },
  cardTitle: {
    fontSize: '13px',
    fontWeight: 600,
    color: '#5a7a8a',
    textTransform: 'uppercase',
    letterSpacing: '0.6px',
    marginBottom: '14px',
  },
  summaryGrid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(160px, 1fr))',
    gap: '12px',
  },
  summaryCard: {
    background: 'linear-gradient(135deg, #111d28, #162230)',
    border: '1px solid #2a3a4a',
    borderRadius: '10px',
    padding: '16px 18px',
    display: 'flex',
    flexDirection: 'column',
    gap: '6px',
  },
  summaryLabel: {
    fontSize: '11px',
    fontWeight: 500,
    color: '#5a7a8a',
    textTransform: 'uppercase',
    letterSpacing: '0.5px',
  },
  summaryValue: {
    fontSize: '22px',
    fontWeight: 700,
    fontFamily: "'JetBrains Mono', monospace",
    color: '#e1e8ed',
  },
  emptyState: {
    color: '#5a7a8a',
    textAlign: 'center',
    padding: '30px 0',
    fontSize: '13px',
    fontStyle: 'italic',
  },
  rangeBtn: (active) => ({
    padding: '5px 14px',
    fontSize: '12px',
    fontWeight: 600,
    fontFamily: "'Inter', sans-serif",
    borderRadius: '6px',
    border: active ? '1px solid #4a9eff' : '1px solid #2a3a4a',
    background: active ? 'rgba(74,158,255,0.15)' : 'transparent',
    color: active ? '#4a9eff' : '#5a7a8a',
    cursor: 'pointer',
    transition: 'all .15s',
  }),
  moverRow: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    padding: '10px 0',
    borderBottom: '1px solid #1c2d3a',
  },
  moverLogin: {
    fontFamily: "'JetBrains Mono', monospace",
    fontSize: '13px',
    color: '#4a9eff',
    minWidth: '70px',
  },
  moverName: {
    fontSize: '13px',
    color: '#e1e8ed',
    flex: 1,
    marginLeft: '12px',
    overflow: 'hidden',
    textOverflow: 'ellipsis',
    whiteSpace: 'nowrap',
  },
  moverProfit: {
    fontFamily: "'JetBrains Mono', monospace",
    fontSize: '13px',
    fontWeight: 600,
    minWidth: '90px',
    textAlign: 'right',
  },
  moverChange: {
    fontFamily: "'JetBrains Mono', monospace",
    fontSize: '11px',
    minWidth: '70px',
    textAlign: 'right',
    marginLeft: '8px',
  },
}

/* ───── Summary Cards Row ───── */
function SummaryCards({ summary }) {
  const cards = [
    { label: 'Total Accounts', value: summary ? fmtNum(summary.totalAccounts) : '0', isProfit: false },
    { label: 'Total Balance', value: summary ? fmt$(summary.totalBalance) : '$0.00', isProfit: false },
    { label: 'Total Equity', value: summary ? fmt$(summary.totalEquity) : '$0.00', isProfit: false },
    { label: 'Total Margin', value: summary ? fmt$(summary.totalMargin) : '$0.00', isProfit: false },
    { label: 'Free Margin', value: summary ? fmt$(summary.totalMarginFree) : '$0.00', isProfit: false },
    { label: 'Total Profit', value: summary ? fmt$(summary.totalProfit) : '$0.00', isProfit: true },
  ]

  return (
    <div style={{ ...styles.summaryGrid, gridColumn: '1 / -1' }}>
      {cards.map((c) => {
        let valueColor = '#e1e8ed'
        if (c.isProfit && summary) {
          valueColor = profitColor(summary.totalProfit)
        }
        return (
          <div key={c.label} style={styles.summaryCard}>
            <span style={styles.summaryLabel}>{c.label}</span>
            <span style={{ ...styles.summaryValue, color: valueColor }}>{c.value}</span>
          </div>
        )
      })}
    </div>
  )
}

/* ───── Exposure by Symbol ───── */
function ExposureWidget({ exposure }) {
  if (!exposure || exposure.length === 0) {
    return (
      <div style={styles.card}>
        <div style={styles.cardTitle}>Exposure by Symbol</div>
        <div style={styles.emptyState}>No exposure data available</div>
      </div>
    )
  }

  const maxVol = Math.max(
    ...exposure.map((e) => Math.max(Math.abs(e.longVolume || 0), Math.abs(e.shortVolume || 0))),
    0.01
  )

  return (
    <div style={styles.card}>
      <div style={styles.cardTitle}>Exposure by Symbol</div>
      <div style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}>
        {exposure.map((e) => {
          const longPct = (Math.abs(e.longVolume || 0) / maxVol) * 100
          const shortPct = (Math.abs(e.shortVolume || 0) / maxVol) * 100
          const np = e.netProfit || 0
          return (
            <div key={e.symbol} style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
              <span style={{
                fontFamily: "'JetBrains Mono', monospace",
                fontSize: '12px',
                color: '#e1e8ed',
                minWidth: '70px',
                fontWeight: 600,
              }}>
                {e.symbol}
              </span>
              <div style={{ flex: 1, display: 'flex', flexDirection: 'column', gap: '3px' }}>
                {/* Long bar */}
                <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                  <div style={{
                    height: '8px',
                    width: `${longPct}%`,
                    minWidth: longPct > 0 ? '2px' : '0px',
                    background: 'linear-gradient(90deg, #00c853, #00e676)',
                    borderRadius: '4px',
                    transition: 'width 0.3s ease',
                  }} />
                  <span style={{
                    fontSize: '10px',
                    color: '#00c853',
                    fontFamily: "'JetBrains Mono', monospace",
                    whiteSpace: 'nowrap',
                  }}>
                    {fmtVol(e.longVolume)}
                  </span>
                </div>
                {/* Short bar */}
                <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                  <div style={{
                    height: '8px',
                    width: `${shortPct}%`,
                    minWidth: shortPct > 0 ? '2px' : '0px',
                    background: 'linear-gradient(90deg, #ff5252, #ff8a80)',
                    borderRadius: '4px',
                    transition: 'width 0.3s ease',
                  }} />
                  <span style={{
                    fontSize: '10px',
                    color: '#ff5252',
                    fontFamily: "'JetBrains Mono', monospace",
                    whiteSpace: 'nowrap',
                  }}>
                    {fmtVol(e.shortVolume)}
                  </span>
                </div>
              </div>
              <span style={{
                fontFamily: "'JetBrains Mono', monospace",
                fontSize: '12px',
                fontWeight: 600,
                color: profitColor(np),
                minWidth: '80px',
                textAlign: 'right',
              }}>
                {fmt$(np)}
              </span>
            </div>
          )
        })}
      </div>
    </div>
  )
}

/* ───── Top Movers ───── */
function TopMoversWidget({ topMovers }) {
  if (!topMovers || topMovers.length === 0) {
    return (
      <div style={styles.card}>
        <div style={styles.cardTitle}>Top Movers</div>
        <div style={styles.emptyState}>No mover data available</div>
      </div>
    )
  }

  return (
    <div style={styles.card}>
      <div style={styles.cardTitle}>Top Movers</div>
      <div>
        {topMovers.map((m, i) => (
          <div key={m.login || i} style={{
            ...styles.moverRow,
            borderBottom: i === topMovers.length - 1 ? 'none' : '1px solid #1c2d3a',
          }}>
            <span style={styles.moverLogin}>{m.login}</span>
            <span style={styles.moverName}>{m.name || '---'}</span>
            <span style={{ ...styles.moverProfit, color: profitColor(m.profit) }}>
              {fmt$(m.profit)}
            </span>
            <span style={{ ...styles.moverChange, color: profitColor(m.profitChange) }}>
              {m.profitChange > 0 ? '+' : ''}{fmt$(m.profitChange)}
            </span>
          </div>
        ))}
      </div>
    </div>
  )
}

/* ───── Realized P&L Area Chart ───── */
function PnlChart({ dealHistory, dealRange, setDealRange }) {
  const containerRef = useRef(null)
  const chartRef = useRef(null)
  const seriesRef = useRef(null)

  // Build chart data: cumulative profit from deal history buckets
  const chartData = useMemo(() => {
    if (!dealHistory || dealHistory.length === 0) return []
    let cumulative = 0
    return dealHistory
      .slice()
      .sort((a, b) => new Date(a.bucketTime).getTime() - new Date(b.bucketTime).getTime())
      .map((d) => {
        cumulative += d.totalProfit || 0
        return {
          time: Math.floor(new Date(d.bucketTime).getTime() / 1000),
          value: cumulative,
        }
      })
  }, [dealHistory])

  // Create chart once
  useEffect(() => {
    if (!containerRef.current) return

    const chart = createChart(containerRef.current, {
      width: containerRef.current.clientWidth,
      height: 280,
      layout: {
        background: { color: 'transparent' },
        textColor: '#5a7a8a',
        fontFamily: "'JetBrains Mono', monospace",
        fontSize: 11,
      },
      grid: {
        vertLines: { color: 'rgba(42,58,74,0.4)' },
        horzLines: { color: 'rgba(42,58,74,0.4)' },
      },
      rightPriceScale: {
        borderColor: '#2a3a4a',
      },
      timeScale: {
        borderColor: '#2a3a4a',
        timeVisible: true,
        secondsVisible: false,
      },
      crosshair: {
        horzLine: { color: 'rgba(74,158,255,0.3)', style: 2 },
        vertLine: { color: 'rgba(74,158,255,0.3)', style: 2 },
      },
    })

    const series = chart.addSeries(AreaSeries, {
      lineColor: '#00c853',
      lineWidth: 2,
      topColor: 'rgba(0,200,83,0.35)',
      bottomColor: 'rgba(0,200,83,0.02)',
      crosshairMarkerBackgroundColor: '#00c853',
      priceFormat: {
        type: 'custom',
        formatter: (price) => fmt$(price),
      },
    })

    chartRef.current = chart
    seriesRef.current = series

    // Handle resize
    const ro = new ResizeObserver((entries) => {
      for (const entry of entries) {
        const w = entry.contentRect.width
        if (w > 0) chart.resize(w, 280)
      }
    })
    ro.observe(containerRef.current)

    return () => {
      ro.disconnect()
      chart.remove()
      chartRef.current = null
      seriesRef.current = null
    }
  }, [])

  // Update data when chartData changes
  useEffect(() => {
    if (seriesRef.current && chartData.length > 0) {
      seriesRef.current.setData(chartData)
      if (chartRef.current) {
        chartRef.current.timeScale().fitContent()
      }
    } else if (seriesRef.current) {
      seriesRef.current.setData([])
    }
  }, [chartData])

  const ranges = ['1D', '7D', '30D']

  return (
    <div style={{ ...styles.card, gridColumn: '1 / -1' }}>
      <div style={{
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        marginBottom: '14px',
      }}>
        <div style={styles.cardTitle}>Realized P&L</div>
        <div style={{ display: 'flex', gap: '6px' }}>
          {ranges.map((r) => (
            <button
              key={r}
              onClick={() => setDealRange(r)}
              style={styles.rangeBtn(dealRange === r)}
            >
              {r}
            </button>
          ))}
        </div>
      </div>
      {(!dealHistory || dealHistory.length === 0) ? (
        <div style={styles.emptyState}>No deal history available</div>
      ) : null}
      <div ref={containerRef} style={{
        width: '100%',
        height: '280px',
        display: (!dealHistory || dealHistory.length === 0) ? 'none' : 'block',
      }} />
    </div>
  )
}

/* ───── Position Distribution ───── */
function PositionDistribution({ exposure }) {
  if (!exposure || exposure.length === 0) {
    return (
      <div style={styles.card}>
        <div style={styles.cardTitle}>Position Distribution</div>
        <div style={styles.emptyState}>No position data available</div>
      </div>
    )
  }

  // Each symbol: total positions = long + short volume count as a measure
  const distData = exposure.map((e) => ({
    symbol: e.symbol,
    total: Math.abs(e.longVolume || 0) + Math.abs(e.shortVolume || 0),
    long: Math.abs(e.longVolume || 0),
    short: Math.abs(e.shortVolume || 0),
  }))

  const maxTotal = Math.max(...distData.map((d) => d.total), 0.01)

  // Color palette for symbols
  const palette = ['#4a9eff', '#00c853', '#ff9800', '#e040fb', '#00bcd4', '#ffeb3b', '#ff5252', '#76ff03']

  return (
    <div style={styles.card}>
      <div style={styles.cardTitle}>Position Distribution</div>
      <div style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}>
        {distData.map((d, i) => {
          const pct = (d.total / maxTotal) * 100
          const barColor = palette[i % palette.length]
          return (
            <div key={d.symbol} style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
              <span style={{
                fontFamily: "'JetBrains Mono', monospace",
                fontSize: '12px',
                color: '#e1e8ed',
                minWidth: '70px',
                fontWeight: 600,
              }}>
                {d.symbol}
              </span>
              <div style={{ flex: 1, position: 'relative' }}>
                <div style={{
                  height: '10px',
                  width: `${pct}%`,
                  minWidth: pct > 0 ? '2px' : '0px',
                  background: barColor,
                  borderRadius: '5px',
                  opacity: 0.8,
                  transition: 'width 0.3s ease',
                }} />
              </div>
              <span style={{
                fontFamily: "'JetBrains Mono', monospace",
                fontSize: '12px',
                color: '#5a7a8a',
                minWidth: '60px',
                textAlign: 'right',
              }}>
                {fmtVol(d.total)}
              </span>
            </div>
          )
        })}
      </div>
    </div>
  )
}

/* ───── Main DashboardTab ───── */
export default function DashboardTab({ data }) {
  const {
    summary,
    exposure,
    topMovers,
    dealHistory,
    dealRange,
    setDealRange,
    loading,
    connected,
  } = data || {}

  return (
    <div style={styles.root}>
      {/* Connection indicator */}
      {!connected && (
        <div style={{
          background: 'rgba(255,82,82,0.1)',
          border: '1px solid rgba(255,82,82,0.3)',
          borderRadius: '8px',
          padding: '10px 16px',
          marginBottom: '14px',
          color: '#ff5252',
          fontSize: '13px',
          display: 'flex',
          alignItems: 'center',
          gap: '8px',
        }}>
          <span style={{
            width: '8px',
            height: '8px',
            borderRadius: '50%',
            background: '#ff5252',
            display: 'inline-block',
          }} />
          Dashboard disconnected - reconnecting...
        </div>
      )}

      {/* Loading overlay */}
      {loading && connected && (
        <div style={{
          color: '#4a9eff',
          fontSize: '13px',
          marginBottom: '14px',
          display: 'flex',
          alignItems: 'center',
          gap: '8px',
        }}>
          <span style={{
            width: '8px',
            height: '8px',
            borderRadius: '50%',
            background: '#4a9eff',
            display: 'inline-block',
            animation: 'none',
            opacity: 0.6,
          }} />
          Loading dashboard data...
        </div>
      )}

      <div style={styles.grid}>
        {/* Row 1: Summary Cards (spans full width) */}
        <SummaryCards summary={summary} />

        {/* Row 2: Exposure by Symbol */}
        <ExposureWidget exposure={exposure} />

        {/* Row 3: Top Movers */}
        <TopMoversWidget topMovers={topMovers} />

        {/* Row 4: P&L Area Chart (spans full width) */}
        <PnlChart
          dealHistory={dealHistory}
          dealRange={dealRange}
          setDealRange={setDealRange}
        />

        {/* Row 5: Position Distribution */}
        <PositionDistribution exposure={exposure} />
      </div>
    </div>
  )
}
