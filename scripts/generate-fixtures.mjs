import * as XLSX from "xlsx";
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const root = path.join(path.dirname(fileURLToPath(import.meta.url)), "..");
const out = path.join(root, "fixtures");
fs.mkdirSync(out, { recursive: true });

const rows = [
  ["Código", "Descrição", "Quantidade", "Unidade", "Especificações"],
  ["CAB-01", "Cabo Ethernet Cat6 305m", 10, "un", "LSZH azul"],
  ["SW-24", "Switch gerível 24 portas PoE+", 4, "un", "1GbE, rack 1U"],
  ["AP-WIFI", "Access Point Wi-Fi 6 dual band", 12, "un", "montagem teto"],
  ["PATCH", "Patch panel 24 portas Cat6", 6, "un", "blindado"],
  ["UPS-1K", "UPS online 1000VA", 2, "un", "rack, autonomia 10min"],
];

const wb = XLSX.utils.book_new();
XLSX.utils.book_append_sheet(wb, XLSX.utils.aoa_to_sheet(rows), "Requisicao");
XLSX.writeFile(wb, path.join(out, "requisicao-exemplo.xlsx"));

function makePdf(lines) {
  const contentLines = [];
  let y = 780;
  for (const line of lines) {
    const safe = line.replace(/\\/g, "\\\\").replace(/\(/g, "\\(").replace(/\)/g, "\\)");
    contentLines.push(`BT /F1 11 Tf 40 ${y} Td (${safe}) Tj ET`);
    y -= 18;
  }
  const stream = contentLines.join("\n");
  const objs = [
    "1 0 obj<< /Type /Catalog /Pages 2 0 R >>endobj",
    "2 0 obj<< /Type /Pages /Kids [3 0 R] /Count 1 >>endobj",
    "3 0 obj<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources<< /Font<< /F1 5 0 R >> >> >>endobj",
    `4 0 obj<< /Length ${Buffer.byteLength(stream)} >>stream\n${stream}\nendstream\nendobj`,
    "5 0 obj<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>endobj",
  ];
  let pdf = "%PDF-1.4\n";
  const offsets = [0];
  for (const o of objs) {
    offsets.push(Buffer.byteLength(pdf));
    pdf += o + "\n";
  }
  const xrefStart = Buffer.byteLength(pdf);
  pdf += `xref\n0 ${objs.length + 1}\n`;
  pdf += "0000000000 65535 f \n";
  for (let i = 1; i < offsets.length; i++) {
    pdf += String(offsets[i]).padStart(10, "0") + " 00000 n \n";
  }
  pdf += `trailer<< /Size ${objs.length + 1} /Root 1 0 R >>\nstartxref\n${xrefStart}\n%%EOF\n`;
  return Buffer.from(pdf);
}

fs.writeFileSync(
  path.join(out, "proposta-netalpha.pdf"),
  makePdf([
    "NETALPHA Solucoes Lda",
    "Proposta comercial PC-2026-041",
    "Prazo de entrega: 12 dias",
    "Pagamento: 30 dias",
    "Cabo Ethernet Cat6 305m LSZH 10 un 78,50 EUR 785,00 EUR",
    "Switch gerivel 24 portas PoE+ 4 un 420,00 EUR 1680,00 EUR",
    "Access Point Wi-Fi 6 dual band 12 un 185,00 EUR 2220,00 EUR",
    "Patch panel 24 portas Cat6 6 un 95,00 EUR 570,00 EUR",
    "UPS online 1000VA rack 2 un 310,00 EUR 620,00 EUR",
    "Total 5875,00 EUR",
  ])
);

fs.writeFileSync(
  path.join(out, "proposta-fibraprime.pdf"),
  makePdf([
    "FibraPrime Distribuicao",
    "Orcamento ORC-8891",
    "Prazo: 8 dias uteis",
    "Condicoes de pagamento: pronto pagamento -2%",
    "Cabo Ethernet Cat6 305m - 10 UN - 82,00 EUR",
    "Switch 24 portas PoE+ gerivel - 4 UN - 399,00 EUR",
    "Access Point WiFi 6 - 12 UN - 169,00 EUR",
    "Patch panel Cat6 24p - 6 UN - 88,00 EUR",
    "Subtotal 5302,00 EUR",
  ])
);

fs.writeFileSync(
  path.join(out, "proposta-portotech.pdf"),
  makePdf([
    "PortoTech Supplies",
    "Cotacao CT-77",
    "Entrega em 20 dias",
    "Cabo Cat6 Ethernet 305m 10 un 71,00 EUR",
    "Switch gerivel PoE 24p 4 un 455,00 EUR",
    "AP Wi-Fi 6 dual band 12 un 198,00 EUR",
    "Patch panel 24 Cat6 6 un 102,00 EUR",
    "UPS 1000VA online 2 un 295,00 EUR",
    "Total 6295,00 EUR",
  ])
);

console.log("Fixtures written to", out);
