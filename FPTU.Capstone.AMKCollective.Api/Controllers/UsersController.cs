using Microsoft.AspNetCore.Mvc;
using FPTU.Capstone.AMKCollective.Application.Interfaces;

namespace FPTU.Capstone.AMKCollective.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet]
        public IActionResult Get()
        {
            var users = _userService.GetAll();
            return Ok(users);
        }
    }
}
