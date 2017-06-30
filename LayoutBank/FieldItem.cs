using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LayoutBank
{
    public class FieldItem
    {
        public string FieldName { get; set; }
        public string FieldValue { get; set; }
        public string FieldFormat { get; set; }
        public bool ContainsFormat { get; set; }
    }
}
