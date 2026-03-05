import { useState, useRef, useEffect, useCallback } from 'react'

const QUICK_SUGGESTIONS = [
  'Account Summary',
  'Risk Analysis',
  'Losing Positions',
  'Margin Status',
]

const styles = {
  wrapper: {
    display: 'flex',
    flexDirection: 'column',
    height: '100%',
    background: '#0d1520',
    fontFamily: "'Inter', sans-serif",
    minWidth: '320px',
  },
  header: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    padding: '12px 16px',
    borderBottom: '1px solid #2a3a4a',
    flexShrink: 0,
  },
  headerLeft: {
    display: 'flex',
    alignItems: 'center',
    gap: '8px',
  },
  headerTitle: {
    fontSize: '14px',
    fontWeight: 600,
    color: '#e1e8ed',
    margin: 0,
  },
  statusDot: (isConnected) => ({
    width: '8px',
    height: '8px',
    borderRadius: '50%',
    background: isConnected ? '#00c853' : '#ff5252',
    flexShrink: 0,
  }),
  closeButton: {
    background: 'none',
    border: 'none',
    color: '#5a7a8a',
    fontSize: '16px',
    cursor: 'pointer',
    padding: '4px 8px',
    borderRadius: '4px',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    transition: 'color 0.2s, background 0.2s',
  },
  messageList: {
    flex: 1,
    overflowY: 'auto',
    overflowX: 'hidden',
    padding: '12px 12px 4px 12px',
    display: 'flex',
    flexDirection: 'column',
    gap: '8px',
  },
  messageBubbleUser: {
    alignSelf: 'flex-end',
    maxWidth: '85%',
    background: '#4a9eff',
    color: '#fff',
    borderRadius: '12px 12px 4px 12px',
    padding: '8px 12px',
    fontSize: '13px',
    lineHeight: '1.45',
    wordBreak: 'break-word',
  },
  messageBubbleAssistant: {
    alignSelf: 'flex-start',
    maxWidth: '85%',
    background: '#162230',
    color: '#e1e8ed',
    borderRadius: '12px 12px 12px 4px',
    padding: '8px 12px',
    fontSize: '13px',
    lineHeight: '1.45',
    wordBreak: 'break-word',
  },
  timestamp: {
    fontSize: '10px',
    color: '#5a7a8a',
    marginTop: '2px',
    fontFamily: "'JetBrains Mono', monospace",
  },
  timestampUser: {
    textAlign: 'right',
  },
  timestampAssistant: {
    textAlign: 'left',
  },
  emptyState: {
    flex: 1,
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    justifyContent: 'center',
    gap: '12px',
    color: '#5a7a8a',
    padding: '40px 20px',
    textAlign: 'center',
  },
  emptyIcon: {
    fontSize: '36px',
    opacity: 0.4,
  },
  emptyText: {
    fontSize: '13px',
    lineHeight: '1.5',
    margin: 0,
  },
  // Action cards
  actionCard: {
    background: 'linear-gradient(135deg, #111d28, #162230)',
    border: '1px solid #2a3a4a',
    borderRadius: '8px',
    padding: '10px 12px',
    marginTop: '6px',
    maxWidth: '85%',
    alignSelf: 'flex-start',
  },
  actionName: {
    fontSize: '12px',
    fontWeight: 600,
    color: '#4a9eff',
    marginBottom: '4px',
    textTransform: 'uppercase',
    letterSpacing: '0.5px',
  },
  actionParams: {
    fontSize: '11px',
    color: '#5a7a8a',
    fontFamily: "'JetBrains Mono', monospace",
    marginBottom: '8px',
    lineHeight: '1.4',
    wordBreak: 'break-all',
  },
  actionButtons: {
    display: 'flex',
    gap: '8px',
  },
  confirmBtn: {
    padding: '4px 14px',
    fontSize: '11px',
    fontWeight: 600,
    border: 'none',
    borderRadius: '4px',
    cursor: 'pointer',
    background: '#00c853',
    color: '#fff',
    transition: 'opacity 0.2s',
  },
  cancelBtn: {
    padding: '4px 14px',
    fontSize: '11px',
    fontWeight: 600,
    border: 'none',
    borderRadius: '4px',
    cursor: 'pointer',
    background: '#ff5252',
    color: '#fff',
    transition: 'opacity 0.2s',
  },
  actionStatusBadge: (status) => ({
    display: 'inline-block',
    padding: '2px 10px',
    fontSize: '11px',
    fontWeight: 600,
    borderRadius: '4px',
    color: '#fff',
    background: status === 'confirmed' ? '#00c853' : '#5a7a8a',
  }),
  // Typing indicator
  typingDots: {
    display: 'flex',
    gap: '4px',
    padding: '4px 0',
    alignItems: 'center',
  },
  dot: {
    width: '6px',
    height: '6px',
    borderRadius: '50%',
    background: '#5a7a8a',
  },
  // Suggestions
  suggestionsRow: {
    display: 'flex',
    gap: '6px',
    padding: '8px 12px 4px 12px',
    overflowX: 'auto',
    flexShrink: 0,
    scrollbarWidth: 'none',
  },
  suggestionChip: {
    flexShrink: 0,
    padding: '5px 12px',
    fontSize: '11px',
    fontWeight: 500,
    color: '#4a9eff',
    background: 'rgba(74, 158, 255, 0.08)',
    border: '1px solid rgba(74, 158, 255, 0.25)',
    borderRadius: '14px',
    cursor: 'pointer',
    whiteSpace: 'nowrap',
    transition: 'background 0.2s, border-color 0.2s',
    fontFamily: "'Inter', sans-serif",
  },
  // Input area
  inputArea: {
    display: 'flex',
    alignItems: 'center',
    gap: '8px',
    padding: '8px 12px 12px 12px',
    borderTop: '1px solid #2a3a4a',
    flexShrink: 0,
  },
  textInput: {
    flex: 1,
    background: '#111d28',
    border: '1px solid #2a3a4a',
    borderRadius: '8px',
    padding: '8px 12px',
    fontSize: '13px',
    color: '#e1e8ed',
    outline: 'none',
    fontFamily: "'Inter', sans-serif",
    transition: 'border-color 0.2s',
    resize: 'none',
    lineHeight: '1.4',
    maxHeight: '80px',
    minHeight: '36px',
  },
  sendButton: (disabled) => ({
    width: '36px',
    height: '36px',
    borderRadius: '8px',
    border: 'none',
    background: disabled ? '#1a2a3a' : 'linear-gradient(135deg, #4a9eff, #2979ff)',
    color: disabled ? '#5a7a8a' : '#fff',
    fontSize: '16px',
    cursor: disabled ? 'default' : 'pointer',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    flexShrink: 0,
    transition: 'background 0.2s, opacity 0.2s',
  }),
}

// Keyframe animation for typing dots (injected once)
const TYPING_STYLE_ID = 'chat-typing-dots-style'
function ensureTypingAnimation() {
  if (typeof document === 'undefined') return
  if (document.getElementById(TYPING_STYLE_ID)) return
  const style = document.createElement('style')
  style.id = TYPING_STYLE_ID
  style.textContent = `
    @keyframes chatTypingBounce {
      0%, 60%, 100% { transform: translateY(0); opacity: 0.4; }
      30% { transform: translateY(-4px); opacity: 1; }
    }
    .chat-sidebar-suggestions::-webkit-scrollbar { display: none; }
    .chat-sidebar-msglist::-webkit-scrollbar { width: 4px; }
    .chat-sidebar-msglist::-webkit-scrollbar-track { background: transparent; }
    .chat-sidebar-msglist::-webkit-scrollbar-thumb { background: #2a3a4a; border-radius: 2px; }
  `
  document.head.appendChild(style)
}

function formatTime(ts) {
  if (!ts) return ''
  const d = new Date(ts)
  const h = d.getHours().toString().padStart(2, '0')
  const m = d.getMinutes().toString().padStart(2, '0')
  return `${h}:${m}`
}

function formatActionParams(params) {
  if (!params || typeof params !== 'object') return ''
  return Object.entries(params)
    .map(([k, v]) => `${k}: ${v}`)
    .join(', ')
}

function TypingDots() {
  return (
    <div style={styles.typingDots}>
      {[0, 1, 2].map((i) => (
        <div
          key={i}
          style={{
            ...styles.dot,
            animation: 'chatTypingBounce 1.2s infinite',
            animationDelay: `${i * 0.2}s`,
          }}
        />
      ))}
    </div>
  )
}

function ActionCard({ action, index, messageId, onConfirmAction }) {
  const isPending = !action.status || action.status === 'pending'

  return (
    <div style={styles.actionCard}>
      <div style={styles.actionName}>{(action.action || '').replace(/_/g, ' ')}</div>
      {action.params && Object.keys(action.params).length > 0 && (
        <div style={styles.actionParams}>{formatActionParams(action.params)}</div>
      )}
      {action.requireConfirm && isPending && (
        <div style={styles.actionButtons}>
          <button
            style={styles.confirmBtn}
            onClick={() => onConfirmAction && onConfirmAction(messageId, index, true)}
            onMouseEnter={(e) => { e.currentTarget.style.opacity = '0.8' }}
            onMouseLeave={(e) => { e.currentTarget.style.opacity = '1' }}
          >
            Confirm
          </button>
          <button
            style={styles.cancelBtn}
            onClick={() => onConfirmAction && onConfirmAction(messageId, index, false)}
            onMouseEnter={(e) => { e.currentTarget.style.opacity = '0.8' }}
            onMouseLeave={(e) => { e.currentTarget.style.opacity = '1' }}
          >
            Cancel
          </button>
        </div>
      )}
      {!isPending && (
        <div style={styles.actionStatusBadge(action.status)}>
          {action.status === 'confirmed' ? 'Confirmed' : 'Cancelled'}
        </div>
      )}
    </div>
  )
}

// Send arrow SVG icon
function SendIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <line x1="22" y1="2" x2="11" y2="13" />
      <polygon points="22 2 15 22 11 13 2 9 22 2" />
    </svg>
  )
}

export default function ChatSidebar({
  messages = [],
  onSend,
  onConfirmAction,
  connected = false,
  streaming = false,
  isOpen,
  onToggle,
}) {
  const [inputValue, setInputValue] = useState('')
  const messageListRef = useRef(null)
  const inputRef = useRef(null)

  useEffect(() => {
    ensureTypingAnimation()
  }, [])

  // Auto-scroll to bottom on new messages
  useEffect(() => {
    const el = messageListRef.current
    if (el) {
      el.scrollTop = el.scrollHeight
    }
  }, [messages])

  // Focus input when sidebar opens
  useEffect(() => {
    if (isOpen && inputRef.current) {
      setTimeout(() => inputRef.current?.focus(), 350)
    }
  }, [isOpen])

  // Detect streaming: last message is assistant with empty content or no chatDone received
  const isStreaming = (() => {
    if (streaming) return true
    if (messages.length === 0) return false
    const last = messages[messages.length - 1]
    return last.role === 'assistant' && last.content === '' && !last._done
  })()

  const handleSend = useCallback(() => {
    const text = inputValue.trim()
    if (!text || !connected || isStreaming) return
    if (onSend) onSend(text)
    setInputValue('')
  }, [inputValue, connected, isStreaming, onSend])

  const handleKeyDown = useCallback(
    (e) => {
      if (e.key === 'Enter' && !e.shiftKey) {
        e.preventDefault()
        handleSend()
      }
    },
    [handleSend]
  )

  const handleSuggestion = useCallback(
    (text) => {
      if (!connected || isStreaming) return
      if (onSend) onSend(text)
    },
    [connected, isStreaming, onSend]
  )

  const handleActionConfirm = useCallback(
    (messageId, actionIndex, isConfirm) => {
      if (onConfirmAction) {
        onConfirmAction(messageId, actionIndex, isConfirm)
      }
    },
    [onConfirmAction]
  )

  const inputDisabled = !connected || isStreaming

  return (
    <div style={styles.wrapper}>
      {/* Header */}
      <div style={styles.header}>
        <div style={styles.headerLeft}>
          <div style={styles.statusDot(connected)} />
          <h3 style={styles.headerTitle}>AI Assistant</h3>
        </div>
        <button
          style={styles.closeButton}
          onClick={onToggle}
          onMouseEnter={(e) => {
            e.currentTarget.style.color = '#e1e8ed'
            e.currentTarget.style.background = 'rgba(255,255,255,0.06)'
          }}
          onMouseLeave={(e) => {
            e.currentTarget.style.color = '#5a7a8a'
            e.currentTarget.style.background = 'none'
          }}
          title="Close chat"
        >
          {'\u2715'}
        </button>
      </div>

      {/* Messages */}
      <div
        ref={messageListRef}
        style={styles.messageList}
        className="chat-sidebar-msglist"
      >
        {messages.length === 0 && !isStreaming && (
          <div style={styles.emptyState}>
            <div style={styles.emptyIcon}>{'\uD83E\uDD16'}</div>
            <p style={styles.emptyText}>
              Ask me about risk, positions,<br />accounts, or market data.
            </p>
          </div>
        )}

        {messages.map((msg) => (
          <div key={msg.id} style={{ display: 'flex', flexDirection: 'column' }}>
            <div
              style={
                msg.role === 'user'
                  ? styles.messageBubbleUser
                  : styles.messageBubbleAssistant
              }
            >
              {/* Content */}
              {msg.content || (msg.role === 'assistant' && isStreaming && msg === messages[messages.length - 1] ? (
                <TypingDots />
              ) : null)}

              {/* Show typing dots after content if still streaming this message */}
              {msg.role === 'assistant' &&
                msg.content &&
                isStreaming &&
                msg === messages[messages.length - 1] && (
                  <TypingDots />
                )}
            </div>

            {/* Timestamp */}
            <div
              style={{
                ...styles.timestamp,
                ...(msg.role === 'user'
                  ? styles.timestampUser
                  : styles.timestampAssistant),
              }}
            >
              {formatTime(msg.timestamp)}
            </div>

            {/* Action cards */}
            {msg.actions &&
              msg.actions.length > 0 &&
              msg.actions.map((action, idx) => (
                <ActionCard
                  key={idx}
                  action={action}
                  index={idx}
                  messageId={msg.id}
                  onConfirmAction={handleActionConfirm}
                />
              ))}
          </div>
        ))}
      </div>

      {/* Quick suggestions */}
      <div style={styles.suggestionsRow} className="chat-sidebar-suggestions">
        {QUICK_SUGGESTIONS.map((text) => (
          <button
            key={text}
            style={styles.suggestionChip}
            onClick={() => handleSuggestion(text)}
            onMouseEnter={(e) => {
              e.currentTarget.style.background = 'rgba(74, 158, 255, 0.15)'
              e.currentTarget.style.borderColor = 'rgba(74, 158, 255, 0.5)'
            }}
            onMouseLeave={(e) => {
              e.currentTarget.style.background = 'rgba(74, 158, 255, 0.08)'
              e.currentTarget.style.borderColor = 'rgba(74, 158, 255, 0.25)'
            }}
            disabled={inputDisabled}
          >
            {text}
          </button>
        ))}
      </div>

      {/* Input area */}
      <div style={styles.inputArea}>
        <textarea
          ref={inputRef}
          rows={1}
          style={{
            ...styles.textInput,
            opacity: inputDisabled ? 0.5 : 1,
          }}
          value={inputValue}
          onChange={(e) => setInputValue(e.target.value)}
          onKeyDown={handleKeyDown}
          onFocus={(e) => { e.currentTarget.style.borderColor = '#4a9eff' }}
          onBlur={(e) => { e.currentTarget.style.borderColor = '#2a3a4a' }}
          placeholder={connected ? 'Ask about risk, positions...' : 'Disconnected...'}
          disabled={inputDisabled}
        />
        <button
          style={styles.sendButton(inputDisabled || !inputValue.trim())}
          onClick={handleSend}
          disabled={inputDisabled || !inputValue.trim()}
          title="Send message"
        >
          <SendIcon />
        </button>
      </div>
    </div>
  )
}
