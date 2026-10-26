/**
 * Sugestão da requisição 56749.
 * Valores copiados de docs/sugestao-fornecedor.md (histórico de compras).
 * Não recalcular nem arredondar: os textos são a fonte.
 */

export type LinhaEstado = "manter" | "encomendar" | "confirmar" | "sem-historico";

export interface LinhaSugestao {
  material: string;
  quantidade: string;
  fornecedor: string;
  preco: string;
  estimativa: string;
  prazo: string;
  estado: LinhaEstado;
  detalhe: string;
}

export const SUGESTAO = {
  requisicao: "56749",
  obra: "3228",
  subobra: "3228.06",
  preparadoEm: "26 de outubro de 2026, 14:46 (hora de Lisboa)",
  fonte:
    "Cópia de SpaceXAI Hackaton.xlsx, folha «Queries Histórico Compras»",
  pesoTotal: "16.771,7 kg",
  nLinhas: 13,
  certificado: "tipo 2.2",
  jaEncomendado: "1.720,25 €",
  emAberto: "12.639,06 €",
  totalSemM20: "14.359,31 €",
  encomenda: "91171",
  previsaoEncomenda: "28/10/2026",
  dataEncomenda: "23/10/2026",
  lead: "Manter a encomenda 91171 à Chagas, SA (23/10/2026, previsão de chegada 28/10/2026) para o tubo CHS, o HEB120 e o SHS 40×3. Para o resto, repartir: o grosso do peso (HEA140 de 14 m) e o UPN160 à J. Justino das Neves, SA; o HEA160 de 12 m e a chapa 15 mm à FAF - Produtos Siderúrgicos, SA, que é também quem deve ser consultado para o HEA160 de 14 m e o IPE200 de 14 m; o SHS 50×3 à Ferromar; o varão M12 e M16 à Arfixem. O varão M20 não tem histórico neste ficheiro.",
  notaPrecos:
    "Estimativa indicativa, a preços líquidos já pagos (não é uma cotação).",
  nomesLegais: [
    {
      curto: "Ferromar",
      legal: "Ferromar-Comercio e Industria de Fernando de Pinho Teixeira, S.a",
    },
    { curto: "Arfixem", legal: "Arfixem - Embalagem e Fixação Lda" },
  ],
} as const;

export const LINHAS: LinhaSugestao[] = [
  {
    material: "Tubo CHS Ø 114,3×3, S275JR, 11×12 m",
    quantidade: "132 ML",
    fornecedor: "Chagas, SA (enc. 91171)",
    preco: "8,1872 €/ML",
    estimativa: "1.080,71 €",
    prazo: "5 dias, previsão 28/10/2026",
    estado: "manter",
    detalhe:
      "Líquido 8,1872 €/ML (tabela 12,04 €/ML com 32% de desconto), 1,023 €/kg, total 1.080,71 €. É a única compra desta especificação no ficheiro. A outra linha do bloco é da J. Vilanova (05/08/2025): CHS 114,3×3,6 em P235GH, 13,90 €/ML, certificado 3.1 — parede e qualidade diferentes.",
  },
  {
    material: "HEA140, S275JR, 14 m",
    quantidade: "28 un (9.682,4 kg)",
    fornecedor: "J. Justino das Neves, SA",
    preco: "0,8357 €/kg",
    estimativa: "8.091,20 €",
    prazo: "2 dias, recebido",
    estado: "encomendar",
    detalhe:
      "Cerca de 58% do peso do pedido. Encomenda 90618, 28/09/2026: uma barra HEA140 S275JR+AR de 14,1 m, 290,81 €, 348 kg, 0,8357 €/kg, certificado tipo 2.2, prazo previsto de 4 dias e receção em 2 dias (30/09/2026). O pedido de origem era de 13 m; a barra entregue foi de 14,1 m. Aplicado aos 9.682,4 kg: 8.091,20 €. O histórico é de uma barra; confirmar stock para 28 barras. Se 14 m não se conseguir, a alternativa com volume é a Ferlito - Ferros do Litoral, SA (encomenda 89970, 24/08/2026, 17 barras de 11,0 m, 0,8275 €/kg, certificado 2.2, previsão de 4 dias e receção ao 7.º dia). A Chagas tem S275JR sem «+AR» só numa barra de 12 m (encomenda 87802, 05/05/2026, 0,8323 €/kg, certificado 3.1, recebido em 3 dias). A Ferromar tem 0,820 €/kg em 18/09/2026, em S235JR e barra de 6 m.",
  },
  {
    material: "HEA160, S275JR, 14 m",
    quantidade: "2 un (851,2 kg)",
    fornecedor: "FAF — confirmar 14 m",
    preco: "0,8268 €/kg (barra de 12 m)",
    estimativa: "703,80 €",
    prazo: "2 dias previstos",
    estado: "confirmar",
    detalhe:
      "O bloco é o mesmo do HEA160 de 12 m (artigo 43), repetido no ficheiro. Não há compra recebida de HEA160 S275 em 14 m. A referência é a barra de 12 m da FAF, a 0,8268 €/kg. Sobre os 851,2 kg: 703,80 €, se o €/kg se mantiver em barra de 14 m. A FAF tem encomenda em aberto de HEA160 S235JR de 14,1 m (encomenda 90634, 28/09/2026, 0,825 €/kg, certificado 3.1, previsão 28/10/2026, 30 dias, por receber). A linha da Justino em S275JR de 13,1 m (encomenda 89771) está anulada e sem preço.",
  },
  {
    material: "HEA160, S275JR, 12 m",
    quantidade: "1 un (364,8 kg)",
    fornecedor: "FAF - Produtos Siderúrgicos, SA",
    preco: "0,8268 €/kg",
    estimativa: "301,63 €",
    prazo: "2 dias previstos",
    estado: "encomendar",
    detalhe:
      "Encomenda 90613, 28/09/2026: HEA160 S275JR de 12,0 m, 301,63 €/barra, 0,8268 €/kg, tabela 820 €/t, certificado tipo 2.2, 2 barras recebidas, prazo previsto de 2 dias (chegada prevista 30/09/2026). A data de receção não está preenchida; a linha está marcada como recebida. Para 1 barra: 301,63 €. A Antero tem S275JR em 11,0 m (0,8336 €/kg, certificado 3.1, recebido em 12 dias).",
  },
  {
    material: "HEB120, S275JR, 12 m",
    quantidade: "1 un (320,4 kg)",
    fornecedor: "Chagas, SA (enc. 91171)",
    preco: "0,850 €/kg",
    estimativa: "272,34 €",
    prazo: "5 dias, previsão 28/10/2026",
    estado: "manter",
    detalhe:
      "272,34 €, ou seja 0,850 €/kg (tabela 850 €/t). Não há compra anterior de HEB120 S275 em 12 m. A Chagas entregou S275JR em barra de 6 m a 02/09/2026 por 0,8595 €/kg (encomenda 90110, recebido em 2 dias). A barra de 12 m mais próxima é da Ferlito (18/09/2026, 0,8426 €/kg), em S235JR.",
  },
  {
    material: "IPE200, S275JR, 14 m",
    quantidade: "2 un (627,2 kg)",
    fornecedor: "FAF — confirmar 14 m",
    preco: "0,8218 €/kg (barra de 12 m)",
    estimativa: "515,43 €",
    prazo: "2 dias, recebido",
    estado: "confirmar",
    detalhe:
      "Não há IPE200 de 14 m no histórico. O melhor S275 recebido é da FAF: encomenda 89894, 19/08/2026, IPE200 S275JR de 12,0 m, 0,8218 €/kg, tabela 815 €/t, certificado 3.1, 4 barras, recebido em 2 dias (21/08/2026). Sobre os 627,2 kg: 515,43 €. Em 28/09/2026 a FAF entregou o mesmo perfil de 12 m com certificado tipo 2.2, a 0,8268 €/kg, recebido em 8 dias (encomenda 90613). A Ferromar tem S235JR de 12 m a 0,8181 €/kg (18/09/2026); a diferença nesta linha ronda 2,50 €.",
  },
  {
    material: "UPN160, S275JR, 12 m",
    quantidade: "2 un (451,2 kg)",
    fornecedor: "J. Justino das Neves, SA",
    preco: "0,7744 €/kg",
    estimativa: "349,41 €",
    prazo: "4 dias, recebido",
    estado: "encomendar",
    detalhe:
      "Encomenda 89531, 30/07/2026: UPN160 S275JR de 12,0 m, 174,705 €/barra, 0,7744 €/kg, tabela 768 €/t, certificado 3.1, 10 barras, recebido em 4 dias (03/08/2026). Para 2 barras: 349,41 €. A Antero tem a mesma especificação (encomenda 87745, 03/05/2026, 0,8311 €/kg, 33 barras) com receção em 23 dias.",
  },
  {
    material: "SHS 40×3, S275JR, 20×6 m",
    quantidade: "120 ML",
    fornecedor: "Chagas, SA (enc. 91171)",
    preco: "3,06 €/ML",
    estimativa: "367,20 €",
    prazo: "5 dias, previsão 28/10/2026",
    estado: "manter",
    detalhe:
      "Líquido 3,06 €/ML (tabela 4,50 €/ML com 32% de desconto), total 367,20 €, 2,41 kg/m. A Chagas já tinha fornecido o mesmo tubo S275JR a 24/03/2026 por 3,015 €/ML, certificado 2.2, recebido em 1 dia (encomenda 86950). A diferença nesta linha é 5,40 €.",
  },
  {
    material: "SHS 50×3, S275JR, 63×6 m",
    quantidade: "378 ML",
    fornecedor: "Ferromar",
    preco: "3,717 €/ML",
    estimativa: "1.405,03 €",
    prazo: "2 dias, recebido",
    estado: "encomendar",
    detalhe:
      "Encomenda 88233, 25/05/2026: SHS 50×3 S275JR, 714 ML, 3,717 €/ML, 0,8545 €/kg, 4,35 kg/m, certificado tipo 2.2, recebido em 2 dias (27/05/2026). Estimativa: 1.405,03 €. A Chagas entregou o mesmo S275JR a 25/08/2026 por 3,84 €/ML, 192 ML, certificado 2.2, recebido em 3 dias (encomenda 90004): mais 46,49 € nesta linha. Ficaram de fora preços de inox (Ferrus, Valsteel) e uma linha anulada da Portugal Alves. A Chagas em S235JR (12/10/2026, líquido 3,944 €/ML) é mais cara.",
  },
  {
    material: "Varão roscado M12, 8.8 Zn, 1 m",
    quantidade: "6 un",
    fornecedor: "Arfixem",
    preco: "1,4523 €/un",
    estimativa: "8,71 €",
    prazo: "3 dias, recebido",
    estado: "encomendar",
    detalhe:
      "Encomenda 89223, 17/07/2026: «Varão Roscado M12 8.8 Zn 1m», 1,4523 €, certificado tipo 2.2, 8 unidades, recebido em 3 dias. Para 6 unidades: 8,71 €. A Fabory, no mesmo comprimento, ficou em 1,79 € (certificado 3.1).",
  },
  {
    material: "Varão roscado M16, 8.8 Zn, 1 m",
    quantidade: "16 un",
    fornecedor: "Arfixem",
    preco: "2,4907 €/un",
    estimativa: "39,85 €",
    prazo: "3 dias, recebido",
    estado: "encomendar",
    detalhe:
      "Encomenda 89159, 14/07/2026: M16×1000, classe 8.8 zincado, 2,4907 €, certificado tipo 2.2, recebido em 3 dias. Para 16 unidades: 39,85 €. A Fabory ficou em 3,0869 € com certificado 3.1. A Fasterfix tem M16 a 2,24 € (encomenda 88672, 19/06/2026), certificado tipo 2.1 e receção em 11 dias. A diferença na linha seria cerca de 4 €.",
  },
  {
    material: "Varão roscado M20, 8.8 Zn, 1 m",
    quantidade: "21 un",
    fornecedor: "Sem histórico",
    preco: "—",
    estimativa: "—",
    prazo: "—",
    estado: "sem-historico",
    detalhe:
      "O artigo 1361 (21 unidades, 8.8 Zn, 1 m) não tem bloco de resultados. Não há preço para extrapolar. Consultar primeiro a Arfixem, que cobre M12 e M16 com certificado 2.2. A Fabory é a segunda referência: forneceu os dois diâmetros, com certificado 3.1 e preço mais alto.",
  },
  {
    material: "Chapa 15 mm, S275JR+AR, 6×2",
    quantidade: "1 un (1.413 kg)",
    fornecedor: "FAF - Produtos Siderúrgicos, SA",
    preco: "1.224,00 €/chapa",
    estimativa: "1.224,00 €",
    prazo: "3 dias, recebido",
    estado: "encomendar",
    detalhe:
      "Encomenda 90620, 28/09/2026: chapa 15 mm S275JR+AR, 6×2, 1.224,00 €, 1.413 kg, 0,8662 €/kg, tabela 850 €/t, certificado tipo 2.2, 2 chapas, recebido em 3 dias (01/10/2026). O pedido é 1 chapa igual: 1.224,00 €. A Chagas tem S275JR+AR a 0,8306 €/kg em 07/08/2026, em formato 3×1,5 m. A encomenda 87743 (03/05/2026), chapa 6×2 em S235JR a 429,30 € (0,304 €/kg) contra tabela de 820 €/t, não entra na conta. As outras chapas de 15 mm rondam 0,83 a 0,87 €/kg.",
  },
];

export const CAVEATS: { titulo: string; texto: string }[] = [
  {
    titulo: "S275JR+AR no HEA140",
    texto:
      "O pedido dos perfis diz S275JR; a compra da Justino (e a da Ferlito) diz S275JR+AR. A chapa do mesmo pedido é que pede S275JR+AR, portanto o ERP distingue os dois códigos. Se «+AR» não for aceite no HEA140, deixa de haver compra recebida de 14 m na qualidade exacta. A referência de S275JR sem «+AR» é a Chagas, numa barra de 12 m de Maio, a 0,8323 €/kg.",
  },
  {
    titulo: "Comprimento de 14 m",
    texto:
      "Se passarem a aceitar barras de 12 m no HEA140, o €/kg da Chagas (0,8323) fica cerca de 33 € abaixo do da Justino nesta linha, com a ressalva de ser uma barra só e de Maio. No HEA160 de 14 m e no IPE200 de 14 m a sugestão da FAF depende de confirmarem esse comprimento; o histórico só tem 12 m em S275.",
  },
  {
    titulo: "Preço da Ferromar no SHS 50×3 (25/05/2026)",
    texto:
      "Se estiver desatualizado, a Chagas de 25/08/2026 (3,84 €/ML, certificado 2.2, 192 ML, recebido em 3 dias) fica à frente. São 46,49 € de diferença na linha.",
  },
  {
    titulo: "O histórico está cortado",
    texto:
      "Cada artigo traz até 10 encomendas, as de menor ValorTabelaMinimo, mais a encomenda atual quando ela cai no bloco. Um fornecedor ausente deste excerto pode ser melhor. Os dois HEA160 partilham exatamente as mesmas 10 linhas.",
  },
  {
    titulo: "Capacidade do HEA140",
    texto:
      "A sugestão assenta numa barra. São 28 barras e 9,7 t. Se a Justino não confirmar, a conversa seguinte é com a Ferlito (17 barras, 11 m, S275JR+AR, 0,8275 €/kg).",
  },
  {
    titulo: "Certificado 2.1 no varão",
    texto:
      "Se o tipo 2.1 chegar para o M16, a Fasterfix a 2,24 € fica à frente da Arfixem a 2,4907 €.",
  },
  {
    titulo: "Preços históricos não são proposta",
    texto:
      "A estimativa multiplica o €/kg (ou o €/ML, ou o preço unitário quando a peça é igual) pela quantidade pedida. O mercado desde a última compra, o corte de barras de 14 m e o desconto da Chagas (32% no CHS e no SHS 40×3) podem sair diferentes numa cotação nova.",
  },
];

export const CRITERIO = [
  "Qualidade S275 (S275JR e S275JR+AR tratados como a mesma família nos perfis; na chapa o pedido pede S275JR+AR).",
  "No varão, classe 8.8 zincada e comprimento de 1 m.",
  "Numa barra, comprimento histórico de pelo menos 98% do pedido (uma barra de 12 m não cumpre um pedido de 14 m).",
  "Nos tubos ao metro, o mesmo kg/m (tolerância de 10%).",
  "Certificado tipo 2.2 ou 3.1 (o 3.1 satisfaz o 2.2 pedido; o 2.1 não).",
  "Linha não anulada, com preço líquido e fora da encomenda 91171.",
];

export function estadoLabel(estado: LinhaEstado): string {
  switch (estado) {
    case "manter":
      return "Manter";
    case "encomendar":
      return "Encomendar";
    case "confirmar":
      return "Confirmar";
    default:
      return "Sem histórico";
  }
}
