using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.BotBuilderSamples;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BotEcho.Bots
{
    public class MyBot<T> : ActivityHandler where T : Dialog
    {
        protected readonly Dialog Dialog;
        protected readonly BotState ConversationState;
        protected readonly BotState UserState;

        public MyBot(ConversationState conversationState, UserState userState, TourDialog dialog)
        {
            ConversationState = conversationState;
            UserState = userState;
            Dialog = dialog;
        }


        public override async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default(CancellationToken))
        {
            await base.OnTurnAsync(turnContext, cancellationToken);
            await ConversationState.SaveChangesAsync(turnContext, false, cancellationToken);
            await UserState.SaveChangesAsync(turnContext, false, cancellationToken);
        }

        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            foreach (var member in membersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text("Welcome!\nI will find your desired tour\nLets start"));
                }
            }
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            await Dialog.Run(turnContext, ConversationState.CreateProperty<DialogState>(nameof(DialogState)), cancellationToken);
        }
    }
}
