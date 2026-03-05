import { useState } from 'react'

export default function useMarketData() {
  return { symbols: [], ticks: {}, connected: false, tickCount: 0 }
}
