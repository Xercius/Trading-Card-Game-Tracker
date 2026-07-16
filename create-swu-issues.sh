#!/usr/bin/env bash
# =============================================================================
# create-swu-issues.sh
# Creates all GitHub issues for the Star Wars Unlimited TCG Sync Roadmap.
#
# Prerequisites:
#   - GitHub CLI (gh) installed: https://cli.github.com/
#   - Authenticated: run `gh auth login` first
#
# Usage:
#   chmod +x create-swu-issues.sh
#   ./create-swu-issues.sh
# =============================================================================

REPO="Xercius/Trading-Card-Game-Tracker"

echo "Creating SWU TCG Sync Roadmap issues in $REPO..."
echo ""

# ---------------------------------------------------------------------------
# PHASE 1: Foundation & Documentation
# ---------------------------------------------------------------------------

echo "[Phase 1] Creating foundation issues..."

gh issue create --repo "$REPO" \
  --title "[Phase 1 - Task 1.1] Create Star Wars Unlimited API Documentation File" \
  --label "documentation,docs" \
  --body "## Phase 1: Foundation & Documentation

### Task 1.1 — Create Star Wars Unlimited API Documentation File

**Type:** Setup

### Description
Create a new text file in the repository root called \`SWUAPI_DOCUMENTATION.txt\` that will serve as the living reference guide for the Star Wars Unlimited API. This file will be incrementally populated and updated throughout the development process.

### Acceptance Criteria
- [ ] \`SWUAPI_DOCUMENTATION.txt\` exists in the repository root
- [ ] File is tracked in git
- [ ] File has a basic header documenting its purpose and creation date
- [ ] File has placeholder sections for: API base URL, authentication, endpoints, response schema, rate limits

### Notes
This file will be referenced and updated in Phase 2 tasks."

echo "  ✓ Task 1.1 created"

# ---------------------------------------------------------------------------
# PHASE 2: API Research & Testing
# ---------------------------------------------------------------------------

echo "[Phase 2] Creating API research & testing issues..."

gh issue create --repo "$REPO" \
  --title "[Phase 2 - Task 2.1] Document Current API Specification" \
  --label "documentation,docs" \
  --body "## Phase 2: API Research & Testing

### Task 2.1 — Document Current API Specification

**Type:** Research + Documentation

### Description
Review your existing Star Wars Unlimited API documentation guide and record the base endpoints, authentication method, rate limits, and overall API structure in \`SWUAPI_DOCUMENTATION.txt\`.

### Acceptance Criteria
- [ ] \`SWUAPI_DOCUMENTATION.txt\` includes the API base URL
- [ ] Authentication method and required headers are documented
- [ ] Rate limiting information is recorded
- [ ] All known available endpoints are listed with their HTTP methods and descriptions

### Dependencies
- Task 1.1 must be complete (file must exist)"

echo "  ✓ Task 2.1 created"

gh issue create --repo "$REPO" \
  --title "[Phase 2 - Task 2.2] Test Basic API Connectivity" \
  --label "documentation" \
  --body "## Phase 2: API Research & Testing

### Task 2.2 — Test Basic API Connectivity

**Type:** Testing

### Description
Use a tool like Postman, Insomnia, or a simple curl command to verify you can successfully authenticate and make a basic request to the Star Wars Unlimited API. Confirm the response structure matches your documentation.

### Acceptance Criteria
- [ ] Successfully authenticate with the API (if required)
- [ ] Retrieve at least one valid response (e.g., list of sets or cards)
- [ ] Confirm the response structure matches what is documented in \`SWUAPI_DOCUMENTATION.txt\`
- [ ] Note any discrepancies and update the documentation file

### Dependencies
- Task 2.1 should be complete"

echo "  ✓ Task 2.2 created"

gh issue create --repo "$REPO" \
  --title "[Phase 2 - Task 2.3] Test Card Retrieval Endpoint" \
  --label "documentation" \
  --body "## Phase 2: API Research & Testing

### Task 2.3 — Test Card Retrieval Endpoint

**Type:** Testing

### Description
Test the card listing/search endpoint to understand how pagination works, what filters are available, and what fields are returned for each card. Document the full response schema.

### Acceptance Criteria
- [ ] Successfully retrieve a page of cards from the API
- [ ] Confirm pagination behavior (page numbers, cursors, etc.)
- [ ] Identify all available query/filter parameters
- [ ] Document the complete card object JSON schema in \`SWUAPI_DOCUMENTATION.txt\`
- [ ] Note the total number of cards available

### Dependencies
- Task 2.2 should be complete"

echo "  ✓ Task 2.3 created"

gh issue create --repo "$REPO" \
  --title "[Phase 2 - Task 2.4] Test Card Metadata & Update Detection" \
  --label "documentation" \
  --body "## Phase 2: API Research & Testing

### Task 2.4 — Test Card Metadata & Update Detection

**Type:** Testing + Research

### Description
Investigate whether the API returns timestamps (created_at, updated_at, last_modified) for cards. Test if the API supports filtering cards by date to identify newly added or recently modified cards — this is critical for the incremental sync feature.

### Acceptance Criteria
- [ ] Determine whether card objects include a timestamp field (updatedAt, lastModified, etc.)
- [ ] Test if the API supports a \`?updatedAfter=\` or similar date filter parameter
- [ ] Document the exact date format used by the API (ISO 8601, Unix timestamp, etc.)
- [ ] Document the reliable method for detecting new/updated cards in \`SWUAPI_DOCUMENTATION.txt\`
- [ ] If date filtering is NOT supported, document the alternative approach (e.g., full sync + diff)

### Dependencies
- Task 2.3 should be complete"

echo "  ✓ Task 2.4 created"

gh issue create --repo "$REPO" \
  --title "[Phase 2 - Task 2.5] Test Set/Release Endpoints" \
  --label "documentation" \
  --body "## Phase 2: API Research & Testing

### Task 2.5 — Test Set/Release Endpoints

**Type:** Testing

### Description
Verify how to retrieve all sets/expansions for Star Wars Unlimited, including set metadata (name, code, release date, card count, etc.). This data will be needed when storing cards in the database.

### Acceptance Criteria
- [ ] Successfully retrieve all sets/expansions from the API
- [ ] Document the set endpoint URL and HTTP method
- [ ] Document the full set object JSON schema in \`SWUAPI_DOCUMENTATION.txt\`
- [ ] Confirm the relationship between set codes and card objects
- [ ] Note the total number of sets currently available

### Dependencies
- Task 2.2 should be complete"

echo "  ✓ Task 2.5 created"

gh issue create --repo "$REPO" \
  --title "[Phase 2 - Task 2.6] Review & Validate API Documentation" \
  --label "documentation,docs" \
  --body "## Phase 2: API Research & Testing

### Task 2.6 — Review & Validate API Documentation

**Type:** Review

### Description
Do a final review of \`SWUAPI_DOCUMENTATION.txt\` to ensure all endpoints, parameters, response formats, and edge cases discovered during testing are accurately recorded. This document will be the reference for all implementation work in later phases.

### Acceptance Criteria
- [ ] All tested endpoints are documented with accurate schemas
- [ ] Authentication information is correct and complete
- [ ] Rate limiting notes are present
- [ ] Update detection method is clearly documented
- [ ] Any known limitations, quirks, or gotchas are noted
- [ ] Document is easy to follow and ready for a developer to implement against

### Dependencies
- Tasks 2.1 through 2.5 should be complete"

echo "  ✓ Task 2.6 created"

# ---------------------------------------------------------------------------
# PHASE 3: Database Schema Planning
# ---------------------------------------------------------------------------

echo "[Phase 3] Creating database schema planning issues..."

gh issue create --repo "$REPO" \
  --title "[Phase 3 - Task 3.1] Design Database Schema for SWU Cards" \
  --label "data,enhancement" \
  --body "## Phase 3: Database Schema Planning

### Task 3.1 — Design Database Schema for SWU Cards

**Type:** Planning + Design

### Description
Plan the database tables needed to store Star Wars Unlimited cards. The schema must support: card definitions, per-set printings, sync timestamp tracking, and integrate with the existing \`User\`, \`Deck\`, and \`WishlistEntry\` entities.

Reference the existing data model (Card, CardPrinting, UserCard, DeckCard, etc.) to ensure the SWU data fits the existing pattern or document why new tables are needed.

### Acceptance Criteria
- [ ] Table names and columns are defined for SWU-specific data
- [ ] Relationships between tables are mapped (foreign keys)
- [ ] A \`SyncLog\` or \`LastSyncTimestamp\` mechanism is designed
- [ ] Indexes are identified for columns used in searches/lookups
- [ ] Schema design document or diagram is added to the \`docs/\` folder
- [ ] Decision on whether to reuse existing \`Card\`/\`CardPrinting\` tables or create SWU-specific ones

### Dependencies
- Phase 2 should be complete (API schema must be known)"

echo "  ✓ Task 3.1 created"

gh issue create --repo "$REPO" \
  --title "[Phase 3 - Task 3.2] Create Database Migration for SWU Tables" \
  --label "data,.NET" \
  --body "## Phase 3: Database Schema Planning

### Task 3.2 — Create Database Migration for SWU Tables

**Type:** Development

### Description
Using EF Core, create a new database migration for the Star Wars Unlimited tables designed in Task 3.1. This includes any new tables for sets, cards, card printings, and sync tracking.

### Acceptance Criteria
- [ ] EF Core migration file is created and named descriptively (e.g., \`AddSWUTables\`)
- [ ] Migration applies to the SQLite database without errors (\`dotnet ef database update\`)
- [ ] Migration can be rolled back cleanly
- [ ] Snapshot file is updated

### Commands to verify
\`\`\`bash
dotnet ef migrations add AddSWUTables --project api/api.csproj
dotnet ef database update --project api/api.csproj
\`\`\`

### Dependencies
- Task 3.1 must be complete"

echo "  ✓ Task 3.2 created"

gh issue create --repo "$REPO" \
  --title "[Phase 3 - Task 3.3] Add SWU Entity Models to API Project" \
  --label ".NET,enhancement" \
  --body "## Phase 3: Database Schema Planning

### Task 3.3 — Add SWU Entity Models to API Project

**Type:** Development

### Description
Create Entity Framework entity models for the new SWU tables in the API project. Follow the existing model patterns (e.g., \`Card\`, \`CardPrinting\`, \`UserCard\`).

### Acceptance Criteria
- [ ] Entity classes are created in an appropriate folder (e.g., \`api/Models/\` or \`api/Features/Cards/\`)
- [ ] Models include proper EF Core annotations or fluent API configuration
- [ ] Relationships (navigation properties, foreign keys) are correctly defined
- [ ] Models include XML doc comments
- [ ] Project compiles without errors

### Dependencies
- Task 3.1 must be complete"

echo "  ✓ Task 3.3 created"

gh issue create --repo "$REPO" \
  --title "[Phase 3 - Task 3.4] Add DbContext Configurations for SWU Entities" \
  --label ".NET,enhancement" \
  --body "## Phase 3: Database Schema Planning

### Task 3.4 — Add DbContext Configurations for SWU Entities

**Type:** Development

### Description
Update the EF Core \`DbContext\` to register the new SWU entity sets and apply any fluent API configurations (indexes, constraints, relationships, value converters).

### Acceptance Criteria
- [ ] New \`DbSet<T>\` properties added to the DbContext
- [ ] Fluent API configurations added in \`OnModelCreating\` (if needed)
- [ ] NOCASE collation applied to text columns used in searches
- [ ] Project compiles without errors
- [ ] Migration confirms tables are created as expected

### Dependencies
- Task 3.3 must be complete"

echo "  ✓ Task 3.4 created"

# ---------------------------------------------------------------------------
# PHASE 4: Sync Infrastructure
# ---------------------------------------------------------------------------

echo "[Phase 4] Creating sync infrastructure issues..."

gh issue create --repo "$REPO" \
  --title "[Phase 4 - Task 4.1] Create Card Sync Service Interface" \
  --label ".NET,enhancement" \
  --body "## Phase 4: Sync Infrastructure

### Task 4.1 — Create Card Sync Service Interface

**Type:** Development

### Description
Define an \`ICardSyncService\` interface (and any supporting interfaces) that describes the contract for syncing Star Wars Unlimited cards. This interface should be in the appropriate Features folder.

### Acceptance Criteria
- [ ] Interface defines methods for: triggering a sync, getting last sync time, getting sync status
- [ ] Interface is documented with XML doc comments
- [ ] Interface is registered in the DI container (\`Program.cs\`)
- [ ] Project compiles without errors

### Example Interface
\`\`\`csharp
public interface ICardSyncService
{
    Task<SyncResult> SyncAsync(CancellationToken ct = default);
    Task<DateTimeOffset?> GetLastSyncTimeAsync(CancellationToken ct = default);
}
\`\`\`

### Dependencies
- Phase 3 must be complete"

echo "  ✓ Task 4.1 created"

gh issue create --repo "$REPO" \
  --title "[Phase 4 - Task 4.2] Implement SWU API Client Service" \
  --label ".NET,enhancement" \
  --body "## Phase 4: Sync Infrastructure

### Task 4.2 — Implement SWU API Client Service

**Type:** Development

### Description
Create a dedicated typed HTTP client service (\`SWUApiClient\` or \`ISWUApiClient\`) that wraps all calls to the Star Wars Unlimited external API. It should handle authentication, pagination, rate limiting, and error handling.

### Acceptance Criteria
- [ ] Typed HTTP client registered via \`AddHttpClient<>()\` in \`Program.cs\`
- [ ] Base URL and authentication configured via \`IOptions<>\` (not hardcoded)
- [ ] Methods for: fetching all cards (with pagination), fetching cards updated after a date, fetching all sets
- [ ] Graceful handling of non-2xx responses (throws a typed exception, not raw HttpRequestException)
- [ ] Retry policy configured (e.g., Polly with 3 retries + exponential backoff) for transient failures
- [ ] Unit-testable (interface extracted, \`HttpClient\` can be mocked)

### Dependencies
- Task 4.1
- Phase 2 (API specification must be known)"

echo "  ✓ Task 4.2 created"

gh issue create --repo "$REPO" \
  --title "[Phase 4 - Task 4.3] Implement Core Sync Service Logic" \
  --label ".NET,enhancement" \
  --body "## Phase 4: Sync Infrastructure

### Task 4.3 — Implement Core Sync Service Logic

**Type:** Development

### Description
Implement the \`CardSyncService\` class that orchestrates the sync process: retrieve last sync timestamp → call API for new/updated cards → compare with database → hand off to insert/update logic.

### Acceptance Criteria
- [ ] Retrieves the last sync timestamp from the database
- [ ] Calls the SWU API to fetch only cards modified since that timestamp (or all cards on first sync)
- [ ] Compares API response with existing database records to identify new vs updated cards
- [ ] Handles the case where no changes exist (no-op, does not update timestamp)
- [ ] Passes new/updated card data to insert/update logic (Task 4.4)
- [ ] Handles first-sync scenario (no previous timestamp)
- [ ] Structured logging for each sync step with EventId

### Dependencies
- Task 4.1 (interface)
- Task 4.2 (API client)"

echo "  ✓ Task 4.3 created"

gh issue create --repo "$REPO" \
  --title "[Phase 4 - Task 4.4] Implement Card Insert/Update (Upsert) Logic" \
  --label ".NET,enhancement" \
  --body "## Phase 4: Sync Infrastructure

### Task 4.4 — Implement Card Insert/Update (Upsert) Logic

**Type:** Development

### Description
Implement the data persistence logic that takes API card data and either inserts new records or updates existing ones in the database. This should be idempotent — running it twice with the same data should not create duplicates.

### Acceptance Criteria
- [ ] New cards are inserted with all fields populated from the API response
- [ ] Existing cards are updated when the API data has changed
- [ ] Sets/CardPrintings are created if they don't exist
- [ ] All changes are wrapped in a transaction (atomic commit)
- [ ] Operation is idempotent — safe to re-run without creating duplicates
- [ ] Performance: uses batch operations where possible (not one EF call per card)

### Dependencies
- Task 4.3
- Tasks 3.2–3.4 (database tables must exist)"

echo "  ✓ Task 4.4 created"

gh issue create --repo "$REPO" \
  --title "[Phase 4 - Task 4.5] Implement Sync Timestamp Persistence" \
  --label ".NET,enhancement" \
  --body "## Phase 4: Sync Infrastructure

### Task 4.5 — Implement Sync Timestamp Persistence

**Type:** Development

### Description
Implement the logic that records the sync completion timestamp after all cards have been successfully processed. The timestamp must only be saved after a successful sync — not if the sync fails partway through.

### Acceptance Criteria
- [ ] Last sync timestamp is persisted to the database after a successful sync
- [ ] Timestamp is stored in UTC
- [ ] Timestamp is NOT updated if the sync fails or is interrupted
- [ ] GetLastSyncTimeAsync() correctly returns the persisted timestamp
- [ ] Timestamp is readable by the sync status endpoint (Task 5.2)

### Dependencies
- Task 4.3
- Task 4.4"

echo "  ✓ Task 4.5 created"

# ---------------------------------------------------------------------------
# PHASE 5: API Endpoint Development
# ---------------------------------------------------------------------------

echo "[Phase 5] Creating API endpoint issues..."

gh issue create --repo "$REPO" \
  --title "[Phase 5 - Task 5.1] Create Sync Trigger Endpoint (POST /api/admin/sync/swu)" \
  --label ".NET,area/api,enhancement" \
  --body "## Phase 5: API Endpoint Development

### Task 5.1 — Create Sync Trigger Endpoint

**Type:** Development

### Description
Create an admin-only endpoint that triggers a Star Wars Unlimited card sync.

**Endpoint:** \`POST /api/admin/sync/star-wars-unlimited\`

### Acceptance Criteria
- [ ] Endpoint is protected with \`[RequireAdmin]\` attribute
- [ ] Returns \`202 Accepted\` immediately if sync is triggered
- [ ] Returns \`409 Conflict\` if a sync is already in progress
- [ ] Response body includes sync ID or status information
- [ ] Endpoint is documented (XML doc comments on controller action)

### Dependencies
- Phase 4 must be complete (sync service must exist)"

echo "  ✓ Task 5.1 created"

gh issue create --repo "$REPO" \
  --title "[Phase 5 - Task 5.2] Create Sync Status Endpoint (GET /api/admin/sync/swu/status)" \
  --label ".NET,area/api,enhancement" \
  --body "## Phase 5: API Endpoint Development

### Task 5.2 — Create Sync Status Endpoint

**Type:** Development

### Description
Create an admin endpoint that returns the current sync status and history.

**Endpoint:** \`GET /api/admin/sync/star-wars-unlimited/status\`

### Acceptance Criteria
- [ ] Endpoint is protected with \`[RequireAdmin]\` attribute
- [ ] Returns: last sync time (UTC), sync status (idle/running/failed), total cards in database, last sync card count
- [ ] Returns \`200 OK\` with JSON body
- [ ] Endpoint is documented

### Example Response
\`\`\`json
{
  \"lastSyncAt\": \"2024-01-15T10:30:00Z\",
  \"status\": \"idle\",
  \"totalCards\": 483,
  \"lastSyncCardsProcessed\": 12
}
\`\`\`

### Dependencies
- Task 4.5 (timestamp persistence)
- Task 5.1"

echo "  ✓ Task 5.2 created"

gh issue create --repo "$REPO" \
  --title "[Phase 5 - Task 5.3] Add Error Handling & Logging to Sync Pipeline" \
  --label ".NET,enhancement" \
  --body "## Phase 5: API Endpoint Development

### Task 5.3 — Add Error Handling & Logging to Sync Pipeline

**Type:** Development

### Description
Implement comprehensive error handling and structured logging throughout the sync pipeline. Sync failures should be graceful — they should not corrupt the database, and they should provide clear diagnostic information.

### Acceptance Criteria
- [ ] All sync service methods log entry, progress, and exit with structured fields (EventId, game, card count, etc.)
- [ ] API errors are caught and logged with the HTTP status code and response body
- [ ] Database errors trigger transaction rollback and are logged
- [ ] Partial sync failures leave the database in a consistent state (no partial updates)
- [ ] Sync status endpoint reflects the failure state (not stuck as 'running')
- [ ] Retry logic for transient API failures (configured via Polly or equivalent)

### Dependencies
- Phase 4 and Task 5.1–5.2"

echo "  ✓ Task 5.3 created"

# ---------------------------------------------------------------------------
# PHASE 6: Testing & Validation
# ---------------------------------------------------------------------------

echo "[Phase 6] Creating testing & validation issues..."

gh issue create --repo "$REPO" \
  --title "[Phase 6 - Task 6.1] Unit Test the Card Sync Service" \
  --label ".NET,enhancement" \
  --body "## Phase 6: Testing & Validation

### Task 6.1 — Unit Test the Card Sync Service

**Type:** Testing + Development

### Description
Write xUnit unit tests for \`CardSyncService\` using mocked dependencies. Cover the key scenarios without requiring a real API or database.

### Test Scenarios
- [ ] First sync (no previous timestamp) — all cards inserted
- [ ] Incremental sync — only new/updated cards processed
- [ ] No changes — sync completes as no-op, timestamp NOT updated
- [ ] API returns error — sync fails gracefully, timestamp NOT updated
- [ ] Empty API response — handled without exception

### Acceptance Criteria
- [ ] Tests are in \`api.Tests/Features/<Area>/\`
- [ ] All tests pass: \`dotnet test ./api/api.sln -c Release\`
- [ ] Mocks use Moq or NSubstitute (follow existing test patterns)
- [ ] Test coverage includes happy path and all failure scenarios

### Dependencies
- Phase 4 must be complete"

echo "  ✓ Task 6.1 created"

gh issue create --repo "$REPO" \
  --title "[Phase 6 - Task 6.2] Integration Test the Full Sync Flow" \
  --label ".NET,enhancement" \
  --body "## Phase 6: Testing & Validation

### Task 6.2 — Integration Test the Full Sync Flow

**Type:** Testing + Development

### Description
Write integration tests using \`WebApplicationFactory\` that test the full flow end-to-end: trigger sync endpoint → API call → database write → timestamp persisted → status endpoint reflects update.

### Test Scenarios
- [ ] POST /api/admin/sync/swu triggers a sync and returns 202
- [ ] GET /api/admin/sync/swu/status returns correct last sync time after sync
- [ ] Cards are in the database after sync completes
- [ ] Running sync twice does not duplicate cards

### Acceptance Criteria
- [ ] Tests are in \`api.Tests/Features/<Area>/\`
- [ ] Use a stub/mock HTTP server to simulate the SWU API (no real API calls in CI)
- [ ] All tests pass: \`dotnet test ./api/api.sln -c Release\`

### Dependencies
- Phase 5 must be complete"

echo "  ✓ Task 6.2 created"

gh issue create --repo "$REPO" \
  --title "[Phase 6 - Task 6.3] Manual Testing with the Real SWU API" \
  --label "documentation" \
  --body "## Phase 6: Testing & Validation

### Task 6.3 — Manual Testing with the Real SWU API

**Type:** Testing

### Description
Manually test the sync endpoint against the real Star Wars Unlimited API with your local development database. Verify cards are correctly inserted, subsequent syncs detect and apply updates, and the timestamp tracking works as expected.

### Test Steps
1. Start the API locally
2. Verify the database starts empty (or reset it)
3. POST to \`/api/admin/sync/star-wars-unlimited\`
4. Check GET \`/api/admin/sync/star-wars-unlimited/status\` shows last sync time
5. Verify cards exist in the database (use a DB browser or GET /api/cards)
6. Wait/modify, then run sync again — confirm no duplicates and only updates applied

### Acceptance Criteria
- [ ] First sync retrieves all cards from the real API
- [ ] Cards are stored correctly with all expected fields
- [ ] Second sync does not duplicate cards
- [ ] Timestamp is persisted and correct
- [ ] Any API discrepancies found are documented in \`SWUAPI_DOCUMENTATION.txt\`

### Dependencies
- Phase 5 must be complete"

echo "  ✓ Task 6.3 created"

gh issue create --repo "$REPO" \
  --title "[Phase 6 - Task 6.4] Test Edge Cases & Failure Scenarios" \
  --label "documentation" \
  --body "## Phase 6: Testing & Validation

### Task 6.4 — Test Edge Cases & Failure Scenarios

**Type:** Testing

### Description
Deliberately test failure and edge-case scenarios to verify the sync pipeline is robust and does not corrupt data.

### Scenarios to Test
- [ ] API is unreachable (no network / wrong URL) — graceful failure, no DB corruption
- [ ] API returns rate-limit response (429) — handled with backoff/retry
- [ ] Sync is triggered while another sync is running — returns 409, no duplicate sync
- [ ] Database is locked during sync — sync fails gracefully
- [ ] API returns malformed/partial JSON — handled without crash
- [ ] Card data has missing optional fields — default values applied correctly
- [ ] Very large sync (full catalog on first sync) — completes without timeout

### Acceptance Criteria
- [ ] All scenarios are tested and behave gracefully
- [ ] No database corruption in any failure scenario
- [ ] Findings documented — update \`SWUAPI_DOCUMENTATION.txt\` with any gotchas

### Dependencies
- Task 6.3"

echo "  ✓ Task 6.4 created"

# ---------------------------------------------------------------------------
# PHASE 7: Client UI
# ---------------------------------------------------------------------------

echo "[Phase 7] Creating client UI issues..."

gh issue create --repo "$REPO" \
  --title "[Phase 7 - Task 7.1] Add Sync Trigger UI Component (Admin Panel)" \
  --label "area/ui,frontend,enhancement" \
  --body "## Phase 7: Client UI (Optional Foundation)

### Task 7.1 — Add Sync Trigger UI Component

**Type:** Development

### Description
Create a React component in the admin panel that displays the last sync time for Star Wars Unlimited cards and provides a button to trigger a sync. This should be added to the existing admin UI.

### Acceptance Criteria
- [ ] Component displays: game name, last sync date/time (formatted), total card count
- [ ] 'Sync Now' button calls \`POST /api/admin/sync/star-wars-unlimited\`
- [ ] Button is disabled while a sync is in progress
- [ ] Component uses TanStack Query (\`useQuery\` for status, \`useMutation\` for trigger)
- [ ] Component follows existing shadcn/ui patterns

### Dependencies
- Phase 5 must be complete (endpoints must exist)"

echo "  ✓ Task 7.1 created"

gh issue create --repo "$REPO" \
  --title "[Phase 7 - Task 7.2] Add Sync Status Display & Progress Feedback" \
  --label "area/ui,frontend,enhancement" \
  --body "## Phase 7: Client UI (Optional Foundation)

### Task 7.2 — Add Sync Status Display & Progress Feedback

**Type:** Development

### Description
Enhance the sync UI component (from Task 7.1) to show real-time feedback during sync operations and display the result after completion.

### Acceptance Criteria
- [ ] Loading/spinner shown while sync is in progress
- [ ] Success toast/message shown after sync completes
- [ ] Error message shown if sync fails (with a human-readable reason)
- [ ] Status auto-refreshes while sync is running (polling or after trigger)
- [ ] Follows existing toast/notification patterns in the app

### Dependencies
- Task 7.1"

echo "  ✓ Task 7.2 created"

# ---------------------------------------------------------------------------
# PHASE 8: Future Planning & Refinement
# ---------------------------------------------------------------------------

echo "[Phase 8] Creating future planning issues..."

gh issue create --repo "$REPO" \
  --title "[Phase 8 - Task 8.1] Document Discovered API Limitations & Gotchas" \
  --label "documentation,docs" \
  --body "## Phase 8: Future Planning & Refinement

### Task 8.1 — Document Discovered Limitations

**Type:** Documentation

### Description
After completing the implementation and testing phases, do a thorough update of \`SWUAPI_DOCUMENTATION.txt\` to record any API limitations, unexpected behaviors, workarounds, or lessons learned that were discovered during development.

### Acceptance Criteria
- [ ] \`SWUAPI_DOCUMENTATION.txt\` updated with a 'Known Limitations' section
- [ ] Any rate limiting behaviors noted
- [ ] Workarounds for API quirks are documented
- [ ] Anything a future developer would want to know before working with this API is included
- [ ] Change log entry added to the document

### Dependencies
- Phase 6 (testing) should be complete"

echo "  ✓ Task 8.1 created"

gh issue create --repo "$REPO" \
  --title "[Phase 8 - Task 8.2] Plan Multi-Game Sync Support" \
  --label "documentation,enhancement" \
  --body "## Phase 8: Future Planning & Refinement

### Task 8.2 — Plan Multi-Game Support

**Type:** Planning

### Description
Based on lessons learned from the Star Wars Unlimited sync implementation, update the project roadmap with specific next steps for supporting other TCGs (Magic: The Gathering, Disney Lorcana, Pokémon, etc.). Focus on what would need to change in the sync infrastructure to support multiple games.

### Acceptance Criteria
- [ ] Document which parts of the sync infrastructure are game-agnostic and reusable
- [ ] Identify what is SWU-specific and would need to be abstracted
- [ ] Create a high-level plan for adding a second game's sync
- [ ] Add notes to \`CONTRIBUTING.md\` or a new \`docs/SYNC_ARCHITECTURE.md\` file
- [ ] Create new GitHub issues for the next game's sync implementation

### Dependencies
- Phase 6 should be complete"

echo "  ✓ Task 8.2 created"

gh issue create --repo "$REPO" \
  --title "[Phase 8 - Task 8.3] Create Deployment Checklist for Card Sync Feature" \
  --label "documentation,docs" \
  --body "## Phase 8: Future Planning & Refinement

### Task 8.3 — Create Deployment Checklist

**Type:** Documentation

### Description
Document the steps needed to deploy the card sync feature in a non-local environment (e.g., a server or cloud host). Include environment variables, initial sync steps, monitoring considerations, and any database setup required.

### Acceptance Criteria
- [ ] Required environment variables are listed (API keys, base URLs, etc.)
- [ ] Database migration steps are documented
- [ ] How to trigger the initial full sync is documented
- [ ] Recommended monitoring/alerting for sync failures is noted
- [ ] Document is added to \`docs/\` or appended to \`ADMIN.md\`

### Dependencies
- Phase 5–6 should be complete"

echo "  ✓ Task 8.3 created"

echo ""
echo "============================================================"
echo "  All issues created successfully!"
echo "  View them at: https://github.com/$REPO/issues"
echo "============================================================"
