using System;
using System.Collections.Generic;
using System.Text;

namespace Model
{
    public class Totals
    {

        public bool IncludePreviousBalance { get; set; }

        public decimal PreviousBalance { get; set; }

        public decimal Inflow { get; set; }

        public decimal Outflow { get; set; }

        public decimal Total { get; set; }
    }
}
