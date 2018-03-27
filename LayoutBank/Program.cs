using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Configuration;

namespace LayoutBank
{
    class Program
    {

        static void Main(string[] args)
        {

            

            Int64 hours = Int32.Parse(ConfigurationManager.AppSettings["nextStartHour"].ToString());
            Int64 minutes = Int32.Parse(ConfigurationManager.AppSettings["nextStartMinute"].ToString());


            if ((minutes <= 0 && hours <= 0) || (minutes > 59 ) || (hours > 23))
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("Verifique la configuración Hora o Minuto Incorrecto. ");
                Console.ResetColor();            
                Console.ReadLine();
                
                return;
            }

             hours *=  60*60000;
             minutes *= 60000;

             Int64 totalTime = hours + minutes;
                                                
            var autoEvent = new AutoResetEvent(false);

           // var statusChecker = new StatusChecker(10);
            var statusChecker = new StatusChecker();

            Console.WriteLine("{0:dd-MM-yyyy hh:mm} Inicializando.\n", DateTime.Now);
            var stateTimer = new Timer(statusChecker.CheckStatus, autoEvent, 3000, totalTime);
            autoEvent.WaitOne();
             
           
        }


        class StatusChecker
        {
            /*
            private int invokeCount;
            private int maxCount;

            public StatusChecker(int count)
            {
                invokeCount = 0;
                maxCount = count;
            }
            */

            public void CheckStatus(Object stateInfo)
            {
                FileReader fr = new FileReader();   
                fr.Start();                
                AutoResetEvent autoEvent = (AutoResetEvent)stateInfo;                                
            }
        }
    }
}
