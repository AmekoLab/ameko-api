using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Settings
{
    public class ScoreWeights
    {
        public double Issue { get; set; }
        public double Response { get; set; }
        public double Refund { get; set; }
        public double Repurchase { get; set; }
        public double Feedback { get; set; }
    }
}
