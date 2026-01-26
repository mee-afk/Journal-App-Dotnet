using JournalApp.Data;
using Microsoft.EntityFrameworkCore;

namespace JournalApp.Services
{
    /// <summary>
    /// Service for managing application theme
    /// </summary>
    public class ThemeService
    {
        private readonly JournalDbContext _context;
        private string _currentTheme = "Light";

        public event EventHandler<string>? ThemeChanged;

        public string CurrentTheme => _currentTheme;

        public ThemeService(JournalDbContext context)
        {
            _context = context;
            _ = LoadThemeAsync();
        }

        /// <summary>
        /// Loads the saved theme from database
        /// </summary>
        private async Task LoadThemeAsync()
        {
            try
            {
                var settings = await _context.AppSettings.FirstOrDefaultAsync();
                if (settings != null)
                {
                    _currentTheme = settings.Theme ?? "Light";
                }
            }
            catch
            {
                _currentTheme = "Light";
            }
        }

        /// <summary>
        /// Toggles between Light and Dark theme
        /// </summary>
        public async Task ToggleThemeAsync()
        {
            _currentTheme = _currentTheme == "Light" ? "Dark" : "Light";
            await SaveThemeAsync();
            ThemeChanged?.Invoke(this, _currentTheme);
        }

        /// <summary>
        /// Sets a specific theme
        /// </summary>
        public async Task SetThemeAsync(string theme)
        {
            if (theme != "Light" && theme != "Dark")
                return;

            _currentTheme = theme;
            await SaveThemeAsync();
            ThemeChanged?.Invoke(this, _currentTheme);
        }

        /// <summary>
        /// Gets the current theme
        /// </summary>
        public string GetTheme()
        {
            return _currentTheme;
        }

        /// <summary>
        /// Saves the current theme to database
        /// </summary>
        private async Task SaveThemeAsync()
        {
            try
            {
                var settings = await _context.AppSettings.FirstOrDefaultAsync();
                if (settings != null)
                {
                    settings.Theme = _currentTheme;
                    _context.AppSettings.Update(settings);
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save theme: {ex.Message}");
            }
        }
    }
}