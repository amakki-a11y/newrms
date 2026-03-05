import { useState, useEffect, useCallback } from 'react'
import { onToast } from '../utils/toast'

const typeConfig = {
  success: { color: '#00c853', icon: '\u2713', bg: 'rgba(0, 200, 83, 0.1)', border: 'rgba(0, 200, 83, 0.3)' },
  error: { color: '#ff5252', icon: '\u2717', bg: 'rgba(255, 82, 82, 0.1)', border: 'rgba(255, 82, 82, 0.3)' },
  warning: { color: '#ffab40', icon: '\u26A0', bg: 'rgba(255, 171, 64, 0.1)', border: 'rgba(255, 171, 64, 0.3)' },
  info: { color: '#4a9eff', icon: '\u2139', bg: 'rgba(74, 158, 255, 0.1)', border: 'rgba(74, 158, 255, 0.3)' },
}

const styles = {
  container: {
    position: 'fixed',
    top: '60px',
    right: '16px',
    zIndex: 9999,
    display: 'flex',
    flexDirection: 'column',
    gap: '8px',
    pointerEvents: 'none',
  },
  toast: (type, entering) => {
    const cfg = typeConfig[type] || typeConfig.info
    return {
      display: 'flex',
      alignItems: 'flex-start',
      gap: '10px',
      padding: '12px 16px',
      borderRadius: '8px',
      background: cfg.bg,
      border: `1px solid ${cfg.border}`,
      backdropFilter: 'blur(10px)',
      minWidth: '280px',
      maxWidth: '380px',
      boxShadow: '0 4px 20px rgba(0, 0, 0, 0.4)',
      transform: entering ? 'translateX(0)' : 'translateX(120%)',
      opacity: entering ? 1 : 0,
      transition: 'transform 0.3s ease, opacity 0.3s ease',
      pointerEvents: 'auto',
    }
  },
  iconCircle: (type) => {
    const cfg = typeConfig[type] || typeConfig.info
    return {
      width: '24px',
      height: '24px',
      borderRadius: '50%',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      background: cfg.color,
      color: '#fff',
      fontSize: '12px',
      fontWeight: 700,
      flexShrink: 0,
    }
  },
  content: {
    flex: 1,
    minWidth: 0,
  },
  title: {
    fontSize: '13px',
    fontWeight: 600,
    fontFamily: "'Inter', sans-serif",
    color: '#e1e8ed',
    marginBottom: '2px',
    lineHeight: 1.3,
  },
  message: {
    fontSize: '12px',
    fontFamily: "'Inter', sans-serif",
    color: '#5a7a8a',
    lineHeight: 1.4,
    wordBreak: 'break-word',
  },
  closeBtn: {
    background: 'none',
    border: 'none',
    color: '#5a7a8a',
    cursor: 'pointer',
    fontSize: '14px',
    padding: '0 0 0 8px',
    lineHeight: 1,
    flexShrink: 0,
  },
}

export default function ToastContainer() {
  const [toasts, setToasts] = useState([])

  const removeToast = useCallback((id) => {
    setToasts(prev => prev.map(t =>
      t.id === id ? { ...t, entering: false } : t
    ))
    // Remove from DOM after animation
    setTimeout(() => {
      setToasts(prev => prev.filter(t => t.id !== id))
    }, 350)
  }, [])

  useEffect(() => {
    const unsub = onToast((toast) => {
      const newToast = { ...toast, entering: false }
      setToasts(prev => [...prev, newToast])

      // Trigger enter animation on next frame
      requestAnimationFrame(() => {
        requestAnimationFrame(() => {
          setToasts(prev => prev.map(t =>
            t.id === toast.id ? { ...t, entering: true } : t
          ))
        })
      })

      // Auto-dismiss
      const dur = toast.duration || 3000
      setTimeout(() => {
        removeToast(toast.id)
      }, dur)
    })

    return unsub
  }, [removeToast])

  if (toasts.length === 0) return null

  return (
    <div style={styles.container}>
      {toasts.map(t => {
        const cfg = typeConfig[t.type] || typeConfig.info
        return (
          <div key={t.id} style={styles.toast(t.type, t.entering)}>
            <div style={styles.iconCircle(t.type)}>
              {cfg.icon}
            </div>
            <div style={styles.content}>
              {t.title && <div style={styles.title}>{t.title}</div>}
              {t.message && <div style={styles.message}>{t.message}</div>}
            </div>
            <button
              style={styles.closeBtn}
              onClick={() => removeToast(t.id)}
              onMouseEnter={(e) => { e.target.style.color = '#e1e8ed' }}
              onMouseLeave={(e) => { e.target.style.color = '#5a7a8a' }}
            >
              {'\u2715'}
            </button>
          </div>
        )
      })}
    </div>
  )
}
