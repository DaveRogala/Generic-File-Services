using GenericFileImportServices.Models;

namespace GenericFileImportServices.Extensions;

/// <summary>
/// Extension methods for <see cref="ModelBuilder"/> to configure the optional
/// file-import metadata tables.
/// </summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Registers <see cref="FileImportRecord"/> and <see cref="FileImportEntityLink"/>
    /// in the model with a composite primary key, index on <c>DateTimeAddedUtc</c>,
    /// and an index on <c>EntityId</c> for reverse lookups.
    /// <para>
    /// Call this from <c>DbContext.OnModelCreating</c> when
    /// <see cref="IFileImportRecordServices{C}"/> is registered.
    /// </para>
    /// </summary>
    public static ModelBuilder AddFileImportMetadata(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FileImportRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ImportFileName).IsRequired();
            entity.Property(e => e.ArchivedFileName).IsRequired();
            entity.HasIndex(e => e.DateTimeAddedUtc);
        });

        modelBuilder.Entity<FileImportEntityLink>(entity =>
        {
            entity.HasKey(e => new { e.FileImportRecordId, e.EntityId });
            entity.HasOne(e => e.FileImportRecord)
                  .WithMany()
                  .HasForeignKey(e => e.FileImportRecordId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.EntityId);
        });

        return modelBuilder;
    }
}
