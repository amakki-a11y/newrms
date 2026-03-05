import { useState, useEffect, useRef } from 'react'

const styles = {
  cell: (color, flashing) => ({
    fontFamily: "'JetBrains Mono', monospace",
    fontWeight: 500,
    fontSize: '13px',
    color: color,
    display: 'inline-block',
    padding: '1px 4px',
    borderRadius: '3px',
    background: flashing
      ? color === '#00c853'
        ? 'rgba(0, 200, 83, 0.15)'
        : color === '#ff5252'
          ? 'rgba(255, 82, 82, 0.15)'
          : 'transparent'
      : 'transparent',
    transition: 'background 0.3s ease, color 0.3s ease',
  }),
}

export default function PriceCell({ value, previousValue, digits }) {
  const [flashing, setFlashing] = useState(false)
  const prevRef = useRef(previousValue)
  const timerRef = useRef(null)

  const d = digits != null ? digits : 2
  const formattedValue = value != null ? Number(value).toFixed(d) : '--'

  // Determine color direction
  let color = '#e1e8ed' // neutral
  if (value != null && previousValue != null) {
    if (value > previousValue) {
      color = '#00c853' // green - up
    } else if (value < previousValue) {
      color = '#ff5252' // red - down
    }
  }

  // Flash effect on value change
  useEffect(() => {
    if (value != null && prevRef.current != null && value !== prevRef.current) {
      setFlashing(true)
      if (timerRef.current) clearTimeout(timerRef.current)
      timerRef.current = setTimeout(() => {
        setFlashing(false)
      }, 400)
    }
    prevRef.current = value
    return () => {
      if (timerRef.current) clearTimeout(timerRef.current)
    }
  }, [value])

  return (
    <span style={styles.cell(color, flashing)}>
      {formattedValue}
    </span>
  )
}
