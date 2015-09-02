using Castle.Windsor;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Web.Mvc;
using System.Web.Routing;
using System;
using System.Collections.Generic;
using Castle.MicroKernel.Registration;
using CalDav.Server.Models;
using CalDav.MVC.Models;
using System.Linq;

namespace Demo.MVC {
	// Note: For instructions on enabling IIS6 or IIS7 classic mode, 
	// visit http://go.microsoft.com/?LinkId=9394801

	public class MvcApplication : System.Web.HttpApplication {
		public static void RegisterGlobalFilters(GlobalFilterCollection filters) {
			filters.Add(new HandleErrorAttribute());
		}

		public static void RegisterRoutes(RouteCollection routes) {
			routes.IgnoreRoute("{resource}.axd/{*pathInfo}");
			CalDav.Server.Controllers.CalDavController.RegisterRoutes(routes);

			routes.MapRoute(
					"Default", // Route name
					"{controller}/{action}/{id}", // URL with parameters
					new { controller = "Home", action = "Index", id = UrlParameter.Optional } // Parameter defaults
			);


		}

        public static WindsorContainer Container { get; private set; }

		protected void Application_Start() {
			AreaRegistration.RegisterAllAreas();

			// Use LocalDB for Entity Framework by default
			Database.DefaultConnectionFactory = new SqlConnectionFactory(@"Data Source=(localdb)\v11.0; Integrated Security=True; MultipleActiveResultSets=True");

			RegisterGlobalFilters(GlobalFilters.Filters);
			RegisterRoutes(RouteTable.Routes);

            Container = new WindsorContainer();

            DependencyResolver.SetResolver(new WindsorResolver(Container));

            Container.Register(
                Component.For<ICalendarRepository>()
                    .ImplementedBy<CalendarRepository>());
        }
	}

    public class WindsorResolver : IDependencyResolver
    {
        private readonly IWindsorContainer container;

        public WindsorResolver(IWindsorContainer container)
        {
            this.container = container;
        }

        public object GetService(Type serviceType)
        {
            return container.Kernel.HasComponent(serviceType) ? container.Resolve(serviceType) : null;
        }

        public IEnumerable<object> GetServices(Type serviceType)
        {
            return container.Kernel.HasComponent(serviceType) ? 
                container.ResolveAll(serviceType).Cast<object>() : new object[] { };
        }

    }
}