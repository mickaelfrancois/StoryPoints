using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.EntityFrameworkCore;
using StoryPoints.Components;
using StoryPoints.Data;
using StoryPoints.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var dbPath = Path.Combine(builder.Environment.ContentRootPath, "StoryPoints.db");
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<RoomCoordinator>();
builder.Services.AddSingleton<RoomCreationGuard>();
builder.Services.AddScoped<CircuitMemberTracker>();
builder.Services.AddScoped<CircuitHandler>(sp => sp.GetRequiredService<CircuitMemberTracker>());
builder.Services.Configure<CleanupOptions>(builder.Configuration.GetSection(CleanupOptions.SectionName));
builder.Services.Configure<RoomLimitOptions>(builder.Configuration.GetSection(RoomLimitOptions.SectionName));
builder.Services.AddHostedService<CleanupService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    using var db = factory.CreateDbContext();
    db.Database.EnsureCreated();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
