namespace UniversityRegistration.Application;

public sealed class CourseCatalogQuery
{
    public string? Search { get; }
    public int? Credits { get; }
    public int Page { get; }
    public int PageSize { get; }
    public int Offset { get; }

    public CourseCatalogQuery(string? search = null, int? credits = null, int page = 1, int pageSize = 20)
    {
        var errors = Validate(credits, page, pageSize);
        if (errors.Count > 0) throw new ArgumentOutOfRangeException(errors.Keys.First());
        Search = search?.Trim();
        Credits = credits;
        Page = page;
        PageSize = pageSize;
        Offset = checked((int)(((long)page - 1) * pageSize));
    }

    // Shared by HTTP's aggregated field errors and direct-call constructor guards.
    public static Dictionary<string, string[]> Validate(int? credits, int page, int pageSize)
    {
        var errors = new Dictionary<string, string[]>();
        if (page < 1) errors["page"] = ["Sayfa en az 1 olmalıdır."];
        if (pageSize < 1 || pageSize > 100) errors["pageSize"] = ["Sayfa boyutu 1 ile 100 arasında olmalıdır."];
        if (credits is <= 0) errors["credits"] = ["Kredi pozitif olmalıdır."];
        var offset = ((long)page - 1) * pageSize;
        if (page >= 1 && pageSize is >= 1 and <= 100 && offset > int.MaxValue)
            errors["page"] = ["İstenen sayfanın başlangıcı desteklenen aralığı aşıyor."];
        return errors;
    }
}
