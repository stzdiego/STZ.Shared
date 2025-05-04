namespace STZ.Shared.Dtos;

public class ApiResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalItems { get; set; }
}