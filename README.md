# Milk Yield Tracker

A small .NET Web API that records daily milk yields per cow and flags any cow whose output has dropped noticeably below her own normal.

**Stack:** C# / .NET 10, ASP.NET Core Web API, Entity Framework Core, SQL Server, xUnit.

## Why this project

In 2024 I built an Excel milk-yield tracker (with AI help) for the dairy farm I worked on in the Tsitsikamma. This is the same idea rebuilt as a database-backed API: record each milking, then ask the system which cows need a closer look today.

## How it works

The core is one rule, kept separate from the database code so it is easy to read and test (`Services/YieldDropDetector.cs`):

> Compare a cow's total litres for a day with the average of **her own** complete days in the previous 7 days. If today is **more than 15% below** that average, flag her.

Example: a cow averaging 24 L/day who gives 16.8 L today is 30% down and gets flagged. A cow averaging 21 L/day who gives 18.9 L is 10% down and does not.

Design decisions behind the rule:

- **Each cow is compared with herself**, not with the herd, because a naturally low producer is not a problem cow.
- **Only complete days count.** A day needs both milkings recorded. Without this, checking at midday would flag every cow, because the evening milking is missing.
- **Missing days are skipped, not counted as zero**, so a gap in record-keeping does not drag a cow's baseline down.
- **At least 3 complete days of history** are needed before a cow is judged. A newly added cow gets `InsufficientBaseline` instead of a guess.
- **"More than 15%" is strict.** A drop of exactly 15% is not flagged.
- **The numbers are settings, not code.** Threshold, window length, minimum history and milkings per day live in `appsettings.json` under `YieldDrop`.

Every result says *why* it came out as it did (litres today, baseline average, days used, drop %), so a flag can be checked by hand.

## Running it

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download) and a SQL Server instance. It was run and tested against SQL Server Express at `localhost\SQLEXPRESS`; for any other instance, change `ConnectionStrings:MilkDb` in `src/MilkYieldTracker.Api/appsettings.json`.

```
dotnet run --project src/MilkYieldTracker.Api --launch-profile http
```

In Development, the first run creates the database from the migration and loads a small **made-up** herd of six cows so there is something to query. Then try:

```
GET http://localhost:5099/api/alerts/yield-drops
```

`src/MilkYieldTracker.Api/MilkYieldTracker.Api.http` has ready-made requests for every endpoint (works in VS Code with the REST Client extension and in Visual Studio). Demo data is generated relative to the day of the first run; to check another day, pass `?date=YYYY-MM-DD`.

## Endpoints

| Method | Path | Purpose |
|---|---|---|
| GET | `/api/alerts/yield-drops?date=` | Which cows are flagged on a day (default: today) |
| GET | `/api/cows/{id}/yield-check?date=` | The rule's full working for one cow |
| GET / POST | `/api/cows` | List cows / add a cow |
| GET / PUT | `/api/cows/{id}` | Read a cow / rename or mark inactive |
| GET / POST | `/api/cows/{id}/sessions` | Read / record milkings |

Validation covers duplicate ear tags, litres outside 0.1-60 per milking, future dates, duplicate milkings, and inactive cows.

## Tests

```
dotnet test
```

24 tests: 12 on the rule itself (thresholds, boundaries, missing and half-recorded days, zero baseline) and 12 end-to-end tests that send real HTTP requests to the API. The end-to-end tests use an in-memory SQLite database, so they need no server and cannot touch real data. The app itself targets SQL Server, and was also run against SQL Server Express to confirm the migration, demo data and alerts work there.

Two bugs were found by these tests while building it: record validation attributes were attached in a way ASP.NET Core rejects, and milking periods (stored as text) sorted alphabetically, putting Evening before Morning.

## Limitations

- No user interface and no authentication; it is an API only.
- Assumes two milkings per day (change `SessionsPerDay` for other herds).
- "Today" is the server's clock.
- The rule only looks at litres. It knows nothing about lactation stage, feed changes, weather or illness, so a flag means "look at this cow", not "this cow is sick".
- Demo data is invented. No real farm data is in this repository.

## Built with AI assistance

The code, tests and this README were written by Claude (Anthropic's AI assistant) in a chat session with me, and the commit history carries a `Co-Authored-By` line for that reason. I chose the project and agreed the rule's starting values (15% below a 7-day average) and the database (SQL Server). Claude made the other design decisions listed above while building it.
