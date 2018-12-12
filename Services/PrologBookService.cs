using Prolog;
using System.Linq;

namespace BasicBot.Services
{
    public class PrologBookService
    {
        private PrologEngine _prologEngine;

        public PrologBookService(PrologEngine prologEngine)
        {
            _prologEngine = prologEngine;
        }

        public string GetAuthorOfTheBookName(string bookName)
        {
            var solutions = _prologEngine.GetAllSolutions(null, $"book(\"{bookName}\", Author, Rate, Genre).");
            var solution = solutions.NextSolution.FirstOrDefault();
            return GetVariableByName(solution, "Author");
        }

        public string RecommendBook(string genre = null)
        {
            SolutionSet solutions;
            if (genre != null)
            {
                solutions = _prologEngine.GetAllSolutions(null, $"book(BookName, BookAuthor, Rate, \"{genre}\").");
            }
            else
            {
                solutions = _prologEngine.GetAllSolutions(null, $"book(BookName, BookAuthor, Rate, Genre).");
            }

            if (solutions.Success)
            {
                var amount = solutions.NextSolution.Count() == 1 ? 1 : solutions.NextSolution.Count() - 1;
                var max = solutions.NextSolution.Take(amount)
                    .Max(s => int.Parse(GetVariableByName(s, "Rate")));
                var recommendation = solutions.NextSolution.Take(amount).FirstOrDefault(s => int.Parse(GetVariableByName(s, "Rate")) == max);
                return $"I recommend you such book: \n Name: '{GetVariableByName(recommendation, "BookName")}' \n " +
                        $"Author: {GetVariableByName(recommendation, "BookAuthor")} \n Rate: {GetVariableByName(recommendation, "Rate")}";
            }
            else
            {
                return "Not enough data. Please connect bot to the Internet";
            }

        }

        private string GetVariableByName(Solution solution, string name)
        {
            var result = solution.NextVariable.FirstOrDefault(v => v.Name == name)?.Value;
            return result;
        }
    }
}
