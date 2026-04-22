using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Exceptions
{
    public class DuplicateTransactionException : Exception
    {
        public DuplicateTransactionException(string message) : base(message)
        {
        }
    }
}
