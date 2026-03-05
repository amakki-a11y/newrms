const styles = {
  container: {
    display: 'flex',
    flexWrap: 'wrap',
    gap: '12px',
    padding: '16px',
  },
  card: {
    flex: '1 1 160px',
    minWidth: '150px',
    background: 'linear-gradient(135deg, #111d28, #162230)',
    border: '1px solid #2a3a4a',
    borderRadius: '10px',
    padding: '16px',
    display: 'flex',
    flexDirection: 'column',
    gap: '8px',
  },
  cardHeader: {
    display: 'flex',
    alignItems: 'center',
    gap: '8px',
  },
  icon: {
    width: '32px',
    height: '32px',
    borderRadius: '8px',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    fontSize: '14px',
    flexShrink: 0,
  },
  label: {
    fontSize: '11px',
    fontFamily: "'Inter', sans-serif",
    color: '#5a7a8a',
    textTransform: 'uppercase',
    letterSpacing: '0.5px',
    fontWeight: 500,
  },
  value: {
    fontSize: '20px',
    fontFamily: "'JetBrains Mono', monospace",
    fontWeight: 600,
    color: '#e1e8ed',
    letterSpacing: '-0.5px',
  },
}

function formatCurrency(val) {
  if (val == null || isNaN(val)) return '$0.00'
  const abs = Math.abs(val)
  const sign = val < 0 ? '-' : ''
  return sign + '$' + abs.toLocaleString('en-US', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })
}

function formatNumber(val) {
  if (val == null || isNaN(val)) return '0'
  return Number(val).toLocaleString('en-US')
}

const cardDefs = [
  {
    key: 'totalAccounts',
    label: 'Total Accounts',
    icon: '\u{1F464}',
    iconBg: 'rgba(74, 158, 255, 0.15)',
    iconColor: '#4a9eff',
    format: formatNumber,
  },
  {
    key: 'totalBalance',
    label: 'Total Balance',
    icon: '\u{1F4B0}',
    iconBg: 'rgba(74, 158, 255, 0.15)',
    iconColor: '#4a9eff',
    format: formatCurrency,
  },
  {
    key: 'totalEquity',
    label: 'Total Equity',
    icon: '\u{1F4CA}',
    iconBg: 'rgba(74, 158, 255, 0.15)',
    iconColor: '#4a9eff',
    format: formatCurrency,
  },
  {
    key: 'totalMargin',
    label: 'Total Margin',
    icon: '\u{1F512}',
    iconBg: 'rgba(255, 170, 0, 0.15)',
    iconColor: '#ffaa00',
    format: formatCurrency,
  },
  {
    key: 'totalMarginFree',
    label: 'Free Margin',
    icon: '\u{1F513}',
    iconBg: 'rgba(0, 200, 83, 0.15)',
    iconColor: '#00c853',
    format: formatCurrency,
  },
  {
    key: 'totalProfit',
    label: 'Total Profit',
    icon: '\u{1F4C8}',
    iconBg: 'rgba(0, 200, 83, 0.15)',
    iconColor: '#00c853',
    format: formatCurrency,
    dynamic: true,
  },
]

export default function AccountSummary({ summary }) {
  if (!summary) return null

  return (
    <div style={styles.container}>
      {cardDefs.map(def => {
        const val = summary[def.key] ?? 0
        let valueColor = '#e1e8ed'
        let iconBg = def.iconBg
        let iconColor = def.iconColor

        if (def.dynamic) {
          if (val > 0) {
            valueColor = '#00c853'
            iconBg = 'rgba(0, 200, 83, 0.15)'
            iconColor = '#00c853'
          } else if (val < 0) {
            valueColor = '#ff5252'
            iconBg = 'rgba(255, 82, 82, 0.15)'
            iconColor = '#ff5252'
          }
        }

        return (
          <div key={def.key} style={styles.card}>
            <div style={styles.cardHeader}>
              <div style={{ ...styles.icon, background: iconBg, color: iconColor }}>
                {def.icon}
              </div>
              <span style={styles.label}>{def.label}</span>
            </div>
            <span style={{ ...styles.value, color: valueColor }}>
              {def.format(val)}
            </span>
          </div>
        )
      })}
    </div>
  )
}
