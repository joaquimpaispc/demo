"use client";

import { useCallback, useMemo, useState } from "react";
import { Dropzone } from "@/components/Dropzone";
import { ClipboardIcon, RefreshIcon } from "@/components/ClipboardIcon";
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

type Tab = "formulario" | "listagem";

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

function statusClass(status: string) {
  if (status === "matched") return "badge badge-ok";
  if (status === "partial" || status === "qty_mismatch") return "badge badge-warn";
  return "badge badge-bad";
}

export function ComparadorApp() {
  const [session, setSession] = useState<SessionState | null>(null);
  const [tab, setTab] = useState<Tab>("formulario");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [mapping, setMapping] = useState<ColumnMapping>({});
  const [filter, setFilter] = useState("");
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [listView, setListView] = useState<"pedido" | "propostas" | "comparacao">(
    "pedido"
  );

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
        setListView("pedido");
        setTab("listagem");
        setSelectedId(data.session.requestLines[0]?.id ?? null);
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
        setListView("propostas");
        if (data.session.requestLines?.length) setTab("listagem");
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
      setListView("comparacao");
      setTab("listagem");
      setSelectedId(data.session.comparison?.scores[0]?.proposalId ?? null);
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
  const q = filter.trim().toLowerCase();

  const filteredRequestLines = useMemo(() => {
    const lines = session?.requestLines ?? [];
    if (!q) return lines;
    return lines.filter((l) =>
      [l.code, l.description, l.unit, l.specs]
        .filter(Boolean)
        .join(" ")
        .toLowerCase()
        .includes(q)
    );
  }, [session?.requestLines, q]);

  const filteredProposalRows = useMemo(() => {
    const rows =
      session?.proposals.flatMap((p) =>
        p.lines.map((line) => ({
          key: `${p.id}-${line.id}`,
          proposalId: p.id,
          supplier: p.supplierName,
          filename: p.filename,
          description: line.description,
          quantity: line.quantity,
          unitPrice: line.unitPrice,
          totalPrice: line.totalPrice,
          currency: p.currency,
        }))
      ) ?? [];
    if (!q) return rows;
    return rows.filter((r) =>
      [r.supplier, r.description, r.filename]
        .join(" ")
        .toLowerCase()
        .includes(q)
    );
  }, [session?.proposals, q]);

  const filteredScores = useMemo(() => {
    const scores = session?.comparison?.scores ?? [];
    if (!q) return scores;
    return scores.filter((s) =>
      [s.supplierName, s.filename, ...s.rationale]
        .join(" ")
        .toLowerCase()
        .includes(q)
    );
  }, [session?.comparison?.scores, q]);

  const recordCount = useMemo(() => {
    if (tab === "formulario") return { shown: 0, total: 0 };
    if (listView === "pedido") {
      return {
        shown: filteredRequestLines.length,
        total: session?.requestLines.length ?? 0,
      };
    }
    if (listView === "propostas") {
      const total =
        session?.proposals.reduce((n, p) => n + p.lines.length, 0) ?? 0;
      return { shown: filteredProposalRows.length, total };
    }
    return {
      shown: filteredScores.length,
      total: session?.comparison?.scores.length ?? 0,
    };
  }, [
    tab,
    listView,
    filteredRequestLines,
    filteredProposalRows,
    filteredScores,
    session,
  ]);

  const exportCsv = useCallback(() => {
    if (!session?.comparison) return;
    const rows = [
      ["Fornecedor", "Pontuação", "Total estimado", "Em falta", "Prazo médio"],
      ...session.comparison.scores.map((s) => [
        s.supplierName,
        String(Math.round(s.totalScore * 100)),
        String(s.estimatedTotal),
        String(s.missingCount),
        s.avgLeadTimeDays != null ? String(Math.round(s.avgLeadTimeDays)) : "",
      ]),
    ];
    const csv = rows.map((r) => r.map((c) => `"${c.replace(/"/g, '""')}"`).join(";")).join("\n");
    const blob = new Blob([csv], { type: "text/csv;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = "cotai-comparacao.csv";
    a.click();
    URL.revokeObjectURL(url);
  }, [session]);

  const refreshSession = useCallback(async () => {
    if (!session?.id) return;
    setBusy(true);
    try {
      const res = await fetch(`/api/session?id=${session.id}`);
      const data = await res.json();
      if (!res.ok) throw new Error(data.error || "Erro ao actualizar");
      setSession(data.session);
      setMapping(data.session.columnMapping || {});
    } catch (e) {
      setError(e instanceof Error ? e.message : "Erro ao actualizar");
    } finally {
      setBusy(false);
    }
  }, [session?.id]);

  return (
    <div className="app-shell">
      <header className="app-header">
        <div className="app-header__brand">
          <ClipboardIcon className="app-header__icon" />
          <h1 className="app-header__title">
            Cotai — comparação de propostas (requisição Excel e PDFs)
          </h1>
        </div>
        <nav className="app-header__tabs" aria-label="Vistas">
          <button
            type="button"
            className={`app-tab ${tab === "formulario" ? "is-active" : ""}`}
            onClick={() => setTab("formulario")}
          >
            Formulário
          </button>
          <button
            type="button"
            className={`app-tab ${tab === "listagem" ? "is-active" : ""}`}
            onClick={() => setTab("listagem")}
          >
            Listagem
          </button>
        </nav>
        <div className="app-header__win" aria-hidden>
          <span>─</span>
          <span>□</span>
          <span>×</span>
        </div>
      </header>

      <div className="app-filter">
        <input
          type="search"
          value={filter}
          onChange={(e) => setFilter(e.target.value)}
          placeholder="Filtrar linhas, fornecedores, descrições…"
          aria-label="Filtro"
        />
      </div>

      <main className="app-main">
        {error && (
          <p className="alert" role="alert">
            {error}
          </p>
        )}

        {tab === "formulario" && (
          <div className="form-panel">
            <div className="app-toolbar" style={{ margin: "-10px -10px 0", borderTop: 0 }}>
              <span className="app-toolbar__hint">
                Carregue a requisição e as propostas. Depois use Listagem para
                rever e comparar.
              </span>
              <button
                type="button"
                className="btn"
                disabled={busy || !session}
                onClick={applyMapping}
              >
                Aplicar mapeamento
              </button>
              <button
                type="button"
                className="btn btn-primary"
                disabled={!canCompare || busy}
                onClick={runCompare}
              >
                {busy ? "A processar…" : "Comparar propostas"}
              </button>
            </div>

            <div className="form-grid">
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

            {(session?.excelFilename || (session?.proposals.length ?? 0) > 0) && (
              <p className="muted" style={{ margin: 0 }}>
                {session?.excelFilename
                  ? `Excel: ${session.excelFilename} · ${session.requestLines.length} linha(s)`
                  : "Excel por carregar"}
                {" · "}
                {session?.proposals.length ?? 0} proposta(s) PDF
              </p>
            )}

            {session && session.excelHeaders.length > 0 && (
              <>
                <p className="section-label" style={{ margin: "0 -10px" }}>
                  Mapeamento de colunas
                </p>
                <div className="field-grid">
                  {session.excelHeaders.map((header) => (
                    <div className="field" key={header}>
                      <label htmlFor={`map-${header}`}>{header}</label>
                      <select
                        id={`map-${header}`}
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
                    </div>
                  ))}
                </div>
              </>
            )}

            {session?.proposals.map((p) => (
              <div key={p.id}>
                <p className="section-label" style={{ margin: "0 -10px" }}>
                  {p.supplierName}
                  <span>
                    {p.filename} · {p.lines.length} linha(s)
                    {p.leadTimeDays != null ? ` · prazo ~${p.leadTimeDays} d` : ""}
                  </span>
                  <button
                    type="button"
                    className="btn-link"
                    style={{ float: "right" }}
                    onClick={() => removeProposal(p.id)}
                  >
                    Remover
                  </button>
                </p>
              </div>
            ))}
          </div>
        )}

        {tab === "listagem" && (
          <>
            <div className="app-toolbar">
              <span className="app-toolbar__hint">
                {listView === "pedido" && "Linhas da requisição"}
                {listView === "propostas" && "Linhas extraídas das propostas"}
                {listView === "comparacao" &&
                  (winner
                    ? `Melhor opção: ${winner.supplierName} (${pct(winner.totalScore)}/100)`
                    : "Comparação entre fornecedores")}
              </span>
              <button
                type="button"
                className={`btn ${listView === "pedido" ? "btn-primary" : ""}`}
                onClick={() => setListView("pedido")}
              >
                Pedido
              </button>
              <button
                type="button"
                className={`btn ${listView === "propostas" ? "btn-primary" : ""}`}
                onClick={() => setListView("propostas")}
              >
                Propostas
              </button>
              <button
                type="button"
                className={`btn ${listView === "comparacao" ? "btn-primary" : ""}`}
                onClick={() => setListView("comparacao")}
                disabled={!session?.comparison}
              >
                Comparação
              </button>
              <button
                type="button"
                className="btn btn-primary"
                disabled={!canCompare || busy}
                onClick={runCompare}
              >
                {busy ? "A processar…" : "Comparar"}
              </button>
            </div>

            {listView === "pedido" && (
              <div className="table-wrap">
                {filteredRequestLines.length === 0 ? (
                  <p className="empty-state">
                    Sem linhas. Carregue um Excel no separador Formulário.
                  </p>
                ) : (
                  <table className="erp">
                    <thead>
                      <tr>
                        <th>Código</th>
                        <th>Descrição</th>
                        <th>Qtd</th>
                        <th>Un.</th>
                        <th>Especificações</th>
                      </tr>
                    </thead>
                    <tbody>
                      {filteredRequestLines.map((line) => (
                        <tr
                          key={line.id}
                          className={selectedId === line.id ? "is-selected" : ""}
                          onClick={() => setSelectedId(line.id)}
                        >
                          <td>{line.code || "—"}</td>
                          <td>{line.description}</td>
                          <td>{line.quantity}</td>
                          <td>{line.unit || "—"}</td>
                          <td className="muted">{line.specs || "—"}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}
              </div>
            )}

            {listView === "propostas" && (
              <div className="table-wrap">
                {filteredProposalRows.length === 0 ? (
                  <p className="empty-state">
                    Sem propostas. Carregue PDFs no separador Formulário.
                  </p>
                ) : (
                  <table className="erp">
                    <thead>
                      <tr>
                        <th>Fornecedor</th>
                        <th>Item</th>
                        <th>Qtd</th>
                        <th>Preço un.</th>
                        <th>Total</th>
                        <th>Ficheiro</th>
                      </tr>
                    </thead>
                    <tbody>
                      {filteredProposalRows.map((row) => (
                        <tr
                          key={row.key}
                          className={selectedId === row.key ? "is-selected" : ""}
                          onClick={() => setSelectedId(row.key)}
                        >
                          <td>{row.supplier}</td>
                          <td>{row.description}</td>
                          <td>{row.quantity ?? "—"}</td>
                          <td>{money(row.unitPrice, row.currency)}</td>
                          <td>{money(row.totalPrice, row.currency)}</td>
                          <td className="muted">{row.filename}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}
              </div>
            )}

            {listView === "comparacao" && session?.comparison && (
              <>
                <div className="table-wrap">
                  <table className="erp">
                    <thead>
                      <tr>
                        <th>Fornecedor</th>
                        <th>Pontuação</th>
                        <th>Preço</th>
                        <th>Completude</th>
                        <th>Prazo</th>
                        <th>Qtd</th>
                        <th>Total est.</th>
                        <th>Em falta</th>
                        <th>Prazo méd.</th>
                      </tr>
                    </thead>
                    <tbody>
                      {filteredScores.map((s) => (
                        <tr
                          key={s.proposalId}
                          className={[
                            selectedId === s.proposalId ? "is-selected" : "",
                            winner?.proposalId === s.proposalId ? "is-winner" : "",
                          ]
                            .filter(Boolean)
                            .join(" ")}
                          onClick={() => setSelectedId(s.proposalId)}
                        >
                          <td>
                            {s.supplierName}
                            {winner?.proposalId === s.proposalId ? " ★" : ""}
                          </td>
                          <td>{pct(s.totalScore)}/100</td>
                          <td>{pct(s.priceScore)}%</td>
                          <td>{pct(s.completenessScore)}%</td>
                          <td>{pct(s.leadTimeScore)}%</td>
                          <td>{pct(s.qtyScore)}%</td>
                          <td>{money(s.estimatedTotal, s.currency)}</td>
                          <td>{s.missingCount}</td>
                          <td>
                            {s.avgLeadTimeDays != null
                              ? `${Math.round(s.avgLeadTimeDays)} d`
                              : "—"}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>

                {winner && (
                  <>
                    <p className="section-label">
                      Melhor opção: {winner.supplierName}
                      <span>{pct(winner.totalScore)}/100</span>
                    </p>
                    <ul className="rationale">
                      {winner.rationale.map((r) => (
                        <li key={r}>{r}</li>
                      ))}
                    </ul>
                  </>
                )}

                {selectedId &&
                  (() => {
                    const score = session.comparison!.scores.find(
                      (s) => s.proposalId === selectedId
                    );
                    if (!score) return null;
                    const matches = q
                      ? score.matches.filter((m) =>
                          [
                            m.requestDescription,
                            m.proposalDescription,
                            m.notes,
                            m.status,
                          ]
                            .filter(Boolean)
                            .join(" ")
                            .toLowerCase()
                            .includes(q)
                        )
                      : score.matches;
                    return (
                      <>
                        <p className="section-label">
                          Detalhe: {score.supplierName}
                          <span>
                            {score.matchedCount} ok · {score.missingCount} em
                            falta · {score.qtyMismatchCount} qtd ≠
                          </span>
                        </p>
                        <div className="table-wrap">
                          <table className="erp">
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
                              {matches.map((m) => (
                                <tr key={m.requestLineId}>
                                  <td>{m.requestDescription}</td>
                                  <td>{m.proposalDescription || "—"}</td>
                                  <td>
                                    <span className={statusClass(m.status)}>
                                      {statusLabel(m.status)}
                                    </span>
                                  </td>
                                  <td>{money(m.unitPrice, score.currency)}</td>
                                  <td className="muted">
                                    {m.notes ||
                                      `${Math.round(m.similarity * 100)}% semelhança`}
                                  </td>
                                </tr>
                              ))}
                            </tbody>
                          </table>
                        </div>
                      </>
                    );
                  })()}

                {session.comparison.winners.byLine.length > 0 && (
                  <>
                    <p className="section-label">Melhor por linha</p>
                    <div className="table-wrap">
                      <table className="erp">
                        <thead>
                          <tr>
                            <th>Item pedido</th>
                            <th>Fornecedor</th>
                            <th>Preço un.</th>
                            <th>Porquê</th>
                          </tr>
                        </thead>
                        <tbody>
                          {session.comparison.winners.byLine
                            .filter(
                              (row) =>
                                !q ||
                                [row.description, row.winnerSupplier, row.reason]
                                  .filter(Boolean)
                                  .join(" ")
                                  .toLowerCase()
                                  .includes(q)
                            )
                            .map((row) => (
                              <tr key={row.requestLineId}>
                                <td>{row.description}</td>
                                <td>{row.winnerSupplier || "—"}</td>
                                <td>{money(row.unitPrice)}</td>
                                <td className="muted">{row.reason}</td>
                              </tr>
                            ))}
                        </tbody>
                      </table>
                    </div>
                  </>
                )}
              </>
            )}

            {listView === "comparacao" && !session?.comparison && (
              <p className="empty-state">
                Ainda sem comparação. Carregue dados e clique em Comparar.
              </p>
            )}
          </>
        )}
      </main>

      <footer className="app-footer">
        <div />
        <div className="app-footer__center">
          <button
            type="button"
            onClick={exportCsv}
            disabled={!session?.comparison}
            title={
              session?.comparison
                ? "Exportar resultados da comparação"
                : "Compare primeiro para exportar"
            }
          >
            Exportar Excel
          </button>
        </div>
        <div className="app-footer__right">
          <button
            type="button"
            className="app-footer__refresh"
            onClick={refreshSession}
            disabled={!session || busy}
            title="Actualizar"
            aria-label="Actualizar"
          >
            <RefreshIcon />
          </button>
          <span className="app-footer__count">
            {recordCount.shown} de {recordCount.total}
          </span>
        </div>
      </footer>
    </div>
  );
}
