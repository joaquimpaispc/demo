import type { Metadata } from "next";
import { Source_Sans_3 } from "next/font/google";
import "./globals.css";

const sourceSans = Source_Sans_3({
  variable: "--font-app",
  subsets: ["latin"],
  weight: ["400", "600", "700"],
});

export const metadata: Metadata = {
  title: "Cotai — Comparador de propostas | xAICMP",
  description:
    "Carregue a requisição em Excel e propostas PDF. Cotai recomenda a melhor opção com pontuação transparente.",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="pt">
      <body className={`${sourceSans.variable} antialiased`}>{children}</body>
    </html>
  );
}
