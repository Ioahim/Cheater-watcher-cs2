import type { Metadata } from "next";
import localFont from "next/font/local";
import { Footer } from "@/components/footer";
import "./globals.css";

const departureMono = localFont({
  src: "./fonts/DepartureMono-Regular.woff2",
  weight: "400",
  variable: "--font-departure-mono",
  display: "swap",
});

export const metadata: Metadata = {
  title: "Cheater Watcher CS2",
  description:
    "Track your CS2 matches, manage your accounts and flag suspicious players.",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html
      lang="en"
      className={`${departureMono.variable} h-full antialiased`}
    >
      <body className="min-h-full flex flex-col">
        {children}
        <Footer />
      </body>
    </html>
  );
}
