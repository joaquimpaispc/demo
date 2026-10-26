export function money(value?: number, currency = "EUR"): string {
  if (value == null || Number.isNaN(value)) return "—";
  return value.toLocaleString("pt-PT", {
    style: "currency",
    currency,
    maximumFractionDigits: 2,
  });
}

export function pct(value: number): string {
  return `${Math.round(value * 100)}`;
}
