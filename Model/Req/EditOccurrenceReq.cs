using Model.DTO;

namespace Model.Req
{
    public enum EditScope
    {
        ThisOnly,
        ThisAndFuture,
        All,
    }

    public class EditOccurrenceReq
    {
        public int TransactionId { get; set; }

        public Guid RecurringRuleId { get; set; }

        public EditScope Scope { get; set; }

        public required RecurringRuleDTO UpdatedRule { get; set; }
    }
}
