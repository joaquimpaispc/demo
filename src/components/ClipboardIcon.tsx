export function ClipboardIcon({ className }: { className?: string }) {
  return (
    <svg
      className={className}
      viewBox="0 0 24 24"
      width="18"
      height="18"
      aria-hidden
    >
      <rect
        x="6"
        y="3"
        width="12"
        height="18"
        rx="2"
        fill="#2e9b3a"
        stroke="#166b22"
        strokeWidth="1"
      />
      <rect x="8.5" y="1.5" width="7" height="3.5" rx="1" fill="#3cb54a" />
      <path
        d="M9 10.5h6M9 13.5h6M9 16.5h4"
        stroke="#fff"
        strokeWidth="1.6"
        strokeLinecap="round"
      />
    </svg>
  );
}

export function RefreshIcon({ className }: { className?: string }) {
  return (
    <svg
      className={className}
      viewBox="0 0 24 24"
      width="16"
      height="16"
      fill="none"
      aria-hidden
    >
      <path
        d="M20 12a8 8 0 1 1-2.2-5.5"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
      />
      <path
        d="M20 4v5h-5"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}
