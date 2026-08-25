using System.ComponentModel.DataAnnotations;

namespace SqlEstatePortal.Models;

public class AssessmentFinding
{
    public int Id { get; set; }
    public int AssessmentRunId { get; set; }
    public AssessmentRun AssessmentRun { get; set; } = null!;

    [MaxLength(200)]
    public string ServerName { get; set; } = string.Empty;

    [MaxLength(30)]
    public string Severity { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Area { get; set; } = string.Empty;

    public string Finding { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
}
