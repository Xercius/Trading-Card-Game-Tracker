# Copilot Instructions

## How to Start
- Install the .NET SDK specified in `global.json` (currently .NET 9) and Node.js 20+.
- Restore and build the API:
  ```bash
  dotnet restore ./api/api.csproj
  dotnet build ./api/api.csproj
  dotnet run --project ./api/api.csproj
  ```
- Install client dependencies and run the Vite dev server:
  ```bash
  pnpm install --filter client-vite...
  pnpm --filter client-vite dev
  ```

## Coding Standards
- **C#**: Favor async code paths, dependency injection, and small focused services. Keep DTOs immutable where possible.
- **TypeScript/React**: Use functional components, hooks, and explicit typings. Avoid implicit `any` and prefer composition.
- **ProblemDetails**: Surface validation/errors with RFC 7807 responses; never expose raw exception details.
- **Testing**: Add deterministic xUnit tests for backend changes and Vitest/RTL coverage for the client when applicable.

## Security Guidance
- Never trust the left-most `X-Forwarded-For` entry; resolve the effective client by walking from the right while skipping trusted proxies (`KnownProxies`/`KnownNetworks`) and respecting `ForwardLimit`.
- Do not rely on the `Host` header for authorization. Treat forwarded headers as trustworthy only when the caller is a configured proxy.
- Keep secrets and API keys out of source control; provide them via configuration or environment variables.

## Pull Request Guidance
- Keep diffs small and focused; split unrelated work into separate PRs.
- Include the commands used for testing in the PR description and ensure automated tests pass locally.
