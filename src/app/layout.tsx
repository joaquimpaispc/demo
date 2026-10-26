import type { Metadata } from "next";
import { Sora, Syne } from "next/font/google";
import "./globals.css";

const sora = Sora({
  variable: "--font-sora",
  subsets: ["latin"],
  weight: ["300", "400", "500", "600", "700"],
});

const syne = Syne({
  variable: "--font-syne",
  subsets: ["latin"],
  weight: ["500", "600", "700", "800"],
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
      <body className={`${sora.variable} ${syne.variable} antialiased`}>
        {children}
      </body>
    </html>
  );
}
