/** @deprecated Replaced by FORMULÁRIO / LISTAGEM tabs in ComparadorApp */
export function StepNav({ current }: { current: number }) {
  const labels = ["Carregar", "Rever dados", "Comparar", "Melhor opção"];
  return (
    <nav aria-label="Progresso" className="muted">
      {labels.map((label, i) => (
        <span key={label} style={{ marginRight: 8 }}>
          {i === current ? <strong>{label}</strong> : label}
        </span>
      ))}
    </nav>
  );
}
