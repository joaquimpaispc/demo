import type { Metadata } from "next";
import { SugestaoFornecedor } from "@/components/SugestaoFornecedor";

export const metadata: Metadata = {
  title: "Sugestão requisição 56749 | Cotai",
  description:
    "Sugestão de fornecedor da requisição 56749: manter a encomenda 91171 à Chagas e repartir o resto.",
};

export default function SugestaoPage() {
  return <SugestaoFornecedor />;
}
