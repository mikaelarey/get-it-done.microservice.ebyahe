using System.ComponentModel.DataAnnotations;

namespace EByahe.Application.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class NonEmptyGuidAttribute : ValidationAttribute
{
  public NonEmptyGuidAttribute() => ErrorMessage = "A non-empty GUID is required.";
  public override bool IsValid(object? value) => value is Guid id && id != Guid.Empty;
}
