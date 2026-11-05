namespace GameStore.Api.validations;

using System.ComponentModel.DataAnnotations;

public class ValidReleaseDateAttribute : ValidationAttribute
{
    private readonly int _minYear;
    private readonly int _maxYearsInFuture;

    public ValidReleaseDateAttribute(int minYear = 1950, int maxYearsInFuture = 15)
    {
        _minYear = minYear;
        _maxYearsInFuture = maxYearsInFuture;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is DateOnly releaseDate)
        {
            var minDate = new DateOnly(_minYear, 1, 1);
            // Allow announcements up to a reasonable number of years in the future (e.g., 10 years)
            var maxDate = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(_maxYearsInFuture);

            if (releaseDate < minDate || releaseDate > maxDate)
            {
                return new ValidationResult(
                    ErrorMessage ?? $"The release date must be between {_minYear} and {maxDate.Year}.");
            }

            return ValidationResult.Success;
        }

        return new ValidationResult("The field must be a valid date.");
    }
}