using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using STZ.Shared.Bases;

namespace STZ.Shared.Entities;

[Index(nameof(Nit), IsUnique = true)]
public class Company : AuditBase<Guid>
{
    [Required] 
    [StringLength(12)] 
    public string Nit { get; set; } = null!;

    [Required] 
    [StringLength(50)] 
    public string Name { get; set; } = null!;

    [Required] 
    [StringLength(50)] 
    public string Country { get; set; } = null!;

    [Required] 
    [StringLength(50)] 
    public string State { get; set; } = null!;

    [Required] [StringLength(50)] public string City { get; set; } = null!;

    [Required]
    [StringLength(50)]
    [EmailAddress]
    public string Email { get; set; } = null!;
    
    [StringLength(20)]
    [Phone]
    public string? Phone { get; set; }
}