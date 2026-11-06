using ST10389916_CLDV7112_Practicum;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationInsightsTelemetry();
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<AzureManagementService>();
builder.Services.AddSingleton<UserAccountStore>();
builder.Services.AddSingleton<BlobMediaStore>();
builder.Services.AddHttpClient<FunctionGateway>();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    utc = DateTimeOffset.UtcNow
}));

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
