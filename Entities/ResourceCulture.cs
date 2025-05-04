using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using STZ.Shared.Bases;

namespace STZ.Shared.Entities;

public class ResourceCulture<TR, TC> : EntityBase<Guid>
{
    [StringLength(2000)]
    public string Text { get; set; } = null!;

    [ForeignKey(nameof(Resource))]
    public TR ResourceId { get; set; } = default!;
    public Resource? Resource { get; set; }
    
    [ForeignKey(nameof(Culture))]
    public TC CultureId { get; set; } = default!;
    public Culture? Culture { get; set; }
}