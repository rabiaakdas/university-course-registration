namespace UniversityRegistration.Application;

public sealed class CourseKeysetQuery
{
    public CourseCatalogQuery Filter { get; }
    public int? AfterId { get; }
    public CourseKeysetQuery(string? search=null,int? credits=null,int? afterId=null,int pageSize=20)
    {
        if(afterId is <=0) throw new ArgumentOutOfRangeException(nameof(afterId));
        Filter=new(search,credits,1,pageSize); AfterId=afterId;
    }
}
