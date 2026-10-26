import { NextResponse } from "next/server";
import { createSession, getSession } from "@/lib/session-store";

export const runtime = "nodejs";

export async function POST() {
  const session = createSession();
  return NextResponse.json({ session });
}

export async function GET(request: Request) {
  const { searchParams } = new URL(request.url);
  const id = searchParams.get("id");
  if (!id) {
    return NextResponse.json({ error: "id em falta" }, { status: 400 });
  }
  const session = getSession(id);
  if (!session) {
    return NextResponse.json({ error: "Sessão não encontrada" }, { status: 404 });
  }
  return NextResponse.json({ session });
}
