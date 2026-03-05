import { useState, useEffect, useRef, useCallback } from 'react'

const WS_URL = 'ws://localhost:8181'

const INTERVAL_DURATIONS_MS = {
  '1m': 60 * 1000,
  '5m': 5 * 60 * 1000,
  '15m': 15 * 60 * 1000,
  '1h': 60 * 60 * 1000,
  '4h': 4 * 60 * 60 * 1000,
  '1d': 24 * 60 * 60 * 1000,
}

export default function useChartData() {
  const [candles, setCandles] = useState([])
  const [symbol, setSymbolState] = useState('EURUSD')
  const [interval, setIntervalState] = useState('1h')
  const [loading, setLoading] = useState(false)

  const wsRef = useRef(null)
  const reconnectTimeoutRef = useRef(null)
  const reconnectAttemptsRef = useRef(0)
  const symbolRef = useRef(symbol)
  const intervalRef = useRef(interval)
  const candlesRef = useRef(candles)

  // Keep refs in sync
  useEffect(() => { symbolRef.current = symbol }, [symbol])
  useEffect(() => { intervalRef.current = interval }, [interval])
  useEffect(() => { candlesRef.current = candles }, [candles])

  const requestHistory = useCallback((ws, sym, intv) => {
    if (!ws || ws.readyState !== WebSocket.OPEN) return
    setLoading(true)
    const now = new Date()
    const from = new Date(now.getTime() - 30 * 24 * 60 * 60 * 1000) // 30 days back
    ws.send(JSON.stringify({
      type: 'getTickHistory',
      data: {
        symbol: sym,
        interval: intv,
        from: from.toISOString(),
        to: now.toISOString(),
        limit: 500
      }
    }))
  }, [])

  const connect = useCallback(() => {
    if (wsRef.current && (wsRef.current.readyState === WebSocket.OPEN || wsRef.current.readyState === WebSocket.CONNECTING)) {
      return
    }

    const ws = new WebSocket(WS_URL)
    wsRef.current = ws

    ws.onopen = () => {
      reconnectAttemptsRef.current = 0
      requestHistory(ws, symbolRef.current, intervalRef.current)
    }

    ws.onmessage = (event) => {
      try {
        const msg = JSON.parse(event.data)

        if (msg.type === 'tickHistory') {
          const data = Array.isArray(msg.data) ? msg.data : []
          const sorted = data
            .map(c => ({
              time: typeof c.time === 'number' ? c.time : Math.floor(new Date(c.time).getTime() / 1000),
              open: Number(c.open),
              high: Number(c.high),
              low: Number(c.low),
              close: Number(c.close),
              volume: Number(c.volume || 0),
            }))
            .sort((a, b) => a.time - b.time)
          setCandles(sorted)
          setLoading(false)
        }

        if (msg.type === 'tick' && msg.data) {
          const tick = msg.data
          if (tick.symbol !== symbolRef.current) return

          const price = (Number(tick.bid) + Number(tick.ask)) / 2
          const tickTimeMs = typeof tick.time === 'number'
            ? (tick.time > 1e12 ? tick.time : tick.time * 1000)
            : new Date(tick.time || Date.now()).getTime()

          const intervalMs = INTERVAL_DURATIONS_MS[intervalRef.current] || 3600000

          setCandles(prev => {
            if (prev.length === 0) return prev
            const last = prev[prev.length - 1]
            const lastTimeMs = last.time * 1000
            const candleEndMs = lastTimeMs + intervalMs

            if (tickTimeMs < candleEndMs) {
              // Update existing candle
              const updated = {
                ...last,
                high: Math.max(last.high, price),
                low: Math.min(last.low, price),
                close: price,
                volume: last.volume + 1,
              }
              return [...prev.slice(0, -1), updated]
            } else {
              // Create new candle
              const newCandleTime = Math.floor(tickTimeMs / intervalMs) * (intervalMs / 1000)
              const newCandle = {
                time: newCandleTime,
                open: price,
                high: price,
                low: price,
                close: price,
                volume: 1,
              }
              return [...prev, newCandle]
            }
          })
        }
      } catch (e) {
        // Ignore malformed messages
      }
    }

    ws.onclose = () => {
      scheduleReconnect()
    }

    ws.onerror = () => {
      ws.close()
    }
  }, [requestHistory])

  const scheduleReconnect = useCallback(() => {
    const attempts = reconnectAttemptsRef.current
    const delay = Math.min(1000 * Math.pow(2, attempts), 30000)
    reconnectAttemptsRef.current = attempts + 1

    if (reconnectTimeoutRef.current) {
      clearTimeout(reconnectTimeoutRef.current)
    }

    reconnectTimeoutRef.current = setTimeout(() => {
      connect()
    }, delay)
  }, [connect])

  // Connect on mount
  useEffect(() => {
    connect()

    return () => {
      if (reconnectTimeoutRef.current) {
        clearTimeout(reconnectTimeoutRef.current)
      }
      if (wsRef.current) {
        wsRef.current.onclose = null // prevent reconnect on intentional close
        wsRef.current.close()
        wsRef.current = null
      }
    }
  }, [connect])

  // Request new history when symbol or interval changes
  useEffect(() => {
    if (wsRef.current && wsRef.current.readyState === WebSocket.OPEN) {
      requestHistory(wsRef.current, symbol, interval)
    }
  }, [symbol, interval, requestHistory])

  const setSymbol = useCallback((s) => {
    setCandles([])
    setSymbolState(s)
  }, [])

  const setInterval = useCallback((i) => {
    setCandles([])
    setIntervalState(i)
  }, [])

  return { candles, symbol, interval, setSymbol, setInterval, loading }
}
