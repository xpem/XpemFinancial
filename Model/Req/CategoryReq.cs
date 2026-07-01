namespace Model.Req
{
    public record CategoryReq
    {
        public Guid? CategoryId { get; set; }

        public required string Name { get; set; }

        public bool IsMainTransactionCategory { get; set; }

        public int? ParentTransactionCategoryId { get; set; }

        public bool Inactive { get; set; }

        public string? Color { get; set; }
    }
}
