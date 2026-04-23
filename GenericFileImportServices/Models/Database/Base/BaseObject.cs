namespace GenericFileImportServices.Models.Database.Base;

[Index(nameof(DateDeletedUtc))]
public class BaseObject
{
    [SetsRequiredMembers]
    public BaseObject()
    {
        DateAddedUtc = DateTime.UtcNow;
        DateUpdatedUtc = DateTime.UtcNow;
    }
    [SetsRequiredMembers]
    public BaseObject(DateTime dateAddedUtc, DateTime dateUpdatedUtc)
    {
        DateAddedUtc = dateAddedUtc;
        DateUpdatedUtc = dateUpdatedUtc;
    }
    public int Id { get; set; }
    public required DateTime DateAddedUtc { get; set; }
    public required DateTime DateUpdatedUtc { get; set; }
    public DateTime? DateDeletedUtc { get; set; }
}
