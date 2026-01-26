using JournalApp.Services;
using Microsoft.EntityFrameworkCore;
using JournalApp.Data;
using JournalApp.Models;

namespace JournalApp.Services
{
    /// <summary>
    /// Service for managing journal entries (multi-user)
    /// </summary>
    public class JournalService
    {
        private readonly JournalDbContext _context;
        private readonly UserService _userService;

        public JournalService(JournalDbContext context, UserService userService)
        {
            _context = context;
            _userService = userService;
        }

        private int CurrentUserId => _userService.CurrentUser?.Id ?? 0;

        #region CRUD Operations

        /// <summary>
        /// Gets the journal entry for a specific date for current user
        /// </summary>
        public async Task<JournalEntry?> GetEntryByDateAsync(DateTime date)
        {
            if (CurrentUserId == 0) return null;

            var dateOnly = date.Date;
            return await _context.JournalEntries
                .FirstOrDefaultAsync(e => e.UserId == CurrentUserId && e.EntryDate.Date == dateOnly);
        }

        /// <summary>
        /// Gets a journal entry by ID (only if belongs to current user)
        /// </summary>
        public async Task<JournalEntry?> GetEntryByIdAsync(int id)
        {
            if (CurrentUserId == 0) return null;

            return await _context.JournalEntries
                .FirstOrDefaultAsync(e => e.Id == id && e.UserId == CurrentUserId);
        }

        /// <summary>
        /// Gets all journal entries for current user
        /// </summary>
        public async Task<List<JournalEntry>> GetAllEntriesAsync()
        {
            if (CurrentUserId == 0) return new List<JournalEntry>();

            return await _context.JournalEntries
                .Where(e => e.UserId == CurrentUserId)
                .OrderByDescending(e => e.EntryDate)
                .ToListAsync();
        }

        /// <summary>
        /// Creates or updates a journal entry for current user
        /// </summary>
        public async Task<JournalEntry> SaveEntryAsync(JournalEntry entry)
        {
            if (CurrentUserId == 0)
                throw new InvalidOperationException("No user logged in");

            entry.UpdatedAt = DateTime.Now;
            entry.EntryDate = entry.EntryDate.Date;
            entry.UserId = CurrentUserId;

            var existing = await GetEntryByDateAsync(entry.EntryDate);

            if (existing != null)
            {
                // Update existing entry
                existing.Content = entry.Content;
                existing.PrimaryMood = entry.PrimaryMood;
                existing.MoodCategory = entry.MoodCategory;
                existing.SecondaryMood1 = entry.SecondaryMood1;
                existing.SecondaryMood1Category = entry.SecondaryMood1Category;
                existing.SecondaryMood2 = entry.SecondaryMood2;
                existing.SecondaryMood2Category = entry.SecondaryMood2Category;
                existing.Category = entry.Category;
                existing.Tags = entry.Tags;
                existing.WordCount = entry.WordCount;
                existing.IsMarkdown = entry.IsMarkdown;
                existing.UpdatedAt = DateTime.Now;

                _context.JournalEntries.Update(existing);
                await _context.SaveChangesAsync();
                return existing;
            }
            else
            {
                // Create new entry
                entry.CreatedAt = DateTime.Now;
                _context.JournalEntries.Add(entry);
                await _context.SaveChangesAsync();

                // Update user streak
                //await _userService.UpdateUserStreakAsync(CurrentUserId, entry.EntryDate);
                await _userService.UpdateUserStreakAsync(_userService.CurrentUser);


                return entry;
            }
        }

        /// <summary>
        /// Deletes a journal entry (only if belongs to current user)
        /// </summary>
        public async Task<bool> DeleteEntryAsync(int id)
        {
            if (CurrentUserId == 0) return false;

            var entry = await _context.JournalEntries
                .FirstOrDefaultAsync(e => e.Id == id && e.UserId == CurrentUserId);

            if (entry == null)
                return false;

            _context.JournalEntries.Remove(entry);
            await _context.SaveChangesAsync();

            return true;
        }

        #endregion

        #region Search & Filter

        /// <summary>
        /// Gets entries by date range for current user
        /// </summary>
        public async Task<List<JournalEntry>> GetEntriesByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            if (CurrentUserId == 0) return new List<JournalEntry>();

            return await _context.JournalEntries
                .Where(e => e.UserId == CurrentUserId &&
                           e.EntryDate.Date >= startDate.Date &&
                           e.EntryDate.Date <= endDate.Date)
                .OrderByDescending(e => e.EntryDate)
                .ToListAsync();
        }

        /// <summary>
        /// Complex search with multiple filters for current user
        /// </summary>
        public async Task<List<JournalEntry>> SearchEntriesAsync(
            string? searchText = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            List<string>? moods = null,
            List<string>? tags = null,
            string? category = null)
        {
            if (CurrentUserId == 0) return new List<JournalEntry>();

            var query = _context.JournalEntries
                .Where(e => e.UserId == CurrentUserId)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                query = query.Where(e => e.Content.Contains(searchText));
            }

            if (startDate.HasValue)
            {
                query = query.Where(e => e.EntryDate.Date >= startDate.Value.Date);
            }

            if (endDate.HasValue)
            {
                query = query.Where(e => e.EntryDate.Date <= endDate.Value.Date);
            }

            if (moods != null && moods.Any())
            {
                query = query.Where(e =>
                    moods.Contains(e.PrimaryMood) ||
                    (e.SecondaryMood1 != null && moods.Contains(e.SecondaryMood1)) ||
                    (e.SecondaryMood2 != null && moods.Contains(e.SecondaryMood2)));
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(e => e.Category == category);
            }

            if (tags != null && tags.Any())
            {
                foreach (var tag in tags)
                {
                    query = query.Where(e => e.Tags != null && e.Tags.Contains(tag));
                }
            }

            return await query.OrderByDescending(e => e.EntryDate).ToListAsync();
        }

        #endregion

        #region Pagination

        /// <summary>
        /// Gets paginated entries for current user
        /// </summary>
        public async Task<(List<JournalEntry> Entries, int TotalCount)> GetPaginatedEntriesAsync(
            int pageNumber, int pageSize)
        {
            if (CurrentUserId == 0) return (new List<JournalEntry>(), 0);

            var query = _context.JournalEntries
                .Where(e => e.UserId == CurrentUserId)
                .OrderByDescending(e => e.EntryDate);

            var totalCount = await query.CountAsync();
            var entries = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (entries, totalCount);
        }

        #endregion

        #region Analytics

        /// <summary>
        /// Gets mood distribution for current user
        /// </summary>
        public async Task<Dictionary<string, int>> GetMoodDistributionAsync(
            DateTime? startDate = null, DateTime? endDate = null)
        {
            if (CurrentUserId == 0) return new Dictionary<string, int>();

            var query = _context.JournalEntries
                .Where(e => e.UserId == CurrentUserId)
                .AsQueryable();

            if (startDate.HasValue)
                query = query.Where(e => e.EntryDate.Date >= startDate.Value.Date);
            if (endDate.HasValue)
                query = query.Where(e => e.EntryDate.Date <= endDate.Value.Date);

            var entries = await query.ToListAsync();

            var distribution = new Dictionary<string, int>
            {
                { "Positive", 0 },
                { "Neutral", 0 },
                { "Negative", 0 }
            };

            foreach (var entry in entries)
            {
                if (distribution.ContainsKey(entry.MoodCategory))
                    distribution[entry.MoodCategory]++;
            }

            return distribution;
        }

        /// <summary>
        /// Gets most frequent mood for current user
        /// </summary>
        public async Task<string?> GetMostFrequentMoodAsync(
            DateTime? startDate = null, DateTime? endDate = null)
        {
            if (CurrentUserId == 0) return null;

            var query = _context.JournalEntries
                .Where(e => e.UserId == CurrentUserId)
                .AsQueryable();

            if (startDate.HasValue)
                query = query.Where(e => e.EntryDate.Date >= startDate.Value.Date);
            if (endDate.HasValue)
                query = query.Where(e => e.EntryDate.Date <= endDate.Value.Date);

            return await query
                .GroupBy(e => e.PrimaryMood)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Gets most used tags for current user
        /// </summary>
        public async Task<Dictionary<string, int>> GetMostUsedTagsAsync(
            DateTime? startDate = null, DateTime? endDate = null, int topCount = 10)
        {
            if (CurrentUserId == 0) return new Dictionary<string, int>();

            var query = _context.JournalEntries
                .Where(e => e.UserId == CurrentUserId)
                .AsQueryable();

            if (startDate.HasValue)
                query = query.Where(e => e.EntryDate.Date >= startDate.Value.Date);
            if (endDate.HasValue)
                query = query.Where(e => e.EntryDate.Date <= endDate.Value.Date);

            var entries = await query.Where(e => e.Tags != null).ToListAsync();

            var tagCounts = new Dictionary<string, int>();

            foreach (var entry in entries)
            {
                var tags = entry.TagList;
                foreach (var tag in tags)
                {
                    if (tagCounts.ContainsKey(tag))
                        tagCounts[tag]++;
                    else
                        tagCounts[tag] = 1;
                }
            }

            return tagCounts.OrderByDescending(kvp => kvp.Value)
                           .Take(topCount)
                           .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        /// <summary>
        /// Gets word count trends for current user
        /// </summary>
        public async Task<Dictionary<DateTime, double>> GetWordCountTrendsAsync(
            DateTime? startDate = null, DateTime? endDate = null)
        {
            if (CurrentUserId == 0) return new Dictionary<DateTime, double>();

            var query = _context.JournalEntries
                .Where(e => e.UserId == CurrentUserId)
                .AsQueryable();

            if (startDate.HasValue)
                query = query.Where(e => e.EntryDate.Date >= startDate.Value.Date);
            if (endDate.HasValue)
                query = query.Where(e => e.EntryDate.Date <= endDate.Value.Date);

            var entries = await query
                .OrderBy(e => e.EntryDate)
                .Select(e => new { e.EntryDate, e.WordCount })
                .ToListAsync();

            return entries.ToDictionary(e => e.EntryDate.Date, e => (double)e.WordCount);
        }

        /// <summary>
        /// Gets total entry count for current user
        /// </summary>
        public async Task<int> GetTotalEntryCountAsync()
        {
            if (CurrentUserId == 0) return 0;

            return await _context.JournalEntries
                .Where(e => e.UserId == CurrentUserId)
                .CountAsync();
        }

        /// <summary>
        /// Gets missed days for current user
        /// </summary>
        public async Task<List<DateTime>> GetMissedDaysAsync(DateTime startDate, DateTime endDate)
        {
            if (CurrentUserId == 0) return new List<DateTime>();

            var entries = await _context.JournalEntries
                .Where(e => e.UserId == CurrentUserId &&
                           e.EntryDate.Date >= startDate.Date &&
                           e.EntryDate.Date <= endDate.Date)
                .Select(e => e.EntryDate.Date)
                .ToListAsync();

            var missedDays = new List<DateTime>();
            for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
            {
                if (!entries.Contains(date))
                {
                    missedDays.Add(date);
                }
            }

            return missedDays;
        }

        #endregion
    }
}