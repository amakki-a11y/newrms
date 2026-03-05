import { useState, useEffect, useRef, useCallback } from 'react'
import { showToast } from '../utils/toast'

export default function useAccountData() {
  const [accounts, setAccounts] = useState([])
  const [positions, setPositions] = useState({})
  const [summary, setSummary] = useState(null)
  const [page, setPageState] = useState(1)
  const [totalPages, setTotalPages] = useState(1)
  const [search, setSearchState] = useState('')
  const [loadingTickets, setLoadingTickets] = useState(new Set())
  const [connected, setConnected] = useState(false)

  const wsRef = useRef(null)
  const reconnectTimeoutRef = useRef(null)
  const refreshIntervalRef = useRef(null)
  const backoffRef = useRef(1000)
  const mountedRef = useRef(true)
  const pageRef = useRef(1)
  const searchRef = useRef('')
  const pageSizeRef = useRef(50)

  const sendMessage = useCallback((msg) => {
    if (wsRef.current && wsRef.current.readyState === WebSocket.OPEN) {
      try {
        wsRef.current.send(JSON.stringify(msg))
      } catch (_e) {
        // ignore send errors
      }
    }
  }, [])

  const requestAccounts = useCallback((p, s) => {
    sendMessage({
      type: 'getAccounts',
      data: { page: p, pageSize: pageSizeRef.current, search: s || '' }
    })
  }, [sendMessage])

  const startRefreshInterval = useCallback(() => {
    if (refreshIntervalRef.current) {
      clearInterval(refreshIntervalRef.current)
    }
    refreshIntervalRef.current = setInterval(() => {
      if (mountedRef.current) {
        requestAccounts(pageRef.current, searchRef.current)
      }
    }, 30000)
  }, [requestAccounts])

  const connectWebSocket = useCallback(() => {
    if (!mountedRef.current) return

    try {
      const ws = new WebSocket('ws://localhost:8181')
      wsRef.current = ws

      ws.onopen = () => {
        if (!mountedRef.current) return
        setConnected(true)
        backoffRef.current = 1000
        requestAccounts(pageRef.current, searchRef.current)
        startRefreshInterval()
      }

      ws.onmessage = (event) => {
        if (!mountedRef.current) return

        try {
          const msg = JSON.parse(event.data)

          if (msg.type === 'pong') return

          if (msg.type === 'accounts' && msg.data) {
            const d = msg.data
            setAccounts(d.accounts || [])
            setTotalPages(Math.max(1, Math.ceil((d.totalCount || 0) / (d.pageSize || 50))))
            setPageState(d.page || 1)
            pageRef.current = d.page || 1
            if (d.summary) {
              setSummary(d.summary)
            }
          }

          if (msg.type === 'positions' && msg.data) {
            const posArr = Array.isArray(msg.data) ? msg.data : []
            if (posArr.length > 0 && posArr[0] && posArr[0].login != null) {
              setPositions(prev => ({
                ...prev,
                [posArr[0].login]: posArr
              }))
            }
          }

          if (msg.type === 'accountUpdate' && msg.data) {
            const updated = msg.data
            setAccounts(prev =>
              prev.map(a => (a.login === updated.login ? { ...a, ...updated } : a))
            )
          }

          if (msg.type === 'dealEvent' && msg.data) {
            // Deal events can trigger a position refresh for the affected login
            const deal = msg.data
            if (deal.login) {
              sendMessage({ type: 'getPositions', data: { login: deal.login } })
            }
          }

          if (msg.type === 'closePositionResult' && msg.data) {
            const result = msg.data
            setLoadingTickets(prev => {
              const next = new Set(prev)
              next.delete(result.ticket)
              return next
            })
            showToast({
              type: result.success ? 'success' : 'error',
              title: result.success ? 'Position Closed' : 'Close Failed',
              message: result.message || (result.success ? `Ticket #${result.ticket} closed` : 'Failed to close position'),
              duration: 4000
            })
          }

          if (msg.type === 'openPositionResult' && msg.data) {
            const result = msg.data
            showToast({
              type: result.success ? 'success' : 'error',
              title: result.success ? 'Position Opened' : 'Open Failed',
              message: result.message || (result.success ? `Ticket #${result.ticket} opened` : 'Failed to open position'),
              duration: 4000
            })
          }

          if (msg.type === 'modifyPositionResult' && msg.data) {
            const result = msg.data
            setLoadingTickets(prev => {
              const next = new Set(prev)
              next.delete(result.ticket)
              return next
            })
            showToast({
              type: result.success ? 'success' : 'error',
              title: result.success ? 'Position Modified' : 'Modify Failed',
              message: result.message || (result.success ? `Ticket #${result.ticket} modified` : 'Failed to modify position'),
              duration: 4000
            })
          }
        } catch (_e) {
          // ignore parse errors
        }
      }

      ws.onclose = () => {
        if (!mountedRef.current) return
        setConnected(false)
        if (refreshIntervalRef.current) {
          clearInterval(refreshIntervalRef.current)
          refreshIntervalRef.current = null
        }
        const delay = backoffRef.current
        backoffRef.current = Math.min(backoffRef.current * 2, 30000)
        reconnectTimeoutRef.current = setTimeout(() => {
          connectWebSocket()
        }, delay)
      }

      ws.onerror = () => {
        // onclose will fire after onerror, triggering reconnect
      }
    } catch (_e) {
      const delay = backoffRef.current
      backoffRef.current = Math.min(backoffRef.current * 2, 30000)
      reconnectTimeoutRef.current = setTimeout(() => {
        connectWebSocket()
      }, delay)
    }
  }, [requestAccounts, sendMessage, startRefreshInterval])

  useEffect(() => {
    mountedRef.current = true
    connectWebSocket()

    return () => {
      mountedRef.current = false

      if (reconnectTimeoutRef.current) {
        clearTimeout(reconnectTimeoutRef.current)
        reconnectTimeoutRef.current = null
      }
      if (refreshIntervalRef.current) {
        clearInterval(refreshIntervalRef.current)
        refreshIntervalRef.current = null
      }
      if (wsRef.current) {
        wsRef.current.onclose = null
        wsRef.current.onerror = null
        wsRef.current.onmessage = null
        wsRef.current.close()
        wsRef.current = null
      }
    }
  }, [connectWebSocket])

  const setPage = useCallback((n) => {
    pageRef.current = n
    setPageState(n)
    requestAccounts(n, searchRef.current)
  }, [requestAccounts])

  const setSearch = useCallback((s) => {
    searchRef.current = s
    setSearchState(s)
    pageRef.current = 1
    setPageState(1)
    requestAccounts(1, s)
  }, [requestAccounts])

  const getPositions = useCallback((login) => {
    sendMessage({ type: 'getPositions', data: { login } })
  }, [sendMessage])

  const closePosition = useCallback((login, ticket, symbol, action, volume) => {
    setLoadingTickets(prev => {
      const next = new Set(prev)
      next.add(ticket)
      return next
    })
    sendMessage({
      type: 'closePosition',
      data: { login, ticket, symbol, action, volume }
    })
  }, [sendMessage])

  const openPosition = useCallback((login, symbol, action, volume) => {
    sendMessage({
      type: 'openPosition',
      data: { login, symbol, action, volume }
    })
  }, [sendMessage])

  const modifyPosition = useCallback((login, ticket, symbol, sl, tp) => {
    setLoadingTickets(prev => {
      const next = new Set(prev)
      next.add(ticket)
      return next
    })
    sendMessage({
      type: 'modifyPosition',
      data: { login, ticket, symbol, sl, tp }
    })
  }, [sendMessage])

  return {
    accounts,
    positions,
    summary,
    page,
    totalPages,
    search,
    setPage,
    setSearch,
    getPositions,
    closePosition,
    openPosition,
    modifyPosition,
    loadingTickets,
    connected
  }
}
