"use client";

import { useEffect } from "react";

export default function GlobalError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  useEffect(() => {
    console.error(error);
  }, [error]);

  return (
    <html>
      <body
        style={{
          minHeight: "100vh",
          display: "flex",
          flexDirection: "column",
          alignItems: "center",
          justifyContent: "center",
          textAlign: "center",
          padding: "24px",
          fontFamily: "system-ui, sans-serif",
          color: "#1b4332",
          background: "#ffffff",
        }}
      >
        <h1 style={{ fontSize: "1.5rem", fontWeight: 700 }}>Something went wrong</h1>
        <p style={{ marginTop: "12px", maxWidth: "28rem", color: "rgba(27, 67, 50, 0.7)" }}>
          Adeni hit an unexpected error loading this page. Please try again.
        </p>
        <div style={{ marginTop: "24px", display: "flex", gap: "12px" }}>
          <a
            href="/"
            style={{
              borderRadius: "9999px",
              border: "1px solid rgba(27, 67, 50, 0.2)",
              padding: "10px 20px",
              fontSize: "0.875rem",
              fontWeight: 600,
              color: "#1b4332",
              textDecoration: "none",
            }}
          >
            Go home
          </a>
          <button
            type="button"
            onClick={reset}
            style={{
              borderRadius: "9999px",
              border: "none",
              padding: "10px 20px",
              fontSize: "0.875rem",
              fontWeight: 600,
              color: "#ffffff",
              background: "#1b4332",
              cursor: "pointer",
            }}
          >
            Try again
          </button>
        </div>
      </body>
    </html>
  );
}
