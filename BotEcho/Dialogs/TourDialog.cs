using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Recognizers.Text.DataTypes.TimexExpression;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace BotEcho
{
    public class TourDialog : Dialogs.CancelDialog
    {
        private readonly IStatePropertyAccessor<TourInfo> _userTourInfo;
        private TourInfo userProfile;

        public TourDialog(UserState userState) : base(nameof(TourDialog))
        {
            _userTourInfo = userState.CreateProperty<TourInfo>("TourInfo");

            var waterFallSteps = new WaterfallStep[]
            {
                DestinationStepAsync,
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

        private async Task<DialogTurnResult> DateStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            stepContext.Values["destination"] = (string)stepContext.Result;

            var promptOprion = new PromptOptions
            {
                Prompt = MessageFactory.Text("When would you like to travel?\n(mm/dd/yyyy)"),
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
            return await stepContext.NextAsync();
        }

        private async Task<DialogTurnResult> SummaryStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            userProfile = await _userTourInfo.GetAsync(stepContext.Context, () => new TourInfo(), cancellationToken);

            userProfile.Destination = (string)stepContext.Values["destination"];
            userProfile.Date = Convert.ToDateTime((string)stepContext.Values["date"]);
            userProfile.Days = (int)stepContext.Values["days"];

            var msg = $"Tour wish detail:" +
                $"\nTo: {userProfile.Destination}" +
                $"\nDate: {userProfile.Date.Month.ToString()}/{userProfile.Date.Day}/{userProfile.Date.Year}" +
                $"\nDays: {userProfile.Days}";

            await stepContext.Context.SendActivityAsync(MessageFactory.Text(msg), cancellationToken);
            return await stepContext.PromptAsync(nameof(ConfirmPrompt), new PromptOptions { Prompt = MessageFactory.Text("Details are correct?") }, cancellationToken);
        }

        private async Task<DialogTurnResult> ConfirmationStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if ((bool)stepContext.Result)
            {
                var date = $"{userProfile.Date.Day}/{userProfile.Date.Month}/{userProfile.Date.Year}";
                string url = $"https://www.tourradar.com/d/{userProfile.Destination.ToLower()}#when={date},duration={userProfile.Days}";
                //System.Diagnostics.Process.Start(url);
                url = url.Replace("&", "^&");
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });

                await stepContext.Context.SendActivityAsync(MessageFactory.Text(url));

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
                var res = Convert.ToDateTime(timex);
                var isDefinite = new TimexProperty(timex).Types.Contains(Constants.TimexTypes.Definite);
                if (res < DateTime.Now && res.Year >= DateTime.Now.Year) isDefinite = false;
                return Task.FromResult(isDefinite);
            }
            else
            {
                return Task.FromResult(false);
            }
        }
    }

}
