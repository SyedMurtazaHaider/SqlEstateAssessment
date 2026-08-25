using System.ComponentModel.DataAnnotations;

namespace SqlEstatePortal.Models;

public class Team
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public ICollection<TeamMember> Members { get; set; } = new List<TeamMember>();
}
