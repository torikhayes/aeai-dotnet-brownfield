# Quickstart: Club Scoring & Ratings

## Local validation

1. Restore and build the catalog service:

```bash
dotnet build src/Catalog.API/Catalog.API.csproj
```

2. Run the functional test suite that exercises the catalog API surface:

```bash
dotnet test tests/Catalog.FunctionalTests/Catalog.FunctionalTests.csproj
```

3. If you are creating the migration locally, use the catalog context from the service project:

```bash
dotnet ef migrations add AddClubScoringAndTags --project src/Catalog.API --startup-project src/Catalog.API --context CatalogContext
```

4. Apply the migration against the local PostgreSQL database:

```bash
dotnet ef database update --project src/Catalog.API --startup-project src/Catalog.API --context CatalogContext
```

## Smoke checks

- `GET /api/catalog/items/{id}` should increment `ViewCount`.
- `POST /api/catalog/items/{id}/rate` should update `AverageRating` and `RatingCount`.
- `POST /api/catalog/items/{id}/favorite` should toggle the caller's favorite state.
- `PATCH /api/catalog/items/{id}/tags` should persist seller edits to the item's tags.

## Notes

- Run the service through the existing Aspire/AppHost flow if you need the full dependency graph.
- The feature is intended to remain inside `Catalog.API`; no new service host is required.