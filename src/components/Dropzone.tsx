"use client";

import { useCallback, useState } from "react";

interface DropzoneProps {
  accept: string;
  multiple?: boolean;
  label: string;
  hint: string;
  onFiles: (files: File[]) => void;
  disabled?: boolean;
}

export function Dropzone({
  accept,
  multiple,
  label,
  hint,
  onFiles,
  disabled,
}: DropzoneProps) {
  const [active, setActive] = useState(false);

  const handleFiles = useCallback(
    (list: FileList | null) => {
      if (!list?.length) return;
      onFiles(Array.from(list));
    },
    [onFiles]
  );

  return (
    <label
      className={`dropzone block cursor-pointer rounded-2xl px-6 py-10 text-center ${
        active ? "is-active" : ""
      } ${disabled ? "pointer-events-none opacity-50" : ""}`}
      onDragEnter={(e) => {
        e.preventDefault();
        setActive(true);
      }}
      onDragOver={(e) => {
        e.preventDefault();
        setActive(true);
      }}
      onDragLeave={() => setActive(false)}
      onDrop={(e) => {
        e.preventDefault();
        setActive(false);
        handleFiles(e.dataTransfer.files);
      }}
    >
      <input
        type="file"
        className="sr-only"
        accept={accept}
        multiple={multiple}
        disabled={disabled}
        onChange={(e) => handleFiles(e.target.files)}
      />
      <p className="font-display text-xl tracking-tight text-[var(--foam)]">
        {label}
      </p>
      <p className="mt-2 text-sm text-[var(--mist)]">{hint}</p>
    </label>
  );
}
