let listeners = []

export function showToast({ type = 'info', title = '', message = '', duration = 3000 }) {
  listeners.forEach(fn => fn({ type, title, message, duration, id: Date.now() }))
}

export function onToast(fn) {
  listeners.push(fn)
  return () => { listeners = listeners.filter(l => l !== fn) }
}
