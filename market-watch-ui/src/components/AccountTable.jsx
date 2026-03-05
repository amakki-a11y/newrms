import React, { useState, useEffect, useRef, useCallback } from 'react'
import PositionTable from './PositionTable'
import OpenPositionDialog from './OpenPositionDialog'

const styles = {
  container: {
    display: 'flex',
    flexDirection: 'column',
    height: '100%',
    fontFamily: "'Inter', sans-serif",
  },
  toolbar: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    padding: '12px 16px',
    gap: '12px',
    flexWrap: 'wrap',
  },
  searchContainer: {
    position: 'relative',
    display: 'flex',
    alignItems: 'center',
  },
  searchInput: {
    height: '32px',
    width: '240px',
    padding: '0 12px 0 34px',
    borderRadius: '6px',
    border: '1px solid #2a3a4a',
    background: '#0a1118',
    color: '#e1e8ed',
    fontSize: '12px',
    fontFamily: "'Inter', sans-serif",
    outline: 'none',
    transition: 'border-color 0.2s',
  },
  searchIcon: {
    position: 'absolute',
    left: '10px',
    color: '#5a7a8a',
    fontSize: '13px',
    pointerEvents: 'none',
  },
  openPosBtn: {
    padding: '6px 16px',
    borderRadius: '6px',
    border: '1px solid rgba(74, 158, 255, 0.4)',
    background: 'rgba(74, 158, 255, 0.15)',
    color: '#4a9eff',
    cursor: 'pointer',
    fontSize: '12px',
    fontFamily: "'Inter', sans-serif",
    fontWeight: 600,
    display: 'flex',
    alignItems: 'center',
    gap: '6px',
    transition: 'all 0.2s',
  },
  tableWrapper: {
    flex: 1,
    overflowX: 'auto',
    overflowY: 'auto',
    padding: '0 16px',
  },
  table: {
    width: '100%',
    borderCollapse: 'collapse',
    fontSize: '12px',
  },
  th: {
    padding: '10px 12px',
    textAlign: 'left',
    color: '#5a7a8a',
    fontWeight: 500,
    fontSize: '10px',
    textTransform: 'uppercase',
    letterSpacing: '0.5px',
    borderBottom: '1px solid #2a3a4a',
    whiteSpace: 'nowrap',
    fontFamily: "'Inter', sans-serif",
    position: 'sticky',
    top: 0,
    background: '#0a1118',
    zIndex: 1,
  },
  thRight: {
    padding: '10px 12px',
    textAlign: 'right',
    color: '#5a7a8a',
    fontWeight: 500,
    fontSize: '10px',
    textTransform: 'uppercase',
    letterSpacing: '0.5px',
    borderBottom: '1px solid #2a3a4a',
    whiteSpace: 'nowrap',
    fontFamily: "'Inter', sans-serif",
    position: 'sticky',
    top: 0,
    background: '#0a1118',
    zIndex: 1,
  },
  tr: {
    cursor: 'pointer',
    transition: 'background 0.15s',
  },
  td: {
    padding: '10px 12px',
    textAlign: 'left',
    color: '#e1e8ed',
    fontFamily: "'JetBrains Mono', monospace",
    fontSize: '12px',
    borderBottom: '1px solid rgba(42, 58, 74, 0.4)',
    whiteSpace: 'nowrap',
  },
  tdRight: {
    padding: '10px 12px',
    textAlign: 'right',
    color: '#e1e8ed',
    fontFamily: "'JetBrains Mono', monospace",
    fontSize: '12px',
    borderBottom: '1px solid rgba(42, 58, 74, 0.4)',
    whiteSpace: 'nowrap',
  },
  tdName: {
    padding: '10px 12px',
    textAlign: 'left',
    color: '#e1e8ed',
    fontFamily: "'Inter', sans-serif",
    fontSize: '12px',
    borderBottom: '1px solid rgba(42, 58, 74, 0.4)',
    whiteSpace: 'nowrap',
    maxWidth: '160px',
    overflow: 'hidden',
    textOverflow: 'ellipsis',
  },
  expandedRow: {
    background: 'rgba(17, 29, 40, 0.5)',
  },
  expandedCell: {
    padding: '8px 12px 12px 12px',
    borderBottom: '1px solid #2a3a4a',
  },
  pagination: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    gap: '4px',
    padding: '12px 16px',
    borderTop: '1px solid #1a2a3a',
  },
  pageBtn: {
    minWidth: '32px',
    height: '32px',
    padding: '0 8px',
    borderRadius: '6px',
    border: '1px solid #2a3a4a',
    background: 'transparent',
    color: '#e1e8ed',
    cursor: 'pointer',
    fontSize: '12px',
    fontFamily: "'JetBrains Mono', monospace",
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    transition: 'all 0.15s',
  },
  pageBtnActive: {
    minWidth: '32px',
    height: '32px',
    padding: '0 8px',
    borderRadius: '6px',
    border: '1px solid #4a9eff',
    background: 'rgba(74, 158, 255, 0.2)',
    color: '#4a9eff',
    cursor: 'pointer',
    fontSize: '12px',
    fontFamily: "'JetBrains Mono', monospace",
    fontWeight: 600,
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
  },
  pageBtnDisabled: {
    minWidth: '32px',
    height: '32px',
    padding: '0 8px',
    borderRadius: '6px',
    border: '1px solid #1a2a3a',
    background: 'transparent',
    color: '#2a3a4a',
    cursor: 'default',
    fontSize: '12px',
    fontFamily: "'Inter', sans-serif",
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
  },
  empty: {
    padding: '40px',
    textAlign: 'center',
    color: '#5a7a8a',
    fontSize: '13px',
    fontFamily: "'Inter', sans-serif",
  },
  expandArrow: {
    display: 'inline-block',
    transition: 'transform 0.2s',
    fontSize: '10px',
    color: '#5a7a8a',
    marginRight: '6px',
  },
}

function formatCurrency(val) {
  if (val == null || isNaN(val)) return '$0.00'
  const abs = Math.abs(val)
  const sign = val < 0 ? '-' : ''
  return sign + '$' + abs.toLocaleString('en-US', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })
}

function formatMarginLevel(val) {
  if (val == null || isNaN(val) || val === 0) return '-'
  return val.toFixed(1) + '%'
}

function marginLevelColor(val) {
  if (val == null || isNaN(val) || val === 0) return '#5a7a8a'
  if (val > 200) return '#00c853'
  if (val >= 100) return '#ffaa00'
  return '#ff5252'
}

export default function AccountTable({
  accounts,
  positions,
  summary,
  page,
  totalPages,
  search,
  onSetPage,
  onSetSearch,
  onGetPositions,
  onClosePosition,
  onOpenPosition,
  onModifyPosition,
  loadingTickets,
  ticks,
}) {
  const [expandedLogin, setExpandedLogin] = useState(null)
  const [localSearch, setLocalSearch] = useState(search || '')
  const [dialogOpen, setDialogOpen] = useState(false)
  const [dialogLogin, setDialogLogin] = useState(null)
  const debounceRef = useRef(null)

  // Sync local search with prop
  useEffect(() => {
    setLocalSearch(search || '')
  }, [search])

  const handleSearchChange = useCallback((e) => {
    const val = e.target.value
    setLocalSearch(val)
    if (debounceRef.current) clearTimeout(debounceRef.current)
    debounceRef.current = setTimeout(() => {
      if (onSetSearch) onSetSearch(val)
    }, 300)
  }, [onSetSearch])

  const handleRowClick = useCallback((login) => {
    if (expandedLogin === login) {
      setExpandedLogin(null)
    } else {
      setExpandedLogin(login)
      if (onGetPositions) onGetPositions(login)
    }
  }, [expandedLogin, onGetPositions])

  const handleOpenPositionClick = useCallback(() => {
    setDialogLogin(expandedLogin)
    setDialogOpen(true)
  }, [expandedLogin])

  const handleDialogSubmit = useCallback((symbol, action, volume) => {
    const login = dialogLogin || (accounts.length > 0 ? accounts[0].login : null)
    if (login && onOpenPosition) {
      onOpenPosition(login, symbol, action, volume)
    }
    setDialogOpen(false)
  }, [dialogLogin, accounts, onOpenPosition])

  // Build page numbers
  const pageNumbers = []
  const maxVisible = 5
  let startPage = Math.max(1, page - Math.floor(maxVisible / 2))
  let endPage = Math.min(totalPages, startPage + maxVisible - 1)
  if (endPage - startPage < maxVisible - 1) {
    startPage = Math.max(1, endPage - maxVisible + 1)
  }
  for (let i = startPage; i <= endPage; i++) {
    pageNumbers.push(i)
  }

  // Collect symbols for dialog
  const symbolList = ticks ? Object.keys(ticks) : []

  return (
    <div style={styles.container}>
      <div style={styles.toolbar}>
        <div style={styles.searchContainer}>
          <span style={styles.searchIcon}>{'\u{1F50D}'}</span>
          <input
            style={styles.searchInput}
            type="text"
            placeholder="Search accounts..."
            value={localSearch}
            onChange={handleSearchChange}
            onFocus={e => { e.target.style.borderColor = '#4a9eff' }}
            onBlur={e => { e.target.style.borderColor = '#2a3a4a' }}
          />
        </div>
        <button
          style={styles.openPosBtn}
          onClick={handleOpenPositionClick}
          onMouseEnter={e => { e.target.style.background = 'rgba(74, 158, 255, 0.3)' }}
          onMouseLeave={e => { e.target.style.background = 'rgba(74, 158, 255, 0.15)' }}
        >
          + Open Position
        </button>
      </div>

      <div style={styles.tableWrapper}>
        {(!accounts || accounts.length === 0) ? (
          <div style={styles.empty}>No accounts found</div>
        ) : (
          <table style={styles.table}>
            <thead>
              <tr>
                <th style={styles.th}></th>
                <th style={styles.th}>Login</th>
                <th style={styles.th}>Name</th>
                <th style={styles.th}>Group</th>
                <th style={styles.thRight}>Balance</th>
                <th style={styles.thRight}>Equity</th>
                <th style={styles.thRight}>Margin</th>
                <th style={styles.thRight}>Free Margin</th>
                <th style={styles.thRight}>Margin Level</th>
                <th style={styles.thRight}>Profit</th>
              </tr>
            </thead>
            <tbody>
              {accounts.map(acc => {
                const isExpanded = expandedLogin === acc.login
                const profit = acc.profit ?? 0
                const profitColor = profit >= 0 ? '#00c853' : '#ff5252'
                const ml = acc.marginLevel ?? 0
                const mlColor = marginLevelColor(ml)
                const posForLogin = positions && positions[acc.login]

                return (
                  <React.Fragment key={acc.login}>
                    <tr
                      style={{
                        ...styles.tr,
                        background: isExpanded ? 'rgba(74, 158, 255, 0.05)' : 'transparent',
                      }}
                      onClick={() => handleRowClick(acc.login)}
                      onMouseEnter={e => {
                        if (!isExpanded) e.currentTarget.style.background = 'rgba(255,255,255,0.02)'
                      }}
                      onMouseLeave={e => {
                        if (!isExpanded) e.currentTarget.style.background = 'transparent'
                      }}
                    >
                      <td style={{ ...styles.td, width: '24px', padding: '10px 4px 10px 12px' }}>
                        <span style={{
                          ...styles.expandArrow,
                          transform: isExpanded ? 'rotate(90deg)' : 'rotate(0deg)',
                        }}>
                          {'\u25B6'}
                        </span>
                      </td>
                      <td style={styles.td}>{acc.login}</td>
                      <td style={styles.tdName}>{acc.name || '-'}</td>
                      <td style={{ ...styles.td, fontFamily: "'Inter', sans-serif", color: '#5a7a8a', fontSize: '11px' }}>
                        {acc.group || '-'}
                      </td>
                      <td style={styles.tdRight}>{formatCurrency(acc.balance)}</td>
                      <td style={styles.tdRight}>{formatCurrency(acc.equity)}</td>
                      <td style={styles.tdRight}>{formatCurrency(acc.margin)}</td>
                      <td style={styles.tdRight}>{formatCurrency(acc.marginFree)}</td>
                      <td style={{ ...styles.tdRight, color: mlColor, fontWeight: 600 }}>
                        {formatMarginLevel(ml)}
                      </td>
                      <td style={{ ...styles.tdRight, color: profitColor, fontWeight: 600 }}>
                        {formatCurrency(profit)}
                      </td>
                    </tr>
                    {isExpanded && (
                      <tr style={styles.expandedRow}>
                        <td colSpan={10} style={styles.expandedCell}>
                          <PositionTable
                            positions={posForLogin}
                            login={acc.login}
                            onClose={onClosePosition}
                            onModify={onModifyPosition}
                            loadingTickets={loadingTickets}
                            ticks={ticks}
                          />
                        </td>
                      </tr>
                    )}
                  </React.Fragment>
                )
              })}
            </tbody>
          </table>
        )}
      </div>

      {totalPages > 1 && (
        <div style={styles.pagination}>
          <button
            style={page <= 1 ? styles.pageBtnDisabled : styles.pageBtn}
            onClick={() => page > 1 && onSetPage && onSetPage(page - 1)}
            disabled={page <= 1}
            onMouseEnter={e => { if (page > 1) e.target.style.background = 'rgba(255,255,255,0.05)' }}
            onMouseLeave={e => { e.target.style.background = 'transparent' }}
          >
            Prev
          </button>
          {startPage > 1 && (
            <>
              <button
                style={styles.pageBtn}
                onClick={() => onSetPage && onSetPage(1)}
                onMouseEnter={e => { e.target.style.background = 'rgba(255,255,255,0.05)' }}
                onMouseLeave={e => { e.target.style.background = 'transparent' }}
              >
                1
              </button>
              {startPage > 2 && <span style={{ color: '#5a7a8a', fontSize: '12px', padding: '0 4px' }}>...</span>}
            </>
          )}
          {pageNumbers.map(n => (
            <button
              key={n}
              style={n === page ? styles.pageBtnActive : styles.pageBtn}
              onClick={() => onSetPage && onSetPage(n)}
              onMouseEnter={e => { if (n !== page) e.target.style.background = 'rgba(255,255,255,0.05)' }}
              onMouseLeave={e => { if (n !== page) e.target.style.background = 'transparent' }}
            >
              {n}
            </button>
          ))}
          {endPage < totalPages && (
            <>
              {endPage < totalPages - 1 && <span style={{ color: '#5a7a8a', fontSize: '12px', padding: '0 4px' }}>...</span>}
              <button
                style={styles.pageBtn}
                onClick={() => onSetPage && onSetPage(totalPages)}
                onMouseEnter={e => { e.target.style.background = 'rgba(255,255,255,0.05)' }}
                onMouseLeave={e => { e.target.style.background = 'transparent' }}
              >
                {totalPages}
              </button>
            </>
          )}
          <button
            style={page >= totalPages ? styles.pageBtnDisabled : styles.pageBtn}
            onClick={() => page < totalPages && onSetPage && onSetPage(page + 1)}
            disabled={page >= totalPages}
            onMouseEnter={e => { if (page < totalPages) e.target.style.background = 'rgba(255,255,255,0.05)' }}
            onMouseLeave={e => { e.target.style.background = 'transparent' }}
          >
            Next
          </button>
        </div>
      )}

      <OpenPositionDialog
        isOpen={dialogOpen}
        onClose={() => setDialogOpen(false)}
        onSubmit={handleDialogSubmit}
        symbols={symbolList}
      />
    </div>
  )
}
