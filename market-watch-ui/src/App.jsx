import { useState, useCallback } from 'react'
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

const TABS = [
  { key: 'market', label: 'Market Watch' },
  { key: 'accounts', label: 'Accounts' },
  { key: 'dashboard', label: 'Dashboard' },
  { key: 'chart', label: 'Chart' },
]

const styles = {
  root: {
    display: 'flex',
    flexDirection: 'column',
    height: '100vh',
    background: '#0a1118',
    color: '#e1e8ed',
    overflow: 'hidden',
  },
  bodyRow: {
    display: 'flex',
    flex: 1,
    overflow: 'hidden',
  },
  mainColumn: (chatOpen) => ({
    display: 'flex',
    flexDirection: 'column',
    flex: 1,
    minWidth: 0,
    overflow: 'hidden',
    transition: 'margin-right 0.3s ease',
  }),
  tabBar: {
    display: 'flex',
    alignItems: 'center',
    gap: '0',
    padding: '0 16px',
    background: '#0d1520',
    borderBottom: '1px solid #1a2a3a',
    flexShrink: 0,
    height: '40px',
  },
  tabButton: (isActive) => ({
    position: 'relative',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    padding: '0 20px',
    height: '100%',
    border: 'none',
    background: 'transparent',
    color: isActive ? '#4a9eff' : '#5a7a8a',
    fontSize: '13px',
    fontWeight: isActive ? 600 : 400,
    fontFamily: "'Inter', sans-serif",
    cursor: 'pointer',
    transition: 'color 0.2s',
    borderBottom: isActive ? '2px solid #4a9eff' : '2px solid transparent',
    boxSizing: 'border-box',
  }),
  contentArea: {
    flex: 1,
    overflow: 'auto',
    padding: '0',
  },
  chatToggle: (chatOpen) => ({
    position: 'fixed',
    bottom: '20px',
    right: chatOpen ? '340px' : '20px',
    width: '44px',
    height: '44px',
    borderRadius: '50%',
    background: 'linear-gradient(135deg, #4a9eff, #2979ff)',
    border: 'none',
    color: '#fff',
    fontSize: '20px',
    cursor: 'pointer',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    boxShadow: '0 4px 16px rgba(74, 158, 255, 0.3)',
    zIndex: 1000,
    transition: 'right 0.3s ease, transform 0.2s ease',
  }),
  sidebarContainer: (chatOpen) => ({
    width: chatOpen ? '320px' : '0px',
    flexShrink: 0,
    overflow: 'hidden',
    transition: 'width 0.3s ease',
    borderLeft: chatOpen ? '1px solid #1a2a3a' : 'none',
    background: '#0d1520',
  }),
  // Account tab composite layout
  accountLayout: {
    display: 'flex',
    flexDirection: 'column',
    height: '100%',
    overflow: 'auto',
  },
}

function App() {
  const [activeTab, setActiveTab] = useState('market')
  const [chatOpen, setChatOpen] = useState(false)
  const [selectedAccount, setSelectedAccount] = useState(null)
  const [openPositionDialogOpen, setOpenPositionDialogOpen] = useState(false)

  const marketData = useMarketData()
  const accountData = useAccountData()
  const chartData = useChartData()
  const dashboardData = useDashboardData()
  const chatData = useChatData()

  const toggleChat = useCallback(() => {
    setChatOpen(prev => !prev)
  }, [])

  const handleSelectAccount = useCallback((account) => {
    setSelectedAccount(account)
    if (account && accountData.getPositions) {
      accountData.getPositions(account)
    }
  }, [accountData])

  const renderTabContent = () => {
    switch (activeTab) {
      case 'market':
        return (
          <MarketTable
            ticks={marketData.ticks}
            connected={marketData.connected}
          />
        )

      case 'accounts':
        return (
          <div style={styles.accountLayout}>
            <AccountSummary summary={accountData.summary} />
            <AccountTable
              accounts={accountData.accounts}
              positions={accountData.positions}
              onSelectAccount={handleSelectAccount}
              onGetPositions={accountData.getPositions}
              onClosePosition={accountData.closePosition}
              onOpenPosition={() => setOpenPositionDialogOpen(true)}
              onModifyPosition={accountData.modifyPosition}
            />
            {selectedAccount && accountData.positions[selectedAccount] && (
              <PositionTable
                positions={accountData.positions[selectedAccount]}
                onClose={accountData.closePosition}
                onModify={accountData.modifyPosition}
              />
            )}
            <OpenPositionDialog
              isOpen={openPositionDialogOpen}
              onClose={() => setOpenPositionDialogOpen(false)}
              onSubmit={accountData.openPosition}
              symbols={marketData.symbols}
            />
          </div>
        )

      case 'dashboard':
        return <DashboardTab data={dashboardData} />

      case 'chart':
        return <ChartTab chartData={chartData} />

      default:
        return null
    }
  }

  return (
    <div style={styles.root}>
      {/* Header */}
      <Header
        connected={marketData.connected}
        symbolCount={marketData.symbols?.length ?? 0}
        tickCount={marketData.tickCount ?? 0}
      />

      {/* Tab bar + Content + Chat in a row */}
      <div style={styles.bodyRow}>
        {/* Main column: tabs + content */}
        <div style={styles.mainColumn(chatOpen)}>
          {/* Tab bar */}
          <div style={styles.tabBar}>
            {TABS.map(tab => (
              <button
                key={tab.key}
                style={styles.tabButton(activeTab === tab.key)}
                onClick={() => setActiveTab(tab.key)}
                onMouseEnter={(e) => {
                  if (activeTab !== tab.key) {
                    e.currentTarget.style.color = '#e1e8ed'
                  }
                }}
                onMouseLeave={(e) => {
                  if (activeTab !== tab.key) {
                    e.currentTarget.style.color = '#5a7a8a'
                  }
                }}
              >
                {tab.label}
              </button>
            ))}
          </div>

          {/* Active tab content */}
          <div style={styles.contentArea}>
            {renderTabContent()}
          </div>
        </div>

        {/* Chat sidebar */}
        <div style={styles.sidebarContainer(chatOpen)}>
          <ChatSidebar
            messages={chatData.messages}
            onSend={chatData.sendMessage}
            connected={chatData.connected}
            isOpen={chatOpen}
            onToggle={toggleChat}
          />
        </div>
      </div>

      {/* Chat toggle button */}
      <button
        style={styles.chatToggle(chatOpen)}
        onClick={toggleChat}
        onMouseEnter={(e) => { e.currentTarget.style.transform = 'scale(1.1)' }}
        onMouseLeave={(e) => { e.currentTarget.style.transform = 'scale(1)' }}
        title={chatOpen ? 'Close Chat' : 'Open Chat'}
      >
        {chatOpen ? '\u2715' : '\uD83D\uDCAC'}
      </button>

      {/* Toast notifications */}
      <ToastContainer />
    </div>
  )
}

export default App
