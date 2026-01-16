using Microsoft.AspNetCore.Mvc;
using FPTU.Capstone.AMKCollective.Application.Interfaces;
using FPTU.Capstone.AMKCollective.Application.DTOs;

namespace FPTU.Capstone.AMKCollective.Api.Controllers
{
    [Route("api/[controller]")]
    public class UsersController : BaseApiController
    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }

        /// <summary>
        /// Lấy danh sách tất cả users
        /// </summary>
        [HttpGet]
        public IActionResult Get()
        {
            try
            {
                var users = _userService.GetAll();
                return SuccessResponse(users, "Lấy danh sách users thành công");
            }
            catch (Exception ex)
            {
                return ErrorResponse<IEnumerable<UserDto>>($"Lỗi: {ex.Message}");
            }
        }
    }
}
