using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Diagnostics;
using EByahe.Domain.Models;
namespace EByahe.Infrastructure.Persistence;

// Keeps decoded status projections/filters usable while storage and relationships use integer codes.
internal sealed class CodeDecodeQueryInterceptor : IQueryExpressionInterceptor
{
  public Expression QueryCompilationStarting(Expression expression, QueryExpressionEventData eventData) => new DecodeVisitor().Visit(expression);
  private sealed class DecodeVisitor : ExpressionVisitor
  {
    private static readonly Dictionary<(Type, string), (string Column, string Group)> Bindings = new()
    {
      [(typeof(BusBookingStatusLog), "Status")] = ("CodeDecodeBookingStatus", "BookingStatuses"),
      [(typeof(BusBookingStatusLog), "PreviousStatus")] = ("CodeDecodePreviousBookingStatus", "BookingStatuses"),
      [(typeof(BusBookingStatusLog), "PaymentStatus")] = ("CodeDecodePaymentStatus", "PaymentStatuses"),
      [(typeof(BusBookingStatusLog), "PreviousPaymentStatus")] = ("CodeDecodePreviousPaymentStatus", "PaymentStatuses"),
      [(typeof(BusBookingStatusLog), "PaymentType")] = ("CodeDecodePaymentType", "PaymentTypes"),
      [(typeof(BusBooking), "Status")] = ("CodeDecodeBookingStatus", "BookingStatuses"),
      [(typeof(BusBooking), "PaymentStatus")] = ("CodeDecodePaymentStatus", "PaymentStatuses"),
      [(typeof(BusBooking), "PaymentType")] = ("CodeDecodePaymentType", "PaymentTypes"),
      [(typeof(Partner), "Status")] = ("CodeDecodePartnerStatus", "PartnerStatuses"),
      [(typeof(ScheduledTrip), "Status")] = ("CodeDecodeScheduledTripStatus", "ScheduledTripStatuses"),
      [(typeof(Incoming), "Status")] = ("CodeDecodeIncomingStatus", "IncomingStatuses"),
      [(typeof(IncomingLog), "Status")] = ("CodeDecodeIncomingStatus", "IncomingStatuses"),
      [(typeof(OutboxMessage), "Status")] = ("CodeDecodeOutboxStatus", "OutboxStatuses"),
      [(typeof(Outgoing), "Status")] = ("CodeDecodeOutgoingStatus", "OutgoingStatuses"),
      [(typeof(PartnerApplication), "EntityType")] = ("CodeDecodeEntityType", "EntityTypes"),
      [(typeof(PartnerApplication), "SettlementFrequency")] = ("CodeDecodeSettlementFrequency", "SettlementFrequencies"),
      [(typeof(RefUser), "Role")] = ("CodeDecodeRole", "Roles"),
      [(typeof(OutboxMessage), "EventType")] = ("CodeDecodeEventType", "EventTypes"),
      [(typeof(Outgoing), "EventType")] = ("CodeDecodeEventType", "EventTypes"),
      [(typeof(PartnerApplicationDocument), "DocumentType")] = ("CodeDecodeDocumentType", "DocumentTypes"),
      [(typeof(PartnerApplication), "BusinessCategory")] = ("CodeDecodeBusinessCategory", "BusinessCategories"),
      [(typeof(PartnerMemberRequest), "BusinessCategory")] = ("CodeDecodeBusinessCategory", "BusinessCategories"),
      [(typeof(PartnerApplication), "Status")] = ("CodeDecodePartnerApplicationStatus", "PartnerApplicationStatuses"),
      [(typeof(PartnerApplicationApproval), "Status")] = ("CodeDecodePartnerApplicationApprovalStatus", "PartnerApplicationApprovalStatuses"),
      [(typeof(PartnerApplicationApproval), "PreviousStatus")] = ("CodeDecodePreviousPartnerApplicationApprovalStatus", "PartnerApplicationApprovalStatuses"),
      [(typeof(PartnerApplicationHistory), "Status")] = ("CodeDecodePartnerApplicationStatus", "PartnerApplicationStatuses"),
      [(typeof(PartnerMemberRequest), "Status")] = ("CodeDecodePartnerMemberRequestStatus", "PartnerMemberRequestStatuses"),
    };
    protected override Expression VisitMember(MemberExpression node)
    {
      if (node.Expression is not null && typeof(ITopicRecord).IsAssignableFrom(node.Expression.Type) && node.Member.Name == nameof(ITopicRecord.Topic))
        return Expression.Coalesce(Expression.Property(Expression.Property(Visit(node.Expression), nameof(ITopicRecord.TopicSetup)), nameof(CodeDecodeTopic.Decode)), Expression.Constant(""));
      if (node.Expression?.Type == typeof(PartnerApplication) && node.Member.Name == nameof(PartnerApplication.BankName))
        return Expression.Coalesce(Expression.Property(Expression.Property(Visit(node.Expression), nameof(PartnerApplication.Bank)), nameof(CodeDecodeBank.Decode)), Expression.Constant(""));
      if (node.Expression is not null && Bindings.TryGetValue((node.Expression.Type, node.Member.Name), out var binding))
      {
        var code = Expression.Property(Visit(node.Expression), binding.Column);
        Expression decoded = Expression.Constant(code.Type == typeof(int?) ? null : "", typeof(string));
        var values = CodeDecodeValues.Values[binding.Group];
        for (var i = values.Length - 1; i >= 0; i--)
          decoded = Expression.Condition(Expression.Equal(code, Expression.Convert(Expression.Constant(i + 1), code.Type)), Expression.Constant(values[i]), decoded);
        return decoded;
      }
      return base.VisitMember(node);
    }
  }
}
