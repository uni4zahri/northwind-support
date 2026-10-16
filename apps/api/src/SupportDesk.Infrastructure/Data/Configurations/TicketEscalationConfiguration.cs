using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
namespace SupportDesk.Infrastructure.Data.Configurations;
public sealed class TicketEscalationConfiguration : IEntityTypeConfiguration<TicketEscalation>
{
    public void Configure(EntityTypeBuilder<TicketEscalation> b)
    {
        b.ToTable("TicketEscalations"); b.HasKey(e => e.Id);
        b.Property(e => e.Reason).HasMaxLength(500).IsRequired();
        b.Property(e => e.EscalatedBy).HasMaxLength(100).IsRequired();
        b.HasOne<Ticket>().WithMany().HasForeignKey(e => e.TicketId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Agent>().WithMany().HasForeignKey(e => e.FromAgentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Agent>().WithMany().HasForeignKey(e => e.ToAgentId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(e => new { e.TicketId, e.EscalatedAtUtc });
    }
}
