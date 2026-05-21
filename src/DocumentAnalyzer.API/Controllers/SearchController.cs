using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace DocumentAnalyzer.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SearchController : ControllerBase
    {
        private readonly ILogger<SearchController> _logger;
        private const int MaxSearchResults = 100;
        private const int MaxQueryLength = 500;
        private const int DefaultPageSize = 20;

        public SearchController(ILogger<SearchController> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet("documents")]
        public async Task<IActionResult> SearchDocuments(
            [Required][StringLength(MaxQueryLength)] string query,
            [Range(1, int.MaxValue)] int page = 1,
            [Range(1, MaxSearchResults)] int pageSize = DefaultPageSize,
            string? sortBy = "relevance",
            string? filterBy = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                    return BadRequest("Search query is required");

                var sanitizedQuery = SanitizeSearchQuery(query);
                var userId = GetUserId();

                _logger.LogInformation("Document search by user {UserId}: {Query}", userId, sanitizedQuery);

                // Validate and sanitize parameters
                if (!IsValidSortOption(sortBy))
                    sortBy = "relevance";

                var searchResults = await PerformDocumentSearch(sanitizedQuery, page, pageSize, sortBy, filterBy, userId);

                var response = new
                {
                    Query = sanitizedQuery,
                    Page = page,
                    PageSize = pageSize,
                    TotalResults = searchResults.TotalCount,
                    TotalPages = (int)Math.Ceiling((double)searchResults.TotalCount / pageSize),
                    Results = searchResults.Documents.Select(doc => new
                    {
                        doc.Id,
                        doc.Title,
                        doc.Summary,
                        doc.LastModified,
                        doc.Relevance,
                        // Only include snippet, not full content for security
                        Snippet = TruncateContent(doc.Content, 200)
                    }),
                    SearchTime = searchResults.SearchTimeMs,
                    UserId = userId
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing document search for query: {Query}", query);
                return StatusCode(500, "Internal server error during search");
            }
        }

        [HttpGet("content")]
        public async Task<IActionResult> SearchContent(
            [Required][StringLength(MaxQueryLength)] string query,
            [Range(1, int.MaxValue)] int page = 1,
            [Range(1, MaxSearchResults)] int pageSize = DefaultPageSize,
            bool includeMetadata = false)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                    return BadRequest("Search query is required");

                var sanitizedQuery = SanitizeSearchQuery(query);
                var userId = GetUserId();

                _logger.LogInformation("Content search by user {UserId}: {Query}", userId, sanitizedQuery);

                var searchResults = await PerformContentSearch(sanitizedQuery, page, pageSize, includeMetadata, userId);

                var response = new
                {
                    Query = sanitizedQuery,
                    Page = page,
                    PageSize = pageSize,
                    TotalResults = searchResults.TotalCount,
                    Results = searchResults.ContentMatches.Select(match => new
                    {
                        match.DocumentId,
                        match.DocumentTitle,
                        match.MatchedText,
                        match.Context,
                        match.Confidence,
                        Metadata = includeMetadata ? match.Metadata : null
                    }),
                    SearchTime = searchResults.SearchTimeMs
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing content search for query: {Query}", query);
                return StatusCode(500, "Internal server error during content search");
            }
        }

        [HttpGet("suggestions")]
        public async Task<IActionResult> GetSearchSuggestions([Required][StringLength(100)] string partial)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(partial) || partial.Length < 2)
                    return BadRequest("Partial query must be at least 2 characters");

                var sanitizedPartial = SanitizeSearchQuery(partial);
                var userId = GetUserId();

                var suggestions = await GenerateSearchSuggestions(sanitizedPartial, userId);

                var response = new
                {
                    Partial = sanitizedPartial,
                    Suggestions = suggestions.Take(10), // Limit suggestions
                    GeneratedAt = DateTime.UtcNow
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating search suggestions for: {Partial}", partial);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("filters")]
        public async Task<IActionResult> GetAvailableFilters()
        {
            try
            {
                var userId = GetUserId();

                var filters = new
                {
                    DocumentTypes = new[] { "pdf", "docx", "txt", "md" },
                    DateRanges = new[] { "today", "week", "month", "year", "custom" },
                    Authors = new[] { "system", "user" },
                    Tags = new[] { "important", "draft", "reviewed", "archived" },
                    Sizes = new[] { "small", "medium", "large" }
                };

                return Ok(filters);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available filters");
                return StatusCode(500, "Internal server error");
            }
        }

        private async Task<DocumentSearchResult> PerformDocumentSearch(
            string query, int page, int pageSize, string sortBy, string? filterBy, string userId)
        {
            // Mock search implementation
            await Task.Delay(100); // Simulate search delay

            var mockDocuments = GenerateMockDocuments(query, page, pageSize);

            return new DocumentSearchResult
            {
                Documents = mockDocuments,
                TotalCount = 50, // Mock total
                SearchTimeMs = 95
            };
        }

        private async Task<ContentSearchResult> PerformContentSearch(
            string query, int page, int pageSize, bool includeMetadata, string userId)
        {
            // Mock content search implementation
            await Task.Delay(120); // Simulate search delay

            var mockMatches = GenerateMockContentMatches(query, page, pageSize, includeMetadata);

            return new ContentSearchResult
            {
                ContentMatches = mockMatches,
                TotalCount = 25, // Mock total
                SearchTimeMs = 115
            };
        }

        private async Task<string[]> GenerateSearchSuggestions(string partial, string userId)
        {
            // Mock suggestions
            await Task.Delay(50);

            return new[]
            {
                $"{partial} analysis",
                $"{partial} report",
                $"{partial} document",
                $"{partial} processing"
            };
        }

        private static string SanitizeSearchQuery(string query)
        {
            if (string.IsNullOrEmpty(query))
                return string.Empty;

            // Remove potential injection patterns and normalize
            var sanitized = Regex.Replace(query, @"[<>""';\\]", " ");
            sanitized = Regex.Replace(sanitized, @"\s+", " ");
            return sanitized.Trim();
        }

        private static bool IsValidSortOption(string? sortBy)
        {
            var validOptions = new[] { "relevance", "date", "title", "size" };
            return validOptions.Contains(sortBy?.ToLowerInvariant());
        }

        private static string TruncateContent(string content, int maxLength)
        {
            if (string.IsNullOrEmpty(content) || content.Length <= maxLength)
                return content ?? string.Empty;

            return content.Substring(0, maxLength) + "...";
        }

        private string GetUserId()
        {
            return User?.Identity?.Name ?? "anonymous";
        }

        private List<DocumentResult> GenerateMockDocuments(string query, int page, int pageSize)
        {
            var results = new List<DocumentResult>();
            var random = new Random();

            for (int i = 0; i < Math.Min(pageSize, 10); i++)
            {
                results.Add(new DocumentResult
                {
                    Id = Guid.NewGuid(),
                    Title = $"Document containing '{query}' - {i + 1}",
                    Summary = $"This document discusses {query} and related topics...",
                    Content = $"Full content of document about {query}...",
                    LastModified = DateTime.UtcNow.AddDays(-random.Next(1, 365)),
                    Relevance = Math.Round(random.NextDouble(), 2)
                });
            }

            return results;
        }

        private List<ContentMatch> GenerateMockContentMatches(string query, int page, int pageSize, bool includeMetadata)
        {
            var results = new List<ContentMatch>();
            var random = new Random();

            for (int i = 0; i < Math.Min(pageSize, 8); i++)
            {
                results.Add(new ContentMatch
                {
                    DocumentId = Guid.NewGuid(),
                    DocumentTitle = $"Document {i + 1}",
                    MatchedText = $"...text containing {query} found here...",
                    Context = $"Broader context around the match for {query}",
                    Confidence = Math.Round(random.NextDouble(), 2),
                    Metadata = includeMetadata ? new { Author = "System", Type = "Analysis" } : null
                });
            }

            return results;
        }
    }

    public class DocumentSearchResult
    {
        public List<DocumentResult> Documents { get; set; } = new();
        public int TotalCount { get; set; }
        public int SearchTimeMs { get; set; }
    }

    public class ContentSearchResult
    {
        public List<ContentMatch> ContentMatches { get; set; } = new();
        public int TotalCount { get; set; }
        public int SearchTimeMs { get; set; }
    }

    public class DocumentResult
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime LastModified { get; set; }
        public double Relevance { get; set; }
    }

    public class ContentMatch
    {
        public Guid DocumentId { get; set; }
        public string DocumentTitle { get; set; } = string.Empty;
        public string MatchedText { get; set; } = string.Empty;
        public string Context { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public object? Metadata { get; set; }
    }
}
