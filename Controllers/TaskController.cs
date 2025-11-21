using Microsoft.AspNetCore.Mvc;

namespace MiddleWareWebApi.Controllers
{
    public class TaskController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
