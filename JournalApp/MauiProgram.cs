using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using JournalApp.Data;
using JournalApp.Services;

namespace JournalApp
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            // Add Blazor WebView
            builder.Services.AddMauiBlazorWebView();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif

            // Configure SQLite Database with Entity Framework Core
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "journal.db3");

            System.Diagnostics.Debug.WriteLine($"Database path: {dbPath}");

            builder.Services.AddDbContext<JournalDbContext>(options =>
                options.UseSqlite($"Data Source={dbPath}"));

            // Register Services
            builder.Services.AddScoped<JournalService>();
            builder.Services.AddScoped<AuthenticationService>();
            builder.Services.AddScoped<ExportService>();
            builder.Services.AddScoped<ThemeService>();

            // Build the app
            var app = builder.Build();

            // Initialize database on startup
            Task.Run(async () =>
            {
                try
                {
                    using var scope = app.Services.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<JournalDbContext>();
                    await context.InitializeDatabaseAsync();

                    System.Diagnostics.Debug.WriteLine("Database initialized successfully");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Database initialization failed: {ex.Message}");
                }
            }).Wait();

            return app;
        }
    }
}