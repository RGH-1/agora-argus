using System.ComponentModel.DataAnnotations;

namespace Argus.Web.Data.Entities;

/// <summary>
/// The MessageId of every message Argus has processed. RabbitMQ can deliver a message twice,
/// so consumers skip any id already in this table (§7.2).
/// </summary>
public class ProcessedMessage
{
    [Key] public Guid MessageId { get; set; }
    public DateTime ProcessedAtUtc { get; set; }
}