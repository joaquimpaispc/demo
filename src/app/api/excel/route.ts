import { NextResponse } from "next/server";
import { parseExcelBuffer, rowsToRequestLines } from "@/lib/excel";
import { getSession, saveSession, touchOrCreate } from "@/lib/session-store";
import type { ColumnMapping } from "@/lib/types";

export const runtime = "nodejs";

export async function POST(request: Request) {
  try {
    const form = await request.formData();
    const file = form.get("file");
    const sessionId = form.get("sessionId");
    const mappingRaw = form.get("mapping");

    if (!(file instanceof File)) {
      return NextResponse.json(
        { error: "Envie um ficheiro Excel (.xlsx)." },
        { status: 400 }
      );
    }

    const name = file.name.toLowerCase();
    if (!name.endsWith(".xlsx") && !name.endsWith(".xls") && !name.endsWith(".csv")) {
      return NextResponse.json(
        { error: "Formato inválido. Use .xlsx (preferencial)." },
        { status: 400 }
      );
    }

    const buffer = Buffer.from(await file.arrayBuffer());
    const parsed = parseExcelBuffer(buffer);
    const session = touchOrCreate(
      typeof sessionId === "string" ? sessionId : null
    );

    let mapping = parsed.mapping;
    if (typeof mappingRaw === "string" && mappingRaw.trim()) {
      try {
        mapping = JSON.parse(mappingRaw) as ColumnMapping;
      } catch {
        /* keep detected */
      }
    }

    const requestLines = rowsToRequestLines(parsed.rows, mapping);
    session.excelFilename = file.name;
    session.excelHeaders = parsed.headers;
    session.columnMapping = mapping;
    session.requestLines = requestLines;
    session.comparison = undefined;
    saveSession(session);

    return NextResponse.json({
      session,
      previewRows: parsed.rows.slice(0, 8),
      detectedMapping: parsed.mapping,
    });
  } catch (err) {
    const message = err instanceof Error ? err.message : "Erro ao ler Excel";
    return NextResponse.json({ error: message }, { status: 400 });
  }
}

export async function PUT(request: Request) {
  try {
    const body = (await request.json()) as {
      sessionId?: string;
      mapping?: ColumnMapping;
      rows?: Record<string, string | number | null>[];
    };
    if (!body.sessionId || !body.mapping) {
      return NextResponse.json({ error: "Dados incompletos" }, { status: 400 });
    }
    const session = getSession(body.sessionId);
    if (!session) {
      return NextResponse.json({ error: "Sessão não encontrada" }, { status: 404 });
    }

    // Re-read from stored raw on lines if rows not provided
    const rows =
      body.rows ||
      session.requestLines.map((l) => l.raw);
    const requestLines = rowsToRequestLines(rows, body.mapping);
    session.columnMapping = body.mapping;
    session.requestLines = requestLines;
    session.comparison = undefined;
    saveSession(session);
    return NextResponse.json({ session });
  } catch (err) {
    const message = err instanceof Error ? err.message : "Erro ao mapear colunas";
    return NextResponse.json({ error: message }, { status: 400 });
  }
}
