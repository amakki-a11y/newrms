import { useState, useEffect, useRef, useCallback } from 'react'

function calcFromDate(range) {
  const now = new Date()
  switch (range) {
    case '1D':
      return new Date(now.getTime() - 24 * 60 * 60 * 1000).toISOString()
    case '7D':
      return new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000).toISOString()
    case '30D':
      return new Date(now.getTime() - 30 * 24 * 60 * 60 * 1000).toISOString()
    default:
      return new Date(now.getTime() - 24 * 60 * 60 * 1000).toISOString()
  }
}

function intervalForRange(range) {
  switch (range) {
    case '1D':
      return '1 hour'
    case '7D':
      return '6 hours'
    case '30D':
      return '1 day'
    default:
      return '1 hour'
  }
}

export default function useDashboardData() {
  const [summary, setSummary] = useState(null)
  const [exposure, setExposure] = useState([])
  const [topMovers, setTopMovers] = useState([])
  const [dealHistory, setDealHistory] = useState([])
  const [dealRange, setDealRangeState] = useState('1D')
  const [loading, setLoading] = useState(true)
  const [connected, setConnected] = useState(false)

  const wsRef = useRef(null)
  const reconnectTimer = useRef(null)
  const refreshTimer = useRef(null)
  const backoffRef = useRef(1000)
  const dealRangeRef = useRef(dealRange)
  const mountedRef = useRef(true)

  const sendMsg = useCallback((type, data) => {
    const ws = wsRef.current
    if (ws && ws.readyState === WebSocket.OPEN) {
      ws.send(JSON.stringify({ type, data }))
    }
  }, [])

  const requestDashboard = useCallback(() => {
    sendMsg('getDashboardData', {})
  }, [sendMsg])

  const requestDealHistory = useCallback((range) => {
    const from = calcFromDate(range)
    const to = new Date().toISOString()
    const interval = intervalForRange(range)
    sendMsg('getDealHistory', { from, to, interval })
  }, [sendMsg])

  const setDealRange = useCallback((range) => {
    setDealRangeState(range)
    dealRangeRef.current = range
    requestDealHistory(range)
  }, [requestDealHistory])

  useEffect(() => {
    mountedRef.current = true

    function connect() {
      if (!mountedRef.current) return

      const ws = new WebSocket('ws://localhost:8181')
      wsRef.current = ws

      ws.onopen = () => {
        if (!mountedRef.current) return
        setConnected(true)
        setLoading(true)
        backoffRef.current = 1000
        requestDashboard()
        requestDealHistory(dealRangeRef.current)

        // Auto-refresh every 10 seconds
        if (refreshTimer.current) clearInterval(refreshTimer.current)
        refreshTimer.current = setInterval(() => {
          requestDashboard()
        }, 10000)
      }

      ws.onmessage = (event) => {
        if (!mountedRef.current) return
        try {
          const msg = JSON.parse(event.data)

          if (msg.type === 'dashboardData' && msg.data) {
            const d = msg.data
            if (d.summary) setSummary(d.summary)
            if (d.exposure) setExposure(d.exposure)
            if (d.topMovers) setTopMovers(d.topMovers)
            if (d.dealHistory) setDealHistory(d.dealHistory)
            setLoading(false)
          }

          if (msg.type === 'dealHistory' && msg.data) {
            setDealHistory(msg.data)
          }
        } catch (_e) {
          // ignore parse errors
        }
      }

      ws.onclose = () => {
        if (!mountedRef.current) return
        setConnected(false)
        if (refreshTimer.current) clearInterval(refreshTimer.current)

        // Reconnect with exponential backoff
        const delay = backoffRef.current
        backoffRef.current = Math.min(backoffRef.current * 2, 30000)
        reconnectTimer.current = setTimeout(connect, delay)
      }

      ws.onerror = () => {
        // onclose will fire after onerror
      }
    }

    connect()

    return () => {
      mountedRef.current = false
      if (reconnectTimer.current) clearTimeout(reconnectTimer.current)
      if (refreshTimer.current) clearInterval(refreshTimer.current)
      if (wsRef.current) {
        wsRef.current.onclose = null
        wsRef.current.close()
      }
    }
  }, [requestDashboard, requestDealHistory])

  return {
    summary,
    exposure,
    topMovers,
    dealHistory,
    dealRange,
    setDealRange,
    loading,
    connected,
  }
}
