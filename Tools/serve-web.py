#!/usr/bin/env python3
"""Serves a built Web player over plain HTTP for local testing.

Exists because the Unity build is a static directory that still needs correct MIME types, a
`Content-Encoding` header, and a trailing-slash-safe root; `python3 -m http.server` alone mislabels
`.unityweb` and `.wasm` and serves the Brotli payload with no encoding at all.
Binds every interface so a phone on the same network can reach it, and prints both the localhost
URL (a secure context, so getUserMedia works) and the LAN URL (plain HTTP, so it does not).
"""

import functools
import http.server
import socket
import socketserver
import sys
from pathlib import Path

PORT = int(sys.argv[1]) if len(sys.argv) > 1 else 8123
BUILD_DIRECTORY = Path(__file__).resolve().parent.parent / "dist"


class WebPlayerRequestHandler(http.server.SimpleHTTPRequestHandler):
    """Static handler for the Unity Web output.

    Adds the MIME types the loader expects, labels the Brotli payload as Brotli, and disables
    caching so a rebuilt player is picked up on refresh instead of served from the disk cache.
    """

    extensions_map = {
        **http.server.SimpleHTTPRequestHandler.extensions_map,
        ".wasm": "application/wasm",
        ".unityweb": "application/octet-stream",
        ".data": "application/octet-stream",
        ".symbols.json": "application/json",
    }

    def requested_path(self):
        """Reports the request path without its query string, for suffix tests."""
        return self.path.split("?", 1)[0]

    def guess_type(self, path):
        """Types a Brotli payload by what is inside it, not by the `.br` on the outside.

        `dist.wasm.br` has to arrive as `application/wasm` or the loader cannot use
        `instantiateStreaming` and silently falls back to a slower whole-buffer compile.
        """
        if path.endswith(".br"):
            return super().guess_type(path[: -len(".br")])
        return super().guess_type(path)

    def end_headers(self):
        self.send_header("Cache-Control", "no-store")
        # The build ships with decompressionFallback off, matching the parent's CDN, so the loader
        # has no JavaScript decompressor to fall back on: without this header the browser hands
        # raw Brotli to the wasm compiler and the player never starts. Unity names the payload
        # `.br` rather than `.unityweb` precisely because the fallback is off.
        if self.requested_path().endswith(".br"):
            self.send_header("Content-Encoding", "br")
        super().end_headers()

    def log_message(self, format_string, *arguments):
        sys.stderr.write("%s - %s\n" % (self.address_string(), format_string % arguments))


def local_network_address():
    """Reports the address this machine has on the LAN, for opening the build on a phone.

    Uses a connectionless UDP socket so it resolves the outbound interface without sending traffic.
    """
    probe = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    try:
        probe.connect(("8.8.8.8", 80))
        return probe.getsockname()[0]
    except OSError:
        return "127.0.0.1"
    finally:
        probe.close()


def main():
    """Entry point: serves `dist` until interrupted, after reporting both reachable URLs."""
    if not (BUILD_DIRECTORY / "index.html").exists():
        sys.exit(f"No build at {BUILD_DIRECTORY} - run `npm run build` first.")

    handler = functools.partial(WebPlayerRequestHandler, directory=str(BUILD_DIRECTORY))
    socketserver.TCPServer.allow_reuse_address = True
    with socketserver.ThreadingTCPServer(("0.0.0.0", PORT), handler) as server:
        print(f"Serving {BUILD_DIRECTORY}")
        print(f"  desktop:  http://localhost:{PORT}/      (secure context - microphone works)")
        print(f"  phone:    http://{local_network_address()}:{PORT}/   (plain HTTP - no microphone)")
        server.serve_forever()


if __name__ == "__main__":
    main()
