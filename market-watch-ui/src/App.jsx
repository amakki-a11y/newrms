import { useState } from 'react'
import Header from './components/Header'
import MarketTable from './components/MarketTable'
import AccountTable from './components/AccountTable'
import AccountSummary from './components/AccountSummary'
import PositionTable from './components/PositionTable'
import OpenPositionDialog from './components/OpenPositionDialog'
import DashboardTab from './components/DashboardTab'
import ChartTab from './components/ChartTab'
import ChatSidebar from './components/ChatSidebar'
import ToastContainer from './components/ToastContainer'
import useMarketData from './hooks/useMarketData'
import useAccountData from './hooks/useAccountData'
import useChartData from './hooks/useChartData'
import useDashboardData from './hooks/useDashboardData'
import useChatData from './hooks/useChatData'

function App() {
  const [activeTab, setActiveTab] = useState('market')
  const marketData = useMarketData()
  const accountData = useAccountData()
  const chartData = useChartData()
  const dashboardData = useDashboardData()
  const chatData = useChatData()

  return (
    <div style={{ background: '#0a1118', color: '#e1e8ed', minHeight: '100vh' }}>
      <Header connected={marketData.connected} />
      <div>
        <div>[Stub] Active Tab: {activeTab}</div>
      </div>
      <ToastContainer />
    </div>
  )
}

export default App
