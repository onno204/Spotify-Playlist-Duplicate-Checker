using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Spot_Dub_Checker_UI {
    static class Program {
        public static MainUI MainUIobj;
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main() {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            MainUIobj = new MainUI();
            Application.Run(MainUIobj);
        }
    }
}
