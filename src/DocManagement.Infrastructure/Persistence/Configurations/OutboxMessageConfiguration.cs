using DocManagement.Domain;
using DocManagement.Domain.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocManagement.Infrastructure.Persistence.Configurations;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.Type)
            .IsRequired()
            .HasMaxLength(OutboxConstants.TypeMaxLength);

        builder.Property(m => m.Payload).IsRequired();
        builder.Property(m => m.OccurredAt).IsRequired();

        builder.Property(m => m.Error)
            .HasMaxLength(OutboxConstants.ErrorMaxLength);

        builder.HasIndex(m => m.OccurredAt)
            .HasFilter("[ProcessedAt] IS NULL");
    }
}
