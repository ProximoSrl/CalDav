using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Castle.Windsor;
using Microsoft.Owin.Hosting;

namespace CalDav.Host.Support
{
    public class HostInstaller
    {
        private IDisposable _webApp;
        private WindsorContainer Container { get; set; }

        public void Start()
        {
            string apiUrl = "http://+:56100";
            _webApp = WebApp.Start<WebAppInstaller>(new StartOptions(apiUrl));
        }

        public void Stop()
        {
            _webApp.Dispose();
        }
    }
}
