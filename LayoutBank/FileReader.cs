using System;
using System.IO;
using System.Data;
using System.Net;
using System.Linq;
using System.Text;
using System.Configuration;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace LayoutBank
{
    public class FileReader
    {
        enum Estatus { Procesado = 1, ConErrores, NoProcesado, Vacio };
        string URLFile;

        public void Start()
        {
            try
            {
                Int64 hours = Int32.Parse(ConfigurationManager.AppSettings["nextStartHour"].ToString());
                Int64 minutes = Int32.Parse(ConfigurationManager.AppSettings["nextStartMinute"].ToString());

                Console.WriteLine("Comienza lectura Layouts {0} ", DateTime.Now.ToString("dd-MM-yyyy hh:mm"));

                DataTable tableFormato = GetTable("select * from Formato where estatus = 1 ");

                foreach (DataRow dr in tableFormato.Rows)
                {
                    Console.WriteLine("Procesando: " + dr["Nombre"].ToString());
                    Formato archivo = SetValues(dr);

                    if (dr["FTP"].ToString() == "True")
                    {
                        Console.WriteLine("Verificando archivos en el FTP");
                        if (this.DescargaLayouts(archivo))
                        {
                            ProcessFile(archivo);
                        }
                    }
                    else
                    {
                        ProcessFile(archivo);
                    }
                    Console.WriteLine("");
                }

                Console.WriteLine("Termina lectura Layouts: {0} \nSiguiente busqueda: {1} ",
                DateTime.Now.ToString("dd-MM-yyyy hh:mm"), DateTime.Now.AddHours(hours).AddMinutes(minutes).ToString("dd-MM-yyyy hh:mm"));
            }
            catch (Exception ex)
            {
                
                Int64 hours = Int32.Parse(ConfigurationManager.AppSettings["nextStartHour"].ToString());
                Int64 minutes = Int32.Parse(ConfigurationManager.AppSettings["nextStartMinute"].ToString());
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine(DateTime.Now.ToString("dd-MM-yyyy hh:mm").ToString() + ": \n" + ex.Message.ToString());
                Console.ResetColor();
                Console.WriteLine("Termina lectura Layouts: {0} \nSiguiente busqueda: {1} ",
                DateTime.Now.ToString("dd-MM-yyyy hh:mm"), DateTime.Now.AddHours(hours).AddMinutes(minutes).ToString("dd-MM-yyyy hh:mm"));

                if (ex.HResult == -2147024713) {
                    File.Delete(URLFile);
                }
            }
        }


        private Formato SetValues(DataRow dr)
        {
            Formato archivo = new Formato();
            archivo.IDFormato       = Int32.Parse(dr["IDFormato"].ToString());
            archivo.Nombre          = dr["Nombre"].ToString();
            archivo.Descripcion     = dr["Descripcion"].ToString();
            archivo.RutaOrigen      = dr["RutaArchivoOrigen"].ToString();
            archivo.RutaDestino     = dr["RutaArchivoDestino"].ToString();
            archivo.LongitudLinea   = Int32.Parse(dr["LongitudLinea"].ToString());
            archivo.TablaDestino    = dr["TablaDestino"].ToString();
            archivo.IDBanco         = Int32.Parse(dr["IDBanco"].ToString());
            archivo.FTPServer       = dr["FTPServer"].ToString();
            archivo.FTPUser         = dr["FTPUser"].ToString();
            archivo.FTPPassword     = dr["FTPPassword"].ToString();
            archivo.Estatus = (int)Estatus.NoProcesado;
            return archivo;
        }

        private void ProcessFile(Formato archivo)
        {
            string processFolder = GetFolderPath(archivo.RutaDestino);
            string[] txtFiles = GetTextFiles(archivo.RutaOrigen);
            string queryDetalle = string.Format("select * from FormatoDetalle where IdFormato ={0}", archivo.IDFormato);
            DataTable tablaDetalle = GetTable(queryDetalle);

            if (tablaDetalle.Rows.Count <= 0) return;

            foreach (string txtPath in txtFiles)
            {
                string[] txtLines = ReadAllLine(txtPath);
                string fileName = Path.GetFileName(txtPath);
                string noCuenta = string.Empty;
                int numeroLinea = 0;

                foreach (string line in txtLines)
                {
                    List<FieldItem> lstCampos = new List<FieldItem>();

                    if (archivo.IDFormato == 1 && numeroLinea == 0)
                    {
                        noCuenta = line.Substring(2, 18);
                        numeroLinea = 1;
                    }

                    foreach (DataRow dr in tablaDetalle.Rows)
                    {
                        if (line.Length < archivo.LongitudLinea) continue;

                        FieldItem field = new FieldItem();
                        int inicio = Int32.Parse(dr["inicio"].ToString());
                        int longitud = Int32.Parse(dr["longitud"].ToString());

                        field.FieldName = dr["CampoDestino"].ToString();
                        field.FieldFormat = dr["Formato"].ToString();                        
                        field.ContainsFormat = (bool)dr["Formatear"];
                        field.FieldValue = line.Substring(inicio, longitud);

                        lstCampos.Add(field);
                    }

                    if (lstCampos.Count > 0)
                    {
                        if (archivo.IDFormato == 1)
                        {
                            FieldItem field = new FieldItem();
                            field.FieldName = "NoCuenta";
                            field.FieldValue = noCuenta;
                            lstCampos.Add(field);
                        }

                        string insertQuery = CreateQuery(lstCampos, archivo.TablaDestino, fileName, archivo.IDBanco);

                        switch (archivo.IDFormato) {
                            case 1:
                                if (ValidateExistRowBancomer(lstCampos, archivo.TablaDestino))
                                {
                                    ExecuteQuery(insertQuery);
                                }
                                break;
                            case 2:
                                if (ValidateExistRowSantander(lstCampos, archivo.TablaDestino))
                                {
                                    ExecuteQuery(insertQuery);
                                }
                                break;
                        }
                       /// if (archivo.IDFormato != 2)
                       // {
                         //   ExecuteQuery(insertQuery);
                        //}else
                        //{
                            //if (ValidateExistRowBancomer(lstCampos, archivo.TablaDestino))
                            //{
                            //    ExecuteQuery(insertQuery);
                            //}
                        //}
                    }
                }

                URLFile = txtPath;
                string procesDate = string.Format("{0:ddMMyyyyhhmm}_", DateTime.Now);
                if (Directory.Exists(processFolder))
                {
                    File.Move(txtPath,   processFolder  + fileName);
                }
                else
                {
                    File.Move(txtPath, archivo.RutaDestino  + fileName);
                }

                string insertLog =
                        string.Format("insert into archivo (idFormato,nombre,estatus) values ({0},'{1}',{2})",
                        archivo.IDFormato, fileName, 1);

                ExecuteQuery(insertLog);

                Console.WriteLine("Finaliza Proceso");
            }

        }

        private bool ValidateExistRowBancomer(List<FieldItem> fields, string table)
        {
            bool result = false;
            StringBuilder where = new StringBuilder();
            int count = 0;

            foreach(FieldItem item in fields)
            {
                count++;
                where.Append(item.FieldName);
                where.Append(" = ");
                
                if (item.ContainsFormat)
                {
                    where.Append(string.Format(item.FieldFormat, item.FieldValue));
                }
                else
                {
                    where.Append("'");
                    where.Append(item.FieldValue);
                    where.Append("'");
                }

                if (count < fields.Count)
                {
                    where.Append(" and ");
                }
            }

            string query = string.Format("if exists (select * from {0} where {1}) begin  select valid = 'False' end  else  begin  select valid = 'True' end", table, where.ToString());
            DataTable response = GetTable(query);

            if (response.Rows.Count > 0)
            {
                DataRow dr = response.Rows[0];
                result = bool.Parse(dr["valid"].ToString());
            }

            return result;
        }

        private bool ValidateExistRowSantander(List<FieldItem> fields, string table)
        {
            bool result = false;
            StringBuilder where = new StringBuilder();
            int count = 0;

            foreach (FieldItem item in fields)
            {
                count++;
                if (item.FieldName == "importe")
                {
                    // Console.WriteLine("El importe ya no se validara");
                }
                else if(item.FieldName == "saldo")
                {
                    // Console.WriteLine("El saldo ya no se validara");
                }
                else
                {
                    
                    where.Append(item.FieldName);
                    where.Append(" = ");

                    if (item.ContainsFormat)
                    {
                        where.Append(string.Format(item.FieldFormat, item.FieldValue));
                    }
                    else
                    {
                        where.Append("'");
                        where.Append(item.FieldValue);
                        where.Append("'");
                    }

                    if (count < fields.Count)
                    {
                        where.Append(" and ");
                    }
                }
            }

            string query = string.Format("if exists (select * from {0} where {1}) begin  select valid = 'False' end  else  begin  select valid = 'True' end", table, where.ToString());
            
            DataTable response = GetTable(query);

            if (response.Rows.Count > 0)
            {
                DataRow dr = response.Rows[0];
                result = bool.Parse(dr["valid"].ToString());
            }

            return result;
        }

        private string CreateQuery(List<FieldItem> fields, string table, string fileName, int idbanco)
        {
            StringBuilder colums = new StringBuilder();
            StringBuilder values = new StringBuilder();

            foreach (FieldItem item in fields)
            {

                colums.Append(item.FieldName);
                colums.Append(",");

                if (item.ContainsFormat)
                {                    
                    values.Append(string.Format(item.FieldFormat, item.FieldValue));
                }
                else
                {
                    values.Append("'");
                    values.Append(item.FieldValue);
                    values.Append("'");
                }

                values.Append(",");
            }

            if (colums.Length > 1) colums.Length--;
            if (values.Length > 1) values.Length--;

            return
                string.Format("insert into {0} (txtOrigen,estatus,idBanco,{1}) values ('{2}',{3},{5},{4})",
                table, colums.ToString(), fileName, (int)Estatus.Procesado, values.ToString(), idbanco);

        }



        private DataTable GetTable(string query)
        {
            SqlConnection cnx = new SqlConnection();
            SqlCommand cmd = new SqlCommand();
            SqlDataAdapter da = new SqlDataAdapter();
            DataTable dt = new DataTable();

            cnx.ConnectionString = ConfigurationManager.ConnectionStrings["cnxReferencias"].ToString();
            cmd.Connection = cnx;
            cmd.CommandText = query;
            da.SelectCommand = cmd;
            da.Fill(dt);

            return dt;
        }



        private void ExecuteQuery(string query)
        {
            SqlConnection cnx = new SqlConnection();
            SqlCommand cmd = new SqlCommand();

            cnx.ConnectionString = ConfigurationManager.ConnectionStrings["cnxReferencias"].ToString();
            cmd.Connection = cnx;
            cmd.CommandText = query;

            cnx.Open();
            cmd.ExecuteNonQuery();
            cnx.Close();
        }

        private string[] GetTextFiles(string path)
        {
            return Directory.GetFiles(path);
        }


        private string[] ReadAllLine(string path)
        {
            return File.ReadAllLines(path);
        }


        private string GetFolderPath(string path)
        {

            string monthName = DateTime.Now.ToString("MMM", System.Globalization.CultureInfo.CreateSpecificCulture("es")).Substring(0, 3).ToUpper();
            string year = DateTime.Now.ToString("yyyy");
            string dirName = path + year + monthName + "\\";

            if (!Directory.Exists(dirName))
                Directory.CreateDirectory(dirName);

            return dirName;

        }

        private bool DescargaLayouts(Formato Data) {
            string serverFTP = "ftp://"+ Data.FTPUser +"@"+ Data.FTPServer;
            // Console.WriteLine(serverFTP);

            FtpWebRequest request = (FtpWebRequest)WebRequest.Create(serverFTP);
            request.Method = WebRequestMethods.Ftp.ListDirectory;
            //request.Method = WebRequestMethods.Ftp.ListDirectoryDetails;
            request.Credentials = new NetworkCredential(Data.FTPUser, Data.FTPPassword);

            FtpWebResponse response = (FtpWebResponse)request.GetResponse();
            StreamReader streamReader = new StreamReader(response.GetResponseStream());
            List<string> directories = new List<string>();

            string line = streamReader.ReadLine();
            while (!string.IsNullOrEmpty(line))
            {
                directories.Add(line);
                line = streamReader.ReadLine();
            }

            streamReader.Close();



            foreach (string s in directories)
            {
                // Console.WriteLine(s);
                WriteLog(s);
            }



            using (WebClient ftpClient = new WebClient())
            {
                ftpClient.Credentials = new System.Net.NetworkCredential(Data.FTPUser, Data.FTPPassword);

                for (int i = 0; i <= directories.Count - 1; i++)
                {
                    if (directories[i].Contains("."))
                    {
                        if (directories.Count - 1 == i)
                        {
                            string path = serverFTP + "/" + directories[i].ToString();
                            string trnsfrpth = Data.RutaOrigen + @"\" + directories[i].ToString();
                            Console.WriteLine("Layout reciente " + directories[i].ToString() + ", descargado correctamente.");
                            ftpClient.DownloadFile(path, trnsfrpth);

                            // return true;
                        }
                    }
                }
            }

            return true;
        }

        static void WriteLog(string content)
        {
            string path = Directory.GetCurrentDirectory() + "\\log.txt";
            using (StreamWriter file = new StreamWriter(path, true))
            {
                file.WriteLine(content);
            }
        }

    }
}