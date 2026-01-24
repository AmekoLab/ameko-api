using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Follow
{
    public class FollowRequest
    {
        public Guid FollowerId { get; set; }
        public Guid FollowedId { get; set; }
    }
}
