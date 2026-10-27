import Link from "next/link";
import {
  CAVEATS,
  CRITERIO,
  LINHAS,
  SUGESTAO,
  estadoLabel,
  type LinhaEstado,
} from "@/lib/sugestao-56749";

function badgeClass(estado: LinhaEstado): string {
  if (estado === "manter" || estado === "encomendar") return "badge badge-ok";
  if (estado === "confirmar") return "badge badge-warn";
  return "badge badge-bad";
}

export function SugestaoFornecedor() {
  const manter = LINHAS.filter((l) => l.estado === "manter");
  const resto = LINHAS.filter((l) => l.estado !== "manter");

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

      <header className="animate-rise flex flex-wrap items-start justify-between gap-4">
        <div>
          <p className="text-xs uppercase tracking-[0.22em] text-[var(--mist)]">
            Projecto xAICMP
          </p>
          <h1 className="font-display mt-2 text-4xl font-extrabold tracking-tight text-[var(--foam)] sm:text-6xl">
            Cotai
          </h1>
        </div>
        <Link href="/" className="btn-ghost text-sm no-underline">
          Comparador
        </Link>
      </header>

      <section className="animate-rise-delay-1 relative mt-10 sm:mt-14">
        <p className="text-xs uppercase tracking-[0.2em] text-[var(--lime)]">
          Recomendação · requisição {SUGESTAO.requisicao}
        </p>
        <h2 className="font-display mt-3 max-w-3xl text-2xl leading-tight tracking-tight text-[var(--foam)] sm:text-4xl">
          Manter a encomenda {SUGESTAO.encomenda} à Chagas, SA.{" "}
          <span className="text-[var(--lime)]">Repartir o resto.</span>
        </h2>
        <p className="mt-5 max-w-3xl text-base leading-relaxed text-[var(--foam)] sm:text-lg">
          {SUGESTAO.lead}
        </p>
        <p className="mt-4 max-w-3xl text-sm text-[var(--mist)]">
          {SUGESTAO.notaPrecos} {SUGESTAO.jaEncomendado} já encomendados à
          Chagas e {SUGESTAO.emAberto} nas linhas em aberto com referência de
          preço. Total {SUGESTAO.totalSemM20}, sem o M20.
        </p>
        <div className="accent-line mt-6 h-px w-40 bg-[var(--lime)]" />
        <p className="mt-4 text-sm text-[var(--mist)]">
          Obra {SUGESTAO.obra} · subobra {SUGESTAO.subobra} · {SUGESTAO.nLinhas}{" "}
          linhas · {SUGESTAO.pesoTotal} · certificado {SUGESTAO.certificado} ·{" "}
          {SUGESTAO.preparadoEm}
        </p>
      </section>

      <section className="mt-10 grid gap-4 sm:grid-cols-3">
        <article className="border-t border-[var(--lime)] pt-4">
          <p className="text-xs font-bold uppercase tracking-[0.16em] text-[var(--mist)]">
            Já na encomenda {SUGESTAO.encomenda}
          </p>
          <p className="font-display mt-2 text-3xl tracking-tight text-[var(--lime)]">
            {SUGESTAO.jaEncomendado}
          </p>
          <p className="mt-2 text-sm text-[var(--mist)]">
            Chagas, SA · tubo CHS, HEB120 e SHS 40×3 · previsão{" "}
            {SUGESTAO.previsaoEncomenda}
          </p>
        </article>
        <article className="border-t border-[var(--line)] pt-4">
          <p className="text-xs uppercase tracking-[0.16em] text-[var(--mist)]">
            Em aberto, com preço
          </p>
          <p className="font-display mt-2 text-3xl tracking-tight">
            {SUGESTAO.emAberto}
          </p>
          <p className="mt-2 text-sm text-[var(--mist)]">
            Justino, FAF, Ferromar e Arfixem
          </p>
        </article>
        <article className="border-t border-[var(--line)] pt-4">
          <p className="text-xs uppercase tracking-[0.16em] text-[var(--mist)]">
            Total indicativo
          </p>
          <p className="font-display mt-2 text-3xl tracking-tight">
            {SUGESTAO.totalSemM20}
          </p>
          <p className="mt-2 text-sm text-[var(--mist)]">
            Sem o varão M20, que não tem histórico
          </p>
        </article>
      </section>

      <section className="panel mt-16 py-10">
        <div className="mb-6 flex flex-wrap items-end justify-between gap-3">
          <div>
            <h3 className="font-display text-2xl tracking-tight">
              Linha a linha
            </h3>
            <p className="mt-2 max-w-2xl text-sm text-[var(--mist)]">
              Preços de referência do histórico de compras. A encomenda{" "}
              {SUGESTAO.encomenda} ({SUGESTAO.dataEncomenda}) ainda não tem
              receção.
            </p>
          </div>
        </div>
        <div className="table-wrap">
          <table className="data">
            <thead>
              <tr>
                <th>Material</th>
                <th>Qtd.</th>
                <th>Fornecedor</th>
                <th>Preço de referência</th>
                <th>Estimativa</th>
                <th>Prazo</th>
                <th>Estado</th>
              </tr>
            </thead>
            <tbody>
              {LINHAS.map((linha) => (
                <tr key={linha.material}>
                  <td>{linha.material}</td>
                  <td>{linha.quantidade}</td>
                  <td>{linha.fornecedor}</td>
                  <td>{linha.preco}</td>
                  <td>{linha.estimativa}</td>
                  <td>{linha.prazo}</td>
                  <td>
                    <span className={badgeClass(linha.estado)}>
                      {estadoLabel(linha.estado)}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <p className="mt-4 text-sm text-[var(--mist)]">
          {SUGESTAO.nomesLegais.map((n) => (
            <span key={n.curto} className="mr-4 inline-block">
              {n.curto} no ficheiro: «{n.legal}».
            </span>
          ))}
        </p>
      </section>

      <section className="mt-10 border-t border-[var(--line)] pt-10">
        <h3 className="font-display text-2xl tracking-tight">
          O que já está encomendado à Chagas
        </h3>
        <p className="mt-2 max-w-3xl text-sm text-[var(--mist)]">
          As três linhas estão na encomenda {SUGESTAO.encomenda}, de{" "}
          {SUGESTAO.dataEncomenda}, certificado tipo 2.2, ainda sem receção. A
          previsão de chegada é {SUGESTAO.previsaoEncomenda} (5 dias).
        </p>
        <ul className="mt-6 space-y-4">
          {manter.map((linha) => (
            <li
              key={linha.material}
              className="border-l-2 border-[var(--lime)] pl-3"
            >
              <p className="font-display text-lg">{linha.material}</p>
              <p className="mt-1 text-sm text-[var(--mist)]">
                {linha.quantidade} · {linha.preco} · {linha.estimativa} ·{" "}
                {linha.prazo}
              </p>
              <p className="mt-2 max-w-3xl text-sm leading-relaxed text-[var(--foam)]">
                {linha.detalhe}
              </p>
            </li>
          ))}
        </ul>
      </section>

      <section className="panel mt-10 py-10">
        <h3 className="font-display text-2xl tracking-tight">
          Linhas a encomendar
        </h3>
        <div className="mt-6 space-y-8">
          {resto.map((linha) => (
            <article key={linha.material} className="border-t border-[var(--line)] pt-5">
              <div className="flex flex-wrap items-baseline justify-between gap-2">
                <h4 className="font-display text-xl">{linha.material}</h4>
                <span className={badgeClass(linha.estado)}>
                  {estadoLabel(linha.estado)}
                </span>
              </div>
              <p className="mt-2 text-sm text-[var(--mist)]">
                {linha.fornecedor} · {linha.quantidade} · {linha.preco} ·{" "}
                {linha.estimativa} · {linha.prazo}
              </p>
              <p className="mt-3 max-w-3xl text-sm leading-relaxed">
                {linha.detalhe}
              </p>
            </article>
          ))}
        </div>
      </section>

      <section id="mudaria" className="mt-10 border-t border-[var(--line)] pt-10">
        <p className="text-xs uppercase tracking-[0.2em] text-[var(--lime)]">
          Ressalvas
        </p>
        <h3 className="font-display mt-2 text-3xl tracking-tight">
          O que mudaria o fornecedor
        </h3>
        <ol className="mt-6 max-w-3xl space-y-4">
          {CAVEATS.map((item, index) => (
            <li key={item.titulo} className="border-l-2 border-[var(--coral)] pl-3">
              <p className="font-display text-lg">
                {index + 1}. {item.titulo}
              </p>
              <p className="mt-1 text-sm leading-relaxed text-[var(--mist)]">
                {item.texto}
              </p>
            </li>
          ))}
        </ol>
      </section>

      <section className="mt-10 border-t border-[var(--line)] pt-10">
        <h3 className="font-display text-2xl tracking-tight">
          Como foi fechada a escolha
        </h3>
        <p className="mt-3 max-w-3xl text-sm leading-relaxed text-[var(--mist)]">
          O comparador Cotai cruza uma requisição Excel com propostas PDF
          (preço 45%, cobertura 30%, prazo 15%, quantidades 10%) e, em cada
          linha, fica com o mais barato entre quem cobre o artigo. Este
          resultado vem do histórico de compras, não de propostas. Entra quem
          cobre a linha:
        </p>
        <ul className="mt-4 max-w-3xl space-y-2 text-sm">
          {CRITERIO.map((item) => (
            <li key={item} className="border-l-2 border-[var(--line)] pl-3">
              {item}
            </li>
          ))}
        </ul>
        <p className="mt-4 max-w-3xl text-sm text-[var(--mist)]">
          Fonte: {SUGESTAO.fonte}. O pedido não traz fornecedor nem prazo
          preenchidos. Cada artigo junta até 10 encomendas anteriores, ordenadas
          pelo preço de tabela mais baixo.
        </p>
      </section>

      <footer className="mt-20 border-t border-[var(--line)] pt-6 text-sm text-[var(--mist)]">
        Cotai · Projecto xAICMP · Joaquim Pais · sugestão da requisição{" "}
        {SUGESTAO.requisicao} a partir do histórico de compras
      </footer>
    </div>
  );
}
