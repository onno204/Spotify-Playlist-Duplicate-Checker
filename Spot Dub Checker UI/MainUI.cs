using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CefSharp;
using CefSharp.WinForms;
using Newtonsoft.Json;

namespace Spot_Dub_Checker_UI {
    public partial class MainUI :Form {
        ChromiumWebBrowser browser;
        public static String filesPath = Directory.GetCurrentDirectory() + "/../../html/";
        public MainUI() {
            InitializeComponent();
            spotConnector.AuthInit();
            while (spotConnector.initFinished == false) {
                Thread.Sleep(250);
                Console.WriteLine("..");
            }
            Cef.Initialize(new CefSettings());
            CefSharpSettings.LegacyJavascriptBindingEnabled = true;
            browser = new ChromiumWebBrowser("http://google.com/");
            browser.Name = "Simple Page";
            browser.Dock = DockStyle.Fill;
            toolStripContainer1.ContentPanel.Controls.Add(browser);
            browser.RegisterJsObject("bound", new BoundObject(), BindingOptions.DefaultBinder);
            string html = File.ReadAllText(filesPath + "main.html");
            browser.LoadHtml(html, filesPath);
            browser.IsBrowserInitializedChanged += browserInitStatusChanged;
        }
        private void browserInitStatusChanged(object sender, IsBrowserInitializedChangedEventArgs e) {
            if(e.IsBrowserInitialized) {
                browser.ShowDevTools();
            }
        }

        public void ExecuteMainWebbrowserScript(String functionName, object[] args) {
            ExecuteWebbrowserScript(browser, functionName, args);
        }
        public void ExecuteWebbrowserScript(ChromiumWebBrowser WB, String functionName, object[] args) {
            if(WB.InvokeRequired) {
                WB.Invoke((MethodInvoker)delegate { WB.ExecuteScriptAsync(functionName, args); });
            } else { WB.ExecuteScriptAsync(functionName, args); }
        }
    }
    public class BoundObject {
        public Boolean AlarmEnabled {
            get { return _AlarmEnabled; }

            set {
                _AlarmEnabled = value;
            }
        }
        private Boolean _AlarmEnabled = false;
        public void getPlaylists() {
            Program.MainUIobj.ExecuteMainWebbrowserScript("setplaylists", new object[] { JsonConvert.SerializeObject(spotConnector.getPlaylists()) });
        }
        public void getDups(string PlaylistID) {
            Program.MainUIobj.ExecuteMainWebbrowserScript("handleduplicates", new object[] { JsonConvert.SerializeObject(spotConnector.getPlaylistsDuplicates(PlaylistID)) });
        }

    }
}
