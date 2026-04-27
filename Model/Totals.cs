using System;
using System.Collections.Generic;
using System.Text;

namespace Model
{
    public class Totals
    {

        public bool IncludePreviousBalance { get; set; }

        public decimal PreviousBalance { get; set; }

        public decimal Income { get; set; }

        public decimal Expense { get; set; }

        public decimal Total { get; set; }
    }
}
