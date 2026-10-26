"use client";

import { useCallback, useMemo, useState } from "react";
import { Dropzone } from "@/components/Dropzone";
import { StepNav } from "@/components/StepNav";
import { money, pct } from "@/lib/format";
import type {
  ColumnMapping,
  ColumnRole,
  SessionState,
} from "@/lib/types";

const ROLE_LABELS: Record<ColumnRole, string> = {
  description: "Descrição / item",
  quantity: "Quantidade",
  unit: "Unidade",
  specs: "Especificações",
  code: "Código / ref.",
  ignore: "Ignorar",
};

type Step = 0 | 1 | 2 | 3;

function statusBadge(status: string) {
  if (status === "matched") return "badge badge-ok";
  if (status === "partial" || status === "qty_mismatch")
    return "badge badge-warn";
  return "badge badge-bad";
}

function statusLabel(status: string) {
  switch (status) {
    case "matched":
      return "Correspondência";
    case "partial":
      return "Parcial";
    case "qty_mismatch":
      return "Qtd. diferente";
    default:
      return "Em falta";
  }
}

export function ComparadorApp() {
  const [session, setSession] = useState<SessionState | null>(null);
  const [step, setStep] = useState<Step>(0);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [mapping, setMapping] = useState<ColumnMapping>({});

  const ensureSession = useCallback(async () => {
    if (session?.id) return session.id;
    const res = await fetch("/api/session", { method: "POST" });
    const data = await res.json();
    if (!res.ok) throw new Error(data.error || "Falha ao criar sessão");
    setSession(data.session);
    return data.session.id as string;
  }, [session?.id]);

  const onExcel = useCallback(
    async (files: File[]) => {
      setError(null);
      setBusy(true);
      try {
        const id = await ensureSession();
        const form = new FormData();
        form.append("file", files[0]);
        form.append("sessionId", id);
        const res = await fetch("/api/excel", { method: "POST", body: form });
        const data = await res.json();
        if (!res.ok) throw new Error(data.error || "Erro no Excel");
        setSession(data.session);
        setMapping(data.session.columnMapping);
        setStep(1);
      } catch (e) {
        setError(e instanceof Error ? e.message : "Erro no Excel");
      } finally {
        setBusy(false);
      }
    },
    [ensureSession]
  );

  const applyMapping = useCallback(async () => {
    if (!session) return;
    setBusy(true);
    setError(null);
    try {
      const res = await fetch("/api/excel", {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          sessionId: session.id,
          mapping,
          rows: session.requestLines.map((l) => l.raw),
        }),
      });
      const data = await res.json();
      if (!res.ok) throw new Error(data.error || "Erro ao mapear");
      setSession(data.session);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Erro ao mapear");
    } finally {
      setBusy(false);
    }
  }, [session, mapping]);

  const onPdfs = useCallback(
    async (files: File[]) => {
      setError(null);
      setBusy(true);
      try {
        const id = await ensureSession();
        const form = new FormData();
        form.append("sessionId", id);
        files.forEach((f) => form.append("files", f));
        const res = await fetch("/api/pdf", { method: "POST", body: form });
        const data = await res.json();
        if (!res.ok) throw new Error(data.error || "Erro nos PDF");
        setSession(data.session);
        if (data.session.requestLines?.length) setStep(1);
      } catch (e) {
        setError(e instanceof Error ? e.message : "Erro nos PDF");
      } finally {
        setBusy(false);
      }
    },
    [ensureSession]
  );

  const removeProposal = useCallback(
    async (proposalId: string) => {
      if (!session) return;
      setBusy(true);
      try {
        const res = await fetch(
          `/api/pdf?sessionId=${session.id}&proposalId=${proposalId}`,
          { method: "DELETE" }
        );
        const data = await res.json();
        if (!res.ok) throw new Error(data.error || "Erro ao remover");
        setSession(data.session);
      } catch (e) {
        setError(e instanceof Error ? e.message : "Erro ao remover");
      } finally {
        setBusy(false);
      }
    },
    [session]
  );

  const runCompare = useCallback(async () => {
    if (!session) return;
    setBusy(true);
    setError(null);
    try {
      const res = await fetch("/api/compare", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ sessionId: session.id }),
      });
      const data = await res.json();
      if (!res.ok) throw new Error(data.error || "Erro na comparação");
      setSession(data.session);
      setStep(2);
      // brief pause then show winner section
      setTimeout(() => setStep(3), 80);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Erro na comparação");
    } finally {
      setBusy(false);
    }
  }, [session]);

  const canCompare = useMemo(
    () =>
      !!session &&
      session.requestLines.length > 0 &&
      session.proposals.length > 0,
    [session]
  );

  const winner = session?.comparison?.winners.overall;

  return (
    <div className="relative mx-auto min-h-screen w-full max-w-6xl px-5 pb-24 pt-6 sm:px-8">
      <div
        aria-hidden
        className="orbit-blob pointer-events-none absolute -right-16 top-24 h-64 w-64 rounded-full bg-[radial-gradient(circle,rgba(212,255,58,0.22),transparent_70%)] blur-2xl"
      />
      <div
        aria-hidden
        className="orbit-blob pointer-events-none absolute -left-20 top-[28rem] h-72 w-72 rounded-full bg-[radial-gradient(circle,rgba(255,92,58,0.16),transparent_70%)] blur-2xl"
        style={{ animationDelay: "2s" }}
      />

      <header className="animate-rise flex items-start justify-between gap-4">
        <div>
          <p className="text-xs uppercase tracking-[0.22em] text-[var(--mist)]">
            Projecto xAICMP
          </p>
          <h1 className="font-display mt-2 text-4xl font-extrabold tracking-tight text-[var(--foam)] sm:text-6xl">
            Cotai
          </h1>
        </div>
        <StepNav current={step} />
      </header>

      {/* Hero / upload composition */}
      <section className="animate-rise-delay-1 relative mt-10 sm:mt-14">
        <div className="max-w-2xl">
          <h2 className="font-display text-2xl leading-tight tracking-tight text-[var(--foam)] sm:text-3xl">
            Excel da requisição + PDFs dos fornecedores.{" "}
            <span className="text-[var(--lime)]">A melhor opção, com números.</span>
          </h2>
          <p className="mt-4 max-w-xl text-base text-[var(--mist)] sm:text-lg">
            Carregue as linhas a comprar e as propostas. Cotai faz o
            cruzamento, aponta lacunas e recomenda com pontuação transparente.
          </p>
          <div className="accent-line mt-6 h-px w-40 bg-[var(--lime)]" />
        </div>

        <div className="mt-10 grid gap-5 md:grid-cols-2">
          <Dropzone
            accept=".xlsx,.xls,.csv"
            label="Requisição Excel"
            hint="Solte o .xlsx ou clique para escolher"
            onFiles={onExcel}
            disabled={busy}
          />
          <Dropzone
            accept=".pdf"
            multiple
            label="Propostas PDF"
            hint="Um ou mais orçamentos de fornecedores"
            onFiles={onPdfs}
            disabled={busy}
          />
        </div>

        {(session?.excelFilename || session?.proposals.length) && (
          <p className="mt-4 text-sm text-[var(--mist)]">
            {session.excelFilename ? (
              <>
                Excel: <span className="text-[var(--foam)]">{session.excelFilename}</span>
                {" · "}
                {session.requestLines.length} linha(s)
              </>
            ) : (
              "Excel por carregar"
            )}
            {" · "}
            {session.proposals.length} proposta(s) PDF
          </p>
        )}
      </section>

      {error && (
        <p
          role="alert"
          className="mt-6 border-l-2 border-[var(--coral)] pl-3 text-[var(--coral)]"
        >
          {error}
        </p>
      )}

      {/* Review */}
      {session && session.requestLines.length > 0 && (
        <section id="rever" className="animate-rise-delay-2 panel mt-16 px-0 py-10">
          <div className="mb-6 flex flex-wrap items-end justify-between gap-4">
            <div>
              <h3 className="font-display text-2xl tracking-tight">
                Rever dados extraídos
              </h3>
              <p className="mt-2 max-w-xl text-sm text-[var(--mist)]">
                Confirme o mapeamento das colunas e as linhas das propostas
                antes de comparar.
              </p>
            </div>
            <div className="flex flex-wrap gap-2">
              <button
                type="button"
                className="btn-ghost"
                disabled={busy}
                onClick={applyMapping}
              >
                Aplicar mapeamento
              </button>
              <button
                type="button"
                className="btn-primary"
                disabled={!canCompare || busy}
                onClick={runCompare}
              >
                {busy ? "A processar…" : "Comparar propostas"}
              </button>
            </div>
          </div>

          <div className="mb-8 grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            {session.excelHeaders.map((header) => (
              <label key={header} className="block text-sm">
                <span className="mb-1 block text-[var(--mist)]">{header}</span>
                <select
                  className="mapping"
                  value={mapping[header] || "ignore"}
                  onChange={(e) =>
                    setMapping((m) => ({
                      ...m,
                      [header]: e.target.value as ColumnRole,
                    }))
                  }
                >
                  {(Object.keys(ROLE_LABELS) as ColumnRole[]).map((role) => (
                    <option key={role} value={role}>
                      {ROLE_LABELS[role]}
                    </option>
                  ))}
                </select>
              </label>
            ))}
          </div>

          <div className="table-wrap">
            <table className="data">
              <thead>
                <tr>
                  <th>Código</th>
                  <th>Descrição</th>
                  <th>Qtd</th>
                  <th>Un.</th>
                  <th>Specs</th>
                </tr>
              </thead>
              <tbody>
                {session.requestLines.map((line) => (
                  <tr key={line.id}>
                    <td>{line.code || "—"}</td>
                    <td>{line.description}</td>
                    <td>{line.quantity}</td>
                    <td>{line.unit || "—"}</td>
                    <td className="text-[var(--mist)]">{line.specs || "—"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="mt-10 space-y-6">
            {session.proposals.map((p) => (
              <div key={p.id}>
                <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
                  <div>
                    <h4 className="font-display text-lg">{p.supplierName}</h4>
                    <p className="text-sm text-[var(--mist)]">
                      {p.filename} · {p.lines.length} linha(s) extraída(s)
                      {p.leadTimeDays != null
                        ? ` · prazo ~${p.leadTimeDays} dias`
                        : ""}
                      {p.paymentTerms ? ` · ${p.paymentTerms}` : ""}
                    </p>
                  </div>
                  <button
                    type="button"
                    className="btn-ghost"
                    onClick={() => removeProposal(p.id)}
                  >
                    Remover
                  </button>
                </div>
                <div className="table-wrap">
                  <table className="data">
                    <thead>
                      <tr>
                        <th>Item</th>
                        <th>Qtd</th>
                        <th>Preço un.</th>
                        <th>Total</th>
                      </tr>
                    </thead>
                    <tbody>
                      {p.lines.length === 0 ? (
                        <tr>
                          <td colSpan={4} className="text-[var(--mist)]">
                            Não foi possível extrair linhas com preço. O PDF
                            pode ser imagem (sem texto) — tente um PDF com
                            texto seleccionável.
                          </td>
                        </tr>
                      ) : (
                        p.lines.map((line) => (
                          <tr key={line.id}>
                            <td>{line.description}</td>
                            <td>{line.quantity ?? "—"}</td>
                            <td>{money(line.unitPrice, p.currency)}</td>
                            <td>{money(line.totalPrice, p.currency)}</td>
                          </tr>
                        ))
                      )}
                    </tbody>
                  </table>
                </div>
              </div>
            ))}
            {!session.proposals.length && (
              <p className="text-[var(--mist)]">
                Ainda sem PDFs — carregue propostas acima.
              </p>
            )}
          </div>
        </section>
      )}

      {/* Comparison */}
      {session?.comparison && (
        <section id="comparar" className="panel mt-10 px-0 py-10">
          <h3 className="font-display text-2xl tracking-tight">
            Comparação entre fornecedores
          </h3>
          <p className="mt-2 max-w-2xl text-sm text-[var(--mist)]">
            Pontuação ponderada: preço 45%, completude 30%, prazo 15%,
            quantidades 10%. Valores relativos entre as propostas carregadas.
          </p>

          <div className="mt-8 space-y-5">
            {session.comparison.scores.map((s) => (
              <div key={s.proposalId} className="border-t border-[var(--line)] pt-5">
                <div className="flex flex-wrap items-baseline justify-between gap-2">
                  <h4 className="font-display text-xl">
                    {s.supplierName}
                    {winner?.proposalId === s.proposalId && (
                      <span className="ml-2 badge badge-ok">Melhor opção</span>
                    )}
                  </h4>
                  <p className="text-sm text-[var(--mist)]">
                    {pct(s.totalScore)}/100 · total est.{" "}
                    {money(s.estimatedTotal, s.currency)}
                  </p>
                </div>
                <div className="score-bar mt-3 max-w-md">
                  <span style={{ width: `${Math.max(4, s.totalScore * 100)}%` }} />
                </div>
                <div className="mt-3 flex flex-wrap gap-3 text-xs text-[var(--mist)]">
                  <span>Preço {pct(s.priceScore)}%</span>
                  <span>Completude {pct(s.completenessScore)}%</span>
                  <span>Prazo {pct(s.leadTimeScore)}%</span>
                  <span>Qtd {pct(s.qtyScore)}%</span>
                  <span>
                    {s.matchedCount} ok · {s.missingCount} em falta ·{" "}
                    {s.qtyMismatchCount} qtd ≠
                  </span>
                </div>

                <div className="table-wrap mt-4">
                  <table className="data">
                    <thead>
                      <tr>
                        <th>Pedido</th>
                        <th>Proposta</th>
                        <th>Estado</th>
                        <th>Preço un.</th>
                        <th>Nota</th>
                      </tr>
                    </thead>
                    <tbody>
                      {s.matches.map((m) => (
                        <tr key={m.requestLineId}>
                          <td>{m.requestDescription}</td>
                          <td>{m.proposalDescription || "—"}</td>
                          <td>
                            <span className={statusBadge(m.status)}>
                              {statusLabel(m.status)}
                            </span>
                          </td>
                          <td>{money(m.unitPrice, s.currency)}</td>
                          <td className="text-[var(--mist)]">
                            {m.notes || `${Math.round(m.similarity * 100)}% semelhança`}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            ))}
          </div>
        </section>
      )}

      {/* Winner */}
      {winner && session?.comparison && (
        <section id="melhor" className="mt-10 border-t border-[var(--line)] pt-10">
          <p className="text-xs uppercase tracking-[0.2em] text-[var(--lime)]">
            Recomendação
          </p>
          <h3 className="font-display mt-2 text-3xl tracking-tight sm:text-4xl">
            Melhor opção: {winner.supplierName}
          </h3>
          <ul className="mt-5 max-w-2xl space-y-2 text-[var(--foam)]">
            {winner.rationale.map((r) => (
              <li key={r} className="border-l-2 border-[var(--lime)] pl-3 text-sm sm:text-base">
                {r}
              </li>
            ))}
          </ul>

          <h4 className="font-display mt-10 text-xl">Melhor por linha</h4>
          <div className="table-wrap mt-4">
            <table className="data">
              <thead>
                <tr>
                  <th>Item pedido</th>
                  <th>Fornecedor</th>
                  <th>Preço un.</th>
                  <th>Porquê</th>
                </tr>
              </thead>
              <tbody>
                {session.comparison.winners.byLine.map((row) => (
                  <tr key={row.requestLineId}>
                    <td>{row.description}</td>
                    <td>{row.winnerSupplier || "—"}</td>
                    <td>{money(row.unitPrice)}</td>
                    <td className="text-[var(--mist)]">{row.reason}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      )}

      <footer className="mt-20 border-t border-[var(--line)] pt-6 text-sm text-[var(--mist)]">
        Cotai · Projecto xAICMP · Joaquim Pais · comparação heurística offline
        (sem API externa necessária)
      </footer>
    </div>
  );
}
