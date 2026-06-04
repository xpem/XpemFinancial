using System;
using System.Collections.Generic;
using System.Text;

namespace Model.Res
{
    public class TransactionDescriptionRes
    {
        public int TransactionID { get; set; }

        public string Description { get; set; }

        public int? CategoryId { get; set; }

        public string CategoryName { get; set; }
    }
}
