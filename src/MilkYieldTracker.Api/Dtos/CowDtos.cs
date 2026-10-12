using System.ComponentModel.DataAnnotations;
using MilkYieldTracker.Api.Models;

namespace MilkYieldTracker.Api.Dtos;

// For records, validation attributes go on the constructor parameter itself
// (not on a "property:" target), otherwise MVC refuses to validate the type.
public record CreateCowRequest(
    [Required, StringLength(20, MinimumLength = 1)] string TagNumber,
    [StringLength(50)] string? Name);

public record UpdateCowRequest(
    [StringLength(50)] string? Name,
    bool IsActive);

public record CowDto(int Id, string TagNumber, string? Name, bool IsActive)
{
    public static CowDto From(Cow cow) => new(cow.Id, cow.TagNumber, cow.Name, cow.IsActive);
}
