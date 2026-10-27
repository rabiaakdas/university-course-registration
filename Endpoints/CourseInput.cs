namespace UniversityRegistration.Endpoints;

public static class CourseInput
{
    public static (string Code, string Name, Dictionary<string, string[]> Errors) Validate(
        string? code, string? name, int credits)
    {
        var normalizedCode = code?.Trim().ToUpperInvariant() ?? string.Empty;
        var trimmedName = name?.Trim() ?? string.Empty;
        var errors = new Dictionary<string, string[]>();
        if (normalizedCode.Length == 0)
            errors["code"] = ["Ders kodu zorunludur."];
        else if (normalizedCode.Length > 20)
            errors["code"] = ["Ders kodu en fazla 20 karakter olabilir."];
        if (trimmedName.Length == 0)
            errors["name"] = ["Ders adı zorunludur."];
        else if (trimmedName.Length > 200)
            errors["name"] = ["Ders adı en fazla 200 karakter olabilir."];
        if (credits <= 0)
            errors["credits"] = ["Kredi sıfırdan büyük olmalıdır."];
        return (normalizedCode, trimmedName, errors);
    }
}
