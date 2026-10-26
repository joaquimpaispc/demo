import { NextResponse } from "next/server";
import { parseProposalPdf } from "@/lib/pdf-extract";
import { getSession, saveSession, touchOrCreate } from "@/lib/session-store";

export const runtime = "nodejs";

export async function POST(request: Request) {
  try {
    const form = await request.formData();
    const sessionId = form.get("sessionId");
    const files = form.getAll("files").filter((f): f is File => f instanceof File);

    if (!files.length) {
      const single = form.get("file");
      if (single instanceof File) files.push(single);
    }

    if (!files.length) {
      return NextResponse.json(
        { error: "Envie pelo menos um PDF de proposta." },
        { status: 400 }
      );
    }

    const session = touchOrCreate(
      typeof sessionId === "string" ? sessionId : null
    );

    const added = [];
    for (const file of files) {
      if (!file.name.toLowerCase().endsWith(".pdf")) {
        return NextResponse.json(
          { error: `Ficheiro inválido: ${file.name}. Apenas PDF.` },
          { status: 400 }
        );
      }
      const buffer = Buffer.from(await file.arrayBuffer());
      const proposal = await parseProposalPdf(buffer, file.name);
      // Replace same filename if re-uploaded
      session.proposals = session.proposals.filter(
        (p) => p.filename !== proposal.filename
      );
      session.proposals.push(proposal);
      added.push(proposal);
    }

    session.comparison = undefined;
    saveSession(session);

    return NextResponse.json({ session, added });
  } catch (err) {
    const message = err instanceof Error ? err.message : "Erro ao ler PDF";
    return NextResponse.json({ error: message }, { status: 400 });
  }
}

export async function DELETE(request: Request) {
  const { searchParams } = new URL(request.url);
  const sessionId = searchParams.get("sessionId");
  const proposalId = searchParams.get("proposalId");
  if (!sessionId || !proposalId) {
    return NextResponse.json({ error: "Parâmetros em falta" }, { status: 400 });
  }
  const session = getSession(sessionId);
  if (!session) {
    return NextResponse.json({ error: "Sessão não encontrada" }, { status: 404 });
  }
  session.proposals = session.proposals.filter((p) => p.id !== proposalId);
  session.comparison = undefined;
  saveSession(session);
  return NextResponse.json({ session });
}
