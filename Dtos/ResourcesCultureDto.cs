namespace STZ.Shared.Dtos;

public class ResourcesCultureDto
{
    public required string CultureCode { get; set; }
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public IList<ResourceDto> Resources { get; set; } = [];
}