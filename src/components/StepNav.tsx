const STEPS = [
  "Carregar",
  "Rever dados",
  "Comparar",
  "Melhor opção",
] as const;

export function StepNav({ current }: { current: number }) {
  return (
    <nav
      aria-label="Progresso"
      className="flex flex-wrap items-center gap-3 text-sm text-[var(--mist)]"
    >
      {STEPS.map((label, i) => (
        <div key={label} className="flex items-center gap-2">
          <span className={`step-dot ${i <= current ? "active" : ""}`} />
          <span
            className={
              i === current ? "text-[var(--foam)] font-medium" : undefined
            }
          >
            {label}
          </span>
          {i < STEPS.length - 1 && (
            <span className="mx-1 hidden text-[var(--line)] sm:inline">/</span>
          )}
        </div>
      ))}
    </nav>
  );
}
