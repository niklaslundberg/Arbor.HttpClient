# Arbor.HttpClient Demo Server

This local demo server is embedded in the desktop app and is intended for testing requests without external dependencies.

## Base URLs

- HTTP: `http://localhost:5999`
- HTTPS: `https://localhost:5998` *(if HTTPS is enabled in Options > HTTP > Demo Server)*

## Endpoints

### `GET /status`
Returns JSON metadata about the demo server version, active ports, and available endpoints.

### `GET|POST|PUT|DELETE /echo`
Echo endpoint for regular HTTP requests.

- If a request body is sent, the body is echoed back with the same content type.
- If no body is sent, the response contains a JSON object with method, path, query, and timestamp.

### `GET /sse`
Server-Sent Events endpoint.

- Streams 5 events, one every 500ms.
- Response content type: `text/event-stream`.

### `GET /ws` (WebSocket upgrade)
WebSocket echo endpoint.

- Connect with request type **WS**.
- Each text frame sent by the client is echoed back.

### `GET /docs`
Returns this documentation as Markdown (`text/markdown`).

### `GET /docs.html`
Returns this documentation rendered as HTML for easier reading in web views.

## Example requests

```http
GET {{baseUrl}}/status
```

```http
POST {{baseUrl}}/echo
Content-Type: application/json

{"hello":"world"}
```

```http
GET {{baseUrl}}/sse
```

```http
GET {{baseUrl}}/docs
```

```http
GET {{baseUrl}}/docs.html
```
