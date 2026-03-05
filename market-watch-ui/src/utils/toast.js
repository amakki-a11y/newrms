let listeners = []
let toastId = 0

export function showToast({ type = 'info', title = '', message = '', duration = 3000 }) {
  toastId += 1
  const toast = { type, title, message, duration, id: toastId, timestamp: Date.now() }
  listeners.forEach(fn => fn(toast))
  return toast.id
}

export function onToast(fn) {
  listeners.push(fn)
  return () => {
    listeners = listeners.filter(l => l !== fn)
  }
}

// Convenience methods
export const toast = {
  success: (title, message, duration) => showToast({ type: 'success', title, message, duration }),
  error: (title, message, duration) => showToast({ type: 'error', title, message, duration }),
  warning: (title, message, duration) => showToast({ type: 'warning', title, message, duration }),
  info: (title, message, duration) => showToast({ type: 'info', title, message, duration }),
}
