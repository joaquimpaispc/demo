import { NextResponse } from "next/server";
import { compareProposals } from "@/lib/compare";
import { getSession, saveSession } from "@/lib/session-store";

export const runtime = "nodejs";

export async function POST(request: Request) {
  try {
    const body = (await request.json()) as { sessionId?: string };
    if (!body.sessionId) {
      return NextResponse.json({ error: "sessionId em falta" }, { status: 400 });
    }
    const session = getSession(body.sessionId);
    if (!session) {
      return NextResponse.json({ error: "Sessão não encontrada" }, { status: 404 });
    }

    const comparison = compareProposals(
      session.requestLines,
      session.proposals
    );
    session.comparison = comparison;
    saveSession(session);
    return NextResponse.json({ session, comparison });
  } catch (err) {
    const message = err instanceof Error ? err.message : "Erro na comparação";
    return NextResponse.json({ error: message }, { status: 400 });
  }
}
