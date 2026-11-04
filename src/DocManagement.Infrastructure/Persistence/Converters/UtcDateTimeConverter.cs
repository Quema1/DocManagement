using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DocManagement.Infrastructure.Persistence.Converters;

public class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    value => value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value,
    value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
