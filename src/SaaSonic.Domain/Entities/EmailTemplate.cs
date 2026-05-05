using System;

namespace SaaSonic.Domain.Entities;

public sealed class EmailTemplate
{
 public int ID { get; set; }
 public string Slug { get; set; }=string.Empty;
 public string Subject { get; set; }=string.Empty;
 public string Body { get; set; }=string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
