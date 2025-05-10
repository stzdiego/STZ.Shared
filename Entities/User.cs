using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using STZ.Shared.Bases;

namespace STZ.Shared.Entities;

[Index(nameof(Nid), IsUnique = true)]
[Index(nameof(Email), IsUnique = true)]
public partial class User :AuditBase<Guid>
{
    [StringLength(20)]
    public string? Nid { get; set; }
    
    [Required]
    [StringLength(50)]
    public string FirstName { get; set; } = null!;
    
    [Required]
    [StringLength(50)]
    public string LastName { get; set; } = null!;
    
    [Required]
    [StringLength(50)]
    [EmailAddress]
    public string Email { get; set; } = null!;
    
    [StringLength(20)]
    [Phone]
    public string? Phone { get; set; }
    
    public bool IsActive { get; set; }
    public bool IsAdmin { get; set; }

    public Guid? CompanyId { get; set; }
    
    public Guid? DefaultLanguage { get; set; }

    [ForeignKey(nameof(CompanyId))]
    public Company? Company { get; set; }
}