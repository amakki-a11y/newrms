import { useState, useEffect, useRef, useCallback } from 'react'

let nextMsgId = 1
function genId() {
  return 'msg_' + (nextMsgId++)
}

export default function useChatData() {
  const [messages, setMessages] = useState([])
  const [connected, setConnected] = useState(false)
  const [streaming, setStreaming] = useState(false)

  const wsRef = useRef(null)
  const reconnectTimeoutRef = useRef(null)
  const backoffRef = useRef(1000)
  const mountedRef = useRef(true)
  const currentResponseRef = useRef('')
  const currentAssistantIdRef = useRef(null)

  const connectWebSocket = useCallback(() => {
    if (!mountedRef.current) return

    try {
      const ws = new WebSocket('ws://localhost:8181')
      wsRef.current = ws

      ws.onopen = () => {
        if (!mountedRef.current) return
        setConnected(true)
        backoffRef.current = 1000
      }

      ws.onmessage = (event) => {
        if (!mountedRef.current) return

        try {
          const msg = JSON.parse(event.data)

          if (msg.type === 'pong') return

          if (msg.type === 'chatChunk' && msg.data) {
            const chunkText = msg.data.text || ''
            currentResponseRef.current += chunkText

            const assistantId = currentAssistantIdRef.current
            if (assistantId) {
              const accumulated = currentResponseRef.current
              setMessages((prev) =>
                prev.map((m) =>
                  m.id === assistantId ? { ...m, content: accumulated } : m
                )
              )
            }
          }

          if (msg.type === 'chatAction' && msg.data) {
            const assistantId = currentAssistantIdRef.current
            if (assistantId) {
              const action = {
                action: msg.data.action,
                params: msg.data.params || {},
                requireConfirm: msg.data.requireConfirm || false,
                status: 'pending',
              }
              setMessages((prev) =>
                prev.map((m) =>
                  m.id === assistantId
                    ? { ...m, actions: [...(m.actions || []), action] }
                    : m
                )
              )
            }
          }

          if (msg.type === 'chatDone') {
            setStreaming(false)
            currentAssistantIdRef.current = null
            currentResponseRef.current = ''
          }
        } catch (_e) {
          // ignore parse errors
        }
      }

      ws.onclose = () => {
        if (!mountedRef.current) return
        setConnected(false)
        setStreaming(false)
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
  }, [])

  useEffect(() => {
    mountedRef.current = true
    connectWebSocket()

    return () => {
      mountedRef.current = false

      if (reconnectTimeoutRef.current) {
        clearTimeout(reconnectTimeoutRef.current)
        reconnectTimeoutRef.current = null
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

  const sendMessage = useCallback(
    (text) => {
      if (!text || !text.trim()) return
      if (!wsRef.current || wsRef.current.readyState !== WebSocket.OPEN) return

      const userMsg = {
        id: genId(),
        role: 'user',
        content: text.trim(),
        timestamp: Date.now(),
      }

      const assistantId = genId()
      const assistantMsg = {
        id: assistantId,
        role: 'assistant',
        content: '',
        timestamp: Date.now(),
        actions: [],
      }

      currentAssistantIdRef.current = assistantId
      currentResponseRef.current = ''

      setMessages((prev) => [...prev, userMsg, assistantMsg])
      setStreaming(true)

      try {
        wsRef.current.send(
          JSON.stringify({ type: 'chat', data: { message: text.trim() } })
        )
      } catch (_e) {
        setStreaming(false)
        currentAssistantIdRef.current = null
      }
    },
    []
  )

  const confirmAction = useCallback(
    (messageId, actionIndex) => {
      if (!wsRef.current || wsRef.current.readyState !== WebSocket.OPEN) return

      setMessages((prev) => {
        let targetAction = null
        const updated = prev.map((m) => {
          if (m.id === messageId && m.actions && m.actions[actionIndex]) {
            const newActions = [...m.actions]
            newActions[actionIndex] = {
              ...newActions[actionIndex],
              status: 'confirmed',
            }
            targetAction = m.actions[actionIndex]
            return { ...m, actions: newActions }
          }
          return m
        })

        if (targetAction) {
          try {
            wsRef.current.send(
              JSON.stringify({
                type: 'executeAction',
                data: {
                  action: targetAction.action,
                  params: targetAction.params,
                },
              })
            )
          } catch (_e) {
            // ignore send errors
          }
        }

        return updated
      })
    },
    []
  )

  const cancelAction = useCallback((messageId, actionIndex) => {
    setMessages((prev) =>
      prev.map((m) => {
        if (m.id === messageId && m.actions && m.actions[actionIndex]) {
          const newActions = [...m.actions]
          newActions[actionIndex] = {
            ...newActions[actionIndex],
            status: 'cancelled',
          }
          return { ...m, actions: newActions }
        }
        return m
      })
    )
  }, [])

  return { messages, sendMessage, confirmAction, cancelAction, connected, streaming }
}
