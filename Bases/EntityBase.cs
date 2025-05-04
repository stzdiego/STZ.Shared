using System.ComponentModel.DataAnnotations;

namespace STZ.Shared.Bases;

public interface IEntity<out TId>
{
    TId Id { get; }
}

public class EntityBase<T> : IEntity<T>
{
    [Key] 
    public T Id { get; set; } = default!;
    
    [Timestamp]
    public virtual uint Version { get; set; } 
}