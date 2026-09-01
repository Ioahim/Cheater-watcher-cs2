import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";

// Runtime-configurable backend target. In Docker this is set via the API_UPSTREAM
// environment variable; locally it defaults to the .NET API.
const upstream = (process.env.API_UPSTREAM ?? "http://localhost:5089").replace(/\/+$/, "");

export async function proxy(request: NextRequest) {
  const url = new URL(
    request.nextUrl.pathname + request.nextUrl.search,
    upstream,
  );

  const headers = new Headers(request.headers);
  headers.set("host", new URL(upstream).host);
  headers.delete("connection");

  const init: RequestInit = { method: request.method, headers };
  if (request.method !== "GET" && request.method !== "HEAD") {
    const duplexInit = init as RequestInit & { duplex?: "half" };
    duplexInit.body = request.body;
    duplexInit.duplex = "half";
  }

  const response = await fetch(url.toString(), init);
  const responseHeaders = new Headers(response.headers);
  responseHeaders.delete("content-encoding");
  responseHeaders.delete("content-length");

  return new NextResponse(response.body, {
    status: response.status,
    statusText: response.statusText,
    headers: responseHeaders,
  });
}

export const config = {
  matcher: "/api/:path*",
};
