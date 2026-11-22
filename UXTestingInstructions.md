# Trading Card Game Tracker — UX Testing Instructions

Thank you for helping test **Trading Card Game Tracker**! This guide walks you through downloading the project in VS Code, launching the backend and frontend locally, importing card data for all four supported games, and testing each page and workflow.

---

## 1. Prerequisites (One-Time Setup)

Before cloning and running the app, make sure you have the following installed on Windows:

1. **Git**: [https://git-scm.com/downloads](https://git-scm.com/downloads)
2. **Visual Studio Code**: [https://code.visualstudio.com/](https://code.visualstudio.com/)
3. **.NET 10 SDK** (`10.0.201` or newer): [https://dotnet.microsoft.com/download/dotnet/10.0](https://dotnet.microsoft.com/download/dotnet/10.0)
4. **Node.js** (v22 LTS or v24 recommended, which includes `npm`): [https://nodejs.org/](https://nodejs.org/)

### Trust the Local .NET HTTPS Certificate (One-Time)

Open a PowerShell terminal and run:

```powershell
dotnet dev-certs https --trust
```

_(If Windows prompts you to trust the localhost certificate, click **Yes**.)_

---

## 2. Download the Project in VS Code

1. Open **VS Code**.
2. Open the Command Palette (`Ctrl+Shift+P`), type **Git: Clone**, and press `Enter`.
3. Select **Clone from GitHub** (sign in to GitHub if prompted) and choose `Xercius/Trading-Card-Game-Tracker`, or paste the repository URL:
   ```text
   https://github.com/Xercius/Trading-Card-Game-Tracker.git
   ```
4. Choose a local folder to save the project and click **Open** when VS Code finishes cloning.

---

## 3. Launch the Application

### Option A: One-Click Single-Server Launch (Recommended on Windows)

From the repository root, double-click **`Start-TCGTracker.bat`** in File Explorer, or run **`.\Start-TCGTracker.ps1`** in PowerShell:

```powershell
.\Start-TCGTracker.ps1
```

The launcher automatically handles first-time client bundling, starts the single-server app at **`http://localhost:5229`**, waits for `/api/health`, opens your default browser automatically, and cleans up the server process when you press `Ctrl+C` or close the window.

- **Useful PowerShell flags:**
  - `.\Start-TCGTracker.ps1 -Rebuild` — Rebuild the frontend (`client-vite`) and backend after pulling updates.
  - `.\Start-TCGTracker.ps1 -NoBrowser` — Start the server without opening a browser tab automatically.
  - `.\Start-TCGTracker.ps1 -Url https://localhost:7226` — Override the listening URL.
- **Tip for an isolated test database:** By default, source-repo launches use `api/app.db`. To test against a fresh temporary SQLite database instead, set `ConnectionStrings__DefaultConnection` before running the launcher:
  ```powershell
  $env:ConnectionStrings__DefaultConnection = "Data Source=$(Join-Path $env:TEMP 'tcg-test.db')"
  .\Start-TCGTracker.ps1
  ```
- **Testing a Standalone Windows Release ZIP (no .NET SDK or Node.js required):** Extract `TradingCardGameTracker-vX.Y.Z-win-x64.zip` and run **`Start-TCGTracker.bat`** inside the extracted folder. It launches the self-contained `api.exe` at **`http://localhost:5229`** without needing any SDK or frontend build.

---

### Option B: Two-Terminal Developer Mode (Hot Reload)

Use this mode if you want Vite frontend hot reload while developing UI changes. You will run two PowerShell terminals in VS Code (`Ctrl+Shift+\`` to open a terminal, and click the `+` icon in the terminal panel to open a second one).

#### Terminal 1 — Start the Backend API

From the repository root in your first PowerShell terminal, run:

```powershell
Set-Location .\api

$dbPath = Join-Path $env:TEMP "tcg-swu-design-20261117c.db"
if (Test-Path $dbPath) { throw "That database file already exists; choose a different filename." }

$env:ConnectionStrings__DefaultConnection = "Data Source=$dbPath"

dotnet run --launch-profile "TradingCardApi (HTTPS)"
```

> **Tip if you restart the backend later:**
> The `if (Test-Path $dbPath)` check prevents overwriting an existing database file. If you stop the backend and want to restart it **using the cards and decks you already imported**, skip the `if (Test-Path ...)` line or change the filename in `$dbPath` to start fresh.

Wait until you see output similar to `Now listening on: https://localhost:7226`. Leave this terminal running.

---

#### Terminal 2 — Install & Start the Frontend

Open a **second** PowerShell terminal at the repository root.

1. **First time only** — install the frontend packages:
   ```powershell
   npm --prefix client-vite install
   ```
2. **Start the frontend dev server:**
   ```powershell
   npm --prefix client-vite run dev
   ```
3. Open the local URL shown in the terminal (typically **`http://localhost:5173`**) in your browser.

---

## 4. What to Test

### Phase A: Global Header & Navigation

1. **What's New Flyout:**
   - Click the **bell icon** in the top-right corner of the header.
   - Verify the unread dot indicator clears when opened, and review the recent release notes flyout.
2. **Backup & Restore Dialog (`#840`):**
   - Click the **`Backup & Restore`** button in the top-right corner of the header (next to the **What's New** bell icon; also accessible in the mobile navigation drawer).
   - Verify the **`Backup & Restore`** modal opens and displays both export and restore controls.
3. **Game Switcher Bar:**
   - Click through the game logos in the second row of the header:
     - **Star Wars Unlimited**
     - **Star Wars CCG**
     - **Magic: The Gathering**
     - **Pokemon TCG**
   - Verify **Hellbreak TCG** appears disabled with a _(Coming soon)_ tooltip.

---

### Phase B: Test All 4 Card Importers

Because you are starting with a brand-new database, you'll first sync card data using the **Importers** dropdown menu in the top navigation bar. The Star Wars: Unlimited sync takes the longest, signifiantly longer than all the others combined. For Pokemon and Magic I wouldn't recommend trying to import a TON of sets at once. At times an import might fail, but in most cases trying again will work.

1. **Star Wars: Unlimited (`Importers → SWU Sync`):**
   - Run a sync to import Star Wars: Unlimited sets and cards.
   - Observe the live status indicator and sync logs.
2. **Star Wars CCG (`Importers → SWCCG Sync`):**
   - Run a sync to import Star Wars CCG sets and cards.
   - Verify status updates and completion logs.
3. **Pokémon TCG (`Importers → Pokémon Sync`):**
   - Pokémon uses a **two-step "Sync Sets First"** workflow:
     1. Click **Sync Sets** first to download the list of available Pokémon expansions.
     2. Select **1 to 3 sets** (syncing every set at once can take a long time!).
     3. Click **Sync Cards** and verify the selected sets lock in as synced once complete.
4. **Magic: The Gathering (`Importers → MTG Sync`):**
   - Magic also uses the **two-step "Sync Sets First"** workflow:
     1. Click **Sync Sets** to load Scryfall set metadata.
     2. Expand a category (e.g., _Core & Expansions_ or _Commander_) and select **1 to 3 sets**.
     3. Click **Sync Cards** and verify progress and completion.

**Importer UX Feedback to Note:**

- Was it clear what to click first on each sync page?
- Did progress indicators, set selection checkboxes, and logs behave as expected?

---

### Phase C: Browse Cards, Search & Game-Specific Filters (`/cards`)

Click **Cards** in the top navigation bar and test browsing across all 4 games:

1. **Header Name Search:**
   - Type a card name into the top search bar (`Search cards, decks...`) and test the **Clear** button.
2. **View Modes & Card Sizing:**
   - Toggle between **Grid** and **Table** views.
   - Drag the **Card Size** slider in Grid view.
   - Test pagination controls (`Page X of Y`, page size selector).
3. **Game-Specific Filter Drawer (`Filters` button):**
   - Switch to each game using the header logos, open **Filters**, and test combining filters with the header search:
     - **Magic: The Gathering:** Set, Type, Subtype, Keywords, Colors (test `OR`, `AND`, and `EXACT` operators), Color Identity, Mana Value, Power, Toughness, Loyalty, and Rarity.
     - **Star Wars: Unlimited:** Set, Aspect, Type, Arena, Trait (`OR`, `AND`, `EXCLUDE`), Keyword, Cost, Power, HP, Upgrade Power, Upgrade HP, and Rarity.
     - **Pokémon TCG:** Energy Type, Set, Type, Stage, Special Type, Evolves From, HP (`>100`, etc.), and Rarity.
     - **Star Wars CCG:** Dark/Light Side toggle, Card Type, Sub-Type, Persona, Set, Rarity, Traits, Destiny, Deploy, Forfeit, Power, Ability, and Force Icons.
   - Verify active filter pills appear above the card grid and can be removed individually or via **Clear all**.
4. **Card Details Modal:**
   - Click any card to open its detail modal.
   - Check card stats, rules/text formatting, printing variations (if multiple printings exist), and multi-face card layouts.

---

### Phase D: Managing Your Collection (`/collection`) & Wishlist (`/wishlist`)

1. **Adding Cards to Collection & Wishlist:**
   - Add cards either from the **Cards** page / Card Modal or directly within **Collection** and **Wishlist**.
   - Test setting both **Owned** quantities and **Proxy** quantities, as well as **Wanted** (wishlist) quantities.
2. **Collection Page (`/collection`):**
   - Review the Sets completion table (unique cards owned, total cards, completion percentage).
   - Click into a specific set to view set details, filter owned/missing cards, and adjust quantities inline.
3. **Wishlist Page (`/wishlist`):**
   - Review your wanted cards across games, adjust wanted quantities, and test removing items from the wishlist.

---

### Phase E: Building Decks (`/decks`)

Click **Decks** in the top navigation bar and practice creating and editing decks for different games:

1. **Create a Deck:**
   - Create a new deck and open the **Deck Construction Modal**.
2. **Deck Construction & Validation Rules:**
   - **Star Wars: Unlimited:** Select a Leader and a Base, toggle **In-aspect only**, add units/events/upgrades, and check aspect penalty and deck validation feedback.
   - **Magic: The Gathering:** Select a format (e.g., Commander or Standard), add cards, and check color identity and deck validation rules.
   - **Pokémon TCG:** Add Pokémon, Trainer, and Energy cards and review the live Pokémon deck validation panel.
   - **Star Wars CCG:** Build a Light Side or Dark Side deck and verify side/uniqueness validation rules.
3. **Filters & Availability in Deck Builder:**
   - Use the **Filters** drawer inside the Deck Construction Modal.
   - Toggle **Include Proxies** to see how card availability counts update against your collection.

---

### Phase F: Value Hub (`/value`) — _Skip for Now_

> **Note:** The **Value** page in the top navigation bar is not implemented yet — please skip testing it during this review.

---

## 5. New Features to Test on the `NextSteps` Branch

In addition to the core walkthrough above, please specifically test the following additions introduced on the `NextSteps` branch:

### A. SWCCG `(AI)` Treatments & CSV Alternate Printings Importer (`Importers → SWCCG Sync` & `/cards`)

1. **Prerequisite Gate Before Base Sync:**
   - On a fresh database, open **Importers → SWCCG Sync** and verify the **Sync Alternate Printings** button is **not** shown until the base SWCCG sync has completed.
2. **Base Sync & `(AI)` Suffix Grouping:**
   - Click **Run SWCCG sync** and wait for it to finish.
   - Search SWCCG cards on `/cards` and verify variants that previously ended in `(AI)`, `(BORDER BREAKER AI)`, `(C-SLIP AI)`, `(C-SLIP AI 2)`, or `(HOLO AI)` are grouped onto their base card as printings with those labels as their **Treatment** (rather than separate duplicate cards).
3. **Alternate Printings & New Cards CSV Import:**
   - After the base sync completes, verify **Sync Alternate Printings** is now visible.
   - Test with the **Dry run** option first (if toggled on) to preview counts without persisting, then run **Sync Alternate Printings** live.
   - Review the **Alternate Printings Import Summary** counts (_Rows processed_, _New cards_, _Printings created_, _Skipped existing_, _Sets/Rarities/Treatments created_, _Failed rows_).
   - If any rows fail, verify the red alert banner, structured **Failures** table, and **Copy Failure Report** button make it obvious what failed; if zero fail, verify the clean completion status message.
   - Click **Sync Alternate Printings** a second time and confirm existing rows are skipped cleanly (_Skipped existing_) with no duplicates created.
4. **Printing Notes in Card Details:**
   - Open an alternate printing or misprint card in the **Card Details Modal** (`/cards`) and verify **Printing Notes** displays when present, alongside inherited artwork/collector number and `Standard` treatment when the CSV treatment column was blank.

### B. Fast Collection Quantity Updates (`/collection`)

1. Open a set detail page (`/collection/sets/:id`) and **rapid-click `+` and `−`** on **Owned** and **Proxy** steppers across multiple rows — controls should stay responsive without locking between requests, and values should persist accurately after a brief pause/refresh.
2. Click directly into a stepper input, type a multi-digit number (e.g., `12`), and press `Enter` or click away to verify it commits cleanly.
3. Open a large set (>50 cards) and verify smooth scrolling and live updates to the set completion summary.

### C. Wishlist Source Tracking (`/wishlist`)

1. Add wanted cards both generally (from **Cards** / **Wishlist**) and from a deck (see **Section E** below).
2. On `/wishlist`, verify per-source badges (e.g., _General_ vs a deck name) appear on each wanted card.
3. For cards wanted by **multiple sources**, adjust quantities with the per-source `+`/`−` controls and verify the total **Edit** button is disabled so ambiguous edits are prevented.
4. Click **Move to Collection** on a multi-source item and verify you can choose which source to move quantities from.

### D. Deck Printing Selection (`/decks`)

1. Open a deck in the **Deck Builder / Deck Construction Modal** and use **`+ Printing`** on a search result to add a specific printing.
2. On an existing row in the deck list, use the inline printing switcher to change to another printing; switch to a printing already in the deck and verify quantities merge cleanly.

### E. Deck Card Ideas Zone & Missing-Cards Wishlist Sync (`/decks`)

1. **Ideas Zone:**
   - Add cards to a deck's **Ideas** zone, adjust their quantities with `+`/`−`, and test **Move to Deck**.
   - Verify copy-limit validation counts copies across Main Deck + Sideboard + Ideas, while deck-size limits exclude Ideas.
2. **Missing-Cards Check & Wishlist Sync:**
   - Review **Missing N** badges against your owned/proxy collection and toggle the **Missing Only** filter.
   - Click per-row **+ Wishlist** and the header **Add Missing (N)** button, then check `/wishlist` to verify those cards are tracked under that deck's source.

---

## 6. New Features to Test on the `HomeStretchto1point0` Branch

### A. Single-Server Hosting, SPA Routing & One-Click Windows Launcher (`#838`, `#839`, `#845`)

1. **Launcher Startup & Shutdown (`#839`):**
   - Run `Start-TCGTracker.bat` or `.\Start-TCGTracker.ps1` from the repository root (or run `Start-TCGTracker.bat` from an extracted Windows release ZIP `TradingCardGameTracker-vX.Y.Z-win-x64.zip`).
   - Verify the server starts at `http://localhost:5229` (in both source and release ZIP modes; override with `-Url` in PowerShell if needed), waits for `/api/health`, and automatically opens the app in your default browser.
   - Press `Ctrl+C` in the launcher window (or close it) and confirm the backend process shuts down cleanly without leaving an orphaned server running.
2. **Single-Server SPA & API Error Routing (`#838`, `#845`):**
   - Navigate directly to or refresh (`F5`) SPA routes such as `/cards`, `/collection`, `/decks`, `/wishlist`, and `/value`, and verify card images and pages load properly.
   - Visit a nonexistent API route such as `/api/does-not-exist` and confirm the backend returns an HTTP `404` `application/problem+json` response rather than serving the SPA `index.html`.

### B. Portable JSON Backup & Restore (`#840`)

1. **Export Backup:**
   - Click **`Backup & Restore`** in the header (or mobile menu) and click **`Download JSON Backup`**.
   - Verify a `.json` backup file downloads with your collection, wishlist, and decks.
2. **Merge vs. Replace Restore Modes:**
   - Create a temporary edit (such as adding/removing a collection quantity or test deck), then reopen **`Backup & Restore`** and select your `.json` file under **`Backup JSON file`**.
   - Test **`Merge (keep existing items and add backup entries)`** and verify existing items remain while missing backup items are restored.
   - Test **`Replace (overwrite all current collection, wishlist, and deck data)`**, check **`I understand Replace mode deletes my existing collection, wishlist, and decks.`**, and click **`Restore Backup`**.
3. **Result Summary & Cross-DB Portability:**
   - Verify the **`Backup restored successfully`** banner shows updated **`Collection`**, **`Wishlist`**, and **`Decks`** counts along with natural-key match info (`Matched N printing(s) using card/set identifiers.`), and test importing into a fresh SQLite database where numeric database IDs differ.

### C. Deck Builder Zone & Role UX Polish (`#843`)

Test both the full **`DeckBuilderPage`** (`/decks/:deckId`) and the **`DeckConstructionModal`** editor on `/decks` across all 4 supported games:

1. **Magic: The Gathering:**
   - In a **Commander** deck, verify the **`★ Commander`** section appears, **`★ Cmdr`** / **`☆ Cmdr`** toggles work on eligible cards, and **`+SB`** is hidden.
   - In a **Constructed** deck (e.g. Standard/Modern), verify the **`Sideboard`** section shows `count / 15` and **`+SB`** moves cards cleanly between main deck and sideboard.
2. **Star Wars Unlimited (`SWU`):**
   - Verify **`Leaders`**, **`Base`**, main deck card type groups, and **`Ideas`** render cleanly with accurate totals.
3. **Star Wars CCG (`SWCCG`):**
   - Verify **`⭐ Starting Cards`** and **`🛡️ Defensive Shields`** sections display properly alongside main deck cards and **`Ideas`**, and confirm **`★ Start`** / **`☆ Start`** toggles work on non-Defensive-Shield cards.
4. **Pokémon TCG:**
   - Verify cards group cleanly under **`Pokémon`**, **`Trainer`**, **`Energy`**, and **`Ideas`** without irrelevant MTG/SWCCG role buttons.
5. **Zone & Layout Consistency:**
   - Verify **`+Idea`** on main rows and **`Move to Deck`**, `+`, `−` inside **`Ideas`** work smoothly across both desktop and narrow layouts without overlapping text or clipped buttons.

### D. Flexible Decklist Text Import & Export (`#841`)

1. **Export Decklist:**
   - Open a deck in `/decks/:deckId` or the `/decks` edit modal and click **`Export`** in the deck header.
   - In **`Export Decklist`**, verify zones and quantities render cleanly, then test both **`Copy to Clipboard`** (`Copied!`) and **`Download .txt`**.
2. **Import Preview, Ambiguity Resolution & Apply:**
   - Click **`Import`** in the deck header to open **`Import Decklist`**.
   - Paste decklist text into **`Decklist text`** or load a file via **`Upload decklist file`** (`.txt`), mixing formats such as `4x Lightning Bolt`, `2 Sol Ring (CMM) 412`, section headers like `[Sideboard]` / `// Sideboard` / `SIDEBOARD:`, Commander/Starting headers, and an ambiguous or misspelled card name.
   - Click **`Preview`** and verify `Resolved`, `Ambiguous`, and `Unmatched` badges appear accurately; use the **`Select printing for <cardName>`** dropdown for any ambiguous card.
   - Verify both merge mode (default) and **`Replace existing cards in deck (unchecked merges with current cards)`**, then click **`Apply Import (N)`** and confirm all resolved cards appear in the expected deck zones.

---

## 7. Sharing Your Feedback (Creating GitHub Issues)

Please submit any bugs you encounter or feature/UX improvement ideas directly as **GitHub Issues** in the repository:

1. Open the repository Issues page in your browser:
   [https://github.com/Xercius/Trading-Card-Game-Tracker/issues/new/choose](https://github.com/Xercius/Trading-Card-Game-Tracker/issues/new/choose)
2. Choose the template that matches your feedback:

### Reporting a Bug → Select **Bug Report**

Use this when something is broken, throws an error, or doesn't behave as expected:

- **Title:** Add a short summary after `[Bug]: ` (e.g., `[Bug]: MTG filter drawer doesn't close on Escape`).
- **Impact:** Choose how much the issue affects normal use (_Minor_, _Moderate_, _Major_, or _Critical_).
- **Area:** Select one or more affected areas (e.g., _Client/Frontend_, _Card Import_, _Collections_, _Decks_, _Wishlist_, _Card Details_).
- **Affected Game (if applicable):** Select the trading card game(s) where you saw the issue.
- **Bug Description:** Describe what you did and what happened (steps to reproduce).
- **Expected Behavior:** Describe what you expected to happen instead.
- **Screenshots:** Drag and drop or upload any screenshots showing the issue.
- Click **Create** at the bottom of the form.

### Suggesting a Feature or UX Improvement → Select **Feature Request**

Use this for usability improvements, layout/polish ideas, or new capabilities you would like to see:

- **Title:** Add a short summary after `[Feature]: ` (e.g., `[Feature]: Add a "Select All" button to set filters`).
- **Area:** Choose the part of the app affected (e.g., _Client/Frontend_, _Collections_, _Decks_, _Wishlist_, _Card Import_).
- **Affected Game (if applicable):** Select the relevant game(s), if any.
- **Problem / Motivation:** Explain what felt confusing, slow, or missing during your walkthrough (_"As a user, I want to ... so that I can ..."_).
- **Proposed Solution:** Describe how you think it should work or look.
- **Screenshots:** Attach any helpful screenshots or mockups.
- Click **Create** at the bottom of the form.
