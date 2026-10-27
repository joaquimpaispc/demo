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
      className={`dropzone block ${active ? "is-active" : ""} ${
        disabled ? "pointer-events-none opacity-50" : ""
      }`}
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
      <p>
        <strong>{label}</strong>
      </p>
      <p className="hint">{hint}</p>
    </label>
  );
}
