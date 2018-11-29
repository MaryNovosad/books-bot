using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BasicBot.Database
{
    public class Book
    {
        public string Name { get; set; }

        public double Rate { get; set; }

        public List<string> Genres { get; set; }
    }
}
