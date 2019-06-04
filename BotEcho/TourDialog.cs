using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Recognizers.Text.DataTypes.TimexExpression;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace BotEcho
{
    public class TourDialog : ComponentDialog
    {
        private readonly IStatePropertyAccessor<TourInfo> _userTourInfo;
        private static readonly HttpClient client = new HttpClient();

        public TourDialog(UserState userState) : base(nameof(TourDialog))
        {
            _userTourInfo = userState.CreateProperty<TourInfo>("TourInfo");

            var waterFallSteps = new WaterfallStep[]
            {
                DestinationStepAsync,
                OriginStepAsync,
                DateStepAsync,
                DateFinalStepAsync,
                DaysStepAsync,
                PersonsStepAsync,
                SummaryStepAsync,
                ConfirmationStepAsync
            };

            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), waterFallSteps));
            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new NumberPrompt<int>(nameof(NumberPrompt<int>), DaysPromptValidatorAsync));
            AddDialog(new ChoicePrompt(nameof(ChoicePrompt)));
            AddDialog(new DateTimePrompt(nameof(DateTimePrompt), DateTimePromptValidator));
            AddDialog(new ConfirmPrompt(nameof(ConfirmPrompt)));

            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> DestinationStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = MessageFactory.Text("Where would you like to travel to?") }, cancellationToken);
        }

        private async Task<DialogTurnResult> OriginStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            stepContext.Values["destination"] = (string)stepContext.Result;

            return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = MessageFactory.Text("Where are you traveling from?") }, cancellationToken);
        }

        private async Task<DialogTurnResult> DateStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            stepContext.Values["origin"] = (string)stepContext.Result;

            var promptOprion = new PromptOptions
            {
                Prompt = MessageFactory.Text("When would you like to travel?\n(dd/mm/yyyy)"),
                RetryPrompt = MessageFactory.Text("I'm sorry, to make your booking please enter a full travel date including Day Month and Year.")
            };

            return await stepContext.PromptAsync(nameof(DateTimePrompt), promptOprion, cancellationToken);
        }

        private async Task<DialogTurnResult> DateFinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var timex = ((List<DateTimeResolution>)stepContext.Result)[0].Timex;
            return await stepContext.NextAsync(timex, cancellationToken);
        }

        private async Task<DialogTurnResult> DaysStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            stepContext.Values["date"] = (string)stepContext.Result;

            var promptOptions = new PromptOptions
            {
                Prompt = MessageFactory.Text("Please enter days count"),
                RetryPrompt = MessageFactory.Text("The value entered must be greater than 0"),
            };

            return await stepContext.PromptAsync(nameof(NumberPrompt<int>), promptOptions, cancellationToken);
        }

        private async Task<DialogTurnResult> PersonsStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            stepContext.Values["days"] = (int)stepContext.Result;

            var promptOptions = new PromptOptions
            {
                Prompt = MessageFactory.Text("How many persons?"),
                RetryPrompt = MessageFactory.Text("The value entered must be greater than 0")
            };

            return await stepContext.PromptAsync(nameof(NumberPrompt<int>), promptOptions, cancellationToken);
        }

        private async Task<DialogTurnResult> SummaryStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            stepContext.Values["persons"] = (int)stepContext.Result;

            var userProfile = await _userTourInfo.GetAsync(stepContext.Context, () => new TourInfo(), cancellationToken);

            userProfile.Destination = (string)stepContext.Values["destination"];
            userProfile.Origin = (string)stepContext.Values["origin"];
            userProfile.Date = (string)stepContext.Values["date"];
            userProfile.Days = (int)stepContext.Values["days"];
            userProfile.Persons = (int)stepContext.Values["persons"];

            var msg = $"Tour wish detail:" +
                $"\nTo: {userProfile.Destination}" +
                $"\nFrom: {userProfile.Origin}" +
                $"\nDate: {userProfile.Date}" +
                $"\nDays: {userProfile.Days}" +
                $"\nPersons: {userProfile.Persons}";

            await stepContext.Context.SendActivityAsync(MessageFactory.Text(msg), cancellationToken);
            return await stepContext.PromptAsync(nameof(ConfirmPrompt), new PromptOptions { Prompt = MessageFactory.Text("Details are correct?") }, cancellationToken);
        }

        private async Task<DialogTurnResult> ConfirmationStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if ((bool)stepContext.Result)
            {

                WebRequest request = WebRequest.Create("https://www.tez-tour.com/tariffsearch/getResult?callback=jsonp1559633389216&_=1559633392496&locale=ru&cityId=345&countryId=1104&after=04.06.2019&before=11.06.2019&nightsMin=6&nightsMax=14&hotelClassId=269506&hotelClassBetter=true&rAndBId=15350&rAndBBetter=true&accommodationId=2&children=0&hotelInStop=false&specialInStop=false&noTicketsTo=false&noTicketsFrom=false&tourType=1&version=2&searchTypeId=3&priceMin=0&priceMax=1500000&currency=18864&contentCountryId=1102");
                WebResponse response = await request.GetResponseAsync();
                using (Stream stream = response.GetResponseStream())
                {
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        var result = reader.ReadLine();
                        string output = result.Substring(0, 500);
                        await stepContext.Context.SendActivityAsync(MessageFactory.Text(output));
                    }
                }
                response.Close();
                await stepContext.Context.SendActivityAsync(MessageFactory.Text(response.ToString()));

                
                return await stepContext.EndDialogAsync();
            }
            else
            {
                return await stepContext.EndDialogAsync();
            }

        }

        private static Task<bool> DaysPromptValidatorAsync(PromptValidatorContext<int> promptContext, CancellationToken cancellationToken)
        {
            return Task.FromResult(promptContext.Recognized.Succeeded && promptContext.Recognized.Value > 0);
        }

        private static Task<bool> DateTimePromptValidator(PromptValidatorContext<IList<DateTimeResolution>> promptContext, CancellationToken cancellationToken)
        {
            if (promptContext.Recognized.Succeeded)
            {
                var timex = promptContext.Recognized.Value[0].Timex.Split('T')[0];

                var isDefinite = new TimexProperty(timex).Types.Contains(Constants.TimexTypes.Definite);

                return Task.FromResult(isDefinite);
            }
            else
            {
                return Task.FromResult(false);
            }
        }
    }

}
