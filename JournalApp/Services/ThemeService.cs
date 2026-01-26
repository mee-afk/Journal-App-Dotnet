using JournalApp.Data;
using Microsoft.EntityFrameworkCore;

namespace JournalApp.Services
{
    /// <summary>
    /// Service for managing application theme (per-user)
    /// </summary>
    public class ThemeService
    {
        private readonly JournalDbContext _context;
        private readonly UserService _userService;
        private string _currentTheme = "Light";

        public event EventHandler<string>? ThemeChanged;

        public string CurrentTheme => _currentTheme;

        public ThemeService(JournalDbContext context, UserService userService)
        {
            _context = context;
            _userService = userService;
            LoadTheme();
        }

        /// <summary>
        /// Loads the saved theme for current user
        /// </summary>
        private void LoadTheme()
        {
            try
            {
                if (_userService.CurrentUser != null)
                {
                    _currentTheme = _userService.CurrentUser.Theme ?? "Light";
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
        /// Saves the current theme to database for current user
        /// </summary>
        private async Task SaveThemeAsync()
        {
            try
            {
                if (_userService.CurrentUser != null)
                {
                    var user = await _context.Users.FindAsync(_userService.CurrentUser.Id);
                    if (user != null)
                    {
                        user.Theme = _currentTheme;
                        _context.Users.Update(user);
                        await _context.SaveChangesAsync();

                        // Update current user in UserService
                        _userService.CurrentUser.Theme = _currentTheme;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save theme: {ex.Message}");
            }
        }

        /// <summary>
        /// Refreshes theme from current user
        /// </summary>
        public void RefreshTheme()
        {
            LoadTheme();
            ThemeChanged?.Invoke(this, _currentTheme);
        }
    }
}