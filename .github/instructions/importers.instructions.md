---
applyTo: "api/Importing/**"
---

# Importers

- Implement source adapters through `ISourceImporter` and register each with a unique key in `ImporterRegistry`. Keep provider parsing, identity, and normalization in game-specific importers/catalogs rather than shared code.
- Replaying the same source data must be idempotent: resolve existing cards, printings, sets, and associations using that importer's identity rules instead of creating duplicates.
- Outbound HTTP: use `IHttpClientFactory` (typed or named clients), never `new HttpClient()`. Set an explicit `Timeout`, pass the `CancellationToken`, and use `HttpCompletionOption.ResponseHeadersRead` with a size cap for large downloads.
- SSRF: only fetch from hosts hard-coded or configured per importer (HTTPS only); never fetch a URL taken from request input or from scraped/imported content (including image URLs) without validating its host against that allow-list. Don't follow redirects to other hosts.
- Scraped HTML (AngleSharp) and JSON payloads are untrusted: extract text/attributes only, never persist raw HTML for rendering, and bound loop/recursion sizes.
