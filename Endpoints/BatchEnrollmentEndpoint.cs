using UniversityRegistration.Contracts;
using UniversityRegistration.Application;

namespace UniversityRegistration.Endpoints;

public static class BatchEnrollmentEndpoint
{
    public static async Task<IResult> CreateAsync(int id, CreateBatchEnrollmentRequest request, EnrollmentOperations operations, CancellationToken ct)
    {
        var ids = request.CourseOfferingIds;
        var messages = new List<string>();
        if (ids is null || ids.Length is < 1 or > 50) messages.Add("1 ile 50 arasında açılış seçilmelidir.");
        if (ids is not null)
        {
            if (ids.Any(value => value <= 0)) messages.Add("Açılış kimlikleri pozitif olmalıdır.");
            if (ids.Distinct().Count() != ids.Length) messages.Add("Aynı açılış birden fazla seçilemez.");
        }
        if (messages.Count > 0) return Results.ValidationProblem(new Dictionary<string, string[]> { ["courseOfferingIds"] = messages.ToArray() });
        var result = await operations.CreateBatchAsync(id, ids!, ct);
        if (result is BatchEnrollmentResult.Created created)
            return Results.Json(new BatchEnrollmentResponse(created.Items), statusCode: StatusCodes.Status201Created);
        return ((BatchEnrollmentResult.Rejected)result).Reason switch
        {
            BatchEnrollmentFailure.StudentMissing => Results.Problem(statusCode: 404, title: "Öğrenci bulunamadı."),
            BatchEnrollmentFailure.OfferingsMissing => Results.ValidationProblem(new Dictionary<string, string[]>
                { ["courseOfferingIds"] = ["Seçilen açılışlardan biri bulunamadı."] }),
            BatchEnrollmentFailure.Duplicate => Results.Problem(statusCode: 409, title: "Seçilen açılışlardan birine zaten kayıt var; toplu kayıt oluşturulmadı."),
            BatchEnrollmentFailure.StudentRemoved => Results.ValidationProblem(new Dictionary<string, string[]>
                { ["studentId"] = ["Öğrenci bulunamadı."] }),
            BatchEnrollmentFailure.OfferingRemoved => Results.ValidationProblem(new Dictionary<string, string[]>
                { ["courseOfferingId"] = ["Ders açılışı bulunamadı."] }),
            _ => throw new InvalidOperationException("Unknown batch enrollment result.")
        };
    }
}
