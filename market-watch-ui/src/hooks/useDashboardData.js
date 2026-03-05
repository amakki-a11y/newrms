import { useState } from 'react'

export default function useDashboardData() {
  return {
    summary: null, exposure: [], topMovers: [],
    dealHistory: [], dealRange: '1D',
    setDealRange: () => {}, loading: false, connected: false
  }
}
