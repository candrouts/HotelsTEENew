using HotelsTEE.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace HotelsTEE.Controllers
{
    public class AccountController : Controller
    {

       

        IFormsAuthenticationService FormsService { get; set; }
        IMembershipService MembershipService { get; set; }

        // GET: Account
        [AllowAnonymous]
        public ActionResult Login(string returnUrl = "", string success = "", string failure = "")
        {
            ViewBag.ReturnUrl = returnUrl;
            ViewBag.success = success;
            ViewBag.CurrentView = "Account/Login";
            ViewBag.failure = failure;
            return View();
        }


        [Authorize]
        public ActionResult LogOff()
        {
            FormsService = new FormsAuthenticationService();

            FormsService.SignOut();

            return RedirectToAction("Login", "Account");
        }

    }
}