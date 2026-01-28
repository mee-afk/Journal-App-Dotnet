using System.Security.Cryptography;
using System.Text;
using JournalApp.Data;
using JournalApp.Models;
using Microsoft.EntityFrameworkCore;

namespace JournalApp.Services
{
    /// <summary>
    /// Service for handling user authentication and PIN protection (Multi-User)
    /// </summary>
    public class AuthenticationService
    {
        private readonly JournalDbContext _context;
        private readonly UserService _userService;

        public AuthenticationService(JournalDbContext context, UserService userService)
        {
            _context = context;
            _userService = userService;
        }

        #region PIN Management - Enhanced for Settings Page

        /// <summary>
        /// Checks if current user has a PIN set
        /// </summary>
        public async Task<bool> IsPinSetAsync()
        {
            var currentUser = _userService.CurrentUser;
            if (currentUser == null) return false;

            var user = await _context.Users.FindAsync(currentUser.Id);
            return user != null && !string.IsNullOrEmpty(user.PinHash);
        }

        /// <summary>
        /// Sets up a new PIN for current user
        /// UPDATED: Now supports both 4-digit (legacy) and 6-digit (new auto-generated) PINs
        /// </summary>
        public async Task<bool> SetPinAsync(string pin)
        {
            var currentUser = _userService.CurrentUser;
            if (currentUser == null) return false;

            // Validate PIN: Must be 4 or 6 digits (for backward compatibility and new system)
            if (string.IsNullOrWhiteSpace(pin) ||
                (pin.Length != 4 && pin.Length != 6) ||
                !pin.All(char.IsDigit))
                return false;

            var user = await _context.Users.FindAsync(currentUser.Id);
            if (user == null) return false;

            user.PinHash = HashPin(pin);
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            // Update current user in UserService
            _userService.RefreshCurrentUser(user);

            return true;
        }

        /// <summary>
        /// Validates a PIN for current user
        /// </summary>
        public async Task<bool> ValidatePinAsync(string pin)
        {
            var currentUser = _userService.CurrentUser;
            if (currentUser == null) return false;

            var user = await _context.Users.FindAsync(currentUser.Id);
            if (user == null || string.IsNullOrEmpty(user.PinHash))
                return false;

            var hashedInput = HashPin(pin);
            return hashedInput == user.PinHash;
        }

        /// <summary>
        /// Changes the PIN for current user
        /// NOTE: This is kept for backward compatibility but the new Settings page
        /// uses delete + regenerate workflow instead
        /// </summary>
        public async Task<bool> ChangePinAsync(string oldPin, string newPin)
        {
            if (!await ValidatePinAsync(oldPin))
                return false;

            if (string.IsNullOrWhiteSpace(newPin) ||
                (newPin.Length != 4 && newPin.Length != 6) ||
                !newPin.All(char.IsDigit))
                return false;

            return await SetPinAsync(newPin);
        }

        /// <summary>
        /// Removes the PIN protection for current user
        /// UPDATED: Simplified for new Settings page - no longer requires PIN verification
        /// (Settings page handles confirmation via UI)
        /// </summary>
        public async Task<bool> RemovePinAsync(string currentPin = "")
        {
            var currentUser = _userService.CurrentUser;
            if (currentUser == null) return false;

            var user = await _context.Users.FindAsync(currentUser.Id);
            if (user == null) return false;

            // Clear PIN without verification (Settings page handles UI confirmation)
            user.PinHash = null;
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            // Update current user in UserService
            _userService.RefreshCurrentUser(user);

            return true;
        }

        #endregion

        #region PIN Login

        /// <summary>
        /// Attempts to login using PIN (for quick login)
        /// </summary>
        public async Task<bool> LoginWithPinAsync(string username, string pin)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == username);

            if (user == null || string.IsNullOrEmpty(user.PinHash))
                return false;

            var hashedInput = HashPin(pin);
            if (hashedInput == user.PinHash)
            {
                _userService.SetCurrentUser(user);
                user.LastLoginAt = DateTime.Now;
                await _context.SaveChangesAsync();
                return true;
            }

            return false;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Hashes a PIN using SHA256
        /// </summary>
        private string HashPin(string pin)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(pin);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        #endregion
    }
}