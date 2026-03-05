import { useState } from 'react'

const styles = {
  header: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    height: '48px',
    padding: '0 16px',
    background: '#0d1520',
    borderBottom: '1px solid #1a2a3a',
    flexShrink: 0,
    zIndex: 100,
  },
  left: {
    display: 'flex',
    alignItems: 'center',
    gap: '12px',
  },
  title: {
    fontSize: '16px',
    fontWeight: 700,
    fontFamily: "'Inter', sans-serif",
    color: '#e1e8ed',
    letterSpacing: '0.5px',
  },
  titleAccent: {
    color: '#4a9eff',
  },
  right: {
    display: 'flex',
    alignItems: 'center',
    gap: '16px',
  },
  statusDot: (connected) => ({
    width: '8px',
    height: '8px',
    borderRadius: '50%',
    background: connected ? '#00c853' : '#ff5252',
    boxShadow: connected
      ? '0 0 6px rgba(0, 200, 83, 0.5)'
      : '0 0 6px rgba(255, 82, 82, 0.5)',
    flexShrink: 0,
  }),
  statusContainer: {
    display: 'flex',
    alignItems: 'center',
    gap: '6px',
    fontSize: '12px',
    fontFamily: "'Inter', sans-serif",
    color: '#5a7a8a',
  },
  badge: {
    display: 'inline-flex',
    alignItems: 'center',
    gap: '4px',
    padding: '2px 8px',
    borderRadius: '10px',
    fontSize: '11px',
    fontFamily: "'JetBrains Mono', monospace",
    fontWeight: 500,
    background: 'rgba(74, 158, 255, 0.1)',
    color: '#4a9eff',
    border: '1px solid rgba(74, 158, 255, 0.2)',
  },
  searchContainer: {
    position: 'relative',
    display: 'flex',
    alignItems: 'center',
  },
  searchInput: {
    height: '28px',
    width: '180px',
    padding: '0 10px 0 30px',
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
    left: '8px',
    color: '#5a7a8a',
    fontSize: '13px',
    pointerEvents: 'none',
  },
}

export default function Header({ connected, symbolCount, tickCount, onSearch }) {
  const [searchValue, setSearchValue] = useState('')

  const handleSearchChange = (e) => {
    const val = e.target.value
    setSearchValue(val)
    if (onSearch) onSearch(val)
  }

  return (
    <div style={styles.header}>
      <div style={styles.left}>
        <div style={styles.title}>
          <span style={styles.titleAccent}>RMS</span> Dashboard
        </div>
      </div>

      <div style={styles.right}>
        {onSearch && (
          <div style={styles.searchContainer}>
            <span style={styles.searchIcon}>&#x1F50D;</span>
            <input
              style={styles.searchInput}
              type="text"
              placeholder="Search symbols..."
              value={searchValue}
              onChange={handleSearchChange}
              onFocus={(e) => { e.target.style.borderColor = '#4a9eff' }}
              onBlur={(e) => { e.target.style.borderColor = '#2a3a4a' }}
            />
          </div>
        )}

        <div style={styles.badge}>
          <span>SYM</span>
          <span>{symbolCount ?? 0}</span>
        </div>

        <div style={styles.badge}>
          <span>TICKS</span>
          <span>{tickCount ?? 0}</span>
        </div>

        <div style={styles.statusContainer}>
          <div style={styles.statusDot(connected)} />
          <span>{connected ? 'Connected' : 'Disconnected'}</span>
        </div>
      </div>
    </div>
  )
}
