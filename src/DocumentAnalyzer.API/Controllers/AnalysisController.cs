using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DocumentAnalyzer.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AnalysisController : ControllerBase
    {
        private readonly ILogger<AnalysisController> _logger;
        private static readonly string[] AllowedExtensions = { ".pdf", ".docx", ".txt", ".md" };
        private const long MaxFileSize = 10 * 1024 * 1024; // 10MB
        private const int MaxProcessingTimeMs = 30000; // 30 seconds

        public AnalysisController(ILogger<AnalysisController> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpPost("extract-text")]
        [RequestSizeLimit(MaxFileSize)]
        public async Task<IActionResult> ExtractText(IFormFile file)
        {
            try
            {
                // Input validation
                var validationResult = ValidateFile(file);
                if (validationResult != null)
                    return validationResult;

                var userId = GetUserId();
                _logger.LogInformation("Text extraction requested by user {UserId} for file {FileName}",
                    userId, SanitizeFileName(file.FileName));

                using var cancellationTokenSource = new CancellationTokenSource(MaxProcessingTimeMs);

                var extractedText = await ExtractTextFromFile(file, cancellationTokenSource.Token);
                var metadata = await ExtractMetadata(file, cancellationTokenSource.Token);

                var response = new
                {
                    DocumentId = Guid.NewGuid(),
                    FileName = SanitizeFileName(file.FileName),
                    ExtractedText = SanitizeText(extractedText),
                    Metadata = metadata,
                    ProcessedAt = DateTime.UtcNow,
                    ProcessedBy = userId
                };

                return Ok(response);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Text extraction timeout for file {FileName}", file?.FileName);
                return StatusCode(408, "Processing timeout exceeded");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting text from file {FileName}", file?.FileName);
                return StatusCode(500, "Internal server error during text extraction");
            }
        }

        [HttpPost("analyze")]
        [RequestSizeLimit(MaxFileSize)]
        public async Task<IActionResult> AnalyzeDocument(IFormFile file, [FromQuery] string analysisType = "full")
        {
            try
            {
                var validationResult = ValidateFile(file);
                if (validationResult != null)
                    return validationResult;

                if (!IsValidAnalysisType(analysisType))
                    return BadRequest("Invalid analysis type");

                var userId = GetUserId();
                _logger.LogInformation("Document analysis requested by user {UserId} for file {FileName}, type {AnalysisType}",
                    userId, SanitizeFileName(file.FileName), analysisType);

                using var cancellationTokenSource = new CancellationTokenSource(MaxProcessingTimeMs);

                var analysisResult = await PerformAnalysis(file, analysisType, cancellationTokenSource.Token);

                var response = new
                {
                    DocumentId = Guid.NewGuid(),
                    FileName = SanitizeFileName(file.FileName),
                    AnalysisType = analysisType,
                    Results = analysisResult,
                    ProcessedAt = DateTime.UtcNow,
                    ProcessedBy = userId
                };

                return Ok(response);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Document analysis timeout for file {FileName}", file?.FileName);
                return StatusCode(408, "Processing timeout exceeded");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing document {FileName}", file?.FileName);
                return StatusCode(500, "Internal server error during document analysis");
            }
        }

        [HttpGet("status/{documentId}")]
        public async Task<IActionResult> GetAnalysisStatus([Required] Guid documentId)
        {
            try
            {
                if (documentId == Guid.Empty)
                    return BadRequest("Invalid document ID");

                var userId = GetUserId();

                // Mock status check - in real implementation, check database/cache
                var status = new
                {
                    DocumentId = documentId,
                    Status = "Completed",
                    Progress = 100,
                    Message = "Analysis completed successfully",
                    UserId = userId,
                    LastUpdated = DateTime.UtcNow
                };

                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting analysis status for document {DocumentId}", documentId);
                return StatusCode(500, "Internal server error");
            }
        }

        private IActionResult? ValidateFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file provided");

            if (file.Length > MaxFileSize)
                return BadRequest($"File size exceeds maximum limit of {MaxFileSize / (1024 * 1024)}MB");

            var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
                return BadRequest($"File type not supported. Allowed types: {string.Join(", ", AllowedExtensions)}");

            return null;
        }

        private async Task<string> ExtractTextFromFile(IFormFile file, CancellationToken cancellationToken)
        {
            var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant();

            using var stream = file.OpenReadStream();
            using var reader = new StreamReader(stream);

            // Simple text extraction - in production, use proper libraries for PDF/DOCX
            switch (extension)
            {
                case ".txt":
                case ".md":
                    return await reader.ReadToEndAsync();
                case ".pdf":
                    // Mock PDF extraction
                    return "Extracted PDF content (mock implementation)";
                case ".docx":
                    // Mock DOCX extraction
                    return "Extracted DOCX content (mock implementation)";
                default:
                    return string.Empty;
            }
        }

        private async Task<object> ExtractMetadata(IFormFile file, CancellationToken cancellationToken)
        {
            return new
            {
                FileName = SanitizeFileName(file.FileName),
                FileSize = file.Length,
                ContentType = file.ContentType,
                Extension = Path.GetExtension(file.FileName)?.ToLowerInvariant(),
                UploadedAt = DateTime.UtcNow
            };
        }

        private async Task<object> PerformAnalysis(IFormFile file, string analysisType, CancellationToken cancellationToken)
        {
            var text = await ExtractTextFromFile(file, cancellationToken);

            return analysisType.ToLowerInvariant() switch
            {
                "sentiment" => new { Sentiment = "Neutral", Confidence = 0.75 },
                "keywords" => new { Keywords = new[] { "document", "analysis", "text" } },
                "summary" => new { Summary = "Document analysis summary" },
                "full" => new
                {
                    WordCount = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length,
                    CharacterCount = text.Length,
                    Sentiment = "Neutral",
                    Keywords = new[] { "document", "analysis", "text" },
                    Summary = "Full document analysis completed"
                },
                _ => new { Error = "Unknown analysis type" }
            };
        }

        private string GetUserId()
        {
            return User?.Identity?.Name ?? "anonymous";
        }

        private static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return "unknown";

            // Remove path traversal attempts and invalid characters
            var sanitized = Path.GetFileName(fileName);
            return Regex.Replace(sanitized, @"[^\w\-_\.]", "_");
        }

        private static string SanitizeText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            // Basic XSS prevention
            return text.Replace("<", "&lt;").Replace(">", "&gt;");
        }

        private static bool IsValidAnalysisType(string analysisType)
        {
            var validTypes = new[] { "sentiment", "keywords", "summary", "full" };
            return validTypes.Contains(analysisType?.ToLowerInvariant());
        }
    }
}
