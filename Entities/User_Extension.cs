using System.ComponentModel.DataAnnotations.Schema;

namespace STZ.Shared.Entities;

public partial class User
{
    [NotMapped]
    public string FullName => $"{FirstName} {LastName}";
}