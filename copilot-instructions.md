# Trading Card Game Tracker – Copilot Quickstart

## Getting Started
1. **Clone the repository**
   ```bash
   git clone https://github.com/<your-account>/Trading-Card-Game-Tracker.git
   cd Trading-Card-Game-Tracker
   ```
2. **Install required SDKs**
   - .NET SDK 9 (global.json pins the version)
   - Node.js 20+
3. **Restore and build the API**
   ```bash
   dotnet build ./api/api.csproj
   ```
4. **Install client dependencies**
   ```bash
   pnpm install --filter client-vite...
   ```
5. **Run the API and client**
   ```bash
   dotnet run --project ./api/api.csproj
   pnpm --filter client-vite dev
   ```

## Coding Standards
- **C#**: Prefer expression-bodied members when terse, favor `async`/`await` over synchronous blocking, and keep DI-friendly constructors.
- **TypeScript/React**: Use functional components, hooks, and TypeScript types/interfaces. Avoid implicit `any` and prefer composition over inheritance.
- **ProblemDetails**: Surface validation and error information through RFC 7807 responses. Avoid returning raw exception messages to clients.
- **DTOs**: Keep API DTOs immutable (`record`/`readonly`), ensure naming consistency between API and client models, and avoid leaking EF entities.
- **Testing**: Write deterministic unit/integration tests. Prefer xUnit + WebApplicationFactory for API, and Vitest + React Testing Library for client.

## Security Rules
- Never commit real secrets or production configuration. Use environment variables or developer secrets locally.
- JWT signing key must be supplied through configuration or `JWT__KEY` environment variable outside local development.
- Tokens must include `sub`, `username`, and `is_admin` claims. Do not mint tokens without validating required fields.
- Always enforce administrator-only flows on the server; client-side checks are insufficient.

## Pull Request Guidance
- Keep diffs focused and well-scoped. Split unrelated changes into separate PRs.
- Include automated test coverage for new features or bug fixes.
- Use the commit message convention: `type(area): summary of change`.
- PR descriptions should summarize the change, list testing commands, and call out any follow-up work.
