import { useState } from 'react'

export default function useAccountData() {
  return {
    accounts: [], positions: {}, summary: null,
    page: 1, totalPages: 1, search: '',
    setPage: () => {}, setSearch: () => {},
    getPositions: () => {}, closePosition: () => {},
    openPosition: () => {}, modifyPosition: () => {},
    connected: false
  }
}
