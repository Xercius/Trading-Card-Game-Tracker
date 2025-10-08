# Trading Card Game Tracker API

## Entity Framework Migrations

Use the provided scripts from the repository root to keep migrations consistent across environments.

### One-liner usage

PowerShell:

```powershell
./api/scripts/ef-migrations.ps1 "AddCardsTable"
```

Bash:

```bash
./api/scripts/ef-migrations.sh "AddCardsTable"
```

### What the scripts do

* Add a migration under `/Data/Migrations`.
* Apply it to the local SQLite database.
* Target .NET 9 and EF Core 9 tooling.

### Visual Studio Package Manager Console alternative

```powershell
Set-Location .\api
Add-Migration AddCardsTable -OutputDir Data\Migrations
Update-Database
```

### Useful checks

List available migrations:

```bash
dotnet ef migrations list --project ./api/api.csproj --startup-project ./api/api.csproj
```

Inspect the SQLite schema:

```bash
sqlite3 ./api/app.db ".schema"
```

> **Notes**
>
> * Always commit generated migration files.
> * Run the scripts from the repository root so relative paths resolve correctly.
> * Do not commit user-specific SQLite database files.
