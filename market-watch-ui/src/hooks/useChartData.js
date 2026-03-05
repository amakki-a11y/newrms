import { useState } from 'react'

export default function useChartData() {
  return {
    candles: [], symbol: 'EURUSD', interval: '1h',
    setSymbol: () => {}, setInterval: () => {},
    loading: false
  }
}
