using System.ComponentModel.DataAnnotations;

namespace SqlEstatePortal.Models;

public class EstateServer
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    [Display(Name = "Server Name")]
    public string ServerName { get; set; } = string.Empty;

    [Display(Name = "Enabled")]
    public bool Enabled { get; set; } = true;

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
