using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BasicBot.Database
{
    public class Author
    {
        public string Name { get; set; }

        public List<Book> Books { get; set; }
    }
}
