using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Color_Quantization
{
    static class Filters
    {
        public static double[,] FloydAndSteinberg_Filter = new double[3, 3] {     {   0,      0,      0    },
                                                                                  {   0,      0,    7/16.0 },
                                                                                  { 3/16.0, 5/16.0, 1/16.0 }};
        public static double[,] Burkes_Filter = new double[3, 5] {{  0,       0,      0,      0,      0    },
                                                                  {  0,       0,      0,    4/16.0, 2/16.0 },
                                                                  { 1/16.0, 2/16.0, 4/16.0, 2/16.0, 1/16.0 }};
        public static double[,] Stucky_Filter = new double[5, 5] {{   0,      0,      0,      0,      0    },
                                                                  {   0,      0,      0,      0,      0    },
                                                                  {   0,      0,      0,    8/42.0, 4/42.0 },
                                                                  { 2/42.0, 4/42.0, 8/42.0, 4/42.0, 2/42.0 },
                                                                  { 1/42.0, 2/42.0, 4/42.0, 2/42.0, 1/42.0 }};
    }
}
