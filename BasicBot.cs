// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BasicBot.Services;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Prolog;

namespace Microsoft.BotBuilderSamples
{
    /// <summary>
    /// Main entry point and orchestration for bot.
    /// </summary>
    public class BasicBot : IBot
    {
        // Supported LUIS Intents
        public const string CancelIntent = "Calcel";
        public const string HelpIntent = "Help";
        public const string SayHello = "SayHello";
        public const string None = "None";
        public const string AskToRecommendBook = "AskToRecommendBook";
        public const string TellAuthorOfSpecificBook = "TellAuthorOfSpecificBook";
        public const string TellAuthorsBooks = "TellAuthorsBooks";
        public const string TellGenresOfBooks = "TellGenresOfBooks";
        public const string TellIfBookIsWorthReading = "TellIfBookIsWorthReading"; 
        public const string TellIfAuthorIsVeryFamous = "TellIfAuthorIsVeryFamous";
        public const string RecommendBookByPreference = "RecommendBookByPreference";

        /// <summary>
        /// Key in the bot config (.bot file) for the LUIS instance.
        /// In the .bot file, multiple instances of LUIS can be configured.
        /// </summary>
        public static readonly string LuisConfiguration = "BasicBotLuisApplication";

        private readonly IStatePropertyAccessor<GreetingState> _greetingStateAccessor;
        private readonly IStatePropertyAccessor<DialogState> _dialogStateAccessor;
        private readonly UserState _userState;
        private readonly ConversationState _conversationState;
        private readonly BotServices _services;
        private readonly PrologBookService _prologBookService;
        private PrologEngine _prologEngine;

        /// <summary>
        /// Initializes a new instance of the <see cref="BasicBot"/> class.
        /// </summary>
        /// <param name="botServices">Bot services.</param>
        /// <param name="accessors">Bot State Accessors.</param>
        public BasicBot(BotServices services, UserState userState, ConversationState conversationState, ILoggerFactory loggerFactory)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _userState = userState ?? throw new ArgumentNullException(nameof(userState));
            _conversationState = conversationState ?? throw new ArgumentNullException(nameof(conversationState));

            _greetingStateAccessor = _userState.CreateProperty<GreetingState>(nameof(GreetingState));
            _dialogStateAccessor = _conversationState.CreateProperty<DialogState>(nameof(DialogState));

            // Verify LUIS configuration.
            if (!_services.LuisServices.ContainsKey(LuisConfiguration))
            {
                throw new InvalidOperationException($"The bot configuration does not contain a service type of `luis` with the id `{LuisConfiguration}`.");
            }

            Dialogs = new DialogSet(_dialogStateAccessor);
            Dialogs.Add(new GreetingDialog(_greetingStateAccessor, loggerFactory));

            _prologEngine = new PrologEngine(persistentCommandHistory: false);
            _prologEngine.Consult("db.pl");
            _prologBookService = new PrologBookService(_prologEngine);
        }

        private DialogSet Dialogs { get; set; }

        /// <summary>
        /// Run every turn of the conversation. Handles orchestration of messages.
        /// </summary>
        /// <param name="turnContext">Bot Turn Context.</param>
        /// <param name="cancellationToken">Task CancellationToken.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            var activity = turnContext.Activity;

            // Create a dialog context
            var dc = await Dialogs.CreateContextAsync(turnContext);

            if (activity.Type == ActivityTypes.Message)
            {
                // Perform a call to LUIS to retrieve results for the current activity message.
                var luisResults = await _services.LuisServices[LuisConfiguration].RecognizeAsync(dc.Context, cancellationToken);

                // If any entities were updated, treat as interruption.
                // For example, "no my name is tony" will manifest as an update of the name to be "tony".
                var topScoringIntent = luisResults?.GetTopScoringIntent();

                var topIntent = topScoringIntent.Value.intent;

                // update greeting state with any entities captured
                await UpdateGreetingState(luisResults, dc.Context);

                // Handle conversation interrupts first.
                var interrupted = await IsTurnInterruptedAsync(dc, topIntent);
                if (interrupted)
                {
                    // Bypass the dialog.
                    // Save state before the next turn.
                    await _conversationState.SaveChangesAsync(turnContext);
                    await _userState.SaveChangesAsync(turnContext);
                    return;
                }

                // Continue the current dialog
                var dialogResult = await dc.ContinueDialogAsync();

                // if no one has responded,
                if (!dc.Context.Responded)
                {
                    // examine results from active dialog
                    switch (dialogResult.Status)
                    {
                        case DialogTurnStatus.Empty:
                            switch (topIntent)
                            {
                                case SayHello:
                                    await dc.BeginDialogAsync(nameof(GreetingDialog));
                                    break;
                                case TellAuthorsBooks:
                                    var ent = luisResults.Entities["AuthorName"];
                                    if(ent != null)
                                    {
                                        var author = ent.First().ToString();
                                        var message = string.Empty;
                                        var solutions = _prologEngine.GetAllSolutions(null, $"book(B, \"{author}\", _, _).");
                                        if (solutions.Success)
                                        {
                                            message += $"{author} wrote such books: ";
                                            foreach (Solution s in solutions.NextSolution)
                                            {
                                                foreach (Variable v in s.NextVariable)
                                                {
                                                    if(v.Type != "namedvar")
                                                    {
                                                        message += $"\n {v.Value}";
                                                    }
                                                }
                                            }
                                        }
                                        else
                                        {
                                            message = "This author is missing in the database. Please, connect bot to Internet.";
                                        }

                                        await turnContext.SendActivityAsync(message);
                                    }
                                    else
                                    {
                                        await turnContext.SendActivityAsync("This author is missing in the database. Please, connect bot to Internet.");
                                    }
                                    break;
                                case AskToRecommendBook:
                                    luisResults.Entities.TryGetValue("Genre", out var genre);
                                    await turnContext.SendActivityAsync(_prologBookService.RecommendBook(genre?.First().First().ToString()));
                                    break;
                                case RecommendBookByPreference:
                                    luisResults.Entities.TryGetValue("UserName", out var username);
                                    await turnContext.SendActivityAsync(_prologBookService.RecommendBookByPreference(username.First().ToString()));
                                    break;
                                case TellAuthorOfSpecificBook:
                                    var bookname = luisResults.Entities["BookName"].First().ToString().Trim('\'');
                                    var authorname = _prologBookService.GetAuthorOfTheBookName(bookname);
                                    var response = string.IsNullOrEmpty(authorname)
                                        ? "There is no such book in the database. \n Please, connect bot to the Internet."
                                        : $"{authorname} wrote book {bookname}";
                                    await turnContext.SendActivityAsync(response);
                                    break;
                                case TellGenresOfBooks:
                                    var bookGenresMessage = "These are different book genres: ";
                                    var genres = _prologEngine.GetAllSolutions(null, $"genre(G).");
                                    if (genres.Success)
                                    {
                                        foreach (Solution s in genres.NextSolution)
                                        {
                                            foreach (Variable v in s.NextVariable)
                                            {
                                                if (v.Type != "namedvar")
                                                {
                                                    bookGenresMessage += $"\n {v.Value}";
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        bookGenresMessage = "Sorry, I can't tell you about book genres. Please, connect bot to Internet.";
                                    }

                                    await turnContext.SendActivityAsync(bookGenresMessage);

                                    break;
                                case TellIfBookIsWorthReading:
                                    var book = luisResults.Entities["BookName"].First().ToString().Replace('\'', '\"');
                                    var bookIsWorthReading = _prologEngine.GetFirstSolution($"bookIsWorthReading({book}).");
                                    if(bookIsWorthReading.Solved)
                                    {
                                        await turnContext.SendActivityAsync("I definitely recommend you this book! It's rate is pretty high.");
                                    }
                                    else
                                    {
                                        await turnContext.SendActivityAsync("I do not recommend you this book. Based on it's rate I can say that many people were disappointed.");
                                    }

                                    break;
                                case TellIfAuthorIsVeryFamous:
                                    var name = luisResults.Entities["AuthorName"].First().ToString();
                                    var authorIsFamous = _prologEngine.GetFirstSolution($"authorIsWorldFamous({name}).");
                                    if (authorIsFamous.Solved)
                                    {
                                        await turnContext.SendActivityAsync($"{name} is very famous all around the world. You should definitely get acquinted with his/her books.");
                                    }
                                    else
                                    {
                                        await turnContext.SendActivityAsync($"No, {name} is not very famous.");
                                    }
                                    break;
                                case None:
                                default:
                                    await dc.Context.SendActivityAsync("I didn't understand what you just said to me.");
                                    break;
                            }

                            break;

                        case DialogTurnStatus.Waiting:
                            // The active dialog is waiting for a response from the user, so do nothing.
                            break;

                        case DialogTurnStatus.Complete:
                            await dc.EndDialogAsync();
                            break;

                        default:
                            await dc.CancelAllDialogsAsync();
                            break;
                    }
                }
            }
            else if (activity.Type == ActivityTypes.ConversationUpdate)
            {
                if (activity.MembersAdded != null)
                {
                    // Iterate over all new members added to the conversation.
                    foreach (var member in activity.MembersAdded)
                    {
                        // Greet anyone that was not the target (recipient) of this message.
                        // To learn more about Adaptive Cards, see https://aka.ms/msbot-adaptivecards for more details.
                        if (member.Id != activity.Recipient.Id)
                        {
                            var welcomeCard = CreateAdaptiveCardAttachment();
                            var response = CreateResponse(activity, welcomeCard);
                            await dc.Context.SendActivityAsync(response);
                        }
                    }
                }
            }

            await _conversationState.SaveChangesAsync(turnContext);
            await _userState.SaveChangesAsync(turnContext);
        }

        // Determine if an interruption has occurred before we dispatch to any active dialog.
        private async Task<bool> IsTurnInterruptedAsync(DialogContext dc, string topIntent)
        {
            // See if there are any conversation interrupts we need to handle.
            if (topIntent.Equals(CancelIntent))
            {
                if (dc.ActiveDialog != null)
                {
                    await dc.CancelAllDialogsAsync();
                    await dc.Context.SendActivityAsync("Ok. I've canceled our last activity.");
                }
                else
                {
                    await dc.Context.SendActivityAsync("I don't have anything to cancel.");
                }

                return true;        // Handled the interrupt.
            }

            if (topIntent.Equals(HelpIntent))
            {
                await dc.Context.SendActivityAsync("Let me try to provide some help.");
                await dc.Context.SendActivityAsync("I understand greetings, being asked for help, or being asked to cancel what I am doing.");
                if (dc.ActiveDialog != null)
                {
                    await dc.RepromptDialogAsync();
                }

                return true;        // Handled the interrupt.
            }

            return false;           // Did not handle the interrupt.
        }

        // Create an attachment message response.
        private Activity CreateResponse(Activity activity, Attachment attachment)
        {
            var response = activity.CreateReply();
            response.Attachments = new List<Attachment>() { attachment };
            return response;
        }

        // Load attachment from file.
        private Attachment CreateAdaptiveCardAttachment()
        {
            var adaptiveCard = File.ReadAllText(@".\Dialogs\Welcome\Resources\welcomeCard.json");
            return new Attachment()
            {
                ContentType = "application/vnd.microsoft.card.adaptive",
                Content = JsonConvert.DeserializeObject(adaptiveCard),
            };
        }

        /// <summary>
        /// Helper function to update greeting state with entities returned by LUIS.
        /// </summary>
        /// <param name="luisResult">LUIS recognizer <see cref="RecognizerResult"/>.</param>
        /// <param name="turnContext">A <see cref="ITurnContext"/> containing all the data needed
        /// for processing this conversation turn.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        private async Task UpdateGreetingState(RecognizerResult luisResult, ITurnContext turnContext)
        {
            if (luisResult.Entities != null && luisResult.Entities.HasValues)
            {
                // Get latest GreetingState
                var greetingState = await _greetingStateAccessor.GetAsync(turnContext, () => new GreetingState());
                var entities = luisResult.Entities;

                // Supported LUIS Entities
                string[] userNameEntities = { "userName", "userName_patternAny" };
                string[] userLocationEntities = { "userLocation", "userLocation_patternAny" };

                // Update any entities
                // Note: Consider a confirm dialog, instead of just updating.
                foreach (var name in userNameEntities)
                {
                    // Check if we found valid slot values in entities returned from LUIS.
                    if (entities[name] != null)
                    {
                        // Capitalize and set new user name.
                        var newName = (string)entities[name][0];
                        greetingState.Name = char.ToUpper(newName[0]) + newName.Substring(1);
                        break;
                    }
                }

                foreach (var genre in userLocationEntities)
                {
                    if (entities[genre] != null)
                    {
                        // Capitalize and set new genre.
                        var newGenre = (string)entities[genre][0];
                        greetingState.Genre = char.ToUpper(newGenre[0]) + newGenre.Substring(1);
                        break;
                    }
                }

                // Set the new values into state.
                await _greetingStateAccessor.SetAsync(turnContext, greetingState);
            }
        }
    }
}
