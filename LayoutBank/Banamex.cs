using System;
using System.IO;
using System.Data;
using System.Net;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Configuration;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace LayoutBank
{
    class Banamex
    {
        FileReader FR = new FileReader();

        public void Procesar(Formato archivo, string banco, string lblBanco)
        {
            string processFolder = FR.GetFolderPath(archivo.RutaDestino);
            string[] txtFiles = FR.GetTextFiles(archivo.RutaOrigen);
            string queryDetalle = string.Format("select * from FormatoDetalle where IdFormato ={0}", archivo.IDFormato);
            DataTable tablaDetalle = FR.GetTable(queryDetalle);

            if (tablaDetalle.Rows.Count <= 0) return;

            MT940(archivo, banco, lblBanco, txtFiles);
        }


        private void MT940(Formato archivo, string banco, string lblBanco, string[] txtFiles)
        {   

            foreach (string txtPath in txtFiles) // Se recorre uno a uno de los layouts
            {
                string deposito = FR.Deposito(banco);                
                string[] txtLines = FR.ReadAllLine(txtPath);
                string fileName = Path.GetFileName(txtPath);

                // Variables para los registros
                int contRegisters = 0;
                string NumeroCuenta = string.Empty;
                int Pagina = 0;
                string[] aperturaBalance;
                string[] cierreBalance;
                string[] cierreBalanceDisponible;
                string[] registro;
                string[] infoReferencia = new string[3];
                string[] detallesComplemenatrios = new string[2];

                Console.WriteLine(txtPath);
                // Se recorre todo el Layout para obtener los datos generales como las aperturas y los cierres del Balance
                foreach (string line in txtLines)
                {
                    var mainNode = Regex.Match(line, @":(.+?):").Groups[1].Value;
                    if (mainNode != "")
                    {
                        switch (mainNode)
                        {
                            case "20": //  - Inicio del Bloque
                                // Se ha determinado el inicio de un Layout
                                break;
                            case "25": //  - Numero de Cuenta
                                NumeroCuenta = this.GetNoCuenta(line);
                                break;
                            case "28": //  - Paginado
                                Pagina = this.GetPagina(line);
                                break;
                            case "60F": // - Apertura de Balance
                                aperturaBalance = this.GetDatosBalace(line);
                                break;
                            case "61": //  - Inicio de Un Registro                                
                                contRegisters++; // Incremento unicamente al final de todo 
                                break;
                            case "62F": // - Cierre de Balance
                                cierreBalance = this.GetDatosBalace(line);
                                break;
                            case "64": //  - Cierre de Balance Disponible
                                cierreBalanceDisponible = this.GetDatosBalace(line);
                                break;
                        }
                    }
                    else
                    {
                        var secondNode = Regex.Match(line, @"/(.+?)/").Groups[1].Value;
                        if (secondNode == "")
                        {
                            if (line == "-") // - Final del Documento
                            {
                                // El cierre del Archivo se ha terminado
                            }
                        }
                    }
                }

                // -----------------------------
                foreach (string line in txtLines)
                {
                    var mainNode = Regex.Match(line, @":(.+?):").Groups[1].Value;
                    if (mainNode != "")
                    {
                        switch (mainNode)
                        {
                            case "61": //  - Inicio de Un Registro
                                registro = this.StatementLine(line);                                

                                break;
                            case "86": //  - Informacion complementaria del registro
                                infoReferencia = this.Complemento(line);
                                break;
                        }
                    }
                    else
                    {
                        var secondNode = Regex.Match(line, @"/(.+?)/").Groups[1].Value;
                        if (secondNode != "")
                        {
                            switch (secondNode)
                            {
                                case "CTC": // -  Descripcion CTC
                                    detallesComplemenatrios = this.detalles(line);
                                    break;
                            }
                        }
                        else
                        {
                            if (line != "-") // - Complemento del nodo 86
                            {
                                infoReferencia[2] += line;
                                Console.WriteLine(infoReferencia[2]);
                            }
                        }
                    }
                }
                
                Console.WriteLine("");
                Console.WriteLine("Finaliza Proceso");
            }
        }

        private string GetNoCuenta(string line)
        {   
            return line.Substring(4, (line.Length - 4));
        }

        private int GetPagina(string line)
        {
            string aux = line.Substring(4, (line.Length - 4));
            string[] nodes = aux.Split('/');
            return Int32.Parse(nodes[1]);
        }

        private string[] GetDatosBalace(string line)
        {
            var nodo = Regex.Match(line, @":(.+?):").Groups[1].Value;
            int longitud = nodo.Length;

            string[] apertura = new string[4];
            // Se Obtiene la linea sin el nodo 
            int len = longitud + 2;
            string aux = line.Substring(len, (line.Length - len));

            // Se obtiene el campo de Credito/Debito
            apertura[0] = aux.Substring(0, 1);

            // Se obtiene el campo de Fecha de Apertura
            string fecha = aux.Substring(1, 6);
            apertura[1] = "20" + fecha.Substring(0, 2) + "-" + fecha.Substring(2, 2) + "-" + fecha.Substring(4, 2);

            // Se obtiene el campo de Moneda
            apertura[2] = aux.Substring(7, 3);

            // Se obtiene el campo del Monto
            string monto = aux.Substring(10, (aux.Length - 10));
            apertura[3] = monto.Replace(',', '.');

            return apertura;
        }

        private string[] StatementLine( string line )
        {
            string[] Statement = new string[9];

            var nodo = Regex.Match(line, @":(.+?):").Groups[1].Value;
            int longitud = nodo.Length;

            string[] apertura = new string[4];
            // Se Obtiene la linea sin el nodo 
            int len = longitud + 2;
            string aux = line.Substring(len, (line.Length - len));

            // Se obtiene el campo de Fecha de Transacción
            string fecha = aux.Substring(0, 6);
            fecha = "20" + fecha.Substring(0, 2) + "-" + fecha.Substring(2, 2) + "-" + fecha.Substring(4, 2);
            Statement[0] = fecha;

            // Se obtiene el campo de Fecha de Entrada
            string fechaentrada = aux.Substring(6, 4);
            Statement[1] = fechaentrada;

            // Se obtiene el campo de Credito o Debito
            string credito = aux.Substring(10, 2);
            int charPlus = 0;
            if(credito == "RC" || credito == "RD")
            {
                charPlus = 10 + 2;
            }
            else
            {
                credito = aux.Substring(10, 1);
                charPlus = 10 + 1;
            }
            Statement[2] = credito;

            // Se obtiene el campo de Moneda (Contiene solo el tercer caracter) USD => D, MXN => N
            string moneda = aux.Substring(charPlus, 1);
            Statement[3] = moneda;

            // Se obtiene el campo del Monto
            string cherPrev = credito + moneda;
            string monto = Regex.Match(line, @""+ cherPrev + "(.+?)N").Groups[1].Value;
            Statement[4] = monto;

            // Se obtiene el campo del Metodo de Entrada
            charPlus = charPlus + monto.Length + 1;
            string metodoEntrada = aux.Substring(charPlus, 1);
            Statement[5] = metodoEntrada;

            // Se obtiene el campo de la Razon de Entrada
            string razon = aux.Substring((charPlus + 1), 3);
            Statement[6] = razon;

            // Se obtiene el campo de Referencia
            string lotReferencia = aux.Substring((charPlus + 4),  (aux.Length - (charPlus + 4)));
            string[] auxRef = lotReferencia.Split(new string[] {"//"}, StringSplitOptions.None);
            Statement[7] = auxRef[0];
            Statement[8] = auxRef[1];

            return Statement;
        }

        private string[] detalles(string line)
        {

            var nodo = Regex.Match(line, @"/(.+?)/").Groups[1].Value;
            int longitud = nodo.Length;

            string[] apertura = new string[4];
            // Se Obtiene la linea sin el nodo 
            int len = longitud + 2;
            string aux = line.Substring(len, (line.Length - len));

            // Se obtienen los datos
            string[] auxDetalles = aux.Split('/');
            
            return auxDetalles;
        }

        private string[] Complemento(string line) {
            string[] Comple = new string[3];

            var nodo = Regex.Match(line, @":(.+?):").Groups[1].Value;
            int longitud = nodo.Length;

            string[] apertura = new string[4];
            // Se Obtiene la linea sin el nodo 
            int len = longitud + 2;
            string aux = line.Substring(len, (line.Length - len));

            // Se obtiene el identificador tipo del producto
            Comple[0] = aux.Substring(0, 4);

            // Se obtiene el tipo del producto
            Comple[1] = aux.Substring(4, 2);

            // Se obtiene la descripción
            Comple[2] = aux.Substring(6, (aux.Length - 6));

            return Comple;
        }

        //private void MT940Respaldo(Formato archivo, string banco, string lblBanco)
        //{
        //    FileReader FR = new FileReader();

        //    string processFolder = FR.GetFolderPath(archivo.RutaDestino);
        //    string[] txtFiles = FR.GetTextFiles(archivo.RutaOrigen);
        //    string queryDetalle = string.Format("select * from FormatoDetalle where IdFormato ={0}", archivo.IDFormato);
        //    DataTable tablaDetalle = FR.GetTable(queryDetalle);

        //    if (tablaDetalle.Rows.Count <= 0) return;

        //    foreach (string txtPath in txtFiles)
        //    {
        //        string deposito = FR.Deposito(banco);
        //        string[] txtLines = FR.ReadAllLine(txtPath);
        //        string fileName = Path.GetFileName(txtPath);
        //        //string noCuenta = string.Empty;

        //        // Variables para los registros
        //        int contRegisters = 0;
        //        // /-Variables para los registros


        //        Console.WriteLine(txtPath);
        //        foreach (string line in txtLines)
        //        {
        //            var mainNode = Regex.Match(line, @":(.+?):").Groups[1].Value;

        //            if (mainNode != "")
        //            {
        //                switch (mainNode)
        //                {
        //                    case "20":
        //                        Console.WriteLine(mainNode + " - Inicio del Bloque");
        //                        break;
        //                    case "25":
        //                        Console.WriteLine(mainNode + " - Numero de Cuenta");
        //                        break;
        //                    case "28":
        //                        Console.WriteLine(mainNode + " - Paginado");
        //                        break;
        //                    case "60F":
        //                        Console.WriteLine(mainNode + " - Apertura de Balance");
        //                        break;
        //                    case "61":
        //                        Console.WriteLine(mainNode + " - Inicio de Un Registro");
        //                        contRegisters++;
        //                        break;
        //                    case "86":
        //                        Console.WriteLine(mainNode + " - Informacion complementaria del registro");
        //                        break;
        //                    case "62F":
        //                        Console.WriteLine(mainNode + " - Cierre de Balance");
        //                        break;
        //                    case "64":
        //                        Console.WriteLine(mainNode + " - Cierre de Balance Disponible");
        //                        break;
        //                }
        //            }
        //            else
        //            {
        //                var secondNode = Regex.Match(line, @"/(.+?)/").Groups[1].Value;
        //                if (secondNode != "")
        //                {
        //                    switch (secondNode)
        //                    {
        //                        case "CTC":
        //                            Console.WriteLine(secondNode + " Descripcion CTC");
        //                            break;
        //                    }
        //                }
        //                else
        //                {
        //                    if (line != "-")
        //                    {
        //                        Console.WriteLine("Complemento del nodo 86");
        //                    }
        //                    else
        //                    {
        //                        Console.WriteLine("Final del Documento.");
        //                    }
        //                }
        //            }

        //            //    List<FieldItem> lstCampos = new List<FieldItem>();

        //            //    if (archivo.IDFormato == 1 && numeroLinea == 0)
        //            //    {
        //            //        noCuenta = line.Substring(2, 18);
        //            //        numeroLinea = 1;
        //            //    }

        //            //    foreach (DataRow dr in tablaDetalle.Rows)
        //            //    {
        //            //        if (line.Length < archivo.LongitudLinea) continue;

        //            //        FieldItem field = new FieldItem();
        //            //        int inicio = Int32.Parse(dr["inicio"].ToString());
        //            //        int longitud = Int32.Parse(dr["longitud"].ToString());

        //            //        field.FieldName = dr["CampoDestino"].ToString();
        //            //        field.FieldFormat = dr["Formato"].ToString();
        //            //        field.ContainsFormat = (bool)dr["Formatear"];
        //            //        field.FieldValue = line.Substring(inicio, longitud);

        //            //        lstCampos.Add(field);
        //            //    }

        //            //    if (lstCampos.Count > 0)
        //            //    {
        //            //        if (archivo.IDFormato == 1)
        //            //        {
        //            //            FieldItem field = new FieldItem();
        //            //            field.FieldName = "NoCuenta";
        //            //            field.FieldValue = noCuenta;
        //            //            lstCampos.Add(field);
        //            //        }

        //            //        string insertQuery = CreateQuery(lstCampos, archivo.TablaDestino, fileName, archivo.IDBanco);

        //            //        switch (archivo.IDFormato)
        //            //        {
        //            //            case 1:
        //            //                if (ValidateExistRowBancomer(lstCampos, archivo.TablaDestino))
        //            //                {
        //            //                    ExecuteQuery(insertQuery);
        //            //                }
        //            //                break;
        //            //            case 2:
        //            //                if (ValidateExistRowSantander(lstCampos, archivo.TablaDestino))
        //            //                {
        //            //                    ExecuteQuery(insertQuery);
        //            //                }
        //            //                break;
        //            //            case 3:
        //            //                ExecuteQuery(insertQuery);
        //            //                break;
        //            //            case 4:
        //            //                break;
        //            //        }
        //            //    }
        //        }
        //        Console.WriteLine("");
        //        //Console.WriteLine("Total Registros: " + contRegisters.ToString());

        //        //URLFile = txtPath;
        //        //string procesDate = string.Format("{0:ddMMyyyyhhmm}_", DateTime.Now);
        //        //if (Directory.Exists(processFolder))
        //        //{
        //        //    if (!File.Exists(processFolder + fileName))
        //        //    {
        //        //        File.Move(txtPath, processFolder + fileName);
        //        //        WriteLog(processFolder + fileName);
        //        //    }
        //        //    else
        //        //    {
        //        //        File.Delete(txtPath);
        //        //    }
        //        //}
        //        //else
        //        //{
        //        //    File.Move(txtPath, archivo.RutaDestino + fileName);
        //        //    WriteLog(processFolder + fileName);
        //        //}

        //        //string insertLog =
        //        //        string.Format("insert into archivo (idFormato,nombre,estatus) values ({0},'{1}',{2})",
        //        //        archivo.IDFormato, fileName, 1);

        //        //ExecuteQuery(insertLog);

        //        //sendDepositosEmail(banco, deposito, lblBanco);
        //        Console.WriteLine("Finaliza Proceso");
        //    }
        //}
    }    
}
