using System.Security.Cryptography;
using System.Text;

using Microsoft.EntityFrameworkCore;

using JournalApp.Data;
using JournalApp.Models;
using JournalApp.Services;


namespace JournalApp.Services
{
    /// <summary>
    /// Service for managing user accounts and authentication
    /// </summary>
    public class UserService
    {
        private readonly JournalDbContext _context;
        private readonly ThemeService _themeService;
        private User? _currentUser;

        public User? CurrentUser => _currentUser;
        public bool IsLoggedIn => _currentUser != null;
        public int CurrentUserId => _currentUser?.Id ?? 0;

        public event EventHandler<User>? UserLoggedIn;
        public event EventHandler? UserLoggedOut;
        public event EventHandler<string>? ThemeChanged;

        public UserService(JournalDbContext context, ThemeService themeService)
        {
            _context = context;
            _themeService = themeService;
        }

        #region User Registration & Login

        /// <summary>
        /// Registers a new user
        /// </summary>
        public async Task<(bool Success, string Message)> RegisterUserAsync(
            string username,
            string fullName,
            string password,
            string? email = null)
        {
            // Validation
            if (string.IsNullOrWhiteSpace(username))
                return (false, "Username is required");

            if (string.IsNullOrWhiteSpace(fullName))
                return (false, "Full name is required");

            if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
                return (false, "Password must be at least 6 characters");

            // Check if username already exists
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());

            if (existingUser != null)
                return (false, "Username already exists");

            // Create new user
            var user = new User
            {
                Username = username,
                FullName = fullName,
                Email = email,
                PasswordHash = HashPassword(password),
                CreatedAt = DateTime.Now,
                IsActive = true,
                Theme = "Light",
                CurrentStreak = 0,
                LongestStreak = 0
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return (true, "Registration successful");
        }

        /// <summary>
        /// Logs in a user with username and password
        /// </summary>
        public async Task<(bool Success, string Message)> LoginAsync(
            string username,
            string password)
        {
            if (string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password))
            {
                return (false, "Username and password are required");
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());

            if (user == null)
                return (false, "Invalid username or password");

            if (!user.IsActive)
                return (false, "Account is disabled");

            var hashedInput = HashPassword(password);
            if (hashedInput != user.PasswordHash)
                return (false, "Invalid username or password");

            // Update last login
            user.LastLoginAt = DateTime.Now;
            await _context.SaveChangesAsync();

            // Set current user
            _currentUser = user;
            _themeService.LoadTheme(user);

            UserLoggedIn?.Invoke(this, user);

            return (true, "Login successful");
        }

        /// <summary>
        /// Logs in a user with PIN (quick login)
        /// </summary>
        public async Task<(bool Success, string Message, List<User>? Users)> LoginWithPinAsync(
            string pin)
        {
            try
            {
                var hashedPin = HashPassword(pin);

                // Find all users with this PIN
                var usersWithPin = await _context.Users
                    .Where(u => u.PinHash == hashedPin && u.IsActive)
                    .ToListAsync();

                if (!usersWithPin.Any())
                    return (false, "Invalid PIN", null);

                if (usersWithPin.Count == 1)
                {
                    // Only one user has this PIN - auto login
                    var user = usersWithPin[0];
                    user.LastLoginAt = DateTime.Now;
                    await _context.SaveChangesAsync();

                    _currentUser = user;
                    _themeService.LoadTheme(user);
                    UserLoggedIn?.Invoke(this, user);

                    return (true, "Login successful", null);
                }
                else
                {
                    // Multiple users have the same PIN - need to select
                    return (true, "Multiple users found", usersWithPin);
                }
            }
            catch (Exception ex)
            {
                return (false, $"PIN login failed: {ex.Message}", null);
            }
        }

        /// <summary>
        /// Selects a specific user after PIN matches multiple accounts
        /// </summary>
        public async Task<(bool Success, string Message)> SelectUserAfterPinAsync(
            int userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null || !user.IsActive)
                    return (false, "User not found");

                user.LastLoginAt = DateTime.Now;
                await _context.SaveChangesAsync();

                _currentUser = user;
                _themeService.LoadTheme(user);
                UserLoggedIn?.Invoke(this, user);

                return (true, "Login successful");
            }
            catch (Exception ex)
            {
                return (false, $"User selection failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Logs out the current user
        /// </summary>
        public void Logout()
        {
            _currentUser = null;
            UserLoggedOut?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Sets the current user (used by AuthenticationService for PIN login)
        /// </summary>
        public void SetCurrentUser(User user)
        {
            _currentUser = user;
            _themeService.LoadTheme(user);
            UserLoggedIn?.Invoke(this, user);
        }

        /// <summary>
        /// Loads the last logged in user on app start
        /// </summary>
        public async Task LoadLastUserAsync()
        {
            _currentUser = await _context.Users
                .OrderByDescending(u => u.LastLoginAt)
                .FirstOrDefaultAsync();
        }

        #endregion

        #region Password Management

        /// <summary>
        /// Changes the current user's password
        /// </summary>
        public async Task<(bool Success, string Message)> ChangePasswordAsync(
            string currentPassword,
            string newPassword,
            string confirmPassword)
        {
            try
            {
                if (_currentUser == null)
                    return (false, "No user logged in");

                if (string.IsNullOrWhiteSpace(currentPassword))
                    return (false, "Current password is required");

                if (string.IsNullOrWhiteSpace(newPassword))
                    return (false, "New password is required");

                if (newPassword.Length < 6)
                    return (false, "New password must be at least 6 characters");

                if (newPassword != confirmPassword)
                    return (false, "New passwords do not match");

                var currentHash = HashPassword(currentPassword);
                if (_currentUser.PasswordHash != currentHash)
                    return (false, "Current password is incorrect");

                var user = await _context.Users.FindAsync(_currentUser.Id);
                if (user == null)
                    return (false, "User not found");

                user.PasswordHash = HashPassword(newPassword);
                await _context.SaveChangesAsync();

                _currentUser.PasswordHash = user.PasswordHash;

                return (true, "Password changed successfully");
            }
            catch (Exception ex)
            {
                return (false, $"Password change failed: {ex.Message}");
            }
        }

        /// <summary>
        /// OVERLOAD: Simplified password change for Settings page (returns bool)
        /// </summary>
        public async Task<bool> ChangePasswordAsync(string currentPassword, string newPassword)
        {
            var result = await ChangePasswordAsync(currentPassword, newPassword, newPassword);
            return result.Success;
        }

        #endregion

        #region User Profile Management

        /// <summary>
        /// Updates user profile information
        /// </summary>
        public async Task<bool> UpdateUserAsync(User user)
        {
            try
            {
                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                if (_currentUser?.Id == user.Id)
                {
                    _currentUser = user;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Updates user's theme preference
        /// </summary>
        public async Task<bool> UpdateUserThemeAsync(string theme)
        {
            if (_currentUser == null)
                return false;

            var user = await _context.Users.FindAsync(_currentUser.Id);
            if (user == null)
                return false;

            user.Theme = theme;
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            _currentUser.Theme = theme;
            ThemeChanged?.Invoke(this, theme);

            return true;
        }

        /// <summary>
        /// Refreshes the current user data from database
        /// </summary>
        public async Task<bool> RefreshCurrentUserAsync()
        {
            if (_currentUser == null)
                return false;

            var user = await _context.Users.FindAsync(_currentUser.Id);
            if (user == null)
                return false;

            _currentUser = user;
            return true;
        }

        /// <summary>
        /// Refreshes current user with provided user object
        /// </summary>
        public void RefreshCurrentUser(User user)
        {
            if (_currentUser?.Id == user.Id)
            {
                _currentUser = user;
            }
        }

        /// <summary>
        /// Checks if current user has a PIN set
        /// </summary>
        public bool HasPin()
        {
            return _currentUser != null &&
                   !string.IsNullOrEmpty(_currentUser.PinHash);
        }

        /// <summary>
        /// Deletes the current user's account and all associated data
        /// </summary>
        public async Task<bool> DeleteAccountAsync()
        {
            if (_currentUser == null)
                return false;

            try
            {
                var user = await _context.Users.FindAsync(_currentUser.Id);
                if (user == null)
                    return false;

                // Delete all user's journal entries
                var entries = await _context.JournalEntries
                    .Where(e => e.UserId == user.Id)
                    .ToListAsync();

                if (entries.Any())
                {
                    _context.JournalEntries.RemoveRange(entries);
                }

                // Delete user
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();

                // Clear current user
                _currentUser = null;
                UserLoggedOut?.Invoke(this, EventArgs.Empty);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting account: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Streak Management

        /// <summary>
        /// Updates user's journaling streak
        /// </summary>
        public async Task UpdateUserStreakAsync(User user)
        {
            var today = DateTime.Today;

            if (user.LastEntryDate == null)
            {
                user.CurrentStreak = 1;
                user.LongestStreak = 1;
            }
            else
            {
                var daysSinceLastEntry =
                    (today - user.LastEntryDate.Value.Date).Days;

                if (daysSinceLastEntry == 1)
                {
                    user.CurrentStreak++;
                    if (user.CurrentStreak > user.LongestStreak)
                        user.LongestStreak = user.CurrentStreak;
                }
                else if (daysSinceLastEntry > 1)
                {
                    user.CurrentStreak = 1;
                }
            }

            user.LastEntryDate = today;
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            if (_currentUser?.Id == user.Id)
            {
                _currentUser = user;
            }
        }

        /// <summary>
        /// Recalculates user streaks from all their entries
        /// </summary>
        public async Task RecalculateUserStreaksAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return;

            var allEntries = await _context.JournalEntries
                .Where(e => e.UserId == userId)
                .OrderBy(e => e.EntryDate)
                .Select(e => e.EntryDate.Date)
                .ToListAsync();

            if (!allEntries.Any())
            {
                user.CurrentStreak = 0;
                user.LongestStreak = 0;
                user.LastEntryDate = null;
            }
            else
            {
                int currentStreak = 1;
                int longestStreak = 1;

                for (int i = 1; i < allEntries.Count; i++)
                {
                    var daysDiff =
                        (allEntries[i] - allEntries[i - 1]).Days;

                    if (daysDiff == 1)
                    {
                        currentStreak++;
                        if (currentStreak > longestStreak)
                            longestStreak = currentStreak;
                    }
                    else
                    {
                        currentStreak = 1;
                    }
                }

                var lastEntry = allEntries.Last();
                var daysSinceLastEntry =
                    (DateTime.Today - lastEntry).Days;

                if (daysSinceLastEntry > 1)
                    currentStreak = 0;

                user.CurrentStreak = currentStreak;
                user.LongestStreak = longestStreak;
                user.LastEntryDate = lastEntry;
            }

            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            if (_currentUser?.Id == userId)
            {
                _currentUser = user;
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Hashes a password using SHA256
        /// </summary>
        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        /// <summary>
        /// Gets a user by ID
        /// </summary>
        public async Task<User?> GetUserByIdAsync(int userId)
        {
            return await _context.Users.FindAsync(userId);
        }

        /// <summary>
        /// Gets all active users (for admin purposes)
        /// </summary>
        public async Task<List<User>> GetAllUsersAsync()
        {
            return await _context.Users
                .Where(u => u.IsActive)
                .OrderBy(u => u.Username)
                .ToListAsync();
        }

        #endregion
    }
}