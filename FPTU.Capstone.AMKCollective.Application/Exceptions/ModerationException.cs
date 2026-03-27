using System;
using FPTU.Capstone.AMKCollective.Application.DTOs.Common;

namespace FPTU.Capstone.AMKCollective.Application.Exceptions
{
    public class ModerationException : Exception
    {
        public ModerationResult Result { get; }

        public ModerationException(ModerationResult result) : base(result.Reason)
        {
            Result = result;
        }
    }
}
