import { useState, useEffect, useRef, useCallback } from 'react'

export default function useMarketData() {
  const [ticks, setTicks] = useState({})
  const [symbols, setSymbols] = useState([])
  const [symbolInfo, setSymbolInfo] = useState({})
  const [connected, setConnected] = useState(false)
  const [tickCount, setTickCount] = useState(0)

  const wsRef = useRef(null)
  const reconnectTimeoutRef = useRef(null)
  const pingIntervalRef = useRef(null)
  const backoffRef = useRef(1000)
  const mountedRef = useRef(true)

  const startPingInterval = useCallback((ws) => {
    if (pingIntervalRef.current) {
      clearInterval(pingIntervalRef.current)
    }
    pingIntervalRef.current = setInterval(() => {
      if (ws && ws.readyState === WebSocket.OPEN) {
        try {
          ws.send(JSON.stringify({ type: 'ping' }))
        } catch (_e) {
          // ignore send errors
        }
      }
    }, 15000)
  }, [])

  const connectWebSocket = useCallback(() => {
    if (!mountedRef.current) return

    try {
      const ws = new WebSocket('ws://localhost:8181')
      wsRef.current = ws

      ws.onopen = () => {
        if (!mountedRef.current) return
        setConnected(true)
        backoffRef.current = 1000
        startPingInterval(ws)
      }

      ws.onmessage = (event) => {
        if (!mountedRef.current) return

        try {
          const msg = JSON.parse(event.data)

          if (msg.type === 'pong') return

          if (msg.type === 'symbols' && Array.isArray(msg.data)) {
            const infoMap = {}
            const names = []
            for (const sym of msg.data) {
              if (sym.symbol) {
                names.push(sym.symbol)
                infoMap[sym.symbol] = sym
              }
            }
            setSymbols(names)
            setSymbolInfo(infoMap)
          }

          if (msg.type === 'snapshot' && Array.isArray(msg.data)) {
            const initialTicks = {}
            for (const tick of msg.data) {
              if (tick.symbol) {
                initialTicks[tick.symbol] = { ...tick, prevBid: tick.bid, prevAsk: tick.ask }
              }
            }
            setTicks(initialTicks)
            // Add any snapshot symbols not already in the list
            setSymbols((prev) => {
              const existing = new Set(prev)
              let changed = false
              const next = [...prev]
              for (const tick of msg.data) {
                if (tick.symbol && !existing.has(tick.symbol)) {
                  next.push(tick.symbol)
                  changed = true
                }
              }
              return changed ? next : prev
            })
          }

          if (msg.type === 'tick' && msg.data && msg.data.symbol) {
            const tick = msg.data
            setTicks((prev) => {
              const prevTick = prev[tick.symbol]
              return {
                ...prev,
                [tick.symbol]: {
                  ...tick,
                  prevBid: prevTick ? prevTick.bid : tick.bid,
                  prevAsk: prevTick ? prevTick.ask : tick.ask,
                },
              }
            })
            setTickCount((c) => c + 1)
          }
        } catch (_e) {
          // ignore parse errors
        }
      }

      ws.onclose = () => {
        if (!mountedRef.current) return
        setConnected(false)
        if (pingIntervalRef.current) {
          clearInterval(pingIntervalRef.current)
          pingIntervalRef.current = null
        }
        // Reconnect with exponential backoff
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
      // If WebSocket constructor fails, schedule reconnect
      const delay = backoffRef.current
      backoffRef.current = Math.min(backoffRef.current * 2, 30000)
      reconnectTimeoutRef.current = setTimeout(() => {
        connectWebSocket()
      }, delay)
    }
  }, [startPingInterval])

  useEffect(() => {
    mountedRef.current = true
    connectWebSocket()

    return () => {
      mountedRef.current = false

      if (reconnectTimeoutRef.current) {
        clearTimeout(reconnectTimeoutRef.current)
        reconnectTimeoutRef.current = null
      }
      if (pingIntervalRef.current) {
        clearInterval(pingIntervalRef.current)
        pingIntervalRef.current = null
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

  return { symbols, symbolInfo, ticks, connected, tickCount }
}
