namespace STZ.Shared.Bases;

public class AuditBase<T> : EntityBase<T>
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    
    public virtual Guid CreatedBy { get; set; }
    public virtual Guid? UpdatedBy { get; set; }
    public virtual Guid? DeletedBy { get; set; }
    
    public virtual bool IsDeleted { get; set; }
}