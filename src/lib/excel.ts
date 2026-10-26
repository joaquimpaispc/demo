import * as XLSX from "xlsx";
import { randomUUID } from "crypto";
import type { ColumnMapping, ColumnRole, RequestLine } from "./types";
import { parseNumberPT } from "./normalize";

const ROLE_PATTERNS: Array<{ role: ColumnRole; patterns: RegExp[] }> = [
  {
    role: "description",
    patterns: [
      /^desc/i,
      /descri/i,
      /designa/i,
      /artigo/i,
      /produto/i,
      /item/i,
      /material/i,
      /servi[cç]o/i,
    ],
  },
  {
    role: "quantity",
    patterns: [/^qtd/i, /^qty/i, /quantidade/i, /^quant/i, /^qtde/i],
  },
  {
    role: "unit",
    patterns: [/^un(id)?$/i, /unidade/i, /^u\.?m\.?$/i, /unit/i],
  },
  {
    role: "specs",
    patterns: [/espec/i, /spec/i, /caracter/i, /obs/i, /detalhe/i, /nota/i],
  },
  {
    role: "code",
    patterns: [/^c[oó]d/i, /referencia/i, /^ref\.?$/i, /sku/i, /^id$/i],
  },
];

export function detectColumnMapping(headers: string[]): ColumnMapping {
  const mapping: ColumnMapping = {};
  const used = new Set<ColumnRole>();

  for (const header of headers) {
    let assigned: ColumnRole = "ignore";
    for (const { role, patterns } of ROLE_PATTERNS) {
      if (used.has(role) && role !== "ignore") continue;
      if (patterns.some((p) => p.test(header.trim()))) {
        assigned = role;
        used.add(role);
        break;
      }
    }
    mapping[header] = assigned;
  }

  // Fallback: first text-like unused column as description
  if (![...Object.values(mapping)].includes("description") && headers.length) {
    mapping[headers[0]] = "description";
  }
  return mapping;
}

export function parseExcelBuffer(buffer: Buffer): {
  headers: string[];
  rows: Record<string, string | number | null>[];
  mapping: ColumnMapping;
} {
  const workbook = XLSX.read(buffer, { type: "buffer", cellDates: true });
  const sheetName = workbook.SheetNames[0];
  if (!sheetName) {
    throw new Error("O ficheiro Excel não contém folhas.");
  }
  const sheet = workbook.Sheets[sheetName];
  const json = XLSX.utils.sheet_to_json<(string | number | null)[]>(sheet, {
    header: 1,
    defval: null,
    raw: false,
  });

  if (!json.length) {
    throw new Error("A folha Excel está vazia.");
  }

  const headerRow = (json[0] || []).map((h, i) =>
    h == null || String(h).trim() === "" ? `Coluna ${i + 1}` : String(h).trim()
  );

  const rows: Record<string, string | number | null>[] = [];
  for (let i = 1; i < json.length; i++) {
    const row = json[i] || [];
    const isEmpty = row.every(
      (c) => c == null || String(c).trim() === ""
    );
    if (isEmpty) continue;
    const obj: Record<string, string | number | null> = {};
    headerRow.forEach((h, idx) => {
      const cell = row[idx];
      obj[h] =
        cell == null || cell === ""
          ? null
          : typeof cell === "number"
            ? cell
            : String(cell);
    });
    rows.push(obj);
  }

  return {
    headers: headerRow,
    rows,
    mapping: detectColumnMapping(headerRow),
  };
}

export function rowsToRequestLines(
  rows: Record<string, string | number | null>[],
  mapping: ColumnMapping
): RequestLine[] {
  const descKey = Object.entries(mapping).find(([, r]) => r === "description")?.[0];
  const qtyKey = Object.entries(mapping).find(([, r]) => r === "quantity")?.[0];
  const unitKey = Object.entries(mapping).find(([, r]) => r === "unit")?.[0];
  const specsKey = Object.entries(mapping).find(([, r]) => r === "specs")?.[0];
  const codeKey = Object.entries(mapping).find(([, r]) => r === "code")?.[0];

  if (!descKey) {
    throw new Error("É necessário mapear uma coluna de descrição/item.");
  }

  const lines: RequestLine[] = [];
  for (const raw of rows) {
    const description = String(raw[descKey] ?? "").trim();
    if (!description) continue;
    const quantity = qtyKey ? parseNumberPT(raw[qtyKey]) ?? 1 : 1;
    lines.push({
      id: randomUUID(),
      code: codeKey ? String(raw[codeKey] ?? "").trim() || undefined : undefined,
      description,
      quantity,
      unit: unitKey ? String(raw[unitKey] ?? "").trim() || undefined : undefined,
      specs: specsKey
        ? String(raw[specsKey] ?? "").trim() || undefined
        : undefined,
      raw,
    });
  }
  return lines;
}
