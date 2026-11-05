namespace GameStore.Api.validations;

using System.ComponentModel.DataAnnotations;

public class NoWhitespaceAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is string str && string.IsNullOrWhiteSpace(str))
        {
            return new ValidationResult(ErrorMessage ?? "The field cannot consist only of whitespace.");
        }

        return ValidationResult.Success;
    }
}