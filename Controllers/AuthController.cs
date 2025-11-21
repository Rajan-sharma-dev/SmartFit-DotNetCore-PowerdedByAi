using Microsoft.AspNetCore.Mvc;

namespace MiddleWareWebApi.Controllers
{
    public class AuthController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
