using BasicBot.Database;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BasicBot.Services
{
    public class BookService
    {
        private DataBase _dataBase;

        public BookService(DataBase database)
        {
            _dataBase = database;
        }

        public List<string> GetAuthorsBooks(string authorName)
        {
            return _dataBase.Authors.FirstOrDefault(a => a.Name.Equals(authorName, StringComparison.InvariantCultureIgnoreCase))
                ?.Books.Select(b => b.Name).ToList();
        }

        public string GetAuthorOfTheBookName(string bookName)
        {
            return _dataBase.Authors.FirstOrDefault(a => a.Books.Any(b => $"'{b.Name}'".Equals(bookName, StringComparison.InvariantCultureIgnoreCase))).Name;
        }

        public string RecommendBook(string genre = null)
        {
            if(genre != null)
            {
                var books = _dataBase.Authors.SelectMany(a => a.Books, (a, b) => new { Author = a.Name, Book = b });
                var maxRate = books.Where(b=> b.Book.Genres.Contains(genre)).Max(b => b.Book.Rate);
                var book = books.First(b => b.Book.Rate == maxRate && b.Book.Genres.Contains(genre));
                return $"I recommend you such book: \n Name: '{book.Book.Name}' \n " +
                    $"Author: {book.Author} \n Rate: {book.Book.Rate}";
            } else
            {
                var books = _dataBase.Authors.SelectMany(a => a.Books, (a, b) => new { Author = a.Name, Book = b });
                var maxRate = books.Max(b => b.Book.Rate);
                var book = books.First(b => b.Book.Rate == maxRate);
                return $"I recommend you such book: \n Name: '{book.Book.Name}' \n " +
                    $"Author: {book.Author} \n Rate: {book.Book.Rate}";
            }
        }
    }
}
