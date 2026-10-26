export function stripAccents(value: string): string {
  return value.normalize("NFD").replace(/\p{M}/gu, "");
}

export function normalizeText(value: string): string {
  return stripAccents(value)
    .toLowerCase()
    .replace(/[^a-z0-9\s]/g, " ")
    .replace(/\s+/g, " ")
    .trim();
}

export function tokenize(value: string): string[] {
  return normalizeText(value)
    .split(" ")
    .filter((t) => t.length > 1);
}

export function parseNumberPT(value: unknown): number | undefined {
  if (typeof value === "number" && Number.isFinite(value)) return value;
  if (value == null) return undefined;
  let s = String(value).trim();
  if (!s) return undefined;
  s = s.replace(/[€$£]|EUR|USD|GBP/gi, "").trim();
  // 1.234,56 → 1234.56 ; 1234.56 stays; 1,234.56 → 1234.56
  if (/^\d{1,3}(\.\d{3})+(,\d+)?$/.test(s)) {
    s = s.replace(/\./g, "").replace(",", ".");
  } else if (/^\d{1,3}(,\d{3})+(\.\d+)?$/.test(s)) {
    s = s.replace(/,/g, "");
  } else if (s.includes(",") && !s.includes(".")) {
    s = s.replace(",", ".");
  } else {
    s = s.replace(/,/g, "");
  }
  const n = Number(s);
  return Number.isFinite(n) ? n : undefined;
}

export function diceCoefficient(a: string[], b: string[]): number {
  if (a.length === 0 && b.length === 0) return 1;
  if (a.length === 0 || b.length === 0) return 0;
  const setB = new Set(b);
  let intersection = 0;
  for (const t of a) {
    if (setB.has(t)) intersection += 1;
  }
  return (2 * intersection) / (a.length + b.length);
}

export function similarity(a: string, b: string): number {
  const ta = tokenize(a);
  const tb = tokenize(b);
  const dice = diceCoefficient(ta, tb);
  const na = normalizeText(a);
  const nb = normalizeText(b);
  if (na === nb) return 1;
  if (na.includes(nb) || nb.includes(na)) {
    return Math.max(dice, 0.82);
  }
  return dice;
}
