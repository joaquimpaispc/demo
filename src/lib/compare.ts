import type {
  ComparisonResult,
  LineMatch,
  RequestLine,
  SupplierProposalDoc,
  SupplierScore,
} from "./types";
import { similarity } from "./normalize";

const MATCH_THRESHOLD = 0.45;
const PARTIAL_THRESHOLD = 0.32;

function bestMatch(
  request: RequestLine,
  proposal: SupplierProposalDoc,
  used: Set<string>
): LineMatch {
  let bestId = "";
  let bestScore = -1;
  let bestIdx = -1;
  proposal.lines.forEach((line, idx) => {
    if (used.has(line.id)) return;
    const score = similarity(
      `${request.description} ${request.specs || ""} ${request.code || ""}`,
      line.description
    );
    if (score > bestScore) {
      bestId = line.id;
      bestScore = score;
      bestIdx = idx;
    }
  });

  if (bestIdx < 0 || bestScore < PARTIAL_THRESHOLD) {
    return {
      requestLineId: request.id,
      status: "missing",
      similarity: Math.max(0, bestScore),
      requestDescription: request.description,
      requestQty: request.quantity,
      notes: "Item não encontrado na proposta.",
    };
  }

  const line = proposal.lines[bestIdx];
  used.add(bestId);

  const qty = line.quantity;
  let status: LineMatch["status"] =
    bestScore >= MATCH_THRESHOLD ? "matched" : "partial";
  let notes: string | undefined;

  if (qty != null && request.quantity > 0) {
    const ratio = qty / request.quantity;
    if (ratio < 0.85 || ratio > 1.15) {
      status = "qty_mismatch";
      notes = `Quantidade proposta (${qty}) difere do pedido (${request.quantity}).`;
    }
  }

  const unitPrice =
    line.unitPrice ??
    (line.totalPrice && (qty || request.quantity)
      ? line.totalPrice / (qty || request.quantity)
      : undefined);
  const totalPrice =
    line.totalPrice ??
    (unitPrice != null ? unitPrice * request.quantity : undefined);

  return {
    requestLineId: request.id,
    proposalLineId: line.id,
    status,
    similarity: bestScore,
    requestDescription: request.description,
    proposalDescription: line.description,
    requestQty: request.quantity,
    proposalQty: qty,
    unitPrice,
    totalPrice,
    leadTimeDays: line.leadTimeDays ?? proposal.leadTimeDays,
    notes,
  };
}

function scoreSupplier(
  proposal: SupplierProposalDoc,
  requestLines: RequestLine[]
): SupplierScore {
  const used = new Set<string>();
  const matches = requestLines.map((r) => bestMatch(r, proposal, used));

  const priced = matches.filter((m) => m.unitPrice != null && m.status !== "missing");
  const estimatedTotal = matches.reduce((sum, m) => {
    if (m.status === "missing") return sum;
    if (m.totalPrice != null) return sum + m.totalPrice;
    if (m.unitPrice != null) return sum + m.unitPrice * m.requestQty;
    return sum;
  }, 0);

  const matchedCount = matches.filter(
    (m) => m.status === "matched" || m.status === "partial"
  ).length;
  const missingCount = matches.filter((m) => m.status === "missing").length;
  const qtyMismatchCount = matches.filter((m) => m.status === "qty_mismatch").length;

  const completenessScore =
    requestLines.length === 0
      ? 0
      : (matchedCount + qtyMismatchCount * 0.5) / requestLines.length;

  const qtyScore =
    requestLines.length === 0
      ? 0
      : 1 - qtyMismatchCount / requestLines.length;

  const leadTimes = matches
    .map((m) => m.leadTimeDays)
    .filter((d): d is number => d != null && d > 0);
  const avgLeadTimeDays = leadTimes.length
    ? leadTimes.reduce((a, b) => a + b, 0) / leadTimes.length
    : proposal.leadTimeDays;

  return {
    proposalId: proposal.id,
    supplierName: proposal.supplierName,
    filename: proposal.filename,
    totalScore: 0,
    priceScore: 0,
    completenessScore,
    leadTimeScore: 0,
    qtyScore,
    matchedCount,
    missingCount,
    qtyMismatchCount,
    estimatedTotal,
    currency: proposal.currency || "EUR",
    avgLeadTimeDays,
    matches,
    rationale: [],
  };
}

/**
 * Build a comparable basket: for missing lines, impute the highest unit
 * price seen among other suppliers × qty × 1.15 (penalty for gaps).
 */
function comparableTotals(
  scores: SupplierScore[],
  requestLines: RequestLine[]
): number[] {
  return scores.map((s) => {
    let total = 0;
    for (const req of requestLines) {
      const mine = s.matches.find((m) => m.requestLineId === req.id);
      if (mine && mine.status !== "missing" && (mine.totalPrice != null || mine.unitPrice != null)) {
        total +=
          mine.totalPrice ??
          (mine.unitPrice != null ? mine.unitPrice * req.quantity : 0);
        continue;
      }
      const peers = scores
        .filter((o) => o.proposalId !== s.proposalId)
        .map((o) =>
          o.matches.find(
            (m) =>
              m.requestLineId === req.id &&
              m.status !== "missing" &&
              m.unitPrice != null
          )
        )
        .filter((m): m is NonNullable<typeof m> => !!m);
      if (!peers.length) continue;
      const maxUnit = Math.max(...peers.map((p) => p.unitPrice!));
      total += maxUnit * req.quantity * 1.15;
    }
    return total;
  });
}

function relativePriceScores(
  scores: SupplierScore[],
  requestLines: RequestLine[]
): void {
  const totals = comparableTotals(scores, requestLines);
  const positive = totals.filter((t) => t > 0);
  if (!positive.length) {
    scores.forEach((s) => {
      s.priceScore = 0;
    });
    return;
  }
  const min = Math.min(...positive);
  const max = Math.max(...positive);
  scores.forEach((s, i) => {
    const t = totals[i];
    if (t <= 0) {
      s.priceScore = 0;
      return;
    }
    if (max === min) {
      s.priceScore = 1;
      return;
    }
    s.priceScore = 1 - (t - min) / (max - min);
  });
}

function relativeLeadScores(scores: SupplierScore[]): void {
  const leads = scores
    .map((s) => s.avgLeadTimeDays)
    .filter((d): d is number => d != null && d > 0);
  if (!leads.length) {
    scores.forEach((s) => {
      s.leadTimeScore = 0.5; // neutral when unknown
    });
    return;
  }
  const min = Math.min(...leads);
  const max = Math.max(...leads);
  scores.forEach((s) => {
    if (s.avgLeadTimeDays == null) {
      s.leadTimeScore = 0.5;
      return;
    }
    if (max === min) {
      s.leadTimeScore = 1;
      return;
    }
    s.leadTimeScore = 1 - (s.avgLeadTimeDays - min) / (max - min);
  });
}

function buildRationale(score: SupplierScore, isWinner: boolean): string[] {
  const lines: string[] = [];
  if (isWinner) {
    lines.push(
      `${score.supplierName} é a melhor opção global com pontuação ${Math.round(score.totalScore * 100)}/100.`
    );
  }
  lines.push(
    `Cobertura: ${score.matchedCount} linha(s) correspondente(s), ${score.missingCount} em falta` +
      (score.qtyMismatchCount
        ? `, ${score.qtyMismatchCount} com quantidade diferente`
        : "") +
      "."
  );
  if (score.estimatedTotal > 0) {
    lines.push(
      `Total estimado (linhas encontradas): ${score.estimatedTotal.toLocaleString("pt-PT", {
        style: "currency",
        currency: score.currency || "EUR",
      })}.`
    );
  }
  if (score.avgLeadTimeDays != null) {
    lines.push(`Prazo médio estimado: ${Math.round(score.avgLeadTimeDays)} dias.`);
  }
  lines.push(
    `Critérios: preço ${Math.round(score.priceScore * 100)}%, completude ${Math.round(score.completenessScore * 100)}%, prazo ${Math.round(score.leadTimeScore * 100)}%, quantidades ${Math.round(score.qtyScore * 100)}%.`
  );
  return lines;
}

export function compareProposals(
  requestLines: RequestLine[],
  proposals: SupplierProposalDoc[]
): ComparisonResult {
  if (!requestLines.length) {
    throw new Error("Não há linhas de requisição para comparar.");
  }
  if (!proposals.length) {
    throw new Error("Carregue pelo menos uma proposta PDF.");
  }

  const scores = proposals.map((p) => scoreSupplier(p, requestLines));
  relativePriceScores(scores, requestLines);
  relativeLeadScores(scores);

  // Weights: price 45%, completeness 30%, lead 15%, qty 10%
  scores.forEach((s) => {
    s.totalScore =
      s.priceScore * 0.45 +
      s.completenessScore * 0.3 +
      s.leadTimeScore * 0.15 +
      s.qtyScore * 0.1;
  });

  scores.sort((a, b) => b.totalScore - a.totalScore);
  const overall = scores[0];
  scores.forEach((s) => {
    s.rationale = buildRationale(s, s.proposalId === overall.proposalId);
  });

  const byLine = requestLines.map((req) => {
    const candidates = scores
      .map((s) => {
        const m = s.matches.find((x) => x.requestLineId === req.id);
        return { score: s, match: m };
      })
      .filter(
        (c) =>
          c.match &&
          c.match.status !== "missing" &&
          c.match.unitPrice != null
      );

    if (!candidates.length) {
      return {
        requestLineId: req.id,
        description: req.description,
        reason: "Nenhuma proposta cobre este item com preço extraído.",
      };
    }

    candidates.sort(
      (a, b) => (a.match!.unitPrice || Infinity) - (b.match!.unitPrice || Infinity)
    );
    const best = candidates[0];
    return {
      requestLineId: req.id,
      description: req.description,
      winnerProposalId: best.score.proposalId,
      winnerSupplier: best.score.supplierName,
      unitPrice: best.match!.unitPrice,
      reason: `Menor preço unitário entre propostas com correspondência (${best.match!.unitPrice!.toLocaleString(
        "pt-PT",
        { style: "currency", currency: best.score.currency || "EUR" }
      )}).`,
    };
  });

  return {
    createdAt: new Date().toISOString(),
    winners: { overall, byLine },
    scores,
  };
}
