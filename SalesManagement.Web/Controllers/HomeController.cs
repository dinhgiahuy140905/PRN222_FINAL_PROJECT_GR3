using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesManagement.Web.Models;
using SalesManagement.Web.Services;

namespace SalesManagement.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ICurrentStoreContext _currentStore;

        public HomeController(ICurrentStoreContext currentStore)
        {
            _currentStore = currentStore;
        }

        [Authorize]
        public async Task<IActionResult> Index()
        {
            if (await _currentStore.GetMembershipAsync() is null)
            {
                return RedirectToAction("Select", "Store");
            }

            return View();
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
