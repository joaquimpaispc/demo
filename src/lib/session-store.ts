import { randomUUID } from "crypto";
import type { SessionState } from "./types";

const globalStore = globalThis as typeof globalThis & {
  __cotaiSessions?: Map<string, SessionState>;
};

function store(): Map<string, SessionState> {
  if (!globalStore.__cotaiSessions) {
    globalStore.__cotaiSessions = new Map();
  }
  return globalStore.__cotaiSessions;
}

export function createSession(): SessionState {
  const now = new Date().toISOString();
  const session: SessionState = {
    id: randomUUID(),
    createdAt: now,
    updatedAt: now,
    excelHeaders: [],
    columnMapping: {},
    requestLines: [],
    proposals: [],
  };
  store().set(session.id, session);
  return session;
}

export function getSession(id: string): SessionState | undefined {
  return store().get(id);
}

export function saveSession(session: SessionState): SessionState {
  session.updatedAt = new Date().toISOString();
  store().set(session.id, session);
  return session;
}

export function touchOrCreate(id?: string | null): SessionState {
  if (id) {
    const existing = getSession(id);
    if (existing) return existing;
  }
  return createSession();
}
