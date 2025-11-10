using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Common.Storage;

[Table("printers")]
public sealed class PrinterEntity
{
    [Key] public required string DevId { get; set; }
    public string? Name { get; set; }
    public string? Model { get; set; }
    public string? Product { get; set; }
    public bool Online { get; set; }
    public DateTimeOffset LastSeen { get; set; }
}

[Table("printer_events")]
public sealed class PrinterEventEntity
{
    [Key] public long Id { get; set; }
    public required string DevId { get; set; }
    public required string Kind { get; set; }
    public DateTimeOffset Ts { get; set; }
    public string? PayloadJson { get; set; }
}