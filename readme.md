Building docker image:  
docker build . -f Headhunter.API/Dockerfile -t headhunter  

Saving docker image:  
docker save headhunter -o headhunter.tar  

Adding a migration (one per provider; the model is shared):  
`dotnet ef migrations add <Name> --project Headhunter.Database` (SQL Server)  
`dotnet ef migrations add <Name> --project Headhunter.Migrations.Postgres` (PostgreSQL)  

Design-time connection string: `dotnet ef` and the migrations bundles read `ConnectionStrings:Headhunter` from
`appsettings.json`, then user secrets, then the environment. Both migration projects share one user-secrets id, so
a stored secret holds one provider's string. Give each command its own through the environment instead:  
`ConnectionStrings__Headhunter='Server=localhost;Database=Headhunter;User Id=sa;Password=...;TrustServerCertificate=True' dotnet ef database update --project Headhunter.Database`  
`ConnectionStrings__Headhunter='Host=localhost;Database=headhunter;Username=postgres;Password=...' dotnet ef database update --project Headhunter.Migrations.Postgres`  

Copying a database to the other provider: `/app/convert` in the image (project `Headhunter.Convert`). It takes
`Source__Provider`, `Source__ConnectionString`, `Target__Provider`, `Target__ConnectionString`, migrates the target,
refuses one that already has rows, and copies in one transaction, checking row counts before it commits.
