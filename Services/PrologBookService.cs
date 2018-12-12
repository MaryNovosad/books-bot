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
                var max = solutions.NextSolution.Max(s => double.Parse(GetVariableByName(s, "Rate")));
                var recommendation = solutions.NextSolution.FirstOrDefault(s => double.Parse(GetVariableByName(s, "Rate")) == max);
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
            return solution.NextVariable.FirstOrDefault(v => v.Name == name)?.Value;
        }
    }
}
