using FurniSpace.Application.Interfaces.Search;
using FurniSpace.Infrastructure.Interfaces;
using FurniSpace.Infrastructure.Repositories.IRepository;
using FurniSpace.Infrastructure.Search;

namespace FurniSpace.Application.Services.Search;

public sealed class ChatMessageSearchIndexer : IChatMessageSearchIndexer
{
    private const string ChatMessageIndexName = "chat-messages";

    private readonly IProjectChatMessageRepository _messages;
    private readonly ISearchIndexService _search;

    public ChatMessageSearchIndexer(
        IProjectChatMessageRepository messages,
        ISearchIndexService search)
    {
        _messages = messages;
        _search = search;
    }

    public async Task SyncMessageAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        try
        {
            var item = await _messages.GetSearchIndexItemAsync(messageId, cancellationToken);
            if (item is null || !ChatMessageSearchDocumentMapper.IsIndexable(item))
            {
                await _search.DeleteAsync(ChatMessageIndexName, messageId.ToString(), cancellationToken);
                return;
            }

            var document = ChatMessageSearchDocumentMapper.ToDocument(item);
            await _search.IndexAsync(ChatMessageIndexName, messageId.ToString(), document, cancellationToken);
        }
        catch
        {
            // Search indexing is eventually consistent and should not fail the database write.
        }
    }
}
