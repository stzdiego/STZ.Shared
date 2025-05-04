using System.ComponentModel.DataAnnotations;
using STZ.Shared.Bases;

namespace STZ.Shared.Entities;

public class Resource : AuditBase<Guid>
{
    [StringLength(100)]
    public string Code { get; set; } = null!;
}