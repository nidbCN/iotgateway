using System;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using WalkingTec.Mvvm.Core;
using WalkingTec.Mvvm.Core.Extensions;
using WalkingTec.Mvvm.Mvc;
using WalkingTec.Mvvm.Mvc.Admin.ViewModels.FrameworkUserVms;
using IoTGateway.ViewModel.HomeVMs;

namespace IoTGateway.Controllers
{
    [AllRights]
    public class LoginController : BaseController
    {

        [Public]
        [ActionDescription("Login")]
        public IActionResult Login()
        {
            var vm = Wtm.CreateVM<LoginVM>();
            vm.Redirect = HttpContext.Request.Query["ReturnUrl"];

            if (Wtm.ConfigInfo.IsQuickDebug != true) return View(vm);
            
            vm.ITCode = "admin";
            vm.Password = "000000";

            return View(vm);
        }

        [Public]
        [HttpPost]
        public async Task<ActionResult> Login(LoginVM vm)
        {
            var user = await vm.DoLoginAsync();
            if (user is null)
            {
                return View(vm);
            }

            Wtm.LoginUserInfo = user;
            var url = string.IsNullOrEmpty(vm.Redirect) ? "/" : vm.Redirect;

            AuthenticationProperties properties = null;
            if (vm.RememberLogin)
            {
                properties = new ()
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.Add(TimeSpan.FromDays(30))
                };
            }

            var principal = user.CreatePrincipal();
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, properties);
            return Redirect(HttpUtility.UrlDecode(url));
        }

        [AllRights]
        [ActionDescription("Logout")]
        public async Task Logout()
        {
            await Wtm.RemoveUserCache(Wtm.LoginUserInfo.ITCode);
            HttpContext.Session.Clear();
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Response.Redirect("/");
        }

        [AllRights]
        [ActionDescription("ChangePassword")]
        public ActionResult ChangePassword()
        {
            var vm = Wtm.CreateVM<ChangePasswordVM>();
            vm.ITCode = Wtm.LoginUserInfo.ITCode;
            return PartialView(vm);
        }

        [AllRights]
        [HttpPost]
        [ActionDescription("ChangePassword")]
        public ActionResult ChangePassword(ChangePasswordVM vm)
        {
            if (!ModelState.IsValid)
            {
                return PartialView(vm);
            }

            vm.DoChange();
            return FFResult().CloseDialog().Alert(Localizer["Login.ChangePasswordSuccess"]);
        }

    }
}
