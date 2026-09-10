$programPath = "backend/AgriAssist.Api/Program.cs"
$program = Get-Content -LiteralPath $programPath -Raw
$old = @'
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (dbContext.Database.IsInMemory())
    {
        await dbContext.Database.EnsureCreatedAsync();
        await SeedData.SeedAsync(dbContext);
    }
}
'@
$new = @'
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (dbContext.Database.IsInMemory())
    {
        await dbContext.Database.EnsureCreatedAsync();
    }

    await SeedData.SeedAsync(dbContext);
}
'@
$program = $program.Replace($old, $new)
Set-Content -LiteralPath $programPath -Value $program -Encoding ASCII
