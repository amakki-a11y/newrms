import { useEffect, useRef, useCallback, useState } from 'react'
import { createChart, CandlestickSeries, HistogramSeries } from 'lightweight-charts'

const styles = {
  container: {
    width: '100%',
    height: '100%',
    position: 'relative',
  },
}

export default function CandlestickChart({ candles, symbol, interval, onCrosshairData }) {
  const containerRef = useRef(null)
  const chartRef = useRef(null)
  const candleSeriesRef = useRef(null)
  const volumeSeriesRef = useRef(null)
  const resizeObserverRef = useRef(null)
  const prevCandlesLenRef = useRef(0)

  // Initialize chart on mount
  useEffect(() => {
    if (!containerRef.current) return

    const chart = createChart(containerRef.current, {
      layout: {
        background: { color: '#0a1118' },
        textColor: '#5a7a8a',
        fontFamily: "'JetBrains Mono', monospace",
      },
      grid: {
        vertLines: { color: '#1a2a3a' },
        horzLines: { color: '#1a2a3a' },
      },
      crosshair: {
        mode: 0, // Normal
        vertLine: {
          color: '#4a9eff55',
          width: 1,
          style: 2,
          labelBackgroundColor: '#2a3a4a',
        },
        horzLine: {
          color: '#4a9eff55',
          width: 1,
          style: 2,
          labelBackgroundColor: '#2a3a4a',
        },
      },
      timeScale: {
        timeVisible: true,
        borderColor: '#2a3a4a',
        rightOffset: 5,
      },
      rightPriceScale: {
        borderColor: '#2a3a4a',
      },
      handleScroll: true,
      handleScale: true,
    })

    chartRef.current = chart

    // Add candlestick series (v5 API)
    const candleSeries = chart.addSeries(CandlestickSeries, {
      upColor: '#00c853',
      downColor: '#ff5252',
      borderUpColor: '#00c853',
      borderDownColor: '#ff5252',
      wickUpColor: '#00c853',
      wickDownColor: '#ff5252',
    })
    candleSeriesRef.current = candleSeries

    // Add volume series (v5 API)
    const volumeSeries = chart.addSeries(HistogramSeries, {
      color: '#4a9eff33',
      priceFormat: { type: 'volume' },
      priceScaleId: 'volume',
    })
    volumeSeriesRef.current = volumeSeries

    chart.priceScale('volume').applyOptions({
      scaleMargins: {
        top: 0.8,
        bottom: 0,
      },
    })

    // Subscribe to crosshair move
    chart.subscribeCrosshairMove((param) => {
      if (!onCrosshairData) return

      if (!param || !param.time || !param.seriesData) {
        onCrosshairData(null)
        return
      }

      const candleData = param.seriesData.get(candleSeries)
      const volumeData = param.seriesData.get(volumeSeries)

      if (candleData && typeof candleData.open === 'number') {
        onCrosshairData({
          open: candleData.open,
          high: candleData.high,
          low: candleData.low,
          close: candleData.close,
          volume: volumeData ? volumeData.value : 0,
        })
      } else {
        onCrosshairData(null)
      }
    })

    // ResizeObserver for responsive chart
    const ro = new ResizeObserver((entries) => {
      for (const entry of entries) {
        const { width, height } = entry.contentRect
        if (width > 0 && height > 0) {
          chart.resize(width, height)
        }
      }
    })
    ro.observe(containerRef.current)
    resizeObserverRef.current = ro

    return () => {
      ro.disconnect()
      chart.remove()
      chartRef.current = null
      candleSeriesRef.current = null
      volumeSeriesRef.current = null
    }
  }, []) // Only on mount/unmount

  // Update data when candles change
  useEffect(() => {
    if (!candleSeriesRef.current || !volumeSeriesRef.current) return
    if (!candles || candles.length === 0) {
      candleSeriesRef.current.setData([])
      volumeSeriesRef.current.setData([])
      prevCandlesLenRef.current = 0
      return
    }

    const prevLen = prevCandlesLenRef.current
    const currentLen = candles.length

    // If length increased by 1 from the previous, use update() for the last candle
    if (currentLen === prevLen + 1 && prevLen > 0) {
      const lastCandle = candles[currentLen - 1]
      candleSeriesRef.current.update({
        time: lastCandle.time,
        open: lastCandle.open,
        high: lastCandle.high,
        low: lastCandle.low,
        close: lastCandle.close,
      })
      volumeSeriesRef.current.update({
        time: lastCandle.time,
        value: lastCandle.volume,
        color: lastCandle.close >= lastCandle.open ? '#00c85333' : '#ff525233',
      })
    } else if (currentLen === prevLen && prevLen > 0) {
      // Same length - update last candle in place (live tick update)
      const lastCandle = candles[currentLen - 1]
      candleSeriesRef.current.update({
        time: lastCandle.time,
        open: lastCandle.open,
        high: lastCandle.high,
        low: lastCandle.low,
        close: lastCandle.close,
      })
      volumeSeriesRef.current.update({
        time: lastCandle.time,
        value: lastCandle.volume,
        color: lastCandle.close >= lastCandle.open ? '#00c85333' : '#ff525233',
      })
    } else {
      // Full data replacement
      const candleData = candles.map(c => ({
        time: c.time,
        open: c.open,
        high: c.high,
        low: c.low,
        close: c.close,
      }))

      const volumeData = candles.map(c => ({
        time: c.time,
        value: c.volume,
        color: c.close >= c.open ? '#00c85333' : '#ff525233',
      }))

      candleSeriesRef.current.setData(candleData)
      volumeSeriesRef.current.setData(volumeData)

      // Fit content on full data load
      if (chartRef.current) {
        chartRef.current.timeScale().fitContent()
      }
    }

    prevCandlesLenRef.current = currentLen
  }, [candles])

  return (
    <div ref={containerRef} style={styles.container} />
  )
}
