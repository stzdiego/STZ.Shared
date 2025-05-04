using System.ComponentModel.DataAnnotations;
using STZ.Shared.Bases;

namespace STZ.Shared.Entities;

public class Culture : AuditBase<Guid>
{
    [StringLength(20)]
    public string Code { get; set; } = null!;
    
    [StringLength(50)]
    public string Name { get; set; } = null!;
}