book("harry potter and philosopher stone", "joan rowling", 4, "Fantasy").
book("harry potter and the prisoner of azkaban.", "joan rowling", 3, "Fantasy").
book("harry potter and the chamber of secrets", "joan rowling", 5, "Fantasy").
book("harry potter and the goblet of fire.", "joan rowling", 1, "Fantasy").
book("harry potter and the order of the phoenix.", "joan rowling", 2, "Fantasy").

book("the picture of dorian gray", "oscar wilde", 8, "Mystery").
book("murder in the library", "agata kristi", 9, "Mystery").
book("intentions", "oscar wilde", 7, "Drama").
book("intentions 2", "oscar wilde", 8, "Drama").

genre("Fantasy").
genre("Horror").
genre("Science").
genre("Adventure").
genre("Poetry").
genre("History").
genre("Comics").
genre("Romance").
genre("Drama").
genre("Psychology").

bookIsPopular(Rate) :-
Rate > 8.

bookIsSlightlyPopular(Rate) :-
(Rate > 4, Rate < 8).

bookIsNotPopular(Rate) :-
Rate < 5.

bookIsWorthReading(Book) :-
	book(Book, _, Rate, _),
	(bookIsSlightlyPopular(Rate); bookIsPopular(Rate)).

authorIsWorldFamous(Author) :-
	book(_,Author, Rate, _),
	bookIsPopular(Rate).

likes("Anna", "Fantasy").
likes("Mary", "Mystery").

likes("Iryna", "Fantasy").
likes("Inna", "Drama").
