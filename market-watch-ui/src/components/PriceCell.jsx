export default function PriceCell({ value, previousValue, digits }) {
  return <span>{value?.toFixed(digits || 2)}</span>
}
