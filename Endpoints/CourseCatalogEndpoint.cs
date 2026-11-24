using UniversityRegistration.Application;

namespace UniversityRegistration.Endpoints;

public static class CourseCatalogEndpoint
{
    public static async Task<IResult> GetAsync(CourseCatalogReader reader, CancellationToken ct,
        string? search = null, int? credits = null, int page = 1, int pageSize = 20)
    {
        var errors = CourseCatalogQuery.Validate(credits, page, pageSize);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        return Results.Ok(await reader.ReadAsync(new CourseCatalogQuery(search, credits, page, pageSize), ct));
    }
    public static async Task<IResult> GetKeysetAsync(CourseCatalogReader reader,HttpRequest http,CancellationToken ct,
        string? search=null,int? credits=null,int? afterId=null,int pageSize=20)
    {
        var errors=CourseCatalogQuery.Validate(credits,1,pageSize);
        if(afterId is <=0 || (http.Query.ContainsKey("afterId") && string.IsNullOrWhiteSpace(http.Query["afterId"])))errors["afterId"]=["Devam kimliği pozitif olmalıdır."];
        if(errors.Count>0)return Results.ValidationProblem(errors);
        return Results.Ok(await reader.ReadKeysetAsync(new(search,credits,afterId,pageSize),ct));
    }
}
