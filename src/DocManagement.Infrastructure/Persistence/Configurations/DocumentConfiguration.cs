using DocManagement.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DocManagement.Domain.Constants;

namespace DocManagement.Infrastructure.Persistence.Configurations;

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("Documents");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.Name)
            .IsRequired()
            .HasMaxLength(DocumentConstants.NameMaxLength);

        builder.Property(d => d.Description)
            .HasMaxLength(DocumentConstants.DescriptionMaxLength);

        builder.Property(d => d.PreviewUrl)
            .HasMaxLength(PreviewConstants.UrlMaxLength);

        builder.Property(d => d.PreviewStatus)
            .HasConversion<string>()
            .HasMaxLength(PreviewConstants.StatusMaxLength)
            .IsRequired();

        builder.Property(d => d.CreatedAt).IsRequired();
        builder.Property(d => d.UpdatedAt).IsRequired();

        builder.Property<byte[]>("RowVersion").IsRowVersion();
    }
}
