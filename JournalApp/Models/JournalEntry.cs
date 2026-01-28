using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JournalApp.Models
{
    /// <summary>
    /// Represents a user account in the system
    /// </summary>
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [MaxLength(256)]
        public string PasswordHash { get; set; } = string.Empty;

        [MaxLength(256)]
        public string? PinHash { get; set; }

        [MaxLength(200)]
        public string? Email { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? LastLoginAt { get; set; }

        [Required]
        public bool IsActive { get; set; } = true;

        [MaxLength(20)]
        public string Theme { get; set; } = "Light";

        public int CurrentStreak { get; set; }
        public int LongestStreak { get; set; }
        public DateTime? LastEntryDate { get; set; }

        // Navigation property
        public virtual ICollection<JournalEntry> JournalEntries { get; set; } = new List<JournalEntry>();
    }

    /// <summary>
    /// Represents a single journal entry for a specific date
    /// </summary>
    public class JournalEntry
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Foreign key to User
        /// </summary>
        [Required]
        public int UserId { get; set; }

        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Date of the journal entry (one per user per day)
        /// </summary>
        [Required]
        public DateTime EntryDate { get; set; }

        /// <summary>
        /// Rich text or Markdown content
        /// </summary>
        [Required]
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// System-generated creation timestamp
        /// </summary>
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// System-generated last update timestamp
        /// </summary>
        [Required]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Primary mood (required)
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string PrimaryMood { get; set; } = string.Empty;

        /// <summary>
        /// Category of the mood (Positive, Neutral, Negative)
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string MoodCategory { get; set; } = string.Empty;

        /// <summary>
        /// Optional secondary moods (up to 2)
        /// </summary>
        [MaxLength(50)]
        public string? SecondaryMood1 { get; set; }

        [MaxLength(20)]
        public string? SecondaryMood1Category { get; set; }

        [MaxLength(50)]
        public string? SecondaryMood2 { get; set; }

        [MaxLength(20)]
        public string? SecondaryMood2Category { get; set; }

        /// <summary>
        /// Entry category (Work, Health, Personal, etc.)
        /// </summary>
        [MaxLength(50)]
        public string? Category { get; set; }

        /// <summary>
        /// Comma-separated tags
        /// </summary>
        [MaxLength(500)]
        public string? Tags { get; set; }

        /// <summary>
        /// Word count for analytics
        /// </summary>
        public int WordCount { get; set; }

        /// <summary>
        /// Whether this is Markdown (true) or Rich Text (false)
        /// </summary>
        public bool IsMarkdown { get; set; }

        // Navigation property
        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;

        // Helper property for tags (not stored)
        [NotMapped]
        public List<string> TagList
        {
            get => string.IsNullOrWhiteSpace(Tags)
                ? new List<string>()
                : Tags.Split(',', StringSplitOptions.RemoveEmptyEntries)
                      .Select(t => t.Trim())
                      .ToList();
            set => Tags = value != null ? string.Join(",", value) : string.Empty;
        }
    }

    /// <summary>
    /// Global app settings (not user-specific)
    /// </summary>
    public class AppSettings
    {
        [Key]
        public int Id { get; set; }

        public int? LastLoggedInUserId { get; set; }

        public bool RememberLastUser { get; set; } = true;
    }

    /// <summary>
    /// Helper class for mood categories
    /// </summary>
    public static class MoodData
    {
        public static readonly Dictionary<string, List<string>> Categories = new()
        {
            { "Positive", new List<string> { "Happy", "Excited", "Relaxed", "Grateful", "Confident" } },
            { "Neutral", new List<string> { "Calm", "Thoughtful", "Curious", "Nostalgic", "Bored" } },
            { "Negative", new List<string> { "Sad", "Angry", "Stressed", "Lonely", "Anxious" } }
        };

        public static string GetMoodCategory(string mood)
        {
            foreach (var category in Categories)
            {
                if (category.Value.Contains(mood, StringComparer.OrdinalIgnoreCase))
                    return category.Key;
            }
            return "Neutral";
        }

        public static readonly List<string> PrebuiltTags = new()
        {
            "Work", "Career", "Studies", "Family", "Friends", "Relationships",
            "Health", "Fitness", "Personal Growth", "Self-care", "Hobbies",
            "Travel", "Nature", "Finance", "Spirituality", "Birthday",
            "Holiday", "Vacation", "Celebration", "Exercise", "Reading",
            "Writing", "Cooking", "Meditation", "Yoga", "Music", "Shopping",
            "Parenting", "Projects", "Planning", "Reflection"
        };
    }
}