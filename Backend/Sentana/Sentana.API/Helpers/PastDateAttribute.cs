using System.ComponentModel.DataAnnotations;

namespace Sentana.API.Helpers
{
    public class PastDateAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is DateTime date)
            {
                if (date >= DateTime.Now.Date)
                {
                    return new ValidationResult(ErrorMessage ?? "Ngày sinh phải là một ngày trong quá khứ.");
                }
            }
            return ValidationResult.Success;
        }
    }
}