using System.ComponentModel.DataAnnotations;

namespace DocumentAnalyzer.Core.Entities;

public class DocumentAnalysis
{
    public int Id { get; set; }

    public int DocumentId { get; set; }

    public AnalysisType AnalysisType { get; set; }

    public AnalysisStatus Status { get; set; } = AnalysisStatus.Pending;

    public string? Results { get; set; }

    public string? Metadata { get; set; }

    public double? ConfidenceScore { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    public string? ErrorMessage { get; set; }

    public int RequestedByUserId { get; set; }

    // Navigation properties
    public virtual Document Document { get; set; } = null!;
    public virtual User RequestedByUser { get; set; } = null!;
}

public enum AnalysisType
{
    TextExtraction = 0,
    Sentiment = 1,
    Classification = 2,
    EntityRecognition = 3,
    KeywordExtraction = 4,
    ContentSummary = 5
}

public enum AnalysisStatus
{
    Pending = 0,
    InProgress = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4
}
