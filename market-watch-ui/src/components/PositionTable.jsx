import { useState } from 'react'

const styles = {
  container: {
    width: '100%',
    overflowX: 'auto',
    background: 'rgba(10, 17, 24, 0.6)',
    borderRadius: '6px',
    padding: '8px',
  },
  table: {
    width: '100%',
    borderCollapse: 'collapse',
    fontSize: '12px',
    fontFamily: "'Inter', sans-serif",
  },
  th: {
    padding: '8px 10px',
    textAlign: 'left',
    color: '#5a7a8a',
    fontWeight: 500,
    fontSize: '10px',
    textTransform: 'uppercase',
    letterSpacing: '0.5px',
    borderBottom: '1px solid #1a2a3a',
    whiteSpace: 'nowrap',
  },
  thRight: {
    padding: '8px 10px',
    textAlign: 'right',
    color: '#5a7a8a',
    fontWeight: 500,
    fontSize: '10px',
    textTransform: 'uppercase',
    letterSpacing: '0.5px',
    borderBottom: '1px solid #1a2a3a',
    whiteSpace: 'nowrap',
  },
  td: {
    padding: '7px 10px',
    textAlign: 'left',
    color: '#e1e8ed',
    fontFamily: "'JetBrains Mono', monospace",
    fontSize: '11px',
    borderBottom: '1px solid rgba(42, 58, 74, 0.4)',
    whiteSpace: 'nowrap',
  },
  tdRight: {
    padding: '7px 10px',
    textAlign: 'right',
    color: '#e1e8ed',
    fontFamily: "'JetBrains Mono', monospace",
    fontSize: '11px',
    borderBottom: '1px solid rgba(42, 58, 74, 0.4)',
    whiteSpace: 'nowrap',
  },
  tdLabel: {
    padding: '7px 10px',
    textAlign: 'left',
    color: '#e1e8ed',
    fontFamily: "'Inter', sans-serif",
    fontSize: '11px',
    borderBottom: '1px solid rgba(42, 58, 74, 0.4)',
    whiteSpace: 'nowrap',
  },
  buyBadge: {
    display: 'inline-block',
    padding: '2px 8px',
    borderRadius: '4px',
    fontSize: '10px',
    fontWeight: 600,
    fontFamily: "'Inter', sans-serif",
    background: 'rgba(0, 200, 83, 0.15)',
    color: '#00c853',
    border: '1px solid rgba(0, 200, 83, 0.3)',
  },
  sellBadge: {
    display: 'inline-block',
    padding: '2px 8px',
    borderRadius: '4px',
    fontSize: '10px',
    fontWeight: 600,
    fontFamily: "'Inter', sans-serif",
    background: 'rgba(255, 82, 82, 0.15)',
    color: '#ff5252',
    border: '1px solid rgba(255, 82, 82, 0.3)',
  },
  closeBtn: {
    padding: '4px 10px',
    borderRadius: '4px',
    border: '1px solid rgba(255, 82, 82, 0.4)',
    background: 'rgba(255, 82, 82, 0.1)',
    color: '#ff5252',
    cursor: 'pointer',
    fontSize: '10px',
    fontFamily: "'Inter', sans-serif",
    fontWeight: 600,
    marginRight: '4px',
    transition: 'all 0.2s',
  },
  modifyBtn: {
    padding: '4px 10px',
    borderRadius: '4px',
    border: '1px solid rgba(74, 158, 255, 0.4)',
    background: 'rgba(74, 158, 255, 0.1)',
    color: '#4a9eff',
    cursor: 'pointer',
    fontSize: '10px',
    fontFamily: "'Inter', sans-serif",
    fontWeight: 600,
    transition: 'all 0.2s',
  },
  spinner: {
    display: 'inline-block',
    width: '14px',
    height: '14px',
    border: '2px solid rgba(74, 158, 255, 0.2)',
    borderTop: '2px solid #4a9eff',
    borderRadius: '50%',
    animation: 'posSpinner 0.8s linear infinite',
  },
  modifyForm: {
    display: 'flex',
    alignItems: 'center',
    gap: '6px',
    padding: '6px 10px',
    background: 'rgba(22, 34, 48, 0.8)',
    borderRadius: '6px',
    border: '1px solid #2a3a4a',
  },
  modifyInput: {
    width: '80px',
    height: '26px',
    padding: '0 6px',
    borderRadius: '4px',
    border: '1px solid #2a3a4a',
    background: '#0a1118',
    color: '#e1e8ed',
    fontSize: '11px',
    fontFamily: "'JetBrains Mono', monospace",
    outline: 'none',
  },
  modifyLabel: {
    fontSize: '10px',
    fontFamily: "'Inter', sans-serif",
    color: '#5a7a8a',
    fontWeight: 500,
  },
  modifySaveBtn: {
    padding: '4px 10px',
    borderRadius: '4px',
    border: '1px solid rgba(0, 200, 83, 0.4)',
    background: 'rgba(0, 200, 83, 0.15)',
    color: '#00c853',
    cursor: 'pointer',
    fontSize: '10px',
    fontFamily: "'Inter', sans-serif",
    fontWeight: 600,
  },
  modifyCancelBtn: {
    padding: '4px 10px',
    borderRadius: '4px',
    border: '1px solid rgba(90, 122, 138, 0.4)',
    background: 'rgba(90, 122, 138, 0.1)',
    color: '#5a7a8a',
    cursor: 'pointer',
    fontSize: '10px',
    fontFamily: "'Inter', sans-serif",
    fontWeight: 600,
  },
  empty: {
    padding: '20px',
    textAlign: 'center',
    color: '#5a7a8a',
    fontSize: '12px',
    fontFamily: "'Inter', sans-serif",
  },
  actionsCell: {
    padding: '7px 10px',
    textAlign: 'right',
    borderBottom: '1px solid rgba(42, 58, 74, 0.4)',
    whiteSpace: 'nowrap',
  },
}

const spinnerKeyframes = `
@keyframes posSpinner {
  0% { transform: rotate(0deg); }
  100% { transform: rotate(360deg); }
}
`

function formatPrice(val) {
  if (val == null || isNaN(val)) return '-'
  return Number(val).toFixed(5)
}

function formatProfit(val) {
  if (val == null || isNaN(val)) return '$0.00'
  const sign = val < 0 ? '-' : ''
  return sign + '$' + Math.abs(val).toLocaleString('en-US', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })
}

function formatVolume(vol) {
  if (vol == null || isNaN(vol)) return '0.00'
  return (vol / 10000).toFixed(2)
}

export default function PositionTable({ positions, login, onClose, onModify, loadingTickets, ticks }) {
  const [modifyTicket, setModifyTicket] = useState(null)
  const [modifySL, setModifySL] = useState('')
  const [modifyTP, setModifyTP] = useState('')

  const posArray = Array.isArray(positions) ? positions : []

  const handleModifyClick = (pos) => {
    setModifyTicket(pos.ticket)
    const sl = pos.priceSl ?? pos.sl
    const tp = pos.priceTp ?? pos.tp
    setModifySL(sl != null ? String(sl) : '0')
    setModifyTP(tp != null ? String(tp) : '0')
  }

  const handleModifySubmit = (pos) => {
    if (onModify) {
      onModify(login, pos.ticket, pos.symbol, parseFloat(modifySL) || 0, parseFloat(modifyTP) || 0)
    }
    setModifyTicket(null)
  }

  const handleModifyCancel = () => {
    setModifyTicket(null)
  }

  if (posArray.length === 0) {
    return (
      <div style={styles.container}>
        <style>{spinnerKeyframes}</style>
        <div style={styles.empty}>No open positions</div>
      </div>
    )
  }

  return (
    <div style={styles.container}>
      <style>{spinnerKeyframes}</style>
      <table style={styles.table}>
        <thead>
          <tr>
            <th style={styles.th}>Ticket</th>
            <th style={styles.th}>Symbol</th>
            <th style={styles.th}>Type</th>
            <th style={styles.thRight}>Volume</th>
            <th style={styles.thRight}>Open Price</th>
            <th style={styles.thRight}>Current Price</th>
            <th style={styles.thRight}>S/L</th>
            <th style={styles.thRight}>T/P</th>
            <th style={styles.thRight}>Profit</th>
            <th style={styles.thRight}>Swap</th>
            <th style={styles.thRight}>Actions</th>
          </tr>
        </thead>
        <tbody>
          {posArray.map(pos => {
            const isBuy = pos.action === 0
            const isLoading = loadingTickets && loadingTickets.has(pos.ticket)
            const profit = pos.profit ?? 0
            const profitColor = profit >= 0 ? '#00c853' : '#ff5252'

            // Use live tick price if available
            const tick = ticks && pos.symbol ? ticks[pos.symbol] : null
            const currentPrice = tick
              ? (isBuy ? tick.bid : tick.ask)
              : (pos.priceCurrent ?? pos.currentPrice ?? '-')

            return (
              <tr key={pos.ticket}>
                <td style={styles.td}>{pos.ticket}</td>
                <td style={styles.tdLabel}>{pos.symbol}</td>
                <td style={{ ...styles.td, fontFamily: "'Inter', sans-serif" }}>
                  <span style={isBuy ? styles.buyBadge : styles.sellBadge}>
                    {isBuy ? 'Buy' : 'Sell'}
                  </span>
                </td>
                <td style={styles.tdRight}>{formatVolume(pos.volume)}</td>
                <td style={styles.tdRight}>{formatPrice(pos.priceOpen ?? pos.openPrice)}</td>
                <td style={styles.tdRight}>
                  {typeof currentPrice === 'number' ? formatPrice(currentPrice) : currentPrice}
                </td>
                <td style={styles.tdRight}>{(pos.priceSl ?? pos.sl) ? formatPrice(pos.priceSl ?? pos.sl) : '-'}</td>
                <td style={styles.tdRight}>{(pos.priceTp ?? pos.tp) ? formatPrice(pos.priceTp ?? pos.tp) : '-'}</td>
                <td style={{ ...styles.tdRight, color: profitColor, fontWeight: 600 }}>
                  {formatProfit(profit)}
                </td>
                <td style={styles.tdRight}>{(pos.storage ?? pos.swap) != null ? formatProfit(pos.storage ?? pos.swap) : '$0.00'}</td>
                <td style={styles.actionsCell}>
                  {isLoading ? (
                    <div style={styles.spinner} />
                  ) : modifyTicket === pos.ticket ? (
                    <div style={styles.modifyForm}>
                      <span style={styles.modifyLabel}>SL:</span>
                      <input
                        style={styles.modifyInput}
                        type="number"
                        step="0.00001"
                        value={modifySL}
                        onChange={e => setModifySL(e.target.value)}
                      />
                      <span style={styles.modifyLabel}>TP:</span>
                      <input
                        style={styles.modifyInput}
                        type="number"
                        step="0.00001"
                        value={modifyTP}
                        onChange={e => setModifyTP(e.target.value)}
                      />
                      <button
                        style={styles.modifySaveBtn}
                        onClick={() => handleModifySubmit(pos)}
                        onMouseEnter={e => { e.target.style.background = 'rgba(0, 200, 83, 0.3)' }}
                        onMouseLeave={e => { e.target.style.background = 'rgba(0, 200, 83, 0.15)' }}
                      >
                        Save
                      </button>
                      <button
                        style={styles.modifyCancelBtn}
                        onClick={handleModifyCancel}
                        onMouseEnter={e => { e.target.style.background = 'rgba(90, 122, 138, 0.2)' }}
                        onMouseLeave={e => { e.target.style.background = 'rgba(90, 122, 138, 0.1)' }}
                      >
                        Cancel
                      </button>
                    </div>
                  ) : (
                    <>
                      <button
                        style={styles.closeBtn}
                        onClick={() => onClose && onClose(login, pos.ticket, pos.symbol, pos.action, pos.volume)}
                        onMouseEnter={e => { e.target.style.background = 'rgba(255, 82, 82, 0.25)' }}
                        onMouseLeave={e => { e.target.style.background = 'rgba(255, 82, 82, 0.1)' }}
                      >
                        Close
                      </button>
                      <button
                        style={styles.modifyBtn}
                        onClick={() => handleModifyClick(pos)}
                        onMouseEnter={e => { e.target.style.background = 'rgba(74, 158, 255, 0.25)' }}
                        onMouseLeave={e => { e.target.style.background = 'rgba(74, 158, 255, 0.1)' }}
                      >
                        Modify
                      </button>
                    </>
                  )}
                </td>
              </tr>
            )
          })}
        </tbody>
      </table>
    </div>
  )
}
