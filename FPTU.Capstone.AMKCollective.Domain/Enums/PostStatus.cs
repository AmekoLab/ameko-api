using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Domain.Enums
{
    public enum PostStatus
    {
        Draft, //Bản nháp

        Published, //Công khai

        Hidden, //Bị ẩn (Report/User ẩn)

        Removed //Bị xóa do vi phạm
    }
}
