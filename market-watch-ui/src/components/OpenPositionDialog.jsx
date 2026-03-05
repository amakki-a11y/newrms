import { useState, useEffect } from 'react'

const styles = {
  overlay: {
    position: 'fixed',
    top: 0,
    left: 0,
    right: 0,
    bottom: 0,
    background: 'rgba(0, 0, 0, 0.7)',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    zIndex: 1000,
    backdropFilter: 'blur(4px)',
  },
  dialog: {
    background: 'linear-gradient(135deg, #111d28, #162230)',
    border: '1px solid #2a3a4a',
    borderRadius: '12px',
    padding: '24px',
    width: '380px',
    maxWidth: '90vw',
    boxShadow: '0 20px 60px rgba(0, 0, 0, 0.5)',
  },
  title: {
    fontSize: '16px',
    fontWeight: 600,
    fontFamily: "'Inter', sans-serif",
    color: '#e1e8ed',
    marginBottom: '20px',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  closeX: {
    background: 'none',
    border: 'none',
    color: '#5a7a8a',
    cursor: 'pointer',
    fontSize: '18px',
    padding: '4px',
    lineHeight: 1,
    transition: 'color 0.2s',
  },
  fieldGroup: {
    marginBottom: '16px',
  },
  label: {
    display: 'block',
    fontSize: '11px',
    fontFamily: "'Inter', sans-serif",
    color: '#5a7a8a',
    textTransform: 'uppercase',
    letterSpacing: '0.5px',
    fontWeight: 500,
    marginBottom: '6px',
  },
  select: {
    width: '100%',
    height: '36px',
    padding: '0 10px',
    borderRadius: '6px',
    border: '1px solid #2a3a4a',
    background: '#0a1118',
    color: '#e1e8ed',
    fontSize: '13px',
    fontFamily: "'JetBrains Mono', monospace",
    outline: 'none',
    cursor: 'pointer',
    transition: 'border-color 0.2s',
    appearance: 'none',
    WebkitAppearance: 'none',
  },
  actionGroup: {
    display: 'flex',
    gap: '8px',
    marginBottom: '16px',
  },
  buyBtn: (active) => ({
    flex: 1,
    height: '40px',
    borderRadius: '6px',
    border: active ? '2px solid #00c853' : '1px solid rgba(0, 200, 83, 0.3)',
    background: active ? 'rgba(0, 200, 83, 0.2)' : 'rgba(0, 200, 83, 0.05)',
    color: '#00c853',
    cursor: 'pointer',
    fontSize: '13px',
    fontFamily: "'Inter', sans-serif",
    fontWeight: 700,
    transition: 'all 0.2s',
    letterSpacing: '0.5px',
  }),
  sellBtn: (active) => ({
    flex: 1,
    height: '40px',
    borderRadius: '6px',
    border: active ? '2px solid #ff5252' : '1px solid rgba(255, 82, 82, 0.3)',
    background: active ? 'rgba(255, 82, 82, 0.2)' : 'rgba(255, 82, 82, 0.05)',
    color: '#ff5252',
    cursor: 'pointer',
    fontSize: '13px',
    fontFamily: "'Inter', sans-serif",
    fontWeight: 700,
    transition: 'all 0.2s',
    letterSpacing: '0.5px',
  }),
  volumeInput: {
    width: '100%',
    height: '36px',
    padding: '0 10px',
    borderRadius: '6px',
    border: '1px solid #2a3a4a',
    background: '#0a1118',
    color: '#e1e8ed',
    fontSize: '13px',
    fontFamily: "'JetBrains Mono', monospace",
    outline: 'none',
    transition: 'border-color 0.2s',
    boxSizing: 'border-box',
  },
  footer: {
    display: 'flex',
    gap: '8px',
    marginTop: '20px',
  },
  cancelBtn: {
    flex: 1,
    height: '38px',
    borderRadius: '6px',
    border: '1px solid #2a3a4a',
    background: 'transparent',
    color: '#5a7a8a',
    cursor: 'pointer',
    fontSize: '13px',
    fontFamily: "'Inter', sans-serif",
    fontWeight: 600,
    transition: 'all 0.2s',
  },
  submitBtn: {
    flex: 1,
    height: '38px',
    borderRadius: '6px',
    border: '1px solid rgba(74, 158, 255, 0.4)',
    background: 'rgba(74, 158, 255, 0.2)',
    color: '#4a9eff',
    cursor: 'pointer',
    fontSize: '13px',
    fontFamily: "'Inter', sans-serif",
    fontWeight: 700,
    transition: 'all 0.2s',
    letterSpacing: '0.3px',
  },
  submitBtnDisabled: {
    flex: 1,
    height: '38px',
    borderRadius: '6px',
    border: '1px solid #1a2a3a',
    background: 'rgba(26, 42, 58, 0.3)',
    color: '#2a3a4a',
    cursor: 'default',
    fontSize: '13px',
    fontFamily: "'Inter', sans-serif",
    fontWeight: 700,
    letterSpacing: '0.3px',
  },
}

export default function OpenPositionDialog({ isOpen, onClose, onSubmit, symbols }) {
  const [symbol, setSymbol] = useState('')
  const [action, setAction] = useState(0) // 0=Buy, 1=Sell
  const [volume, setVolume] = useState('0.01')

  // Reset form when dialog opens
  useEffect(() => {
    if (isOpen) {
      setSymbol(symbols && symbols.length > 0 ? symbols[0] : '')
      setAction(0)
      setVolume('0.01')
    }
  }, [isOpen, symbols])

  if (!isOpen) return null

  const canSubmit = symbol && parseFloat(volume) > 0

  const handleSubmit = () => {
    if (!canSubmit) return
    const vol = parseFloat(volume)
    if (onSubmit) onSubmit(symbol, action, vol)
  }

  const handleOverlayClick = (e) => {
    if (e.target === e.currentTarget && onClose) onClose()
  }

  const handleKeyDown = (e) => {
    if (e.key === 'Escape' && onClose) onClose()
  }

  return (
    <div style={styles.overlay} onClick={handleOverlayClick} onKeyDown={handleKeyDown}>
      <div style={styles.dialog}>
        <div style={styles.title}>
          <span>Open Position</span>
          <button
            style={styles.closeX}
            onClick={onClose}
            onMouseEnter={e => { e.target.style.color = '#e1e8ed' }}
            onMouseLeave={e => { e.target.style.color = '#5a7a8a' }}
          >
            {'\u2715'}
          </button>
        </div>

        <div style={styles.fieldGroup}>
          <label style={styles.label}>Symbol</label>
          <select
            style={styles.select}
            value={symbol}
            onChange={e => setSymbol(e.target.value)}
            onFocus={e => { e.target.style.borderColor = '#4a9eff' }}
            onBlur={e => { e.target.style.borderColor = '#2a3a4a' }}
          >
            {(!symbols || symbols.length === 0) && (
              <option value="">No symbols available</option>
            )}
            {symbols && symbols.map(s => (
              <option key={s} value={s}>{s}</option>
            ))}
          </select>
        </div>

        <div style={styles.fieldGroup}>
          <label style={styles.label}>Action</label>
          <div style={styles.actionGroup}>
            <button
              style={styles.buyBtn(action === 0)}
              onClick={() => setAction(0)}
              onMouseEnter={e => { if (action !== 0) e.target.style.background = 'rgba(0, 200, 83, 0.12)' }}
              onMouseLeave={e => { if (action !== 0) e.target.style.background = 'rgba(0, 200, 83, 0.05)' }}
            >
              BUY
            </button>
            <button
              style={styles.sellBtn(action === 1)}
              onClick={() => setAction(1)}
              onMouseEnter={e => { if (action !== 1) e.target.style.background = 'rgba(255, 82, 82, 0.12)' }}
              onMouseLeave={e => { if (action !== 1) e.target.style.background = 'rgba(255, 82, 82, 0.05)' }}
            >
              SELL
            </button>
          </div>
        </div>

        <div style={styles.fieldGroup}>
          <label style={styles.label}>Volume (lots)</label>
          <input
            style={styles.volumeInput}
            type="number"
            min="0.01"
            step="0.01"
            value={volume}
            onChange={e => setVolume(e.target.value)}
            onFocus={e => { e.target.style.borderColor = '#4a9eff' }}
            onBlur={e => { e.target.style.borderColor = '#2a3a4a' }}
          />
        </div>

        <div style={styles.footer}>
          <button
            style={styles.cancelBtn}
            onClick={onClose}
            onMouseEnter={e => { e.target.style.background = 'rgba(255,255,255,0.03)' }}
            onMouseLeave={e => { e.target.style.background = 'transparent' }}
          >
            Cancel
          </button>
          <button
            style={canSubmit ? styles.submitBtn : styles.submitBtnDisabled}
            onClick={handleSubmit}
            disabled={!canSubmit}
            onMouseEnter={e => { if (canSubmit) e.target.style.background = 'rgba(74, 158, 255, 0.35)' }}
            onMouseLeave={e => { if (canSubmit) e.target.style.background = 'rgba(74, 158, 255, 0.2)' }}
          >
            Open Position
          </button>
        </div>
      </div>
    </div>
  )
}
