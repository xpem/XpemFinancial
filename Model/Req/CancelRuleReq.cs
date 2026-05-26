namespace Model.Req
{
    public enum CancelScope
    {
        FromThisOnwards,
        EntireRule,
    }

    public class CancelRuleReq
    {
        public int TransactionId { get; set; }

        public Guid RecurringRuleId { get; set; }

        public CancelScope Scope { get; set; }
    }
}
