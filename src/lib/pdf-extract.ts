import { randomUUID } from "crypto";
import { PDFParse } from "pdf-parse";
import type { ProposalLine, SupplierProposalDoc } from "./types";
import { normalizeText, parseNumberPT } from "./normalize";

/** Money-like amounts: decimals, thousand separators, or explicit EUR/€. */
const MONEY_RE =
  /(?:€\s*)?(\d{1,3}(?:[.\s]\d{3})+[.,]\d{2}|\d{1,3}(?:[.\s]\d{3})+|\d+[.,]\d{2}|\d+)(?:\s*€|\s*EUR)?/gi;

const LEAD_RE =
  /(?:prazo(?:\s+de\s+entrega)?|entrega(?:\s+em)?|delivery)[^0-9]{0,24}(\d+)\s*(dias?|semanas?|days?|weeks?|uteis|úteis)/i;
const PAYMENT_RE =
  /(pagamento[:\s]+[^\n]+|condi[cç][oõ]es?\s+de\s+pagamento[:\s]+[^\n]+)/i;
const DELIVERY_RE =
  /(entrega[:\s]+[^\n]+|incoterms?[:\s]+[^\n]+|transporte[:\s]+[^\n]+)/i;
/** Require whitespace before the unit so "305m" is not a quantity. */
const QTY_RE =
  /(?:^|[\s\-–|])(?:qtd[:\s]*)?(\d+[.,]?\d*)\s+(un|unid(?:ade)?s?|pcs?|pç|kg|m2|m³|lt|l)\b/i;

function titleCase(value: string): string {
  return value
    .split(/\s+/)
    .filter(Boolean)
    .map((w) => w.charAt(0).toUpperCase() + w.slice(1).toLowerCase())
    .join(" ");
}

function guessSupplierName(filename: string, text: string): string {
  const firstLines = text
    .split(/\n/)
    .map((l) => l.trim())
    .filter(Boolean)
    .slice(0, 8);
  for (const line of firstLines) {
    if (/proposta|or[cç]amento|factura|fatura|cota[cç]|item\s*\|/i.test(line)) {
      continue;
    }
    if (/\d+[.,]\d{2}/.test(line)) continue;
    if (line.length > 3 && line.length < 70) return line;
  }

  const fromFile = filename
    .replace(/\.pdf$/i, "")
    .replace(/[_-]+/g, " ")
    .replace(/proposta|orcamento|orçamento|cotacao|cotação|quote/gi, "")
    .trim();
  if (fromFile.length > 2) return titleCase(fromFile);
  return "Fornecedor";
}

function daysFromLead(match: RegExpMatchArray | null): number | undefined {
  if (!match) return undefined;
  const n = Number(match[1]);
  if (!Number.isFinite(n)) return undefined;
  const unit = (match[2] || "").toLowerCase();
  if (unit.startsWith("sem") || unit.startsWith("week")) return n * 7;
  return n;
}

interface MoneyHit {
  value: number;
  start: number;
  end: number;
  hasCurrencyMarker: boolean;
  hasDecimals: boolean;
}

function findMoneyHits(raw: string): MoneyHit[] {
  const hits: MoneyHit[] = [];
  const re = new RegExp(MONEY_RE);
  let m: RegExpExecArray | null;
  while ((m = re.exec(raw)) !== null) {
    const full = m[0];
    const n = parseNumberPT(m[1]);
    if (n == null || n <= 0) continue;

    const start = m.index;
    const end = m.index + full.length;
    const before = raw.slice(Math.max(0, start - 3), start);
    const after = raw.slice(end, end + 8);

    // Skip product-code fragments: Cat6, Wi-Fi 6, 24p, 1000VA, 305m
    if (/[A-Za-z]$/.test(before) || /^(m|mm|cm|kg|va|w|ghz|p|port)/i.test(after.trim())) {
      continue;
    }
    // Skip quantities: "10 un", "12 UN"
    if (/^\s*(un|unid|kg|m\b|pcs?|pç)/i.test(after)) continue;

    const hasCurrencyMarker = /€|EUR/i.test(full) || /€|EUR/i.test(after.slice(0, 4));
    const hasDecimals = /[.,]\d{2}\b/.test(m[1]) || /[.,]\d{2}/.test(full);

    // Require money-like shape unless clearly marked as currency
    if (!hasDecimals && !hasCurrencyMarker && n < 20) continue;
    if (!hasDecimals && !hasCurrencyMarker && Number.isInteger(n) && n < 100) {
      // Bare integers under 100 are usually qty/ports/codes
      continue;
    }

    hits.push({ value: n, start, end, hasCurrencyMarker, hasDecimals });
  }
  return hits;
}

function pickPrices(hits: MoneyHit[]): { unitPrice?: number; totalPrice?: number } {
  if (!hits.length) return {};
  const preferred = hits.filter((h) => h.hasDecimals || h.hasCurrencyMarker);
  const pool = preferred.length ? preferred : hits;
  if (pool.length === 1) return { unitPrice: pool[0].value };
  // Convention: last = line total, previous = unit
  return {
    unitPrice: pool[pool.length - 2].value,
    totalPrice: pool[pool.length - 1].value,
  };
}

function extractLinesFromText(text: string): ProposalLine[] {
  const lines: ProposalLine[] = [];
  const rawLines = text
    .split(/\n/)
    .map((l) => l.replace(/\s+/g, " ").trim())
    .filter((l) => l.length > 3);

  for (const raw of rawLines) {
    if (
      /^(proposta|or[cç]amento|total|subtotal|iva|nif|tel|email|morada|item\s*\|)/i.test(
        raw
      )
    ) {
      continue;
    }

    const hits = findMoneyHits(raw);
    if (!hits.length) continue;

    const { unitPrice, totalPrice } = pickPrices(hits);
    if (unitPrice == null && totalPrice == null) continue;

    const qtyMatch = raw.match(QTY_RE);
    const quantity = qtyMatch ? parseNumberPT(qtyMatch[1]) : undefined;

    let description = raw;
    for (const h of [...hits].reverse()) {
      description =
        description.slice(0, h.start) + " " + description.slice(h.end);
    }
    if (qtyMatch) {
      description = description.replace(qtyMatch[0], " ");
    }
    description = description
      .replace(/[-–|]+/g, " ")
      .replace(/\s+/g, " ")
      .trim();

    if (description.length < 3) continue;
    if (normalizeText(description).split(" ").length < 2 && !quantity) continue;

    const resolvedUnit =
      unitPrice ??
      (totalPrice != null && quantity ? totalPrice / quantity : totalPrice);
    const resolvedTotal =
      totalPrice ??
      (resolvedUnit != null && quantity ? resolvedUnit * quantity : resolvedUnit);

    lines.push({
      id: randomUUID(),
      description,
      quantity,
      unit: qtyMatch?.[2],
      unitPrice: resolvedUnit,
      totalPrice: resolvedTotal,
      currency: /€|EUR/i.test(raw) ? "EUR" : "EUR",
      leadTimeDays: daysFromLead(raw.match(LEAD_RE)),
      rawText: raw,
    });
  }

  const seen = new Set<string>();
  return lines.filter((l) => {
    const key = normalizeText(l.description).slice(0, 80);
    if (seen.has(key)) return false;
    seen.add(key);
    return true;
  });
}

function extractLinesFromTables(tables: string[][][] | undefined): ProposalLine[] {
  if (!tables?.length) return [];
  const lines: ProposalLine[] = [];

  for (const rows of tables) {
    if (!rows || rows.length < 2) continue;

    const header = rows[0].map((c) => normalizeText(String(c || "")));
    const descIdx = header.findIndex((h) =>
      /desc|item|artigo|produto|design/.test(h)
    );
    const qtyIdx = header.findIndex((h) => /qtd|quant|qty/.test(h));
    const priceIdx = header.findIndex((h) =>
      /preco|preço|unit|p\.?\s*unit|valor/.test(h)
    );
    const totalIdx = header.findIndex((h) => /total|importe/.test(h));

    const d = descIdx >= 0 ? descIdx : 0;
    const p = priceIdx >= 0 ? priceIdx : rows[0].length - 1;

    for (let i = 1; i < rows.length; i++) {
      const row = rows[i].map((c) => String(c || "").trim());
      const description = row[d];
      if (!description || description.length < 2) continue;
      const unitPrice = parseNumberPT(row[p]);
      const quantity = qtyIdx >= 0 ? parseNumberPT(row[qtyIdx]) : undefined;
      const totalPrice =
        totalIdx >= 0
          ? parseNumberPT(row[totalIdx])
          : quantity && unitPrice
            ? quantity * unitPrice
            : unitPrice;
      if (unitPrice == null && totalPrice == null) continue;
      lines.push({
        id: randomUUID(),
        description,
        quantity,
        unitPrice: unitPrice ?? undefined,
        totalPrice: totalPrice ?? undefined,
        currency: "EUR",
        rawText: row.join(" | "),
      });
    }
  }
  return lines;
}

export async function parseProposalPdf(
  buffer: Buffer,
  filename: string
): Promise<SupplierProposalDoc> {
  const parser = new PDFParse({ data: buffer });
  let text = "";
  let tableLines: ProposalLine[] = [];

  try {
    const textResult = await parser.getText();
    text = textResult.text || "";
  } catch {
    text = "";
  }

  try {
    const tableResult = await parser.getTable();
    const fromMerged = tableResult?.mergedTables || [];
    const fromPages = (tableResult?.pages || []).flatMap((p) => p.tables || []);
    tableLines = extractLinesFromTables(
      (fromMerged.length ? fromMerged : fromPages) as string[][][]
    );
  } catch {
    tableLines = [];
  }

  try {
    await parser.destroy();
  } catch {
    /* ignore */
  }

  const textLines = extractLinesFromText(text);
  // Prefer the source that yields more priced lines
  const lines = tableLines.length > textLines.length ? tableLines : textLines;

  const leadMatch = text.match(LEAD_RE);
  const payment = text.match(PAYMENT_RE)?.[1];
  const delivery = text.match(DELIVERY_RE)?.[1];

  return {
    id: randomUUID(),
    filename,
    supplierName: guessSupplierName(filename, text),
    lines,
    currency: /€|EUR/i.test(text) ? "EUR" : "EUR",
    leadTimeDays: daysFromLead(leadMatch),
    paymentTerms: payment?.trim(),
    deliveryTerms: delivery?.trim(),
    rawText: text.slice(0, 20000),
    extractedAt: new Date().toISOString(),
  };
}
