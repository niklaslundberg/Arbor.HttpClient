#!/usr/bin/env python3
"""
Minimal caching HTTP proxy for Alpine's apk package manager.

apk fetches packages from an HTTP repository. This proxy:
  1. Forwards requests to the real Alpine CDN (https://dl-cdn.alpinelinux.org)
  2. Caches responses on disk so identical requests are served locally.

Usage:
    python3 scripts/apk-proxy.py <cache_dir> <port>

Example:
    python3 scripts/apk-proxy.py /tmp/alpine-mirror 8099

Configure apk inside the target system to use:
    http://127.0.0.1:<port>/v3.21/main
    http://127.0.0.1:<port>/v3.21/community
"""
import http.server, urllib.request, urllib.error, ssl, os, sys, hashlib

UPSTREAM = "https://dl-cdn.alpinelinux.org"


def main() -> None:
    cache_dir = os.path.realpath(sys.argv[1] if len(sys.argv) > 1 else "/tmp/alpine-mirror")
    port = int(sys.argv[2]) if len(sys.argv) > 2 else 8099

    class Handler(http.server.BaseHTTPRequestHandler):
        def do_HEAD(self) -> None:
            self._serve(head=True)

        def do_GET(self) -> None:
            self._serve(head=False)

        def _serve(self, *, head: bool) -> None:
            # Validate that the resolved path stays under cache_dir (prevent path traversal)
            local = os.path.realpath(os.path.join(cache_dir, self.path.lstrip("/")))
            if not local.startswith(cache_dir + os.sep) and local != cache_dir:
                self.send_error(400, "Invalid path")
                return
            if os.path.isdir(local):
                self.send_error(404, "Not a file")
                return
            if not os.path.exists(local):
                os.makedirs(os.path.dirname(local), exist_ok=True)
                url = UPSTREAM + self.path
                try:
                    ctx = ssl.create_default_context()
                    req = urllib.request.Request(
                        url, headers={"User-Agent": "curl/8.0"}
                    )
                    with urllib.request.urlopen(req, context=ctx, timeout=60) as resp:
                        data = resp.read()
                    with open(local, "wb") as f:
                        f.write(data)
                    print(f"  FETCH  {self.path}", flush=True)
                except urllib.error.HTTPError as exc:
                    # Forward upstream HTTP errors (404, 403, etc.) to the client
                    print(f"  UPSTREAM {exc.code}  {self.path}", flush=True)
                    body = exc.read()
                    self.send_response(exc.code)
                    self.send_header("Content-Length", str(len(body)))
                    self.end_headers()
                    if not head:
                        self.wfile.write(body)
                    return
                except Exception as exc:
                    print(f"  ERROR  {self.path}: {exc}", flush=True)
                    self.send_error(502, str(exc))
                    return
            else:
                print(f"  CACHE  {self.path}", flush=True)

            with open(local, "rb") as f:
                data = f.read()
            self.send_response(200)
            self.send_header("Content-Length", str(len(data)))
            self.end_headers()
            if not head:
                self.wfile.write(data)

        def log_message(self, *_) -> None:  # suppress default access log
            pass

    print(f"Alpine APK caching proxy  port={port}  cache={cache_dir}", flush=True)
    os.makedirs(cache_dir, exist_ok=True)
    with http.server.HTTPServer(("127.0.0.1", port), Handler) as srv:
        srv.serve_forever()


if __name__ == "__main__":
    main()
