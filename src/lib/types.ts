export type ColumnRole =
  | "description"
  | "quantity"
  | "unit"
  | "specs"
  | "code"
  | "ignore";

export interface ColumnMapping {
  [header: string]: ColumnRole;
}

export interface RequestLine {
  id: string;
  code?: string;
  description: string;
  quantity: number;
  unit?: string;
  specs?: string;
  raw: Record<string, string | number | null>;
}

export interface ProposalLine {
  id: string;
  description: string;
  quantity?: number;
  unit?: string;
  unitPrice?: number;
  totalPrice?: number;
  currency?: string;
  leadTimeDays?: number;
  conditions?: string;
  rawText?: string;
}

export interface SupplierProposalDoc {
  id: string;
  filename: string;
  supplierName: string;
  lines: ProposalLine[];
  currency?: string;
  leadTimeDays?: number;
  paymentTerms?: string;
  deliveryTerms?: string;
  rawText: string;
  extractedAt: string;
}

export type MatchStatus = "matched" | "partial" | "missing" | "qty_mismatch";

export interface LineMatch {
  requestLineId: string;
  proposalLineId?: string;
  status: MatchStatus;
  similarity: number;
  requestDescription: string;
  proposalDescription?: string;
  requestQty: number;
  proposalQty?: number;
  unitPrice?: number;
  totalPrice?: number;
  leadTimeDays?: number;
  notes?: string;
}

export interface SupplierScore {
  proposalId: string;
  supplierName: string;
  filename: string;
  totalScore: number;
  priceScore: number;
  completenessScore: number;
  leadTimeScore: number;
  qtyScore: number;
  matchedCount: number;
  missingCount: number;
  qtyMismatchCount: number;
  estimatedTotal: number;
  currency: string;
  avgLeadTimeDays?: number;
  matches: LineMatch[];
  rationale: string[];
}

export interface ComparisonResult {
  createdAt: string;
  winners: {
    overall?: SupplierScore;
    byLine: Array<{
      requestLineId: string;
      description: string;
      winnerProposalId?: string;
      winnerSupplier?: string;
      unitPrice?: number;
      reason: string;
    }>;
  };
  scores: SupplierScore[];
}

export interface SessionState {
  id: string;
  createdAt: string;
  updatedAt: string;
  excelFilename?: string;
  excelHeaders: string[];
  columnMapping: ColumnMapping;
  requestLines: RequestLine[];
  proposals: SupplierProposalDoc[];
  comparison?: ComparisonResult;
}
