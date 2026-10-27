var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/courses", () => Results.Ok(new[]
{
    new { id = 1, code = "CSE201", name = "Veri Yapıları" },
    new { id = 2, code = "CSE203", name = "Veritabanı Yönetimi" }
}));

app.Run();
