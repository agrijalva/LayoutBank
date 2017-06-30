using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LayoutBank
{
    public class Formato
    {
        public int IDFormato { get; set; }
        public string Nombre { get; set; }
        public string Descripcion { get; set; }
        public string RutaOrigen { get; set; }
        public string RutaDestino { get; set; }
        public int LongitudLinea { get; set; }
        public string TablaDestino { get; set; }        
        public int Estatus { get; set; }
        public int IDBanco { get; set; }

    }

}
