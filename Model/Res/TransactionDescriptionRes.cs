using System;
using System.Collections.Generic;
using System.Text;

namespace Model.Res
{
    public class TransactionDescriptionRes
    {
        public int TransactionID { get; set; }

        public required string Description { get; set; }

        public int? CategoryId { get; set; }

        public required string CategoryName { get; set; }

        public int? AccountId { get; set; }
    }
}
