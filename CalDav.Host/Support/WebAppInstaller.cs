using System;
using System.IO;
using System.Linq;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Routing;
using Castle.Windsor;
using Microsoft.Owin.Cors;
using Owin;

namespace CalDav.Host.Support
{
    public class WebAppInstaller
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureWebApi(app);
            ConfiguraAdmin(app);
        }

        private void ConfigureWebApi(IAppBuilder app)
        {
            var container = new WindsorContainer();
            var config = new HttpConfiguration()
            {
                DependencyResolver = new WindsorResolver(container)
            };
            RegisterRoutes(RouteTable.Routes);
            config.MapHttpAttributeRoutes();
            config.Routes.MapHttpRoute(
                "DefaultApi",
                "{controller}/{action}/{id}",
                new { controller = "CalDav", action = "Index", id = UrlParameter.Optional }
                );

            app.UseCors(CorsOptions.AllowAll);
            app.UseWebApi(config);
        }

        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("ignore", "{resource}.axd/{*pathInfo}");
            Server.Controllers.CalDavController.RegisterRoutes(routes);
        }

        private void ConfiguraAdmin(IAppBuilder app)
        {
            var appFolder = FindAppRoot();
        }

        private string FindAppRoot()
        {
            var root = AppDomain.CurrentDomain.BaseDirectory
                .ToLowerInvariant()
                .Split(Path.DirectorySeparatorChar)
                .ToList();

            while (true)
            {
                var last = root.Last();
                if (last == String.Empty || last == "debug" || last == "release" || last == "bin")
                {
                    root.RemoveAt(root.Count - 1);
                    continue;
                }

                break;
            }

            root.Add("app");

            var appFolder = String.Join("" + Path.DirectorySeparatorChar, root);
            return appFolder;

        }
    }
}
