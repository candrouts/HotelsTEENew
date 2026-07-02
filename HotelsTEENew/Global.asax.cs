using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

namespace HotelsTEE
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            GlobalConfiguration.Configure(WebApiConfig.Register);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }

        // Δίχτυ ασφαλείας: καταγραφή unhandled exceptions (MVC pipeline)
        protected void Application_Error()
        {
            Exception ex = Server.GetLastError();
            if (ex == null) return;

            string source = "UnhandledException";
            string user = null;
            try
            {
                if (Context != null && Context.Request != null)
                    source = "Unhandled: " + Context.Request.HttpMethod + " " + Context.Request.RawUrl;
                if (Context != null && Context.User != null && Context.User.Identity != null)
                    user = Context.User.Identity.Name;
            }
            catch (Exception) { }

            Utils.ErrorLogger.Log(ex, source, user);
        }
    }
}
