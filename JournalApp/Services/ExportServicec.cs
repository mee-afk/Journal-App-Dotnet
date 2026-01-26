using JournalApp.Models;
using System.Text;
using System.Text.RegularExpressions;

namespace JournalApp.Services
{
    /// <summary>
    /// Service for exporting journal entries to HTML and text files
    /// </summary>
    public class ExportService
    {
        private readonly JournalService _journalService;

        public ExportService(JournalService journalService)
        {
            _journalService = journalService;
        }

        /// <summary>
        /// Exports entries to HTML file
        /// </summary>
        public async Task<string> ExportToHtmlAsync(DateTime startDate, DateTime endDate)
        {
            var entries = await _journalService.GetEntriesByDateRangeAsync(startDate, endDate);

            if (!entries.Any())
            {
                throw new InvalidOperationException("No entries found in the specified date range.");
            }

            var fileName = $"Journal_{startDate:yyyyMMdd}_to_{endDate:yyyyMMdd}.html";
            var filePath = Path.Combine(FileSystem.AppDataDirectory, fileName);

            var html = GenerateHtml(entries, startDate, endDate);
            await File.WriteAllTextAsync(filePath, html);

            return filePath;
        }

        /// <summary>
        /// Exports entries to plain text file
        /// </summary>
        public async Task<string> ExportToTextAsync(DateTime startDate, DateTime endDate)
        {
            var entries = await _journalService.GetEntriesByDateRangeAsync(startDate, endDate);

            if (!entries.Any())
            {
                throw new InvalidOperationException("No entries found in the specified date range.");
            }

            var fileName = $"Journal_{startDate:yyyyMMdd}_to_{endDate:yyyyMMdd}.txt";
            var filePath = Path.Combine(FileSystem.AppDataDirectory, fileName);

            var text = GenerateText(entries, startDate, endDate);
            await File.WriteAllTextAsync(filePath, text);

            return filePath;
        }

        /// <summary>
        /// Generates HTML content for export
        /// </summary>
        private string GenerateHtml(List<JournalEntry> entries, DateTime startDate, DateTime endDate)
        {
            var sb = new StringBuilder();

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang='en'>");
            sb.AppendLine("<head>");
            sb.AppendLine("    <meta charset='UTF-8'>");
            sb.AppendLine("    <meta name='viewport' content='width=device-width, initial-scale=1.0'>");
            sb.AppendLine($"    <title>Journal Entries: {startDate:MMM dd, yyyy} - {endDate:MMM dd, yyyy}</title>");
            sb.AppendLine("    <style>");
            sb.AppendLine(@"
                body {
                    font-family: 'Segoe UI', Arial, sans-serif;
                    max-width: 900px;
                    margin: 0 auto;
                    padding: 20px;
                    background-color: #f5f5f5;
                }
                h1 {
                    color: #333;
                    border-bottom: 3px solid #007bff;
                    padding-bottom: 10px;
                }
                .entry {
                    background: white;
                    margin: 20px 0;
                    padding: 20px;
                    border-radius: 8px;
                    box-shadow: 0 2px 4px rgba(0,0,0,0.1);
                }
                .entry-header {
                    display: flex;
                    justify-content: space-between;
                    margin-bottom: 15px;
                    border-bottom: 1px solid #e0e0e0;
                    padding-bottom: 10px;
                }
                .entry-date {
                    font-size: 1.2em;
                    font-weight: bold;
                    color: #007bff;
                }
                .entry-mood {
                    font-weight: 500;
                }
                .mood-positive { color: #28a745; }
                .mood-neutral { color: #6c757d; }
                .mood-negative { color: #dc3545; }
                .entry-meta {
                    font-size: 0.9em;
                    color: #666;
                    margin-bottom: 10px;
                }
                .entry-content {
                    line-height: 1.6;
                    color: #333;
                    margin-top: 15px;
                    padding: 15px;
                    background: #f9f9f9;
                    border-left: 3px solid #007bff;
                }
                .tags {
                    margin-top: 10px;
                }
                .tag {
                    display: inline-block;
                    background: #007bff;
                    color: white;
                    padding: 3px 10px;
                    border-radius: 12px;
                    font-size: 0.85em;
                    margin-right: 5px;
                }
                .footer {
                    text-align: center;
                    margin-top: 40px;
                    padding-top: 20px;
                    border-top: 1px solid #ddd;
                    color: #666;
                }
            ");
            sb.AppendLine("    </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");

            // Header
            sb.AppendLine($"    <h1>My Journal</h1>");
            sb.AppendLine($"    <p><strong>Period:</strong> {startDate:MMMM dd, yyyy} - {endDate:MMMM dd, yyyy}</p>");
            sb.AppendLine($"    <p><strong>Total Entries:</strong> {entries.Count}</p>");

            // Entries
            foreach (var entry in entries.OrderBy(e => e.EntryDate))
            {
                var moodClass = entry.MoodCategory.ToLower();

                sb.AppendLine("    <div class='entry'>");
                sb.AppendLine("        <div class='entry-header'>");
                sb.AppendLine($"            <div class='entry-date'>{entry.EntryDate:dddd, MMMM dd, yyyy}</div>");
                sb.AppendLine($"            <div class='entry-mood mood-{moodClass}'>😊 {entry.PrimaryMood}</div>");
                sb.AppendLine("        </div>");

                // Meta info
                sb.AppendLine("        <div class='entry-meta'>");
                if (!string.IsNullOrEmpty(entry.Category))
                {
                    sb.AppendLine($"            <span><strong>Category:</strong> {entry.Category}</span> | ");
                }
                sb.AppendLine($"            <span><strong>Words:</strong> {entry.WordCount}</span>");

                if (!string.IsNullOrEmpty(entry.SecondaryMood1) || !string.IsNullOrEmpty(entry.SecondaryMood2))
                {
                    sb.Append(" | <span><strong>Also feeling:</strong> ");
                    if (!string.IsNullOrEmpty(entry.SecondaryMood1))
                        sb.Append(entry.SecondaryMood1);
                    if (!string.IsNullOrEmpty(entry.SecondaryMood2))
                        sb.Append($", {entry.SecondaryMood2}");
                    sb.AppendLine("</span>");
                }
                sb.AppendLine("        </div>");

                // Tags
                if (!string.IsNullOrEmpty(entry.Tags))
                {
                    sb.AppendLine("        <div class='tags'>");
                    foreach (var tag in entry.TagList)
                    {
                        sb.AppendLine($"            <span class='tag'>{tag}</span>");
                    }
                    sb.AppendLine("        </div>");
                }

                // Content
                sb.AppendLine("        <div class='entry-content'>");
                sb.AppendLine($"            {entry.Content}");
                sb.AppendLine("        </div>");

                sb.AppendLine("    </div>");
            }

            // Footer
            sb.AppendLine("    <div class='footer'>");
            sb.AppendLine($"        <p>Exported on {DateTime.Now:MMMM dd, yyyy 'at' h:mm tt}</p>");
            sb.AppendLine("    </div>");

            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }

        /// <summary>
        /// Generates plain text content for export
        /// </summary>
        private string GenerateText(List<JournalEntry> entries, DateTime startDate, DateTime endDate)
        {
            var sb = new StringBuilder();

            sb.AppendLine("================================================================================");
            sb.AppendLine("                              MY JOURNAL");
            sb.AppendLine("================================================================================");
            sb.AppendLine($"Period: {startDate:MMMM dd, yyyy} - {endDate:MMMM dd, yyyy}");
            sb.AppendLine($"Total Entries: {entries.Count}");
            sb.AppendLine("================================================================================");
            sb.AppendLine();

            foreach (var entry in entries.OrderBy(e => e.EntryDate))
            {
                sb.AppendLine("--------------------------------------------------------------------------------");
                sb.AppendLine($"Date: {entry.EntryDate:dddd, MMMM dd, yyyy}");
                sb.AppendLine($"Mood: {entry.PrimaryMood} ({entry.MoodCategory})");

                if (!string.IsNullOrEmpty(entry.SecondaryMood1) || !string.IsNullOrEmpty(entry.SecondaryMood2))
                {
                    sb.Append("Also feeling: ");
                    if (!string.IsNullOrEmpty(entry.SecondaryMood1))
                        sb.Append(entry.SecondaryMood1);
                    if (!string.IsNullOrEmpty(entry.SecondaryMood2))
                        sb.Append($", {entry.SecondaryMood2}");
                    sb.AppendLine();
                }

                if (!string.IsNullOrEmpty(entry.Category))
                    sb.AppendLine($"Category: {entry.Category}");

                if (!string.IsNullOrEmpty(entry.Tags))
                    sb.AppendLine($"Tags: {entry.Tags}");

                sb.AppendLine($"Word Count: {entry.WordCount}");
                sb.AppendLine();

                // Strip HTML tags from content
                var plainText = StripHtml(entry.Content);
                sb.AppendLine(plainText);
                sb.AppendLine();
            }

            sb.AppendLine("================================================================================");
            sb.AppendLine($"Exported on {DateTime.Now:MMMM dd, yyyy 'at' h:mm tt}");
            sb.AppendLine("================================================================================");

            return sb.ToString();
        }

        /// <summary>
        /// Strips HTML tags from content
        /// </summary>
        private string StripHtml(string html)
        {
            if (string.IsNullOrEmpty(html))
                return string.Empty;

            // Remove HTML tags
            var text = Regex.Replace(html, "<.*?>", string.Empty);

            // Decode HTML entities
            text = System.Net.WebUtility.HtmlDecode(text);

            return text.Trim();
        }

        /// <summary>
        /// Shares the exported file using native share dialog
        /// </summary>
        public async Task ShareFileAsync(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Export file not found", filePath);

            await Share.RequestAsync(new ShareFileRequest
            {
                Title = "Share Journal Export",
                File = new ShareFile(filePath)
            });
        }
    }
}